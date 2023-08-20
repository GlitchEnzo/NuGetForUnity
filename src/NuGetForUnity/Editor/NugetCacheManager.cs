using System;
using System.IO;

namespace NugetForUnity
{
    internal static class NugetCacheManager
    {
        /// <summary>
        ///     The path where to put created (packed) and downloaded (not installed yet) .nupkg files.
        /// </summary>
        internal static readonly string CacheOutputDirectory = Path.Combine(
            Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)),
            "NuGet",
            "Cache");

        /// <summary>
        ///     Initializes static members of the <see cref="NugetCacheManager" /> class.
        ///     Static constructor called only once.
        /// </summary>
        static NugetCacheManager()
        {
            // create the nupkgs directory, if it doesn't exist
            Directory.CreateDirectory(CacheOutputDirectory);
        }

        /// <summary>
        ///     Gets a NugetPackage from the NuGet server with the exact ID and Version given.
        /// </summary>
        /// <param name="packageId">The <see cref="INugetPackageIdentifier" /> containing the ID and Version of the package to get.</param>
        /// <returns>The retrieved package, if there is one.  Null if no matching package was found.</returns>
        internal static INugetPackage GetPackageFromCacheOrSource(INugetPackageIdentifier packageId)
        {
            // First look to see if the package is already installed
            var package = InstalledPackagesManager.GetInstalledPackage(packageId);

            if (package == null)
            {
                // That package isn't installed yet, so look in the cache next
                package = GetCachedPackage(packageId);
            }

            if (package == null)
            {
                // It's not in the cache, so we need to look in the active sources
                package = GetOnlinePackage(packageId);
            }

            return package;
        }

        /// <summary>
        ///     Tries to find an already cached package that matches the given package ID.
        /// </summary>
        /// <param name="packageId">The <see cref="INugetPackageIdentifier" /> of the <see cref="NugetPackageLocal" /> to find.</param>
        /// <returns>The best <see cref="NugetPackageLocal" /> match, if there is one, otherwise null.</returns>
        private static NugetPackageLocal GetCachedPackage(INugetPackageIdentifier packageId)
        {
            if (ConfigurationManager.NugetConfigFile.InstallFromCache && !packageId.HasVersionRange)
            {
                var cachedPackagePath = Path.Combine(CacheOutputDirectory, packageId.PackageFileName);

                if (File.Exists(cachedPackagePath))
                {
                    NugetLogger.LogVerbose("Found exact package in the cache: {0}", cachedPackagePath);
                    return NugetPackageLocal.FromNupkgFile(
                        cachedPackagePath,
                        new NugetPackageSourceLocal("Nupkg file from cache", Path.GetDirectoryName(cachedPackagePath)));
                }
            }

            return null;
        }

        /// <summary>
        ///     Tries to find an "online" (in the package sources - which could be local) package that matches (or is in the range of) the given package ID.
        /// </summary>
        /// <param name="packageId">The <see cref="INugetPackageIdentifier" /> of the <see cref="INugetPackage" /> to find.</param>
        /// <returns>The best <see cref="INugetPackage" /> match, if there is one, otherwise null.</returns>
        private static INugetPackage GetOnlinePackage(INugetPackageIdentifier packageId)
        {
            var package = ConfigurationManager.GetSpecificPackage(packageId);

            if (package != null)
            {
                NugetLogger.LogVerbose("{0} {1} not found, using {2}", packageId.Id, packageId.Version, package.Version);
            }
            else
            {
                NugetLogger.LogVerbose("Failed to find {0} {1}", packageId.Id, packageId.Version);
            }

            return package;
        }
    }
}
