
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;

namespace App
{
    public interface INugetApi
    {
        SearchFilter SearchFilter { get; set; }
        /// <summary>
        /// Gets a NugetPackage from the NuGet server that matches (or is in range of) the <see cref="NugetPackageIdentifier"/> given.
        /// </summary>
        /// <param name="package">The <see cref="NugetPackageIdentifier"/> containing the ID and Version of the package to get.</param>
        /// <returns>The retrieved package, if there is one.  Null if no matching package was found.</returns>
        IEnumerable<IPackageSearchMetadata> FindPackagesById(string id);

        // /// <summary>
        // /// Gets a NugetPackage from the NuGet server that matches (or is in range of) the <see cref="NugetPackageIdentifier"/> given.
        // /// </summary>
        // /// <param name="package">The <see cref="NugetPackageIdentifier"/> containing the ID and Version of the package to get.</param>
        // /// <returns>The retrieved package, if there is one.  Null if no matching package was found.</returns>
        // IPackageMetadata GetSpecificPackage(IPackageMetadata package);

        /// <summary>
        /// Gets a list of NuGetPackages from this package source.
        /// This allows searching for partial IDs or even the empty string (the default) to list ALL packages.
        /// 
        /// NOTE: See the functions and parameters defined here: https://www.nuget.org/api/v2/$metadata
        /// </summary>
        /// <param name="searchTerm">The search term to use to filter packages. Defaults to the empty string.</param>
        /// <param name="includeAllVersions">True to include older versions that are not the latest version.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="numberToGet">The number of packages to fetch.</param>
        /// <param name="numberToSkip">The number of packages to skip before fetching.</param>
        /// <returns>The list of available packages.</returns>
        IEnumerable<IPackageSearchMetadata> Search(string searchTerm, int skip = 0, int take = 20);

        /// <summary>
        /// Queries the source with the given list of installed packages to get any updates that are available.
        /// </summary>
        /// <param name="installedPackages">The list of currently installed packages.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="includeAllVersions">True to include older versions that are not the latest version.</param>
        /// <param name="targetFrameworks">The specific frameworks to target?</param>
        /// <param name="versionContraints">The version constraints?</param>
        /// <returns>A list of all updates available.</returns>
        IEnumerable<IPackageMetadata> GetUpdates(IEnumerable<IPackageMetadata> installedPackages, bool includePrerelease = false, bool includeAllVersions = false, string targetFrameworks = "", string versionContraints = "");
    }
}