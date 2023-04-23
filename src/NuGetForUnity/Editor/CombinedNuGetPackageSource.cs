using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace NugetForUnity
{
    internal sealed class CombinedNuGetPackageSource : INuGetPackageSource
    {
        private readonly List<INuGetPackageSource> packageSources;

        /// <summary>
        ///     Initializes a new instance of the <see cref="CombinedNuGetPackageSource" /> class.
        /// </summary>
        /// <param name="packageSources">The package source's to combine the results from.</param>
        public CombinedNuGetPackageSource(List<INuGetPackageSource> packageSources)
        {
            this.packageSources = packageSources ?? throw new ArgumentNullException(nameof(packageSources));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // packageSources are disposed by them self.
        }

        /// <inheritdoc />
        public string Name
        {
            get => "All";

            set
            {
            }
        }

        /// <inheritdoc />
        public bool IsEnabled { get; set; }

        /// <inheritdoc />
        public bool IsLocalPath => true;

        /// <inheritdoc />
        public string SavedPassword
        {
            get => null;

            set
            {
            }
        }

        /// <inheritdoc />
        public string SavedPath
        {
            get => "(Aggregate source)";

            set
            {
            }
        }

        /// <inheritdoc />
        public string UserName
        {
            get => null;

            set
            {
            }
        }

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
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public INuGetPackage GetSpecificPackage(INuGetPackageIdentifier package)
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
            INuGetPackage bestMatch = null;
            foreach (var source in packageSources.Where(s => s.IsEnabled))
            {
                var foundPackage = source.GetSpecificPackage(package);
                if (foundPackage == null)
                {
                    continue;
                }

                if (foundPackage.Version == package.Version)
                {
                    NugetHelper.LogVerbose("{0} {1} was found in {2}", foundPackage.Id, foundPackage.Version, source.Name);
                    return foundPackage;
                }

                NugetHelper.LogVerbose(
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
        public List<INuGetPackage> GetUpdates(IEnumerable<INuGetPackage> packages,
            bool includePrerelease = false,
            bool includeAllVersions = false,
            string targetFrameworks = "",
            string versionContraints = "")
        {
            var activeSourcesCount = packageSources.Count(source => source.IsEnabled);
            if (activeSourcesCount == 0)
            {
                Debug.LogWarning("There are no active package sources. Check your nuget.config.");
                return new List<INuGetPackage>();
            }

            if (activeSourcesCount == 1)
            {
                return packageSources.First(source => source.IsEnabled)
                    .GetUpdates(packages, includePrerelease, includeAllVersions, targetFrameworks, versionContraints);
            }

            return packageSources.Where(source => source.IsEnabled)
                .SelectMany(source => source.GetUpdates(packages, includePrerelease, includeAllVersions, targetFrameworks, versionContraints))
                .Distinct()
                .ToList();
        }

        /// <inheritdoc />
        public Task<List<INuGetPackage>> Search(string searchTerm = "",
            bool includeAllVersions = false,
            bool includePrerelease = false,
            int numberToGet = 15,
            int numberToSkip = 0,
            CancellationToken cancellationToken = default)
        {
            var activeSourcesCount = packageSources.Count(source => source.IsEnabled);
            if (activeSourcesCount == 0)
            {
                Debug.LogWarning("There are no active package sources. Check your nuget.config.");
                return Task.FromResult(new List<INuGetPackage>());
            }

            if (activeSourcesCount == 1)
            {
                return packageSources.First(source => source.IsEnabled)
                    .Search(searchTerm, includeAllVersions, includePrerelease, numberToGet, numberToSkip, cancellationToken);
            }

            return SearchMultiple(searchTerm, includeAllVersions, includePrerelease, numberToGet, numberToSkip, cancellationToken);
        }

        /// <inheritdoc />
        public void DownloadNupkgToFile(INuGetPackageIdentifier package, string outputFilePath, string downloadUrlHint)
        {
            throw new NotImplementedException("This shouldn't happen / is currently not implemented.");
        }

        private async Task<List<INuGetPackage>> SearchMultiple(string searchTerm = "",
            bool includeAllVersions = false,
            bool includePrerelease = false,
            int numberToGet = 15,
            int numberToSkip = 0,
            CancellationToken cancellationToken = default)
        {
            var results = await Task.WhenAll(
                packageSources.Where(source => source.IsEnabled)
                    .Select(
                        source => source.Search(searchTerm, includeAllVersions, includePrerelease, numberToGet, numberToSkip, cancellationToken)));
            return results.SelectMany(result => result).Distinct().ToList();
        }
    }
}
