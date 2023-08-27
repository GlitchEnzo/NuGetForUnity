using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NugetForUnity.Configuration;
using NugetForUnity.Models;
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
        /// <param name="installDependencies">True to also install all dependencies of the <paramref name="package" />.</param>
        /// <returns>True if the package was installed successfully, otherwise false.</returns>
        public static bool InstallIdentifier(INugetPackageIdentifier package, bool refreshAssets = true, bool installDependencies = true)
        {
            if (UnityPreImportedLibraryResolver.IsAlreadyImportedInEngine(package, false))
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
            return Install(foundPackage, refreshAssets, installDependencies);
        }

        /// <summary>
        ///     Installs the given package.
        /// </summary>
        /// <param name="package">The package to install.</param>
        /// <param name="refreshAssets">True to refresh the Unity asset database.  False to ignore the changes (temporarily).</param>
        /// <param name="installDependencies">True to also install all dependencies of the <paramref name="package" />.</param>
        /// <returns>True if the package was installed successfully, otherwise false.</returns>
        private static bool Install(INugetPackage package, bool refreshAssets, bool installDependencies)
        {
            if (UnityPreImportedLibraryResolver.IsAlreadyImportedInEngine(package, false))
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
                    return NugetPackageUpdater.Update(installedPackage, package, false);
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
                        return NugetPackageUpdater.Update(installedPackage, package, false);
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

                if (refreshAssets)
                {
                    EditorUtility.DisplayProgressBar($"Installing {package.Id} {package.Version}", "Installing Dependencies", 0.1f);
                }

                if (installDependencies)
                {
                    var dependencies = package.Dependencies;

                    // install all dependencies for target framework
                    var frameworkGroup = TargetFrameworkResolver.GetNullableBestDependencyFrameworkGroupForCurrentSettings(dependencies);

                    if (frameworkGroup == null && dependencies.Count != 0)
                    {
                        Debug.LogWarningFormat(
                            "Can't find a matching dependency group for the NuGet Package {0} {1} that has a TargetFramework supported by the current Unity Scripting Backend. The NuGet Package supports the following TargetFramework's: {2}",
                            package.Id,
                            package.Version,
                            string.Join(", ", dependencies.Select(dependency => dependency.TargetFramework)));
                    }
                    else if (frameworkGroup != null)
                    {
                        NugetLogger.LogVerbose("Installing dependencies for TargetFramework: {0}", frameworkGroup.TargetFramework);
                        foreach (var dependency in frameworkGroup.Dependencies)
                        {
                            NugetLogger.LogVerbose("Installing Dependency: {0} {1}", dependency.Id, dependency.Version);
                            var installed = InstallIdentifier(dependency, refreshAssets);
                            if (!installed)
                            {
                                throw new InvalidOperationException($"Failed to install dependency: {dependency.Id} {dependency.Version}.");
                            }
                        }
                    }
                }

                // update packages.config
                InstalledPackagesManager.AddPackageToConfig(package);

                var cachedPackagePath = Path.Combine(PackageCacheManager.CacheOutputDirectory, package.PackageFileName);
                if (ConfigurationManager.NugetConfigFile.InstallFromCache && File.Exists(cachedPackagePath))
                {
                    NugetLogger.LogVerbose("Cached package found for {0} {1}", package.Id, package.Version);
                }
                else
                {
                    NugetLogger.LogVerbose("Downloading package {0} {1}", package.Id, package.Version);

                    if (refreshAssets)
                    {
                        EditorUtility.DisplayProgressBar($"Installing {package.Id} {package.Version}", "Downloading Package", 0.3f);
                    }

                    package.DownloadNupkgToFile(cachedPackagePath);
                }

                if (refreshAssets)
                {
                    EditorUtility.DisplayProgressBar($"Installing {package.Id} {package.Version}", "Extracting Package", 0.6f);
                }

                if (File.Exists(cachedPackagePath))
                {
                    var baseDirectory = Path.Combine(ConfigurationManager.NugetConfigFile.RepositoryPath, $"{package.Id}.{package.Version}");

                    // unzip the package
                    using (var zip = ZipFile.OpenRead(cachedPackagePath))
                    {
                        foreach (var entry in zip.Entries)
                        {
                            var filePath = Path.Combine(baseDirectory, entry.FullName);

                            var directory = Path.GetDirectoryName(filePath) ??
                                            throw new InvalidOperationException($"Failed to get directory name of '{filePath}'");
                            Directory.CreateDirectory(directory);
                            if (Directory.Exists(filePath))
                            {
                                Debug.LogWarning($"The path {filePath} refers to an existing directory. Overwriting it may lead to data loss.");
                                continue;
                            }

                            entry.ExtractToFile(filePath, true);

                            if (ConfigurationManager.NugetConfigFile.ReadOnlyPackageFiles)
                            {
                                var extractedFile = new FileInfo(filePath);
                                extractedFile.Attributes |= FileAttributes.ReadOnly;
                            }
                        }
                    }

                    // copy the .nupkg inside the Unity project
                    File.Copy(cachedPackagePath, Path.Combine(baseDirectory, package.PackageFileName), true);
                }
                else
                {
                    Debug.LogErrorFormat("File not found: {0}", cachedPackagePath);
                }

                if (refreshAssets)
                {
                    EditorUtility.DisplayProgressBar($"Installing {package.Id} {package.Version}", "Cleaning Package", 0.9f);
                }

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
                    EditorUtility.ClearProgressBar();
                }
            }
        }
    }
}
