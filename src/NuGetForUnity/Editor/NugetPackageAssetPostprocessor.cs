using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NugetForUnity
{
    /// <summary>
    ///     Hook into the import pipeline to change NuGet package related assets after / before they are imported.
    /// </summary>
    public class NugetPackageAssetPostprocessor : AssetPostprocessor
    {
        /// <summary>
        ///     Folder used to store Roslyn-Analyzers inside NuGet packages.
        /// </summary>
        private const string AnalyzersFolderName = "analyzers";

        /// <summary>
        ///     Used to mark an asset as already processed by this class.
        /// </summary>
        private const string ProcessedLabel = "NuGetForUnity";

        /// <summary>
        ///     Used to let unity know an asset is a Roslyn-Analyzer.
        /// </summary>
        private const string RoslynAnalyzerLabel = "RoslynAnalyzer";

        private static readonly List<BuildTarget> NonObsoleteBuildTargets = typeof(BuildTarget).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(fieldInfo => fieldInfo.GetCustomAttribute(typeof(ObsoleteAttribute)) == null)
            .Select(fieldInfo => (BuildTarget)fieldInfo.GetValue(null))
            .ToList();

        /// <summary>
        ///     Get informed about a new asset that is added.
        ///     This is called before unity tried to import a asset but the <see cref="AssetImporter" /> is already created
        ///     so we can change the import settings before unity throws errors about incompatibility etc..
        ///     Currently we change the import settings of:
        ///     Roslyn-Analyzers: are marked so unity knows that the *.dll's are analyzers and treats them accordingly.
        ///     NuGetForUnity config files: so they are not exported to WSA
        ///     PlayerOnly assemblies: configure the assemblies to be excluded form edit-mode
        /// </summary>
        private void OnPreprocessAsset()
        {
            var absoluteRepositoryPath = GetNuGetRepositoryPath();
            var result = HandleAsset(assetPath, absoluteRepositoryPath, false);
            if (result.HasValue)
            {
                LogResults(new[] { result.Value });
            }
        }

        private static (string AssetType, string AssetPath, ResultStatus Status)? HandleAsset(string projectRelativeAssetPath,
            string absoluteRepositoryPath,
            bool reimport)
        {
            var assetFileName = Path.GetFileName(projectRelativeAssetPath);
            if (assetFileName.Equals(NugetConfigFile.FileName, StringComparison.OrdinalIgnoreCase) ||
                assetFileName.Equals(PackagesConfigFile.FileName, StringComparison.OrdinalIgnoreCase))
            {
                // Not sure why but for .config files we need to re-import always. I think this is because they are treated as native plug-ins.
                var result = ModifyImportSettingsOfConfigurationFile(projectRelativeAssetPath, true);
                return ("ConfigurationFile", projectRelativeAssetPath, result);
            }

            var absoluteAssetPath = Path.GetFullPath(Path.Combine(NugetHelper.AbsoluteProjectPath, projectRelativeAssetPath));
            if (!AssetIsDllInsideNuGetRepository(absoluteAssetPath, absoluteRepositoryPath))
            {
                return null;
            }

            var assetPathRelativeToRepository = absoluteAssetPath.Substring(absoluteRepositoryPath.Length);

            // the first component is the package name with version number
            var assetPathComponents = GetPathComponents(assetPathRelativeToRepository);
            if (assetPathComponents.Length > 1 && assetPathComponents[1].Equals(AnalyzersFolderName, StringComparison.OrdinalIgnoreCase))
            {
                var result = ModifyImportSettingsOfRoslynAnalyzer(projectRelativeAssetPath, reimport);
                return ("RoslynAnalyzer", projectRelativeAssetPath, result);
            }

            if (assetPathComponents.Length > 0 &&
                UnityPreImportedLibraryResolver.GetAlreadyImportedEditorOnlyLibraries()
                    .Contains(Path.GetFileNameWithoutExtension(assetPathComponents[assetPathComponents.Length - 1])))
            {
                var result = ModifyImportSettingsOfPlayerOnly(projectRelativeAssetPath, reimport);
                return ("PlayerOnly", projectRelativeAssetPath, result);
            }

            return null;
        }

        private static string[] GetPathComponents(string path)
        {
            return path.Split(Path.DirectorySeparatorChar);
        }

        private static bool AssetIsDllInsideNuGetRepository(string absoluteAssetPath, string absoluteRepositoryPath)
        {
            return absoluteAssetPath.StartsWith(absoluteRepositoryPath, StringComparison.OrdinalIgnoreCase) &&
                   absoluteAssetPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                   File.Exists(absoluteAssetPath);
        }

        /// <summary>
        ///     Gets the absolute path where NuGetForUnity restores NuGet packages, with trailing directory separator.
        /// </summary>
        /// <returns>The absolute path where NuGetForUnity restores NuGet packages, with trailing directory separator.</returns>
        private static string GetNuGetRepositoryPath()
        {
            if (NugetHelper.NugetConfigFile == null)
            {
                NugetHelper.LoadNugetConfigFile();
            }

            return NugetHelper.NugetConfigFile.RepositoryPath + Path.DirectorySeparatorChar;
        }

        private static ResultStatus ModifyImportSettingsOfRoslynAnalyzer(string analyzerAssetPath, bool reimport)
        {
            if (!GetPluginImporter(analyzerAssetPath, out var plugin))
            {
                return ResultStatus.Failure;
            }

            if (AlreadyProcessed(plugin))
            {
                return ResultStatus.AlreadyProcessed;
            }

            plugin.SetCompatibleWithAnyPlatform(false);
            plugin.SetCompatibleWithEditor(false);
            foreach (var platform in NonObsoleteBuildTargets)
            {
                plugin.SetExcludeFromAnyPlatform(platform, false);
            }

            AssetDatabase.SetLabels(plugin, new[] { RoslynAnalyzerLabel, ProcessedLabel });

            if (reimport)
            {
                // Persist and reload the change to the meta file
                plugin.SaveAndReimport();
            }

            NugetHelper.LogVerbose("Configured asset '{0}' as a Roslyn-Analyzer.", analyzerAssetPath);
            return ResultStatus.Success;
        }

        /// <summary>
        ///     Changes the importer settings to exclude it from editor.
        ///     This is needed for assemblies that are imported by unity but only in edit-mode so we can only import it in play-mode.
        ///     <seealso cref="UnityPreImportedLibraryResolver.GetAlreadyImportedEditorOnlyLibraries" />.
        /// </summary>
        /// <param name="assemblyAssetPath">The path to the .dll file.</param>
        /// <param name="reimport">Whether or not to save and re-import the file.</param>
        private static ResultStatus ModifyImportSettingsOfPlayerOnly(string assemblyAssetPath, bool reimport)
        {
            if (!GetPluginImporter(assemblyAssetPath, out var plugin))
            {
                return ResultStatus.Failure;
            }

            if (AlreadyProcessed(plugin))
            {
                return ResultStatus.AlreadyProcessed;
            }

            plugin.SetCompatibleWithAnyPlatform(true);
            plugin.SetExcludeEditorFromAnyPlatform(true);

            AssetDatabase.SetLabels(plugin, new[] { ProcessedLabel });

            if (reimport)
            {
                // Persist and reload the change to the meta file
                plugin.SaveAndReimport();
            }

            NugetHelper.LogVerbose("Configured asset '{0}' as a Player Only.", assemblyAssetPath);
            return ResultStatus.Success;
        }

        /// <summary>
        ///     Changes the importer settings to disables the export to WSA Platform setting.
        /// </summary>
        /// <param name="analyzerAssetPath">The path to the .config file.</param>
        /// <param name="reimport">Whether or not to save and re-import the file.</param>
        private static ResultStatus ModifyImportSettingsOfConfigurationFile(string analyzerAssetPath, bool reimport)
        {
            if (!GetPluginImporter(analyzerAssetPath, out var plugin))
            {
                return ResultStatus.Failure;
            }

            if (AlreadyProcessed(plugin))
            {
                return ResultStatus.AlreadyProcessed;
            }

            plugin.SetCompatibleWithPlatform(BuildTarget.WSAPlayer, false);
            AssetDatabase.SetLabels(plugin, new[] { ProcessedLabel });

            if (reimport)
            {
                // Persist and reload the change to the meta file
                plugin.SaveAndReimport();
            }

            NugetHelper.LogVerbose("Disabling WSA platform on asset settings for {0}", analyzerAssetPath);
            return ResultStatus.Success;
        }

        /// <summary>
        ///     Logs the aggregated result of the processing of the assets.
        /// </summary>
        /// <param name="results">The aggregated result status.</param>
        private static void LogResults(IEnumerable<(string AssetType, string AssetPath, ResultStatus Status)> results)
        {
            var grouped = results.GroupBy(result => result.Status);
            foreach (var groupEntry in grouped)
            {
                if (groupEntry.Key == ResultStatus.Success && NugetHelper.NugetConfigFile.Verbose)
                {
                    NugetHelper.LogVerbose(
                        "NuGetForUnity: successfully processed: {0}",
                        string.Join(",", groupEntry.Select(asset => $"'{asset.AssetPath}' ({asset.AssetType})")));
                }

                if (groupEntry.Key == ResultStatus.Failure)
                {
                    Debug.LogError(
                        $"NuGetForUnity: failed to process: {string.Join(",", groupEntry.Select(asset => $"'{asset.AssetPath}' ({asset.AssetType})"))}");
                }
            }
        }

        /// <summary>
        ///     Given the <paramref name="assetFilePath" /> load the <see cref="PluginImporter" /> for the asset it's associated
        ///     with.
        /// </summary>
        /// <param name="assetFilePath">Path to the assets.</param>
        /// <param name="plugin">The loaded PluginImporter.</param>
        /// <returns>True if the plug-in importer for the asset could be loaded.</returns>
        private static bool GetPluginImporter(string assetFilePath, out PluginImporter plugin)
        {
            var assetPath = assetFilePath;
            plugin = AssetImporter.GetAtPath(assetPath) as PluginImporter;

            if (plugin == null)
            {
                Debug.LogWarning($"Failed to import plug-in at {assetPath}");
                return false;
            }

            NugetHelper.LogVerbose("Plug-in loaded for file: {0}", assetPath);

            return true;
        }

        /// <summary>
        ///     Check the labels on the asset to determine if we've already processed it. Calls to
        ///     <see cref="AssetImporter.SaveAndReimport" /> trigger call backs to this class to occur so not checking
        ///     if an asset has already been processed triggers an infinite loop that Unity detects and logs as an error.
        /// </summary>
        /// <param name="asset">Asset to check.</param>
        /// <returns>True if already processed.</returns>
        private static bool AlreadyProcessed(Object asset)
        {
            return AssetDatabase.GetLabels(asset).Contains(ProcessedLabel);
        }

        private enum ResultStatus
        {
            Success,

            Failure,

            AlreadyProcessed,
        }
    }
}
