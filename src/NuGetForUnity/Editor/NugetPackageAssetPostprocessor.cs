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

        private static readonly PropertyInfo PluginImporterIsExplicitlyReferencedProperty =
            typeof(PluginImporter).GetProperty("IsExplicitlyReferenced", BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException("Can't find property 'IsExplicitlyReferenced'.");

        /// <summary>
        ///     Get informed about a new asset that is added.
        ///     This is called before unity tried to import a asset but the <see cref="AssetImporter" /> is already created
        ///     so we can change the import settings before unity throws errors about incompatibility etc..
        ///     <para>
        ///         Currently we change the import settings of:
        ///     </para>
        ///     <list type="bullet">
        ///         <item>
        ///             <term>Roslyn-Analyzers:</term> are marked so unity knows that the *.dll's are analyzers and treats them accordingly.
        ///         </item>
        ///         <item>
        ///             <term>NuGetForUnity config files:</term> so they are not exported to WSA
        ///         </item>
        ///         <item>
        ///             <term>PlayerOnly assemblies:</term> configure the assemblies to be excluded form edit-mode
        ///         </item>
        ///         <item>
        ///             <term>Normal assemblies (*.dll):</term> apply the IsExplicitlyReferenced setting read from the <c>package.config</c>.
        ///         </item>
        ///     </list>
        /// </summary>
        private void OnPreprocessAsset()
        {
            var absoluteRepositoryPath = GetNuGetRepositoryPath();
            var results = HandleAsset(assetPath, absoluteRepositoryPath, false);
            LogResults(results);
        }

        private static IEnumerable<(string AssetType, string AssetPath, ResultStatus Status)> HandleAsset(string projectRelativeAssetPath,
            string absoluteRepositoryPath,
            bool reimport)
        {
            var assetFileName = Path.GetFileName(projectRelativeAssetPath);
            if (assetFileName.Equals(NugetConfigFile.FileName, StringComparison.OrdinalIgnoreCase) ||
                assetFileName.Equals(PackagesConfigFile.FileName, StringComparison.OrdinalIgnoreCase))
            {
                // Not sure why but for .config files we need to re-import always. I think this is because they are treated as native plug-ins.
                var result = ModifyImportSettingsOfConfigurationFile(projectRelativeAssetPath, true);
                yield return ("ConfigurationFile", projectRelativeAssetPath, result);

                yield break;
            }

            var absoluteAssetPath = Path.GetFullPath(Path.Combine(NugetHelper.AbsoluteProjectPath, projectRelativeAssetPath));
            if (!AssetIsDllInsideNuGetRepository(absoluteAssetPath, absoluteRepositoryPath))
            {
                yield break;
            }

            var assetPathRelativeToRepository = absoluteAssetPath.Substring(absoluteRepositoryPath.Length);

            // the first component is the package name with version number
            var assetPathComponents = GetPathComponents(assetPathRelativeToRepository);
            var packageNameParts = assetPathComponents.Length > 0 ? assetPathComponents[0].Split('.') : null;
            var packageName = string.Join(".", packageNameParts.TakeWhile(part => !part.All(char.IsDigit)));
            var packageConfig = NugetHelper.PackagesConfigFile.Packages.FirstOrDefault(
                packageSettings => packageSettings.Id.Equals(packageName, StringComparison.OrdinalIgnoreCase));

            if (!GetPluginImporter(projectRelativeAssetPath, out var plugin))
            {
                yield return ("GetPluginImporter", projectRelativeAssetPath, ResultStatus.Failure);

                yield break;
            }

            if (AlreadyProcessed(plugin))
            {
                yield return ("AlreadyProcessed", projectRelativeAssetPath, ResultStatus.AlreadyProcessed);

                yield break;
            }

            if (packageConfig != null)
            {
                ModifyImportSettingsOfGeneralPlugin(packageConfig, plugin, reimport);
                yield return ("GeneralSetting", projectRelativeAssetPath, ResultStatus.Success);
            }

            if (assetPathComponents.Length > 1 && assetPathComponents[1].Equals(AnalyzersFolderName, StringComparison.OrdinalIgnoreCase))
            {
                ModifyImportSettingsOfRoslynAnalyzer(plugin, reimport);
                yield return ("RoslynAnalyzer", projectRelativeAssetPath, ResultStatus.Success);

                yield break;
            }

            if (assetPathComponents.Length > 0 &&
                UnityPreImportedLibraryResolver.GetAlreadyImportedEditorOnlyLibraries()
                    .Contains(Path.GetFileNameWithoutExtension(assetPathComponents[assetPathComponents.Length - 1])))
            {
                ModifyImportSettingsOfPlayerOnly(plugin, reimport);
                yield return ("PlayerOnly", projectRelativeAssetPath, ResultStatus.Success);
            }
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

        private static void ModifyImportSettingsOfRoslynAnalyzer(PluginImporter plugin, bool reimport)
        {
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

            NugetHelper.LogVerbose("Configured asset '{0}' as a Roslyn-Analyzer.", plugin.assetPath);
        }

        private static void ModifyImportSettingsOfGeneralPlugin(PackageConfig packageConfig, PluginImporter plugin, bool reimport)
        {
            PluginImporterIsExplicitlyReferencedProperty.SetValue(plugin, !packageConfig.AutoReferenced);

            AssetDatabase.SetLabels(plugin, new[] { ProcessedLabel });

            if (reimport)
            {
                // Persist and reload the change to the meta file
                plugin.SaveAndReimport();
            }
        }

        /// <summary>
        ///     Changes the importer settings to exclude it from editor.
        ///     This is needed for assemblies that are imported by unity but only in edit-mode so we can only import it in play-mode.
        ///     <seealso cref="UnityPreImportedLibraryResolver.GetAlreadyImportedEditorOnlyLibraries" />.
        /// </summary>
        /// <param name="assemblyAssetPath">The path to the .dll file.</param>
        /// <param name="reimport">Whether or not to save and re-import the file.</param>
        private static void ModifyImportSettingsOfPlayerOnly(PluginImporter plugin, bool reimport)
        {
            plugin.SetCompatibleWithAnyPlatform(true);
            plugin.SetExcludeEditorFromAnyPlatform(true);

            AssetDatabase.SetLabels(plugin, new[] { ProcessedLabel });

            if (reimport)
            {
                // Persist and reload the change to the meta file
                plugin.SaveAndReimport();
            }

            NugetHelper.LogVerbose("Configured asset '{0}' as a Player Only.", plugin.assetPath);
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

            NugetHelper.LogVerbose("Disabling WSA platform on asset settings for {0}", plugin.assetPath);
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
