using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NugetForUnity.Models;

namespace NugetForUnity.PackageSource
{
    /// <summary>
    ///     Represents a NuGet package source that can be use to fetch NuGet packages.
    /// </summary>
    public interface INugetPackageSource : IDisposable
    {
        /// <summary>
        ///     Gets or sets the name of the package source.
        /// </summary>
        [NotNull]
        string Name { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether this source is enabled or not.
        /// </summary>
        bool IsEnabled { get; set; }

        /// <summary>
        ///     Gets or sets the password used to access the feed. Null indicates that no password is used.
        /// </summary>
        [CanBeNull]
        string SavedPassword { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether <see cref="SavedPassword" /> is encrypted.
        /// </summary>
        bool SavedPasswordIsEncrypted { get; set; }

        /// <summary>
        ///     Gets or sets the path of the package source.
        /// </summary>
        [NotNull]
        string SavedPath { get; set; }

        /// <summary>
        ///     Gets or sets the explicitly defined protocol version stored inside the 'NuGet.config'.
        /// </summary>
        /// <remarks>Changing this value will only take effect after reloading as it potentially need to change the type of the class.</remarks>
        [CanBeNull]
        string SavedProtocolVersion { get; set; }

        /// <summary>
        ///     Gets or sets the user-name used to access the feed. Null indicates that no authentication is used.
        /// </summary>
        [CanBeNull]
        string UserName { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether this source uses a password for authentication.
        /// </summary>
        bool HasPassword { get; set; }

        /// <summary>
        ///     Download the .nupkg file and store it inside a file at <paramref name="outputFilePath" />.
        /// </summary>
        /// <param name="package">The package to download its .nupkg from.</param>
        /// <param name="outputFilePath">Path where the downloaded file is placed.</param>
        /// <param name="downloadUrlHint">Hint for the url used to download the .nupkg file from.</param>
        void DownloadNupkgToFile([NotNull] INugetPackageIdentifier package, [NotNull] string outputFilePath, [CanBeNull] string downloadUrlHint);

        /// <summary>
        ///     Gets a NugetPackage from the NuGet source that matches (or is in range of) the given <see cref="INugetPackageIdentifier" />.
        /// </summary>
        /// <param name="package">The <see cref="INugetPackageIdentifier" /> containing the ID and Version of the package to get.</param>
        /// <returns>The retrieved package, if there is one.  Null if no matching package was found.</returns>
        [NotNull]
        [ItemNotNull]
        List<INugetPackage> FindPackagesById([NotNull] INugetPackageIdentifier package);

        /// <summary>
        ///     Gets a NugetPackage from the NuGet source that matches (or is in range of) the given <see cref="INugetPackageIdentifier" />.
        /// </summary>
        /// <param name="package">The <see cref="INugetPackageIdentifier" /> containing the ID and Version of the package to get.</param>
        /// <returns>The retrieved package, if there is one.  Null if no matching package was found.</returns>
        [CanBeNull]
        INugetPackage GetSpecificPackage([NotNull] INugetPackageIdentifier package);

        /// <summary>
        ///     Queries the source with the given list of installed packages to get any updates that are available.
        /// </summary>
        /// <param name="packages">The list of packages to use to find updates.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="targetFrameworks">The specific frameworks to target?.</param>
        /// <param name="versionConstraints">The version constraints?.</param>
        /// <returns>A list of all updates available.</returns>
        [NotNull]
        [ItemNotNull]
        List<INugetPackage> GetUpdates(
            [NotNull] [ItemNotNull] IEnumerable<INugetPackage> packages,
            bool includePrerelease = false,
            string targetFrameworks = "",
            string versionConstraints = "");

        /// <summary>
        ///     Gets a list of NugetPackages from this package source.
        ///     This allows searching for partial IDs or even the empty string (the default) to list ALL packages.
        /// </summary>
        /// <param name="searchTerm">The search term to use to filter packages. Defaults to the empty string.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="numberToGet">The number of packages to fetch.</param>
        /// <param name="numberToSkip">The number of packages to skip before fetching.</param>
        /// <param name="cancellationToken">Token that can be used to cancel the asynchronous task.</param>
        /// <returns>The list of available packages.</returns>
        [NotNull]
        [ItemNotNull]
        Task<List<INugetPackage>> SearchAsync(
            string searchTerm = "",
            bool includePrerelease = false,
            int numberToGet = 15,
            int numberToSkip = 0,
            CancellationToken cancellationToken = default);
    }
}
