using System.IO;
using System.Linq;
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

            // delete a remnant .meta file that may exist from packages created by Unity
            FileSystemHelper.DeleteFile(Path.Combine(packageInstallDirectory, $"{package.Id}.nuspec.meta"));

            // delete directories & files that NuGet normally deletes, but since we are installing "manually" they exist
            FileSystemHelper.DeleteDirectory(Path.Combine(packageInstallDirectory, "_rels"), false);
            FileSystemHelper.DeleteDirectory(Path.Combine(packageInstallDirectory, "package"), false);
            FileSystemHelper.DeleteFile(Path.Combine(packageInstallDirectory, $"{package.Id}.nuspec"));
            FileSystemHelper.DeleteFile(Path.Combine(packageInstallDirectory, "[Content_Types].xml"));

            // Unity has no use for the build directory
            FileSystemHelper.DeleteDirectory(Path.Combine(packageInstallDirectory, "build"), false);

            // For now, delete src.  We may use it later...
            FileSystemHelper.DeleteDirectory(Path.Combine(packageInstallDirectory, "src"), false);

            // Since we don't automatically fix up the runtime dll platforms, remove them until we improve support
            // for this newer feature of nuget packages.
            FileSystemHelper.DeleteDirectory(Path.Combine(packageInstallDirectory, "runtimes"), false);

            // Delete documentation folders since they sometimes have HTML docs with JavaScript, which Unity tried to parse as "UnityScript"
            FileSystemHelper.DeleteDirectory(Path.Combine(packageInstallDirectory, "docs"), false);

            // Delete ref folder, as it is just used for compile-time reference and does not contain implementations.
            // Leaving it results in "assembly loading" and "multiple pre-compiled assemblies with same name" errors
            FileSystemHelper.DeleteDirectory(Path.Combine(packageInstallDirectory, "ref"), false);

            var packageLibsDirectory = Path.Combine(packageInstallDirectory, "lib");
            if (Directory.Exists(packageLibsDirectory))
            {
                // go through the library folders in descending order (highest to lowest version)
                var libDirectories = new DirectoryInfo(packageLibsDirectory).GetDirectories();

                var bestLibDirectory = TargetFrameworkResolver.TryGetBestTargetFramework(libDirectories, directory => directory.Name);
                if (bestLibDirectory == null)
                {
                    Debug.LogWarningFormat("Couldn't find a library folder with a supported target-framework for the package {0}", package);
                }
                else
                {
                    NugetLogger.LogVerbose(
                        "Selecting directory '{0}' with the best target framework {1} for current settings",
                        bestLibDirectory,
                        bestLibDirectory.Name);
                }

                // delete all of the libraries except for the selected one
                foreach (var directory in libDirectories)
                {
                    // we use reference equality as the TargetFrameworkResolver returns the input reference.
                    if (directory != bestLibDirectory)
                    {
                        FileSystemHelper.DeleteDirectory(directory.FullName, false);
                    }
                }

                if (bestLibDirectory != null)
                {
                    // some older packages e.g. Microsoft.CodeAnalysis.Common 2.10.0 have multiple localization resource files
                    // e.g. Microsoft.CodeAnalysis.resources.dll each inside a folder with the language name as a folder name e.g. zh-Hant or fr
                    // unity doesn't support importing multiple assemblies with the same file name.
                    // for now we just delete all folders so the language neutral version is used and Unity is happy.
                    var languageSupFolders = bestLibDirectory.GetDirectories();
                    if (languageSupFolders.All(languageSupFolder => languageSupFolder.Name.Split('-').FirstOrDefault()?.Length == 2))
                    {
                        foreach (var languageSupFolder in languageSupFolders)
                        {
                            languageSupFolder.Delete(true);
                        }
                    }
                }
            }

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

            // delete all PDB files since Unity uses Mono and requires MDB files, which causes it to output "missing MDB" errors
            FileSystemHelper.DeleteAllFiles(packageInstallDirectory, "*.pdb");

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
