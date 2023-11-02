using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NugetForUnity.Models;
using UnityEngine;

namespace NugetForUnity.PackageSource
{
    /// <summary>
    ///     A package source that combines the results from multiple other package sources.
    /// </summary>
    internal sealed class NugetPackageSourceCombined : INugetPackageSource
    {
        [NotNull]
        [ItemNotNull]
        private readonly List<INugetPackageSource> packageSources;

        /// <summary>
        ///     Initializes a new instance of the <see cref="NugetPackageSourceCombined" /> class.
        /// </summary>
        /// <param name="packageSources">The package source's to combine the results from.</param>
        public NugetPackageSourceCombined([NotNull] [ItemNotNull] List<INugetPackageSource> packageSources)
        {
            this.packageSources = packageSources ?? throw new ArgumentNullException(nameof(packageSources));
        }

        /// <inheritdoc />
        public string Name
        {
            get => "All";

            set
            {
                // this is a fixed value
            }
        }

        /// <inheritdoc />
        public bool IsEnabled { get; set; }

        /// <inheritdoc />
        public string SavedPath
        {
            get => "(Aggregate source)";

            set
            {
                // this is a fixed value
            }
        }

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
        public bool SavedPasswordIsEncrypted
        {
            get => false;

            set
            {
                // multiple sources can't have credentials
            }
        }

        /// <inheritdoc />
        public string SavedPassword
        {
            get => null;

            set
            {
                // multiple sources can't have credentials
            }
        }

        /// <inheritdoc />
        public string UserName
        {
            get => null;

            set
            {
                // multiple sources can't have credentials
            }
        }

        /// <inheritdoc />
        public bool HasPassword
        {
            get => false;

            set
            {
                // multiple sources can't have passwords
            }
        }

        /// <inheritdoc />
        public List<INugetPackage> FindPackagesById(INugetPackageIdentifier package)
        {
            var activeSourcesCount = packageSources.Count(source => source.IsEnabled);
            if (activeSourcesCount == 0)
            {
                Debug.LogWarning("There are no active package sources. Check your nuget.config.");
                return new List<INugetPackage>();
            }

            if (activeSourcesCount == 1)
            {
                return packageSources.First(source => source.IsEnabled).FindPackagesById(package);
            }

            return packageSources.Where(source => source.IsEnabled).SelectMany(source => source.FindPackagesById(package)).Distinct().ToList();
        }

        /// <inheritdoc />
        public INugetPackage GetSpecificPackage(INugetPackageIdentifier package)
        {
            var activeSourcesCount = packageSources.Count(source => source.IsEnabled);
            if (activeSourcesCount == 0)
            {
                Debug.LogWarning("There are no active package sources. Check your nuget.config.");
                return null;
            }

            if (activeSourcesCount == 1)
            {
                return packageSources.First(source => source.IsEnabled).GetSpecificPackage(package);
            }

            // Loop through all active sources and stop once the package is found
            INugetPackage bestMatch = null;
            foreach (var source in packageSources.Where(s => s.IsEnabled))
            {
                var foundPackage = source.GetSpecificPackage(package);
                if (foundPackage == null)
                {
                    continue;
                }

                if (foundPackage.Version == package.Version)
                {
                    NugetLogger.LogVerbose("{0} {1} was found in {2}", foundPackage.Id, foundPackage.Version, source.Name);
                    return foundPackage;
                }

                NugetLogger.LogVerbose(
                    "{0} {1} was found in {2}, but wanted {3}",
                    foundPackage.Id,
                    foundPackage.Version,
                    source.Name,
                    package.Version);
                if (bestMatch == null)
                {
                    // if another package hasn't been found yet, use the current found one
                    bestMatch = foundPackage;
                }

                // another package has been found previously, but neither match identically
                else if (foundPackage.CompareTo(bestMatch) > 0)
                {
                    // use the new package if it's closer to the desired version
                    bestMatch = foundPackage;
                }
            }

            return bestMatch;
        }

        /// <inheritdoc />
        public List<INugetPackage> GetUpdates(
            IEnumerable<INugetPackage> packages,
            bool includePrerelease = false,
            string targetFrameworks = "",
            string versionConstraints = "")
        {
            var activeSourcesCount = packageSources.Count(source => source.IsEnabled);
            if (activeSourcesCount == 0)
            {
                Debug.LogWarning("There are no active package sources. Check your nuget.config.");
                return new List<INugetPackage>();
            }

            if (activeSourcesCount == 1)
            {
                return packageSources.First(source => source.IsEnabled).GetUpdates(packages, includePrerelease, targetFrameworks, versionConstraints);
            }

            return packageSources.Where(source => source.IsEnabled)
                .SelectMany(source => source.GetUpdates(packages, includePrerelease, targetFrameworks, versionConstraints))
                .Distinct()
                .ToList();
        }

        /// <inheritdoc />
        public Task<List<INugetPackage>> SearchAsync(
            string searchTerm = "",
            bool includePrerelease = false,
            int numberToGet = 15,
            int numberToSkip = 0,
            CancellationToken cancellationToken = default)
        {
            var activeSourcesCount = packageSources.Count(source => source.IsEnabled);
            if (activeSourcesCount == 0)
            {
                Debug.LogWarning("There are no active package sources. Check your nuget.config.");
                return Task.FromResult(new List<INugetPackage>());
            }

            if (activeSourcesCount == 1)
            {
                return packageSources.First(source => source.IsEnabled)
                    .SearchAsync(searchTerm, includePrerelease, numberToGet, numberToSkip, cancellationToken);
            }

            return SearchMultipleAsync(searchTerm, includePrerelease, numberToGet, numberToSkip, cancellationToken);
        }

        /// <inheritdoc />
        public void DownloadNupkgToFile(INugetPackageIdentifier package, string outputFilePath, string downloadUrlHint)
        {
            throw new NotImplementedException(
                "This shouldn't happen / is currently not implemented as each package has its own 'DownloadNupkgToFile' method.");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // packageSources are disposed by them self.
        }

        [NotNull]
        [ItemNotNull]
        private async Task<List<INugetPackage>> SearchMultipleAsync(
            string searchTerm = "",
            bool includePrerelease = false,
            int numberToGet = 15,
            int numberToSkip = 0,
            CancellationToken cancellationToken = default)
        {
            var results = await Task.WhenAll(
                packageSources.Where(source => source.IsEnabled)
                    .Select(source => source.SearchAsync(searchTerm, includePrerelease, numberToGet, numberToSkip, cancellationToken)));
            return results.SelectMany(result => result).Distinct().ToList();
        }
    }
}
