﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace NugetForUnity
{
    /// <summary>
    ///     Represents a NuGet Package Source that uses packages stored on a local or network drive.
    /// </summary>
    [Serializable]
    internal sealed class LocalNuGetPackageSource : INuGetPackageSource
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="LocalNuGetPackageSource" /> class.
        /// </summary>
        /// <param name="name">The name of the package source.</param>
        /// <param name="path">The path to the package source.</param>
        public LocalNuGetPackageSource(string name, string path)
        {
            Name = name;
            SavedPath = path;
            IsEnabled = true;
        }

        /// <summary>
        ///     Gets path, with the values of environment variables expanded.
        /// </summary>
        private string ExpandedPath
        {
            get
            {
                var path = Environment.ExpandEnvironmentVariables(SavedPath);
                if (!Path.IsPathRooted(path))
                {
                    path = Path.Combine(Path.GetDirectoryName(NugetHelper.NugetConfigFilePath), path);
                }

                return path;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // nothing to dispose
        }

        /// <inheritdoc />
        [field: SerializeField]
        public string Name { get; set; }

        /// <inheritdoc />
        [field: SerializeField]
        public string SavedPath { get; set; }

        /// <inheritdoc />
        public string UserName
        {
            get => string.Empty;

            set
            {
            }
        }

        /// <inheritdoc />
        public string SavedPassword
        {
            get => string.Empty;

            set
            {
            }
        }

        /// <inheritdoc />
        public bool IsLocalPath => true;

        /// <inheritdoc />
        [field: SerializeField]
        public bool IsEnabled { get; set; }

        /// <inheritdoc />
        public bool HasPassword
        {
            get => false;

            set
            {
            }
        }

        /// <inheritdoc />
        public List<INuGetPackage> FindPackagesById(INuGetPackageIdentifier package)
        {
            List<INuGetPackage> foundPackages = null;

            if (!package.HasVersionRange && !string.IsNullOrEmpty(package.Version))
            {
                var localPackagePath = GetNuPkgFilePath(package);

                if (File.Exists(localPackagePath))
                {
                    var localPackage = NugetPackage.FromNupkgFile(localPackagePath, this);
                    foundPackages = new List<INuGetPackage> { localPackage };
                }
                else
                {
                    foundPackages = new List<INuGetPackage>();
                }
            }
            else
            {
                // TODO: Optimize to no longer use GetLocalPackages, since that loads the .nupkg itself
                foundPackages = GetLocalPackages($"{package.Id}*", true, true);
            }

            if (foundPackages != null)
            {
                // Return all the packages in the range of versions specified by 'package'.
                foundPackages.RemoveAll(p => !package.InRange(p));
                foundPackages.Sort();
            }

            return foundPackages;
        }

        /// <inheritdoc />
        public INuGetPackage GetSpecificPackage(INuGetPackageIdentifier package)
        {
            // if multiple match we use the lowest version
            return FindPackagesById(package).FirstOrDefault();
        }

        /// <inheritdoc />
        public Task<List<INuGetPackage>> Search(
            string searchTerm = "",
            bool includeAllVersions = false,
            bool includePrerelease = false,
            int numberToGet = 15,
            int numberToSkip = 0,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GetLocalPackages($"*{searchTerm}*", includeAllVersions, includePrerelease, numberToSkip));
        }

        /// <inheritdoc />
        public List<INuGetPackage> GetUpdates(
            IEnumerable<INuGetPackage> packages,
            bool includePrerelease = false,
            bool includeAllVersions = false,
            string targetFrameworks = "",
            string versionConstraints = "")
        {
            return GetLocalUpdates(packages, includePrerelease, includeAllVersions);
        }

        /// <inheritdoc />
        public void DownloadNupkgToFile(INuGetPackageIdentifier package, string outputFilePath, string downloadUrlHint)
        {
            if (!string.IsNullOrEmpty(downloadUrlHint))
            {
                downloadUrlHint = GetNuPkgFilePath(package);
            }

            File.Copy(downloadUrlHint, outputFilePath, true);
        }

        private string GetNuPkgFilePath(INuGetPackageIdentifier package)
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
        /// <param name="includeAllVersions">True to include older versions that are not the latest version.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="numberToSkip">The number of packages to skip before fetching.</param>
        /// <returns>The list of available packages.</returns>
        private List<INuGetPackage> GetLocalPackages(
            string searchTerm = "",
            bool includeAllVersions = false,
            bool includePrerelease = false,
            int numberToSkip = 0)
        {
            if (string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = "*";
            }

            var localPackages = new List<INuGetPackage>();
            if (numberToSkip != 0)
            {
                // we return the entire list the first time, so no more to add
                return localPackages;
            }

            var path = ExpandedPath;
            if (Directory.Exists(path))
            {
                var packagePaths = Directory.GetFiles(path, $"{searchTerm}.nupkg");

                // Hierarchical folder structures are supported in NuGet 3.3+.
                // └─<packageID>
                //   └─<version>
                //     └─<packageID>.<version>.nupkg
                var packagesFromFolders = Directory.GetDirectories(path, searchTerm)
                    .SelectMany(nameFolder => Directory.GetDirectories(nameFolder))
                    .SelectMany(versionFolder => Directory.GetFiles(versionFolder, "*.nupkg"));
                foreach (var packagePath in packagePaths.Concat(packagesFromFolders))
                {
                    var package = NugetPackage.FromNupkgFile(packagePath, this);

                    if (package.IsPrerelease && !includePrerelease)
                    {
                        // if it's a prerelease package and we aren't supposed to return prerelease packages, just skip it
                        continue;
                    }

                    if (includeAllVersions)
                    {
                        // if all versions are being included, simply add it and move on
                        localPackages.Add(package);
                        continue;
                    }

                    var existingPackage = localPackages.FirstOrDefault(x => x.Id == package.Id);
                    if (existingPackage != null)
                    {
                        // there is already a package with the same ID in the list
                        if (existingPackage.CompareTo(package) < 0)
                        {
                            // if the current package is newer than the existing package, swap them
                            localPackages.Remove(existingPackage);
                            localPackages.Add(package);
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
        ///     Gets a list of available packages from a local source (not a web server) that are upgrades for the given list of installed packages.
        /// </summary>
        /// <param name="packages">The list of packages to use to find updates.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="includeAllVersions">True to include older versions that are not the latest version.</param>
        /// <returns>A list of all updates available.</returns>
        private List<INuGetPackage> GetLocalUpdates(
            IEnumerable<INuGetPackage> packages,
            bool includePrerelease = false,
            bool includeAllVersions = false)
        {
            var updates = new List<INuGetPackage>();

            var availablePackages = GetLocalPackages(string.Empty, includeAllVersions, includePrerelease);
            foreach (var installedPackage in packages)
            {
                foreach (var availablePackage in availablePackages)
                {
                    if (installedPackage.Id == availablePackage.Id)
                    {
                        if (installedPackage.CompareTo(availablePackage) < 0)
                        {
                            updates.Add(availablePackage);
                        }
                    }
                }
            }

            return updates;
        }
    }
}
