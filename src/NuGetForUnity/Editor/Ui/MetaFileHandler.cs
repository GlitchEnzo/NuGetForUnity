using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NugetForUnity
{
    public class MetaFileHandler : AssetPostprocessor
    {
        private const string RuntimesFolderName = "runtimes";
        private const string NativeFolderName = "native";
        private const string AssetsFolderName = "Assets";

        /// <summary>
        /// Used to mark an asset as already processed by this class.
        /// </summary>
        private const string ProcessedLabel = "NuGetForUnity";

        private static readonly List<BuildTarget> NotObsoleteBuildTargets = typeof(BuildTarget)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(fieldInfo => fieldInfo.GetCustomAttribute(typeof(ObsoleteAttribute)) == null)
            .Select(fieldInfo => (BuildTarget)fieldInfo.GetValue(null))
            .ToList();

        private enum ProcessState
        {
            Success,
            Failure,
            AlreadyProcessed
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (importedAssets.Length == 0) return;

            if (NugetHelper.NugetConfigFile == null)
            {
                NugetHelper.LoadNugetConfigFile();
            }

            if (NugetHelper.SettingsFile == null)
            {
                NugetHelper.LoadSettingFile();
            }

            var pathPrefix = NugetHelper.NugetConfigFile.RepositoryPath.Substring(
                NugetHelper.NugetConfigFile.RepositoryPath.IndexOf(AssetsFolderName, StringComparison.Ordinal));
            var nugetForUnityAssets = importedAssets.Where(path => path.StartsWith(pathPrefix)).ToList();

            if (nugetForUnityAssets.Count == 0) return;

            var runtimeAssetsFiles = nugetForUnityAssets
                .Where(path => path.Contains(RuntimesFolderName))
                .Where(path => path.Contains(NativeFolderName))
                .Where(File.Exists)
                .ToList();
            var runtimeResults = runtimeAssetsFiles.Select(path => (path, HandleRuntime(path))).ToList();

            LogResults("Handle Runtimes", runtimeResults);
        }

        private static void LogResults(string type, IList<(string, ProcessState)> results)
        {
            var successes = results.Where(r => ProcessState.Success.Equals(r.Item2)).Select(r => r.Item1).ToList();
            var failures = results.Where(r => ProcessState.Failure.Equals(r.Item2)).Select(r => r.Item1).ToList();

            if (successes.Count > 0 && NugetHelper.NugetConfigFile.Verbose)
            {
                NugetHelper.LogVerbose("NuGetForUnity: {0} successfully configured: {1}", type,
                    string.Join(",", successes));
            }

            if (failures.Count > 0)
            {
                Debug.LogError($"NuGetForUnity: {type} failed to configure {string.Join(",", failures)}");
            }
        }

        /// <summary>
        ///     Given the <paramref name="assetFilePath" /> load the <see cref="PluginImporter" /> for the asset it's associated
        ///     with
        /// </summary>
        /// <param name="assetFilePath">Path to the assets</param>
        /// <param name="plugin">The loaded PluginImporter</param>
        /// <returns>True if the plugin importer for the asset could be loaded</returns>
        private static bool GetPluginImporter(string assetFilePath, out PluginImporter plugin)
        {
            var assetPath = assetFilePath;
            plugin = AssetImporter.GetAtPath(assetPath) as PluginImporter;

            if (plugin == null)
            {
                Debug.LogWarning($"Failed to import plugin at {assetPath}");
                return false;
            }

            NugetHelper.LogVerbose("Plugin loaded for file: {0}", assetPath);

            return true;
        }

        /// <summary>
        ///     Check the labels on the asset to determine if we've already processed it. Calls to
        ///     <see cref="AssetImporter.SaveAndReimport"/> trigger call backs to this class to occur so not checking
        ///     if an asset has already been processed triggers an infinite loop that Unity detects and logs as an error
        /// </summary>
        /// <param name="asset">Asset to check</param>
        /// <returns></returns>
        private static bool AlreadyProcessed(UnityEngine.Object asset)
        {
            return AssetDatabase.GetLabels(asset).ToList().Contains(ProcessedLabel);
        }

        /// <summary>
        /// Extract platform information from the assetFilePath
        /// </summary>
        /// <param name="assetFilePath">Path to an asset</param>
        /// <param name="compatibleTargets">List of compatible Unity build targets</param>
        /// <param name="platform">Platform i.e. Linux/OSX/Windows</param>
        /// <param name="architecture">Architecture i.e. x86_64</param>
        /// <returns>True if the platform & architecture are supported</returns>
        private static bool GetPlatform(string assetFilePath, out List<BuildTarget> compatibleTargets,
            out string platform, out string architecture)
        {
            var platformFolder =
                assetFilePath.Substring(0, assetFilePath.IndexOf(NativeFolderName, StringComparison.Ordinal));
            var platformString = new DirectoryInfo(platformFolder).Name;

            var platformAndArch = platformString.Split('-');

            platform = platformString;
            architecture = platformAndArch[1];
            return NugetHelper.SettingsFile.NativeRuntimesMappings.TryGetValue(platformString, out compatibleTargets);
        }

        /// <summary>
        ///     Import and set compatability on a native file.
        ///     <list type="bullet">
        ///         <item>Unsupported platforms/architectures parent folder is deleted</item>
        ///         <item>Set all excludes for all non-deprecated BuildTargets</item>
        ///         <item>Set all compatability for all non-deprecated BuildTargets</item>
        ///     </list>
        /// </summary>
        /// <param name="assetFilePath">Path to an asset</param>
        /// <returns>True if it was able to update the asset</returns>
        private static ProcessState HandleRuntime(string assetFilePath)
        {
            if (!GetPluginImporter(assetFilePath, out var plugin)) return ProcessState.Failure;
            if (AlreadyProcessed(plugin)) return ProcessState.AlreadyProcessed;

            if (!GetPlatform(assetFilePath, out var compatibleTargets, out var platform, out var arch))
            {
                var platformFolder =
                    assetFilePath.Substring(0, assetFilePath.IndexOf(NativeFolderName, StringComparison.Ordinal));
                NugetHelper.LogVerbose("Runtime {0} is not supported", platformFolder);
                Directory.Delete(platformFolder, true);
                return ProcessState.Failure;
            }

            var incompatibleTargets = NotObsoleteBuildTargets.Except(compatibleTargets).ToList();

            NugetHelper.LogVerbose(
                "Runtime {0} of asset {1} setting compatability to {2}, incompatibility to {3}",
                platform,
                assetFilePath,
                string.Join(",", compatibleTargets),
                string.Join(",", incompatibleTargets));

            incompatibleTargets.ForEach(target => plugin.SetExcludeFromAnyPlatform(target, true));
            compatibleTargets.ForEach(target => plugin.SetExcludeFromAnyPlatform(target, false));

            incompatibleTargets.ForEach(target => plugin.SetCompatibleWithPlatform(target, false));
            compatibleTargets.ForEach(target => plugin.SetCompatibleWithPlatform(target, true));

            plugin.SetCompatibleWithEditor(true);
            // Set the editor architecture to prevent loading both 32 & 64 windows DLLs
            switch (arch)
            {
                case "x64":
                    plugin.SetEditorData("CPU", "x86_64");
                    break;
                case "x86":
                    plugin.SetEditorData("CPU", "x86");
                    break;
                default:
                    Debug.LogError($"Unsupported architecture {arch} for {assetFilePath}");
                    return ProcessState.Failure;
            }

            AssetDatabase.SetLabels(plugin, new[] { ProcessedLabel });

            // Persist and reload the change to the meta file
            plugin.SaveAndReimport();
            NugetHelper.LogVerbose("Runtime {0} of asset {1} compatability set", platform, assetFilePath);
            return ProcessState.Success;
        }
    }
}