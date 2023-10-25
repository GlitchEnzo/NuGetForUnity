#pragma warning disable SA1512,SA1124 // Single-line comments should not be followed by blank line

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using NugetForUnity.Configuration;
using NugetForUnity.Helper;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

#region No ReShaper

// ReSharper disable All
// needed because 'JetBrains.Annotations.NotNull' and 'System.Diagnostics.CodeAnalysis.NotNull' collide if this file is compiled with a never version of Unity / C#
using SuppressMessageAttribute = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;

// ReSharper restore All

#endregion

#pragma warning restore SA1512,SA1124 // Single-line comments should not be followed by blank line

namespace NugetForUnity
{
    /// <summary>
    ///     Hook into the import pipeline to change NuGet package related assets after / before they are imported.
    /// </summary>
    public class NugetAssetPostprocessor : AssetPostprocessor
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
        ///     Called when the asset database finishes importing assets.
        ///     We use it to check if packages.config has been changed and if so, we want to restore packages.
        /// </summary>
        /// <param name="importedAssets">The list of assets that were imported.</param>
        /// <param name="deletedAssets">The list of assets that were deleted.</param>
        /// <param name="movedAssets">The list of assets that were moved.</param>
        /// <param name="movedFromAssetPaths">The list of paths of assets that were moved.</param>
        internal static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            // currently only importedAssets are important for us, so if there are none, we do nothing
            if (importedAssets.Length == 0)
            {
                return;
            }

            var packagesConfigFilePath = ConfigurationManager.NugetConfigFile.PackagesConfigFilePath;
            var foundPackagesConfigAsset = importedAssets.Any(
                importedAsset => Path.GetFullPath(importedAsset).Equals(packagesConfigFilePath, StringComparison.Ordinal));

            if (!foundPackagesConfigAsset)
            {
                return;
            }

            InstalledPackagesManager.ReloadPackagesConfig();
            PackageRestorer.Restore(ConfigurationManager.NugetConfigFile.SlimRestore);
        }

        [NotNull]
        private static IEnumerable<(string AssetType, string AssetPath, ResultStatus Status)> HandleAsset(
            [NotNull] string projectRelativeAssetPath,
            [NotNull] string absoluteRepositoryPath,
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

            var absoluteAssetPath = Path.GetFullPath(Path.Combine(UnityPathHelper.AbsoluteProjectPath, projectRelativeAssetPath));
            if (!AssetIsDllInsideNuGetRepository(absoluteAssetPath, absoluteRepositoryPath))
            {
                yield break;
            }

            var assetPathRelativeToRepository = absoluteAssetPath.Substring(absoluteRepositoryPath.Length);

            // the first component is the package name with version number
            var assetPathComponents = GetPathComponents(assetPathRelativeToRepository);
            var packageNameParts = assetPathComponents.Length > 0 ? assetPathComponents[0].Split('.') : Array.Empty<string>();
            var packageName = string.Join(".", packageNameParts.TakeWhile(part => !part.All(char.IsDigit)));
            var packageConfig = InstalledPackagesManager.PackagesConfigFile.Packages.Find(
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

        [NotNull]
        private static string[] GetPathComponents([NotNull] string path)
        {
            return path.Split(Path.DirectorySeparatorChar);
        }

        private static bool AssetIsDllInsideNuGetRepository([NotNull] string absoluteAssetPath, [NotNull] string absoluteRepositoryPath)
        {
            return absoluteAssetPath.StartsWith(absoluteRepositoryPath, PathHelper.PathComparisonType) &&
                   absoluteAssetPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                   File.Exists(absoluteAssetPath);
        }

        /// <summary>
        ///     Gets the absolute path where NuGetForUnity restores NuGet packages, with trailing directory separator.
        /// </summary>
        /// <returns>The absolute path where NuGetForUnity restores NuGet packages, with trailing directory separator.</returns>
        [NotNull]
        private static string GetNuGetRepositoryPath()
        {
            return ConfigurationManager.NugetConfigFile.RepositoryPath + Path.DirectorySeparatorChar;
        }

        private static void ModifyImportSettingsOfRoslynAnalyzer([NotNull] PluginImporter plugin, bool reimport)
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

            NugetLogger.LogVerbose("Configured asset '{0}' as a Roslyn-Analyzer.", plugin.assetPath);
        }

        private static void ModifyImportSettingsOfGeneralPlugin([NotNull] PackageConfig packageConfig, [NotNull] PluginImporter plugin, bool reimport)
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
        /// <param name="plugin">The asset to edit.</param>
        /// <param name="reimport">Whether or not to save and re-import the file.</param>
        private static void ModifyImportSettingsOfPlayerOnly([NotNull] PluginImporter plugin, bool reimport)
        {
            plugin.SetCompatibleWithAnyPlatform(true);
            plugin.SetExcludeEditorFromAnyPlatform(true);

            AssetDatabase.SetLabels(plugin, new[] { ProcessedLabel });

            if (reimport)
            {
                // Persist and reload the change to the meta file
                plugin.SaveAndReimport();
            }

            NugetLogger.LogVerbose("Configured asset '{0}' as a Player Only.", plugin.assetPath);
        }

        /// <summary>
        ///     Changes the importer settings to disables the export to WSA Platform setting.
        /// </summary>
        /// <param name="analyzerAssetPath">The path to the .config file.</param>
        /// <param name="reimport">Whether or not to save and re-import the file.</param>
        private static ResultStatus ModifyImportSettingsOfConfigurationFile([NotNull] string analyzerAssetPath, bool reimport)
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

            NugetLogger.LogVerbose("Disabling WSA platform on asset settings for {0}", plugin.assetPath);
            return ResultStatus.Success;
        }

        /// <summary>
        ///     Logs the aggregated result of the processing of the assets.
        /// </summary>
        /// <param name="results">The aggregated result status.</param>
        private static void LogResults([NotNull] IEnumerable<(string AssetType, string AssetPath, ResultStatus Status)> results)
        {
            var grouped = results.GroupBy(result => result.Status);
            foreach (var groupEntry in grouped)
            {
                if (groupEntry.Key == ResultStatus.Success && ConfigurationManager.NugetConfigFile.Verbose)
                {
                    NugetLogger.LogVerbose(
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
        private static bool GetPluginImporter([NotNull] string assetFilePath, out PluginImporter plugin)
        {
            var assetPath = assetFilePath;
            plugin = AssetImporter.GetAtPath(assetPath) as PluginImporter;

            if (plugin == null)
            {
                Debug.LogWarning($"Failed to import plug-in at {assetPath}");
                return false;
            }

            NugetLogger.LogVerbose("Plug-in loaded for file: {0}", assetPath);

            return true;
        }

        /// <summary>
        ///     Check the labels on the asset to determine if we've already processed it. Calls to
        ///     <see cref="AssetImporter.SaveAndReimport" /> trigger call backs to this class to occur so not checking
        ///     if an asset has already been processed triggers an infinite loop that Unity detects and logs as an error.
        /// </summary>
        /// <param name="asset">Asset to check.</param>
        /// <returns>True if already processed.</returns>
        private static bool AlreadyProcessed([NotNull] Object asset)
        {
            return AssetDatabase.GetLabels(asset).Contains(ProcessedLabel);
        }

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
        [UsedImplicitly]
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called by Unity.")]
        private void OnPreprocessAsset()
        {
            var absoluteRepositoryPath = GetNuGetRepositoryPath();
            var results = HandleAsset(assetPath, absoluteRepositoryPath, false);
            LogResults(results);
        }

        [SuppressMessage(
            "StyleCop.CSharp.OrderingRules",
            "SA1201:Elements should appear in the correct order",
            Justification = "We like private enums at the botom of the file.")]
        private enum ResultStatus
        {
            Success,

            Failure,

            AlreadyProcessed,
        }
    }
}
