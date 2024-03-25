using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using JetBrains.Annotations;
using NugetForUnity.Configuration;
using NugetForUnity.Models;
using NugetForUnity.PluginSupport;
using UnityEditor;
using UnityEngine;

namespace NugetForUnity
{
    /// <summary>
    ///     Installs NuGet packages into the Unity project. Opposite of <see cref="NugetPackageUninstaller" />.
    /// </summary>
    public static class NugetPackageInstaller
    {
        /// <summary>
        ///     Installs the package given by the identifier. It fetches the appropriate full package from the installed packages, package cache, or package
        ///     sources and installs it.
        /// </summary>
        /// <param name="package">The identifier of the package to install.</param>
        /// <param name="refreshAssets">True to refresh the Unity asset database.  False to ignore the changes (temporarily).</param>
        /// <param name="isSlimRestoreInstall">True to skip checking if lib is imported in Unity and skip installing dependencies.</param>
        /// <returns>True if the package was installed successfully, otherwise false.</returns>
        public static bool InstallIdentifier([NotNull] INugetPackageIdentifier package, bool refreshAssets = true, bool isSlimRestoreInstall = false)
        {
            if (!isSlimRestoreInstall && UnityPreImportedLibraryResolver.IsAlreadyImportedInEngine(package.Id, false))
            {
                NugetLogger.LogVerbose("Package {0} is already imported in engine, skipping install.", package);
                return true;
            }

            var foundPackage = PackageCacheManager.GetPackageFromCacheOrSource(package);

            if (foundPackage == null)
            {
                Debug.LogErrorFormat("Could not find {0} {1} or greater.", package.Id, package.Version);
                return false;
            }

            foundPackage.IsManuallyInstalled = package.IsManuallyInstalled;
            return Install(foundPackage, refreshAssets, isSlimRestoreInstall);
        }

        /// <summary>
        ///     Installs the given package.
        /// </summary>
        /// <param name="package">The package to install.</param>
        /// <param name="refreshAssets">True to refresh the Unity asset database.  False to ignore the changes (temporarily).</param>
        /// <param name="isSlimRestoreInstall">True to skip checking if lib is imported in Unity and skip installing dependencies.</param>
        /// <returns>True if the package was installed successfully, otherwise false.</returns>
        private static bool Install([NotNull] INugetPackage package, bool refreshAssets, bool isSlimRestoreInstall)
        {
            if (!isSlimRestoreInstall && UnityPreImportedLibraryResolver.IsAlreadyImportedInEngine(package.Id, false))
            {
                NugetLogger.LogVerbose("Package {0} is already imported in engine, skipping install.", package);
                return true;
            }

            // check if the package (any version) is already installed
            if (InstalledPackagesManager.TryGetById(package.Id, out var installedPackage))
            {
                var comparisonResult = installedPackage.CompareTo(package);
                if (comparisonResult < 0)
                {
                    NugetLogger.LogVerbose(
                        "{0} {1} is installed, but need {2} or greater. Updating to {3}",
                        installedPackage.Id,
                        installedPackage.Version,
                        package.Version,
                        package.Version);
                    return NugetPackageUpdater.Update(installedPackage, package, refreshAssets);
                }

                if (comparisonResult > 0)
                {
                    var configPackage = InstalledPackagesManager.GetPackageConfigurationById(package.Id);
                    if (configPackage != null && configPackage.PackageVersion < installedPackage.PackageVersion)
                    {
                        NugetLogger.LogVerbose(
                            "{0} {1} is installed but config needs {2} so downgrading.",
                            installedPackage.Id,
                            installedPackage.Version,
                            package.Version);
                        return NugetPackageUpdater.Update(installedPackage, package, refreshAssets);
                    }

                    NugetLogger.LogVerbose(
                        "{0} {1} is installed. {2} or greater is needed, so using installed version.",
                        installedPackage.Id,
                        installedPackage.Version,
                        package.Version);
                }
                else
                {
                    NugetLogger.LogVerbose("Already installed: {0} {1}", package.Id, package.Version);
                }

                return true;
            }

            try
            {
                NugetLogger.LogVerbose("Installing: {0} {1}", package.Id, package.Version);

                EditorUtility.DisplayProgressBar($"Installing {package.Id} {package.Version}", "Installing Dependencies", 0.1f);

                if (!isSlimRestoreInstall)
                {
                    var dependencyGroups = package.Dependencies;

                    // install all dependencies for target framework
                    var frameworkGroup = TargetFrameworkResolver.GetNullableBestDependencyFrameworkGroupForCurrentSettings(
                        dependencyGroups,
                        InstalledPackagesManager.GetPackageConfigurationById(package.Id)?.TargetFramework);

                    if (frameworkGroup == null && dependencyGroups.Count != 0)
                    {
                        Debug.LogWarningFormat(
                            "Can't find a matching dependency group for the NuGet Package {0} {1} that has a TargetFramework supported by the current Unity Scripting Backend. The NuGet Package supports the following TargetFramework's: {2}",
                            package.Id,
                            package.Version,
                            string.Join(", ", dependencyGroups.Select(dependency => dependency.TargetFramework)));
                    }
                    else if (frameworkGroup != null)
                    {
                        NugetLogger.LogVerbose("Installing dependencies for TargetFramework: {0}", frameworkGroup.TargetFramework);
                        foreach (var dependency in frameworkGroup.Dependencies)
                        {
                            NugetLogger.LogVerbose("Installing Dependency: {0} {1}", dependency.Id, dependency.Version);
                            var installed = InstallIdentifier(dependency, false);
                            if (!installed)
                            {
                                throw new InvalidOperationException($"Failed to install dependency: {dependency.Id} {dependency.Version}.");
                            }
                        }
                    }
                }

                // update packages.config
                var packageConfig = InstalledPackagesManager.AddPackageToConfig(package);

                var cachedPackagePath = Path.Combine(PackageCacheManager.CacheOutputDirectory, package.PackageFileName);
                if (ConfigurationManager.NugetConfigFile.InstallFromCache && File.Exists(cachedPackagePath))
                {
                    NugetLogger.LogVerbose("Cached package found for {0} {1}", package.Id, package.Version);
                }
                else
                {
                    NugetLogger.LogVerbose("Downloading package {0} {1}", package.Id, package.Version);

                    EditorUtility.DisplayProgressBar($"Installing {package.Id} {package.Version}", "Downloading Package", 0.3f);

                    package.DownloadNupkgToFile(cachedPackagePath);
                }

                EditorUtility.DisplayProgressBar($"Installing {package.Id} {package.Version}", "Extracting Package", 0.6f);

                if (File.Exists(cachedPackagePath))
                {
                    var baseDirectory = package.GetPackageInstallPath();

                    // unzip the package
                    using (var zip = ZipFile.OpenRead(cachedPackagePath))
                    {
                        var libs = new Dictionary<string, List<ZipArchiveEntry>>();
                        var csFiles = new Dictionary<string, List<ZipArchiveEntry>>();
                        var anyFiles = new Dictionary<string, List<ZipArchiveEntry>>();

                        foreach (var entry in zip.Entries)
                        {
                            var entryFullName = entry.FullName;

                            if (PluginRegistry.Instance.HandleFileExtraction(package, entry, baseDirectory))
                            {
                                continue;
                            }

                            if (PackageContentManager.ShouldSkipUnpackingOnPath(entryFullName))
                            {
                                continue;
                            }

                            // we don't want to unpack all lib folders and then delete all but one; rather we will decide the best
                            // target framework before unpacking, but first we need to collect all lib entries from zip
                            const string libDirectoryName = "lib/";
                            if (entryFullName.StartsWith(libDirectoryName, StringComparison.Ordinal))
                            {
                                var frameworkStartIndex = libDirectoryName.Length;
                                var secondSlashIndex = entryFullName.IndexOf('/', frameworkStartIndex);
                                if (secondSlashIndex == -1)
                                {
                                    // a file inside lib folder -> we keep it. This is to support packages that have no framework dependent sub directories.
                                    PackageContentManager.ExtractPackageEntry(entry, baseDirectory);
                                    continue;
                                }

                                var framework = entryFullName.Substring(libDirectoryName.Length, secondSlashIndex - frameworkStartIndex);
                                FillFrameworkZipEntries(libs, framework, entry);

                                continue;
                            }

                            // in case this is a source code package, we want to collect all its entries that have 'cs' or 'any' set as language
                            // and their frameworks so we can get the best framework later
                            const string contentFilesDirectoryName = "contentFiles/";
                            if (entryFullName.StartsWith(contentFilesDirectoryName, StringComparison.Ordinal))
                            {
                                // Folder structure for source code packages:
                                // └─<packageID>.<packageVersion> (not counted here since entries start with next subfolder)
                                //   └─<contentFiles>
                                //     └─<any> (language)
                                //       └─<any> (framework)
                                //         └─<sources>
                                // In order to create shorter paths, we aim to make a structure like this:
                                // └─<packageID>.<packageVersion>
                                //   └─Sources
                                //      └─<sources>
                                var directoriesSplit = entryFullName.Split('/');
                                if (directoriesSplit.Length >= 4)
                                {
                                    var language = directoriesSplit[1];
                                    var framework = directoriesSplit[2];

                                    if (language.Equals("cs", StringComparison.Ordinal))
                                    {
                                        FillFrameworkZipEntries(csFiles, framework, entry);
                                    }

                                    if (language.Equals("any", StringComparison.Ordinal))
                                    {
                                        FillFrameworkZipEntries(anyFiles, framework, entry);
                                    }
                                }

                                // if the entry is in content files we want to skip unpacking it right now anyway
                                continue;
                            }

                            PackageContentManager.ExtractPackageEntry(entry, baseDirectory);
                        }

                        // go through all lib zip entries and find the best target framework, then unpack it
                        if (libs.Count > 0)
                        {
                            var bestFrameworkMatch = TargetFrameworkResolver.TryGetBestTargetFramework(
                                libs,
                                packageConfig.TargetFramework,
                                framework => framework.Key);
                            if (bestFrameworkMatch.Value != null)
                            {
                                NugetLogger.LogVerbose(
                                    "Selecting target framework directory '{0}' as best match for the package {1}",
                                    bestFrameworkMatch.Key,
                                    package);
                                foreach (var entry in bestFrameworkMatch.Value)
                                {
                                    PackageContentManager.ExtractPackageEntry(entry, baseDirectory);
                                }
                            }
                            else
                            {
                                Debug.LogWarningFormat(
                                    "Couldn't find a library folder with a supported target-framework for the package {0}",
                                    package);
                            }
                        }

                        // go through all content files' frameworks and figure the best target network, prioritizing 'cs' over 'any' language
                        if (csFiles.Count > 0)
                        {
                            TryExtractBestFrameworkSources(csFiles, baseDirectory, package, packageConfig);
                        }
                        else if (anyFiles.Count > 0)
                        {
                            TryExtractBestFrameworkSources(anyFiles, baseDirectory, package, packageConfig);
                        }
                    }
                }
                else
                {
                    Debug.LogErrorFormat("File not found: {0}", cachedPackagePath);
                }

                EditorUtility.DisplayProgressBar($"Installing {package.Id} {package.Version}", "Cleaning Package", 0.9f);

                // clean
                PackageContentManager.CleanInstallationDirectory(package);

                // update the installed packages list
                InstalledPackagesManager.AddPackageToInstalled(package);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogErrorFormat("Unable to install package {0} {1}\n{2}", package.Id, package.Version, e);
                return false;
            }
            finally
            {
                if (refreshAssets)
                {
                    EditorUtility.DisplayProgressBar($"Installing {package.Id} {package.Version}", "Importing Package", 0.95f);
                    AssetDatabase.Refresh();
                }

                EditorUtility.ClearProgressBar();
            }
        }

        private static void TryExtractBestFrameworkSources(
            [NotNull] [ItemNotNull] IReadOnlyDictionary<string, List<ZipArchiveEntry>> frameworks,
            [NotNull] string sourceDirName,
            [NotNull] INugetPackage package,
            [NotNull] PackageConfig packageConfig)
        {
            var bestFrameworkMatch = TargetFrameworkResolver.TryGetBestTargetFramework(
                frameworks,
                packageConfig.TargetFramework,
                framework => framework.Key);
            var frameworkKey = bestFrameworkMatch.Key ?? "any";

            if (frameworks.TryGetValue(frameworkKey, out var bestFramework))
            {
                NugetLogger.LogVerbose(
                    "Selecting target framework directory '{0}' and language '{1}' as best match for the package {2}",
                    bestFramework,
                    sourceDirName,
                    package);

                PackageContentManager.ExtractPackageSources(bestFramework, sourceDirName);
            }
            else
            {
                Debug.LogWarningFormat("Couldn't find a source code folder with a supported target-framework for the package {0}", package);
            }
        }

        private static void FillFrameworkZipEntries(
            IDictionary<string, List<ZipArchiveEntry>> frameworkZipEntries,
            string framework,
            ZipArchiveEntry entry)
        {
            if (!frameworkZipEntries.TryGetValue(framework, out var entryList))
            {
                entryList = new List<ZipArchiveEntry>();
                frameworkZipEntries.Add(framework, entryList);
            }

            entryList.Add(entry);
        }
    }
}
