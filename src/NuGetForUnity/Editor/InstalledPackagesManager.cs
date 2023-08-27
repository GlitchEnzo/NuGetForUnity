using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NugetForUnity.Configuration;
using NugetForUnity.Helper;
using NugetForUnity.Models;
using NugetForUnity.PackageSource;
using Debug = UnityEngine.Debug;

namespace NugetForUnity
{
    /// <summary>
    ///     Manages the packages currently installed in the unity project.
    /// </summary>
    public static class InstalledPackagesManager
    {
        /// <summary>
        ///     The dictionary of currently installed <see cref="INugetPackage" />s keyed off of their ID string.
        /// </summary>
        private static Dictionary<string, INugetPackage> installedPackages;

        /// <summary>
        ///     Backing field for the packages.config file.
        /// </summary>
        private static PackagesConfigFile packagesConfigFile;

        /// <summary>
        ///     Gets the loaded packages.config file that hold the dependencies for the project.
        /// </summary>
        public static PackagesConfigFile PackagesConfigFile
        {
            get
            {
                if (packagesConfigFile == null)
                {
                    packagesConfigFile = PackagesConfigFile.Load();
                }

                return packagesConfigFile;
            }
        }

        /// <summary>
        ///     Gets the packages that are actually installed in the project.
        /// </summary>
        public static IEnumerable<INugetPackage> InstalledPackages => InstalledPackagesDictionary.Values;

        /// <summary>
        ///     Gets the dictionary of packages that are actually installed in the project, keyed off of the ID.
        /// </summary>
        private static Dictionary<string, INugetPackage> InstalledPackagesDictionary
        {
            get
            {
                if (installedPackages == null)
                {
                    UpdateInstalledPackages();
                }

                return installedPackages;
            }
        }

        /// <summary>
        ///     Gets the configuration of the given package from the packages.config file.
        /// </summary>
        /// <param name="packageIdentifier">The package identifier used to search the package in the packages.config (<see cref="INugetPackageIdentifier.Id" />).</param>
        /// <returns>The found configuration or <c>null</c> if there is no matching package inside the packages.config.</returns>
        internal static PackageConfig GetPackageConfigurationById(string packageIdentifier)
        {
            return PackagesConfigFile.Packages.Find(pkg => string.Equals(pkg.Id, packageIdentifier, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        ///     Returns whether or not the specified package has been marked as manually installed inside the packages.config file.
        /// </summary>
        /// <param name="packageIdentifier">The package identifier used to search the package in the packages.config (<see cref="INugetPackageIdentifier.Id" />).</param>
        /// <returns>True if the package has been manually installed or no configuration was found, false otherwise.</returns>
        internal static bool GetManuallyInstalledFlagFromConfiguration(string packageIdentifier)
        {
            var packageConfiguration = GetPackageConfigurationById(packageIdentifier);
            return packageConfiguration == null || packageConfiguration.IsManuallyInstalled;
        }

        /// <summary>
        ///     Sets the manually installed flag for the given package and stores the change in the <see cref="PackagesConfigFile" />.
        /// </summary>
        /// <param name="package">The package to mark as manually installed.</param>
        internal static void SetManuallyInstalledFlag(INugetPackageIdentifier package)
        {
            PackagesConfigFile.SetManuallyInstalledFlag(package);
            PackagesConfigFile.Save();
        }

        /// <summary>
        ///     Invalidates the currently loaded 'packages.config' so it is reloaded when it is accessed the next time.
        /// </summary>
        internal static void ReloadPackagesConfig()
        {
            packagesConfigFile = null;
        }

        /// <summary>
        ///     Removes the package from the 'packages.config' and from the installed packages dictionary (<see cref="InstalledPackages" />).
        /// </summary>
        /// <param name="package">The package to remove.</param>
        internal static void RemovePackage(INugetPackage package)
        {
            // update the package.config file
            if (PackagesConfigFile.RemovePackage(package))
            {
                PackagesConfigFile.Save();
            }

            InstalledPackagesDictionary.Remove(package.Id);
        }

        /// <summary>
        ///     Adds the package to the 'packages.config' file.
        /// </summary>
        /// <param name="package">The package to add.</param>
        internal static void AddPackageToConfig(INugetPackage package)
        {
            PackagesConfigFile.AddPackage(package);
            PackagesConfigFile.Save();
        }

        /// <summary>
        ///     Adds the package to the installed packages dictionary (<see cref="InstalledPackages" />).
        /// </summary>
        /// <param name="package">The package to add.</param>
        internal static void AddPackageToInstalled(INugetPackage package)
        {
            InstalledPackagesDictionary.Add(package.Id, package);
        }

        /// <summary>
        ///     Gets the package from the installed packages dictionary (<see cref="InstalledPackages" />) by its ID.
        /// </summary>
        /// <param name="id">The id of the package to search in the currently installed packages.</param>
        /// <param name="package">The found installed package.</param>
        /// <returns>True if a package was found.</returns>
        internal static bool TryGetById(string id, out INugetPackage package)
        {
            return InstalledPackagesDictionary.TryGetValue(id, out package);
        }

        /// <summary>
        ///     Updates the dictionary of packages that are actually installed in the project based on the files that are currently installed.
        /// </summary>
        internal static void UpdateInstalledPackages()
        {
            if (installedPackages == null)
            {
                installedPackages = new Dictionary<string, INugetPackage>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                installedPackages.Clear();
            }

            // loops through the packages that are actually installed in the project
            if (!Directory.Exists(ConfigurationManager.NugetConfigFile.RepositoryPath))
            {
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            var manuallyInstalledPackagesNumber = 0;

            // a package that was installed via NuGet will have the .nupkg it came from inside the folder
            var nupkgFiles = Directory.GetFiles(ConfigurationManager.NugetConfigFile.RepositoryPath, "*.nupkg", SearchOption.AllDirectories);
            foreach (var nupkgFile in nupkgFiles)
            {
                var package = NugetPackageLocal.FromNupkgFile(
                    nupkgFile,
                    new NugetPackageSourceLocal("Nupkg file from Project", Path.GetDirectoryName(nupkgFile)));
                AddPackageToInstalledInternal(package, ref manuallyInstalledPackagesNumber);
            }

            // if the source code & assets for a package are pulled directly into the project (ex: via a symlink/junction) it should have a .nuspec defining the package
            var nuspecFiles = Directory.GetFiles(ConfigurationManager.NugetConfigFile.RepositoryPath, "*.nuspec", SearchOption.AllDirectories);
            foreach (var nuspecFile in nuspecFiles)
            {
                var package = NugetPackageLocal.FromNuspec(
                    NuspecFile.Load(nuspecFile),
                    new NugetPackageSourceLocal("Nuspec file from Project", Path.GetDirectoryName(nuspecFile)));
                AddPackageToInstalledInternal(package, ref manuallyInstalledPackagesNumber);
            }

            if (manuallyInstalledPackagesNumber == 0)
            {
                // set root packages as manually installed if none are marked as such
                foreach (var rootPackage in GetInstalledRootPackages())
                {
                    PackagesConfigFile.SetManuallyInstalledFlag(rootPackage);
                }

                PackagesConfigFile.Save();
            }

            NugetLogger.LogVerbose("Getting installed packages took {0} ms", stopwatch.ElapsedMilliseconds);
        }

        /// <summary>
        ///     Checks if there are any packages inside the package install directory that are not listed inside the packages.config.
        /// </summary>
        /// <returns>True if some packages are deleted.</returns>
        internal static bool RemoveUnnecessaryPackages()
        {
            if (!Directory.Exists(ConfigurationManager.NugetConfigFile.RepositoryPath))
            {
                return false;
            }

            var directories = Directory.GetDirectories(ConfigurationManager.NugetConfigFile.RepositoryPath, "*", SearchOption.TopDirectoryOnly);
            var somethingDeleted = false;
            foreach (var folder in directories)
            {
                var folderName = Path.GetFileName(folder);
                if (folderName.Equals(".svn", StringComparison.OrdinalIgnoreCase))
                {
                    // ignore folder required by SVN tool
                    continue;
                }

                var pkgPath = Path.Combine(folder, $"{folderName}.nupkg");
                if (!File.Exists(pkgPath))
                {
                    // ignore folder not containing a nuget-package
                    continue;
                }

                var package = NugetPackageLocal.FromNupkgFile(
                    pkgPath,
                    new NugetPackageSourceLocal("Nupkg file already installed", Path.GetDirectoryName(pkgPath)));

                var installed = PackagesConfigFile.Packages.Any(packageId => packageId.Equals(package));

                if (!installed)
                {
                    somethingDeleted = true;
                    NugetLogger.LogVerbose("---DELETE unnecessary package {0}", folder);

                    FileSystemHelper.DeleteDirectory(folder, false);
                    FileSystemHelper.DeleteFile($"{folder}.meta");
                }
            }

            if (somethingDeleted)
            {
                UpdateInstalledPackages();
            }

            return somethingDeleted;
        }

        /// <summary>
        ///     Checks if a given package is installed.
        /// </summary>
        /// <param name="package">The package to check if is installed.</param>
        /// <returns>True if the given package is installed.  False if it is not.</returns>
        internal static bool IsInstalled(INugetPackageIdentifier package)
        {
            if (UnityPreImportedLibraryResolver.IsAlreadyImportedInEngine(package))
            {
                return true;
            }

            var isInstalled = false;
            if (InstalledPackagesDictionary.TryGetValue(package.Id, out var installedPackage))
            {
                isInstalled = package.Equals(installedPackage);
            }

            return isInstalled;
        }

        /// <summary>
        ///     Gets a list of all root packages that are installed in the project.
        ///     Root packages are packages that are not depended on by any other package.
        /// </summary>
        /// <returns>The root packages.</returns>
        internal static List<INugetPackage> GetInstalledRootPackages()
        {
            // default all packages to being roots
            var roots = new List<INugetPackage>(InstalledPackages);

            // remove a package as a root if another package is dependent on it
            foreach (var package in InstalledPackages)
            {
                var frameworkGroup = TargetFrameworkResolver.GetBestDependencyFrameworkGroupForCurrentSettings(package);
                foreach (var dependency in frameworkGroup.Dependencies)
                {
                    roots.RemoveAll(p => p.Id == dependency.Id);
                }
            }

            return roots;
        }

        private static void AddPackageToInstalledInternal(INugetPackage package, ref int manuallyInstalledPackagesNumber)
        {
            if (!installedPackages.ContainsKey(package.Id))
            {
                package.IsManuallyInstalled = GetPackageConfigurationById(package.Id)?.IsManuallyInstalled ?? false;
                if (package.IsManuallyInstalled)
                {
                    manuallyInstalledPackagesNumber++;
                }

                installedPackages.Add(package.Id, package);
            }
            else
            {
                Debug.LogErrorFormat("Package is already in installed list: {0}", package.Id);
            }
        }
    }
}
