using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NugetForUnity.Configuration;
using NugetForUnity.Models;
using UnityEngine;

namespace NugetForUnity.PackageSource
{
    /// <summary>
    ///     Represents a NuGet Package Source that uses packages stored on a local or network drive.
    /// </summary>
    [Serializable]
    internal sealed class NugetPackageSourceLocal : INugetPackageSource
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="NugetPackageSourceLocal" /> class.
        /// </summary>
        /// <param name="name">The name of the package source.</param>
        /// <param name="path">The path to the package source.</param>
        public NugetPackageSourceLocal([NotNull] string name, [NotNull] string path)
        {
            Name = name;
            SavedPath = path;
            IsEnabled = true;
        }

        /// <inheritdoc />
        [field: SerializeField]
        public string Name { get; set; }

        /// <inheritdoc />
        [field: SerializeField]
        public string SavedPath { get; set; }

        /// <inheritdoc />
        public string SavedProtocolVersion
        {
            get => null;

            set
            {
                // multiple sources can't have protocol version
            }
        }

        /// <inheritdoc />
        [field: SerializeField]
        public bool IsEnabled { get; set; }

        /// <inheritdoc />
        public string UserName
        {
            get => null;

            set
            {
                // local sources don't have credentials
            }
        }

        /// <inheritdoc />
        public string SavedPassword
        {
            get => null;

            set
            {
                // local sources don't have credentials
            }
        }

        /// <inheritdoc />
        public bool SavedPasswordIsEncrypted
        {
            get => false;

            set
            {
                // local sources don't have credentials
            }
        }

        /// <inheritdoc />
        public bool HasPassword
        {
            get => false;

            set
            {
                // local sources don't have credentials
            }
        }

        /// <summary>
        ///     Gets path, with the values of environment variables expanded.
        /// </summary>
        [NotNull]
        private string ExpandedPath
        {
            get
            {
                var path = Environment.ExpandEnvironmentVariables(SavedPath);
                if (!Path.IsPathRooted(path))
                {
                    path = Path.Combine(ConfigurationManager.NugetConfigFileDirectoryPath, path);
                }

                return path;
            }
        }

        /// <inheritdoc />
        public List<INugetPackage> FindPackagesById(INugetPackageIdentifier package)
        {
            List<INugetPackage> foundPackages;

            if (!package.HasVersionRange && !string.IsNullOrEmpty(package.Version))
            {
                var localPackagePath = GetNuPkgFilePath(package);

                if (File.Exists(localPackagePath))
                {
                    var localPackage = NugetPackageLocal.FromNupkgFile(localPackagePath, this);
                    foundPackages = new List<INugetPackage> { localPackage };
                }
                else
                {
                    foundPackages = new List<INugetPackage>();
                }
            }
            else
            {
                // TODO: Optimize to no longer use GetLocalPackages, since that loads the .nupkg itself
                foundPackages = GetLocalPackages($"{package.Id}*", true, true);
            }

            // Return all the packages in the range of versions specified by 'package'.
            foundPackages.RemoveAll(p => !package.InRange(p));
            foundPackages.Sort();
            return foundPackages;
        }

        /// <inheritdoc />
        public INugetPackage GetSpecificPackage(INugetPackageIdentifier package)
        {
            // if multiple match we use the lowest version
            return FindPackagesById(package).FirstOrDefault();
        }

        /// <inheritdoc />
        public Task<List<INugetPackage>> SearchAsync(
            string searchTerm = "",
            bool includePrerelease = false,
            int numberToGet = 15,
            int numberToSkip = 0,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GetLocalPackages($"*{searchTerm}*", false, includePrerelease, numberToSkip));
        }

        /// <inheritdoc />
        public List<INugetPackage> GetUpdates(
            IEnumerable<INugetPackage> packages,
            bool includePrerelease = false,
            string targetFrameworks = "",
            string versionConstraints = "")
        {
            return GetLocalUpdates(packages, includePrerelease);
        }

        /// <inheritdoc />
        public void DownloadNupkgToFile(INugetPackageIdentifier package, string outputFilePath, string downloadUrlHint)
        {
            if (string.IsNullOrEmpty(downloadUrlHint))
            {
                downloadUrlHint = GetNuPkgFilePath(package);
            }

            File.Copy(downloadUrlHint, outputFilePath, true);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // nothing to dispose
        }

        [NotNull]
        private string GetNuPkgFilePath([NotNull] INugetPackageIdentifier package)
        {
            if (package.HasVersionRange)
            {
                throw new InvalidOperationException($"The package '{package}' has a version range witch is not supported for this function.");
            }

            var localPackagePath = Path.Combine(ExpandedPath, $"{package.Id}.{package.Version}.nupkg");

            if (!File.Exists(localPackagePath))
            {
                // Hierarchical folder structures are supported in NuGet 3.3+.
                // └─<packageID>
                //   └─<version>
                //     └─<packageID>.<version>.nupkg
                localPackagePath = Path.Combine(ExpandedPath, package.Id, package.Version, $"{package.Id}.{package.Version}.nupkg");
            }

            return localPackagePath;
        }

        /// <summary>
        ///     Gets a list of all available packages from a local source (not a web server) that match the given filters.
        /// </summary>
        /// <param name="searchTerm">The search term to use to filter packages. Defaults to the empty string.</param>
        /// <param name="flattenAllVersions">True to include older versions that are not the latest version.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="numberToSkip">The number of packages to skip before fetching.</param>
        /// <returns>The list of available packages.</returns>
        [NotNull]
        [ItemNotNull]
        private List<INugetPackage> GetLocalPackages(
            string searchTerm = "",
            bool flattenAllVersions = false,
            bool includePrerelease = false,
            int numberToSkip = 0)
        {
            if (string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = "*";
            }

            var localPackages = new List<INugetPackage>();
            if (numberToSkip != 0)
            {
                // we return the entire list the first time, so no more to add
                return localPackages;
            }

            var path = ExpandedPath;
            if (Directory.Exists(path))
            {
                var packagePaths = Directory.EnumerateFiles(path, $"{searchTerm}.nupkg");

                // Hierarchical folder structures are supported in NuGet 3.3+.
                // └─<packageID>
                //   └─<version>
                //     └─<packageID>.<version>.nupkg
                var packagesFromFolders = Directory.EnumerateDirectories(path, searchTerm)
                    .SelectMany(Directory.EnumerateDirectories)
                    .SelectMany(versionFolder => Directory.EnumerateFiles(versionFolder, "*.nupkg"));
                foreach (var packagePath in packagePaths.Concat(packagesFromFolders))
                {
                    var package = NugetPackageLocal.FromNupkgFile(packagePath, this);

                    if (package.IsPrerelease && !includePrerelease)
                    {
                        // if it's a prerelease package and we aren't supposed to return prerelease packages, just skip it
                        continue;
                    }

                    if (flattenAllVersions)
                    {
                        // to flatten all versions we simply add it and move on
                        localPackages.Add(package);
                        continue;
                    }

                    var existingPackage = localPackages.Find(x => x.Id == package.Id);
                    if (existingPackage != null)
                    {
                        // there is already a package with the same ID in the list
                        if (existingPackage.CompareTo(package) < 0)
                        {
                            // if the current package is newer than the existing package, swap them
                            localPackages.Remove(existingPackage);
                            localPackages.Add(package);
                            package.Versions.AddRange(existingPackage.Versions);
                        }
                        else
                        {
                            // otherwise, just add the current package version to the existing package
                            existingPackage.Versions.Add(package.PackageVersion);
                        }
                    }
                    else
                    {
                        // there is no package with the same ID in the list yet
                        localPackages.Add(package);
                    }
                }
            }
            else
            {
                Debug.LogErrorFormat("Local folder not found: {0}", path);
            }

            return localPackages;
        }

        /// <summary>
        ///     Gets a list of available packages from a local source (not a web server) that are versions / upgrade or downgrade of the given list of installed
        ///     packages.
        /// </summary>
        /// <param name="packages">The list of packages to use to find updates.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <returns>A list of all updates / downgrades available.</returns>
        [NotNull]
        [ItemNotNull]
        private List<INugetPackage> GetLocalUpdates([NotNull] [ItemNotNull] IEnumerable<INugetPackage> packages, bool includePrerelease = false)
        {
            var updates = new List<INugetPackage>();
            foreach (var packageToSearch in packages)
            {
                var availablePackages = GetLocalPackages($"{packageToSearch.Id}*", false, includePrerelease);
                foreach (var availablePackage in availablePackages)
                {
                    if (packageToSearch.Id.Equals(availablePackage.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        // keep the manually installed state
                        availablePackage.IsManuallyInstalled = packageToSearch.IsManuallyInstalled;
                        updates.Add(availablePackage);
                    }
                }
            }

            return updates;
        }
    }
}
