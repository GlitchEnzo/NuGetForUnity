using System.Collections.Generic;

namespace NuGet.Editor.Models
{
    public interface INugetPackageSource
    {
        /// <summary>
        /// Gets or sets the name of the package source.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Gets or sets the path of the package source.
        /// </summary>
        string SavedPath { get; set; }

        /// <summary>
        /// Gets path, with the values of environment variables expanded.
        /// </summary>
        string ExpandedPath { get; }

        string UserName { get; set; }

        /// <summary>
        /// Gets or sets the password used to access the feed. Null indicates that no password is used.
        /// </summary>
        string SavedPassword { get; set; }

        /// <summary>
        /// Gets password, with the values of environment variables expanded.
        /// </summary>
        string ExpandedPassword { get; }

        bool HasPassword { get; set; }

        /// <summary>
        /// Gets or sets a value indicated whether the path is a local path or a remote path.
        /// </summary>
        bool IsLocalPath { get; }

        /// <summary>
        /// Gets or sets a value indicated whether this source is enabled or not.
        /// </summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// Gets a NugetPackage from the NuGet server that matches (or is in range of) the <see cref="NugetPackageIdentifier"/> given.
        /// </summary>
        /// <param name="package">The <see cref="NugetPackageIdentifier"/> containing the ID and Version of the package to get.</param>
        /// <returns>The retrieved package, if there is one.  Null if no matching package was found.</returns>
        IEnumerable<NugetPackage> FindPackagesById(NugetPackageIdentifier package);

        /// <summary>
        /// Gets a NugetPackage from the NuGet server that matches (or is in range of) the <see cref="NugetPackageIdentifier"/> given.
        /// </summary>
        /// <param name="package">The <see cref="NugetPackageIdentifier"/> containing the ID and Version of the package to get.</param>
        /// <returns>The retrieved package, if there is one.  Null if no matching package was found.</returns>
        NugetPackage GetSpecificPackage(NugetPackageIdentifier package);

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
        IEnumerable<NugetPackage> Search(string searchTerm = "", bool includeAllVersions = false, bool includePrerelease = false, int numberToGet = 15, int numberToSkip = 0);

        /// <summary>
        /// Queries the source with the given list of installed packages to get any updates that are available.
        /// </summary>
        /// <param name="installedPackages">The list of currently installed packages.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="includeAllVersions">True to include older versions that are not the latest version.</param>
        /// <param name="targetFrameworks">The specific frameworks to target?</param>
        /// <param name="versionContraints">The version constraints?</param>
        /// <returns>A list of all updates available.</returns>
        IEnumerable<NugetPackage> GetUpdates(IEnumerable<NugetPackage> installedPackages, bool includePrerelease = false, bool includeAllVersions = false, string targetFrameworks = "", string versionContraints = "");
    }
}