using System;
using System.IO;
using NugetForUnity.Configuration;
using NugetForUnity.Helper;
using NugetForUnity.Models;
using UnityEngine;

namespace NugetForUnity
{
    /// <summary>
    ///     Manages the content (files inside the .nupkg) of NuGet packages.
    /// </summary>
    internal static class PackageContentManager
    {
        /// <summary>
        ///     Deletes all files and folders associated with a package.
        /// </summary>
        /// <param name="package">The package to remove all its content of.</param>
        internal static void DeletePackageContentPackage(INugetPackageIdentifier package)
        {
            var packageInstallDirectory = GetPackageInstallDirectory(package);
            FileSystemHelper.DeleteDirectory(packageInstallDirectory, true);

            var metaFile = $"{packageInstallDirectory}.meta";
            FileSystemHelper.DeleteFile(metaFile);

            var toolsInstallDirectory = GetPackageOutsideInstallDirectory(package);
            FileSystemHelper.DeleteDirectory(toolsInstallDirectory, true);
        }

        /// <summary>
        ///     Cleans up a package after it has been installed.
        ///     Since we are in Unity, we can make certain assumptions on which files will NOT be used, so we can delete them.
        /// </summary>
        /// <param name="package">The NugetPackage to clean.</param>
        internal static void CleanInstallationDirectory(INugetPackageIdentifier package)
        {
            var packageInstallDirectory = GetPackageInstallDirectory(package);

            NugetLogger.LogVerbose("Cleaning {0}", packageInstallDirectory);

            FileSystemHelper.FixSpaces(packageInstallDirectory);

            var packageToolsDirectory = Path.Combine(packageInstallDirectory, "tools");
            if (Directory.Exists(packageToolsDirectory))
            {
                // Move the tools folder outside of the Unity Assets folder
                var toolsInstallDirectory = Path.Combine(GetPackageOutsideInstallDirectory(package), "tools");

                NugetLogger.LogVerbose("Moving {0} to {1}", packageToolsDirectory, toolsInstallDirectory);

                // create the directory to create any of the missing folders in the path
                Directory.CreateDirectory(toolsInstallDirectory);

                // delete the final directory to prevent the Move operation from throwing exceptions.
                FileSystemHelper.DeleteDirectory(toolsInstallDirectory, false);

                Directory.Move(packageToolsDirectory, toolsInstallDirectory);
            }

            // if there are native DLLs, copy them to the Unity project root (1 up from Assets)
            var packageOutputDirectory = Path.Combine(packageInstallDirectory, "output");
            if (Directory.Exists(packageOutputDirectory))
            {
                var files = Directory.GetFiles(packageOutputDirectory);
                foreach (var file in files)
                {
                    var newFilePath = Path.Combine(UnityPathHelper.AbsoluteProjectPath, Path.GetFileName(file));
                    FileSystemHelper.MoveFile(file, newFilePath, false);
                }

                FileSystemHelper.DeleteDirectory(packageOutputDirectory, true);
            }

            // if there are Unity plugin DLLs, copy them to the Unity Plugins folder (Assets/Plugins)
            var packageUnityPluginDirectory = Path.Combine(packageInstallDirectory, "unityplugin");
            if (Directory.Exists(packageUnityPluginDirectory))
            {
                var pluginsDirectory = Path.Combine(Application.dataPath, "Plugins");

                FileSystemHelper.DirectoryMove(packageUnityPluginDirectory, pluginsDirectory);

                FileSystemHelper.DeleteDirectory(packageUnityPluginDirectory, true);
                FileSystemHelper.DeleteFile($"{packageUnityPluginDirectory}.meta");
            }

            // if there are Unity StreamingAssets, copy them to the Unity StreamingAssets folder (Assets/StreamingAssets)
            var packageStreamingAssetsDirectory = Path.Combine(packageInstallDirectory, "StreamingAssets");
            if (Directory.Exists(packageStreamingAssetsDirectory))
            {
                var streamingAssetsDirectory = Path.Combine(Application.dataPath, "StreamingAssets");

                FileSystemHelper.DirectoryMove(packageStreamingAssetsDirectory, streamingAssetsDirectory);

                // delete the package's StreamingAssets folder and .meta file
                FileSystemHelper.DeleteDirectory(packageStreamingAssetsDirectory, true);
                FileSystemHelper.DeleteFile($"{packageStreamingAssetsDirectory}.meta");
            }
        }

        internal static bool ShouldSkipUnpackingOnPath(string path, string packageId)
        {
            // skip a remnant .meta file that may exist from packages created by Unity
            if (path.EndsWith($"{packageId}.nuspec.meta", StringComparison.Ordinal)) return true;

            // skip directories & files that NuGet normally deletes
            if (path.StartsWith("_rels", StringComparison.Ordinal) || path.Contains("/_rels/")) return true;
            if (path.StartsWith("package", StringComparison.Ordinal) || path.Contains("/package/")) return true;
            if (path.EndsWith($"{packageId}.nuspec", StringComparison.Ordinal)) return true;
            if (path.EndsWith("[Content_Types].xml", StringComparison.Ordinal)) return true;

            // Unity has no use for the build directory
            if (path.StartsWith("build", StringComparison.Ordinal) || path.Contains("/build/")) return true;

            // For now, skip src. We may use it later...
            if (path.StartsWith("src", StringComparison.Ordinal) || path.Contains("/src/")) return true;

            // Since we don't automatically fix up the runtime dll platforms, skip them until we improve support
            // for this newer feature of nuget packages.
            if (path.StartsWith("runtimes", StringComparison.Ordinal) || path.Contains("/runtimes/")) return true;

            // Skip documentation folders since they sometimes have HTML docs with JavaScript, which Unity tried to parse as "UnityScript"
            if (path.StartsWith("docs", StringComparison.Ordinal) || path.Contains("/docs/")) return true;

            // Skip ref folder, as it is just used for compile-time reference and does not contain implementations.
            // Leaving it results in "assembly loading" and "multiple pre-compiled assemblies with same name" errors
            if (path.StartsWith("ref", StringComparison.Ordinal) || path.Contains("/ref/")) return true;

            // Skip all PDB files since Unity uses Mono and requires MDB files, which causes it to output "missing MDB" errors
            if (path.EndsWith(".pdb", StringComparison.Ordinal)) return true;

            // Skip all folders that contain localization resource file
            // Format of these entries is lib/<framework>/<language-code>/...
            if (path.StartsWith("lib", StringComparison.Ordinal) || path.Contains("/lib/"))
            {
                var libSlashIndex = path.IndexOf("lib/", StringComparison.Ordinal) + 4;

                var secondSlashIndex = path.IndexOf("/", libSlashIndex, StringComparison.Ordinal);
                if (secondSlashIndex == -1)
                {
                    return false;
                }

                var thirdSlashIndex = path.IndexOf("/", secondSlashIndex + 1, StringComparison.Ordinal);
                if (thirdSlashIndex == -1)
                {
                    return false;
                }

                var langLength = thirdSlashIndex - secondSlashIndex - 1;
                if (langLength == 2 || langLength > 2 && path[secondSlashIndex + 3] == '-')
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetPackageInstallDirectory(INugetPackageIdentifier package)
        {
            return Path.Combine(ConfigurationManager.NugetConfigFile.RepositoryPath, $"{package.Id}.{package.Version}");
        }

        private static string GetPackageOutsideInstallDirectory(INugetPackageIdentifier package)
        {
            return Path.Combine(UnityPathHelper.AbsoluteProjectPath, "Packages", $"{package.Id}.{package.Version}");
        }
    }
}
