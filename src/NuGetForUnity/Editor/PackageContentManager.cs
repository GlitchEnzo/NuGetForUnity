using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using JetBrains.Annotations;
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
        internal static void DeletePackageContentPackage([NotNull] INugetPackageIdentifier package)
        {
            var packageInstallDirectory = package.GetPackageInstallPath();
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
        internal static void CleanInstallationDirectory([NotNull] INugetPackageIdentifier package)
        {
            var packageInstallDirectory = package.GetPackageInstallPath();

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

        /// <summary>
        ///     Specifies if a file should be extracted from a .nupkg, because NuGetForUnity needs it.
        /// </summary>
        /// <param name="path">
        ///     The path of the file inside the .nupkg it is relative starting from the package route. It always uses '/' as a slash on all
        ///     platforms.
        /// </param>
        /// <returns>True if the file can be skipped, is not needed.</returns>
        internal static bool ShouldSkipUnpackingOnPath([NotNull] string path)
        {
            if (path.EndsWith("/"))
            {
                // We do not want to extract empty directory entries. If there are empty directories within the .nupkg, we
                // expect them to have a file named '_._' in them that indicates that it should be extracted, usually as a
                // compatibility indicator (https://stackoverflow.com/questions/36338052/what-do-files-mean-in-nuget-packages)
                return true;
            }

            // skip directories & files that NuGet normally deletes
            if (path.StartsWith("_rels/", StringComparison.Ordinal) || path.Contains("/_rels/"))
            {
                return true;
            }

            if (path.StartsWith("package/", StringComparison.Ordinal) || path.Contains("/package/"))
            {
                return true;
            }

            if (path.EndsWith("[Content_Types].xml", StringComparison.Ordinal))
            {
                return true;
            }

            // Unity has no use for the build directory
            if (path.StartsWith("build/", StringComparison.Ordinal) || path.Contains("/build/"))
            {
                return true;
            }

            // For now, skip src. We may use it later...
            if (path.StartsWith("src/", StringComparison.Ordinal) || path.Contains("/src/"))
            {
                return true;
            }

            // Since we don't automatically fix up the runtime dll platforms, skip them until we improve support
            // for this newer feature of nuget packages.
            if (path.StartsWith("runtimes/", StringComparison.Ordinal) || path.Contains("/runtimes/"))
            {
                return true;
            }

            // Skip documentation folders since they sometimes have HTML docs with JavaScript, which Unity tried to parse as "UnityScript"
            if (path.StartsWith("docs/", StringComparison.Ordinal) || path.Contains("/docs/"))
            {
                return true;
            }

            // Skip ref folder, as it is just used for compile-time reference and does not contain implementations.
            // Leaving it results in "assembly loading" and "multiple pre-compiled assemblies with same name" errors
            if (path.StartsWith("ref/", StringComparison.Ordinal) || path.Contains("/ref/"))
            {
                return true;
            }

            // Skip all PDB files since Unity uses Mono and requires MDB files, which causes it to output "missing MDB" errors
            if (path.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Skip all folders that contain localization resource file
            // Format of these entries is lib/<framework>/<language-code>/...
            const string libDirectoryName = "lib/";
            if (path.StartsWith(libDirectoryName, StringComparison.Ordinal) || path.Contains("/lib/"))
            {
                var libSlashIndex = path.IndexOf(libDirectoryName, StringComparison.Ordinal) + libDirectoryName.Length;

                var secondSlashIndex = path.IndexOf('/', libSlashIndex);
                if (secondSlashIndex == -1)
                {
                    return false;
                }

                var thirdSlashIndex = path.IndexOf('/', secondSlashIndex + 1);
                if (thirdSlashIndex == -1)
                {
                    return false;
                }

                var langLength = thirdSlashIndex - secondSlashIndex - 1;
                if (langLength == 2 || (langLength > 2 && path[secondSlashIndex + 3] == '-'))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Extracts source files from a source code .nupkg <see cref="ZipArchive" /> into the <paramref name="baseDir" />/Sources.
        /// </summary>
        /// <param name="entries">The source file entries from the .nupkg zip file.</param>
        /// <param name="baseDir">The path of the directory under which the 'Sources' sub-directory should be placed.</param>
        internal static void ExtractPackageSources([NotNull] List<ZipArchiveEntry> entries, [NotNull] string baseDir)
        {
            if (entries.Count == 0)
            {
                return;
            }

            var lastCommonDir = entries[0].FullName;
            lastCommonDir = lastCommonDir.Substring(0, lastCommonDir.LastIndexOf('/') + 1);
            for (var i = 1; i < entries.Count; ++i)
            {
                var entryFullName = entries[i].FullName;
                for (var j = 0; j < entryFullName.Length && j < lastCommonDir.Length; j++)
                {
                    if (entryFullName[j] != lastCommonDir[j])
                    {
                        lastCommonDir = entryFullName.Substring(0, entryFullName.LastIndexOf('/', j) + 1);
                        break;
                    }
                }
            }

            var sourcesDirectory = Path.Combine(baseDir, "Sources");
            if (!Directory.Exists(sourcesDirectory))
            {
                Directory.CreateDirectory(sourcesDirectory);
            }

            foreach (var entry in entries)
            {
                ExtractPackageEntry(entry, sourcesDirectory, lastCommonDir.Length);
            }
        }

        /// <summary>
        ///     Extracts a file from a .nupkg <see cref="ZipArchive" /> into the <paramref name="baseDir" />.
        /// </summary>
        /// <param name="entry">The file entry from the .nupkg zip file.</param>
        /// <param name="baseDir">The path of the directory where the package output should be placed.</param>
        /// <param name="skipEntryLength">Index to which we want to skip within path located in FullPath of 'entry' parameter.</param>
        internal static void ExtractPackageEntry([NotNull] ZipArchiveEntry entry, [NotNull] string baseDir, int skipEntryLength = 0)
        {
            // Normalizes the path.
            baseDir = Path.GetFullPath(baseDir);

            // Ensures that the last character on the extraction path is the directory separator char.
            if (!baseDir.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                baseDir += Path.DirectorySeparatorChar;
            }

            // Gets the full path to ensure that relative segments are removed.
            var entryFullName = entry.FullName;
            if (skipEntryLength > 0)
            {
                entryFullName = entryFullName.Substring(skipEntryLength);
            }

            var filePath = Path.GetFullPath(Path.Combine(baseDir, entryFullName));
            if (!filePath.StartsWith(baseDir, StringComparison.Ordinal))
            {
                Debug.LogWarning($"Entry {entryFullName} is trying to leave the output directory. We skip it.");
                return;
            }

            var directory = Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException($"Failed to get directory name of '{filePath}'");
            Directory.CreateDirectory(directory);
            if (Directory.Exists(filePath))
            {
                Debug.LogWarning($"The path {filePath} refers to an existing directory. Overwriting it may lead to data loss.");
                return;
            }

            entry.ExtractToFile(filePath, true);

            if (ConfigurationManager.NugetConfigFile.ReadOnlyPackageFiles)
            {
                var extractedFile = new FileInfo(filePath);
                extractedFile.Attributes |= FileAttributes.ReadOnly;
            }
        }

        /// <summary>
        ///     Moves already installed packages from the old to a new path.
        /// </summary>
        /// <param name="oldPath">Old package installation absolute path.</param>
        /// <param name="newPath">New package installation absolute path.</param>
        internal static void MoveInstalledPackages(string oldPath, string newPath)
        {
            if (!Directory.Exists(oldPath))
            {
                return;
            }

            Directory.CreateDirectory(newPath);

            // We only move the package folders because users might have other things in that folder
            foreach (var package in InstalledPackagesManager.InstalledPackages)
            {
                var packageFolderName = $"{package.Id}.{package.Version}";
                var oldPackagePath = Path.Combine(oldPath, packageFolderName);
                var newPackagePath = Path.Combine(newPath, packageFolderName);
                if (Directory.Exists(oldPackagePath))
                {
                    Directory.Move(oldPackagePath, newPackagePath);
                    var oldMetaPath = $"{oldPackagePath}.meta";
                    if (File.Exists(oldMetaPath))
                    {
                        File.Move(oldMetaPath, $"{newPackagePath}.meta");
                    }
                }
            }

            var oldPackageJsonPath = Path.Combine(oldPath, "package.json");
            if (File.Exists(oldPackageJsonPath))
            {
                File.Delete(oldPackageJsonPath);
                var oldPackageJsonMetaPath = $"{oldPackageJsonPath}.meta";
                if (File.Exists(oldPackageJsonMetaPath))
                {
                    File.Delete(oldPackageJsonMetaPath);
                }
            }

            if (Directory.EnumerateFileSystemEntries(oldPath).Any())
            {
                return;
            }

            // If there is nothing left in the oldPath we remove the whole directory
            Directory.Delete(oldPath);
            var metaPath = oldPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
        }

        [NotNull]
        private static string GetPackageOutsideInstallDirectory([NotNull] INugetPackageIdentifier package)
        {
            return Path.Combine(UnityPathHelper.AbsoluteProjectPath, "Packages", $"{package.Id}.{package.Version}");
        }
    }
}
