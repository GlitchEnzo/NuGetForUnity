using System;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using NugetForUnity.Models;

namespace NugetForUnity.Helper
{
    /// <summary>
    ///     Helper class for analyzers.
    /// </summary>
    internal static class AnalyzerHelper
    {
        /// <summary>
        ///     Folder used to store Roslyn-Analyzers inside NuGet packages.
        /// </summary>
        private const string AnalyzersFolderName = "analyzers";

        /// <summary>
        ///     Used to let unity know an asset is a Roslyn-Analyzer.
        /// </summary>
        private const string RoslynAnalyzerLabel = "RoslynAnalyzer";

        /// <summary>
        ///     Name of the root folder containing dotnet analyzers.
        /// </summary>
        private static readonly string AnalyzersRoslynVersionsFolderName = Path.Combine(AnalyzersFolderName, "dotnet");

        /// <summary>
        ///     Prefix for the path of dll's of roslyn analyzers.
        /// </summary>
        private static readonly string AnalyzersRoslynVersionSubFolderPrefix = Path.Combine(AnalyzersRoslynVersionsFolderName, "roslyn");

        /// <summary>
        ///     Determine if "RoslynAnalyzer" label should be added to an analyzer
        /// </summary>
        /// <param name="path">The path to the analyzer</param>
        /// <returns>True if the label should be added, false otherwise.</returns>
        public static bool ShouldEnableRoslynAnalyzer(string path)
        {
            // The nuget package can contain analyzers for multiple Roslyn versions.
            // In that case, for the same package, the most recent version must be chosen out of those available for the current Unity version.
            var assetPath = Path.GetFullPath(path);
            var assetRoslynVersion = GetRoslynVersionNumberFromAnalyzerPath(assetPath);
            if (assetRoslynVersion != null)
            {
                var maxSupportedRoslynVersion = GetMaxSupportedRoslynVersion();
                if (maxSupportedRoslynVersion == null)
                {
                    // the current unity version doesn't support roslyn analyzers
                    return false;
                }

                var versionPrefixIndex = assetPath.IndexOf(AnalyzersRoslynVersionsFolderName, StringComparison.Ordinal);
                var analyzerVersionsRootDirectoryPath = Path.Combine(assetPath.Substring(0, versionPrefixIndex), AnalyzersRoslynVersionsFolderName);
                var analyzersFolders = Directory.EnumerateDirectories(analyzerVersionsRootDirectoryPath);
                var allEnabledRoslynVersions = analyzersFolders.Select(GetRoslynVersionNumberFromAnalyzerPath)
                    .Where(version => version != null && version.CompareTo(maxSupportedRoslynVersion) <= 0)
                    .ToArray();

                if (allEnabledRoslynVersions.Length == 0)
                {
                    return false;
                }

                // If most recent valid analyzers exist elsewhere, don't add label `RoslynAnalyzer`
                var maxMatchingVersion = allEnabledRoslynVersions.Max();
                if (!allEnabledRoslynVersions.Contains(assetRoslynVersion) || assetRoslynVersion < maxMatchingVersion)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        ///     Writes initial Unity import settings for a Roslyn analyzer DLL before Unity imports it for the first time.
        /// </summary>
        /// <param name="analyzerDllPath">The path to the analyzer DLL.</param>
        public static void WriteInitialRoslynAnalyzerImportSettings([NotNull] string analyzerDllPath)
        {
            var analyzerDllMetaPath = $"{analyzerDllPath}.meta";
            if (File.Exists(analyzerDllMetaPath))
            {
                return;
            }

            var labelsForAsset = ShouldEnableRoslynAnalyzer(analyzerDllPath) ?
                $"labels:{Environment.NewLine}- {RoslynAnalyzerLabel}{Environment.NewLine}" :
                $"labels: []{Environment.NewLine}";

            File.WriteAllText(
                analyzerDllMetaPath,
                "fileFormatVersion: 2" + Environment.NewLine +
                $"guid: {Guid.NewGuid():N}" + Environment.NewLine +
                labelsForAsset +
                "PluginImporter:" + Environment.NewLine +
                "  externalObjects: {}" + Environment.NewLine +
                "  serializedVersion: 2" + Environment.NewLine +
                "  iconMap: {}" + Environment.NewLine +
                "  executionOrder: {}" + Environment.NewLine +
                "  defineConstraints: []" + Environment.NewLine +
                "  isPreloaded: 0" + Environment.NewLine +
                "  isOverridable: 0" + Environment.NewLine +
                "  isExplicitlyReferenced: 0" + Environment.NewLine +
                "  validateReferences: 1" + Environment.NewLine +
                "  platformData:" + Environment.NewLine +
                "  - first:" + Environment.NewLine +
                "      Any: " + Environment.NewLine +
                "    second:" + Environment.NewLine +
                "      enabled: 0" + Environment.NewLine +
                "      settings: {}" + Environment.NewLine +
                "  - first:" + Environment.NewLine +
                "      Editor: Editor" + Environment.NewLine +
                "    second:" + Environment.NewLine +
                "      enabled: 0" + Environment.NewLine +
                "      settings:" + Environment.NewLine +
                "        DefaultValueInitialized: true" + Environment.NewLine +
                "  userData: " + Environment.NewLine +
                "  assetBundleName: " + Environment.NewLine +
                "  assetBundleVariant: " + Environment.NewLine,
                new UTF8Encoding(false));
        }

        [CanBeNull]
        private static NugetPackageVersion GetRoslynVersionNumberFromAnalyzerPath(string analyzerAssetPath)
        {
            var versionPrefixStartIndex = analyzerAssetPath.IndexOf(AnalyzersRoslynVersionSubFolderPrefix, StringComparison.Ordinal);
            if (versionPrefixStartIndex < 0)
            {
                return null;
            }

            var versionStartIndex = versionPrefixStartIndex + AnalyzersRoslynVersionSubFolderPrefix.Length;
            var separatorIndex = analyzerAssetPath.IndexOf(Path.DirectorySeparatorChar, versionStartIndex);
            var versionLength = separatorIndex >= 0 ? separatorIndex - versionStartIndex : analyzerAssetPath.Length - versionStartIndex;
            var versionString = analyzerAssetPath.Substring(versionStartIndex, versionLength);
            return string.IsNullOrEmpty(versionString) ? null : new NugetPackageVersion(versionString);
        }

        [CanBeNull]
        private static NugetPackageVersion GetMaxSupportedRoslynVersion()
        {
            var unityVersion = UnityVersion.Current;
            if (unityVersion >= new UnityVersion(2022, 3, 12, 'f', 1))
            {
                return new NugetPackageVersion("4.3.0");
            }

            if (unityVersion >= new UnityVersion(2022, 2, 1, 'f', 1))
            {
                return new NugetPackageVersion("4.1.0");
            }

            if (unityVersion >= new UnityVersion(2021, 2, 1, 'f', 1))
            {
                return new NugetPackageVersion("3.8.0");
            }

            return null;
        }
    }
}
