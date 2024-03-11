using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using JetBrains.Annotations;
using NugetForUnity.Configuration;
using NugetForUnity.Models;
using NugetForUnity.PackageSource;
using NugetForUnity.PluginSupport;
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
        [CanBeNull]
        private static Dictionary<string, INugetPackage> installedPackages;

        /// <summary>
        ///     Backing field for the packages.config file.
        /// </summary>
        [CanBeNull]
        private static PackagesConfigFile packagesConfigFile;

        /// <summary>
        ///     Gets the loaded packages.config file that hold the dependencies for the project.
        /// </summary>
        [NotNull]
        public static PackagesConfigFile PackagesConfigFile
        {
            get
            {
                if (packagesConfigFile is null)
                {
                    packagesConfigFile = PackagesConfigFile.Load();
                }

                return packagesConfigFile;
            }
        }

        /// <summary>
        ///     Gets the packages that are actually installed in the project.
        /// </summary>
        [NotNull]
        public static IEnumerable<INugetPackage> InstalledPackages => InstalledPackagesDictionary.Values;

        /// <summary>
        ///     Gets the dictionary of packages that are actually installed in the project, keyed off of the ID.
        /// </summary>
        [NotNull]
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
        [CanBeNull]
        internal static PackageConfig GetPackageConfigurationById([NotNull] string packageIdentifier)
        {
            return PackagesConfigFile.GetPackageConfigurationById(packageIdentifier);
        }

        /// <summary>
        ///     Returns whether or not the specified package has been marked as manually installed inside the packages.config file.
        /// </summary>
        /// <param name="packageIdentifier">The package identifier used to search the package in the packages.config (<see cref="INugetPackageIdentifier.Id" />).</param>
        /// <returns>True if the package has been manually installed or no configuration was found, false otherwise.</returns>
        internal static bool GetManuallyInstalledFlagFromConfiguration([NotNull] string packageIdentifier)
        {
            var packageConfiguration = GetPackageConfigurationById(packageIdentifier);
            return packageConfiguration == null || packageConfiguration.IsManuallyInstalled;
        }

        /// <summary>
        ///     Sets the manually installed flag for the given package and stores the change in the <see cref="PackagesConfigFile" />.
        /// </summary>
        /// <param name="package">The package to mark as manually installed.</param>
        internal static void SetManuallyInstalledFlag([NotNull] INugetPackageIdentifier package)
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
        internal static void RemovePackage([NotNull] INugetPackage package)
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
        /// <returns>The newly added or allready existing config entry from the packages.config file.</returns>
        [NotNull]
        internal static PackageConfig AddPackageToConfig([NotNull] INugetPackage package)
        {
            var packageConfig = PackagesConfigFile.AddPackage(package);
            PackagesConfigFile.Save();
            return packageConfig;
        }

        /// <summary>
        ///     Adds the package to the installed packages dictionary (<see cref="InstalledPackages" />).
        /// </summary>
        /// <param name="package">The package to add.</param>
        internal static void AddPackageToInstalled([NotNull] INugetPackage package)
        {
            InstalledPackagesDictionary.Add(package.Id, package);
        }

        /// <summary>
        ///     Gets the package from the installed packages dictionary (<see cref="InstalledPackages" />) by its ID.
        /// </summary>
        /// <param name="id">The id of the package to search in the currently installed packages.</param>
        /// <param name="package">The found installed package.</param>
        /// <returns>True if a package was found.</returns>
        internal static bool TryGetById([NotNull] string id, out INugetPackage package)
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

            // a package that was installed via a old version of NuGetForUnity will have the .nupkg it came from inside the folder,
            // as we updated the behavior to only keep the .nuspec. So we need to extract the .nuspec from the .nupkg
            var nupkgFiles = Directory.GetFiles(ConfigurationManager.NugetConfigFile.RepositoryPath, "*.nupkg", SearchOption.AllDirectories);
            foreach (var nupkgFile in nupkgFiles)
            {
                using (var zip = ZipFile.OpenRead(nupkgFile))
                {
                    foreach (var entry in zip.Entries)
                    {
                        var entryFullName = entry.FullName;

                        // extract only .nuspec and .nuspec.meta files
                        if (entryFullName.EndsWith(".nuspec.meta", StringComparison.Ordinal) ||
                            entryFullName.EndsWith(".nuspec", StringComparison.Ordinal))
                        {
                            PackageContentManager.ExtractPackageEntry(
                                entry,
                                Path.GetDirectoryName(nupkgFile) ??
                                throw new InvalidOperationException($"Failed to get directory from '{nupkgFile}'"));
                        }
                    }
                }

                // delete .nupkg and its .meta file to sync compatibility
                File.Delete(nupkgFile);
                var metaFile = $"{nupkgFile}.meta";
                if (File.Exists(metaFile))
                {
                    File.Delete($"{nupkgFile}.meta");
                }
            }

            var nuspecFiles = Directory.GetFiles(ConfigurationManager.NugetConfigFile.RepositoryPath, "*.nuspec", SearchOption.AllDirectories);
            foreach (var nuspecFile in nuspecFiles)
            {
                var directoryName = Path.GetFileName(Path.GetDirectoryName(nuspecFile));
                if (!string.IsNullOrEmpty(directoryName) && directoryName[0] == '.')
                {
                    // Skip nuspec files that are in directories starting with '.' since those are considered hidden and should be ignored.
                    continue;
                }

                var package = NugetPackageLocal.FromNuspec(
                    NuspecFile.Load(nuspecFile),
                    new NugetPackageSourceLocal(
                        "Nuspec file from Project",
                        Path.GetDirectoryName(nuspecFile) ?? throw new InvalidOperationException($"Failed to get directory from '{nuspecFile}'")));
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
                if (folderName.StartsWith(".", StringComparison.Ordinal))
                {
                    // ignore folders whose name starts with a dot because they are considered hidden
                    continue;
                }

                var nuspecPath = Directory.GetFiles(folder, "*.nuspec").FirstOrDefault();
                if (!File.Exists(nuspecPath))
                {
                    // ignore folder not containing a nuspec file
                    continue;
                }

                var package = NugetPackageLocal.FromNuspecFile(
                    nuspecPath,
                    new NugetPackageSourceLocal(
                        "Nuspec file already installed",
                        Path.GetDirectoryName(nuspecPath) ?? throw new InvalidOperationException($"Failed to get directory from '{nuspecPath}'")));

                var installed = PackagesConfigFile.Packages.Exists(packageId => packageId.Equals(package));

                if (!installed)
                {
                    somethingDeleted = true;
                    NugetLogger.LogVerbose("---DELETE unnecessary package {0}", folder);

                    PackageContentManager.DeletePackageContentPackage(package);
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
        /// <param name="checkIsAlreadyImportedInEngine">Determine if it should check if the package is already imported by unity itself.</param>
        /// <returns>True if the given package is installed.  False if it is not.</returns>
        internal static bool IsInstalled([NotNull] INugetPackageIdentifier package, bool checkIsAlreadyImportedInEngine)
        {
            if (checkIsAlreadyImportedInEngine && UnityPreImportedLibraryResolver.IsAlreadyImportedInEngine(package.Id))
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
        ///     Checks if any version of the given package Id is installed.
        /// </summary>
        /// <param name="packageId">The package to check if is installed.</param>
        /// <param name="checkIsAlreadyImportedInEngine">Determine if it should check if the package is already imported by unity itself.</param>
        /// <returns>True if the given package is installed.  False if it is not.</returns>
        internal static bool IsInstalled([NotNull] string packageId, bool checkIsAlreadyImportedInEngine)
        {
            if (checkIsAlreadyImportedInEngine && UnityPreImportedLibraryResolver.IsAlreadyImportedInEngine(packageId))
            {
                return true;
            }

            return InstalledPackagesDictionary.ContainsKey(packageId);
        }

        /// <summary>
        ///     Gets a list of all root packages that are installed in the project.
        ///     Root packages are packages that are not depended on by any other package.
        /// </summary>
        /// <returns>The root packages.</returns>
        [NotNull]
        [ItemNotNull]
        internal static List<INugetPackage> GetInstalledRootPackages()
        {
            // default all packages to being roots
            var roots = new List<INugetPackage>(InstalledPackages);

            // remove a package as a root if another package is dependent on it
            foreach (var package in InstalledPackages)
            {
                var frameworkDependencies = package.CurrentFrameworkDependencies;
                foreach (var dependency in frameworkDependencies)
                {
                    roots.RemoveAll(p => p.Id == dependency.Id);
                }
            }

            return roots;
        }

        private static void AddPackageToInstalledInternal([NotNull] INugetPackage package, ref int manuallyInstalledPackagesNumber)
        {
            var packages = InstalledPackagesDictionary;
            if (!packages.ContainsKey(package.Id))
            {
                PluginRegistry.Instance.ProcessInstalledPackage(package);
                package.IsManuallyInstalled = GetPackageConfigurationById(package.Id)?.IsManuallyInstalled ?? false;
                if (package.IsManuallyInstalled)
                {
                    manuallyInstalledPackagesNumber++;
                }

                packages.Add(package.Id, package);
            }
            else
            {
                Debug.LogErrorFormat("Package is already in installed list: {0}", package.Id);
            }
        }
    }
}
