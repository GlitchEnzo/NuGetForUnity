﻿using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NugetForUnity.PackageSource;
using UnityEngine;

namespace NugetForUnity.Models
{
    /// <summary>
    ///     Represents a NuGet package.
    /// </summary>
    public interface INugetPackage : INugetPackageIdentifier
    {
        /// <summary>
        ///     Gets a list of all available versions of the package.
        /// </summary>
        [NotNull]
        List<NugetPackageVersion> Versions { get; }

        /// <summary>
        ///     Gets the authors of the package.
        /// </summary>
        [NotNull]
        List<string> Authors { get; }

        /// <summary>
        ///     Gets the NuGet packages that this NuGet package depends on grouped by target framework.
        /// </summary>
        [NotNull]
        List<NugetFrameworkGroup> Dependencies { get; }

        /// <summary>
        ///     Gets the description of the NuGet package.
        /// </summary>
        [CanBeNull]
        string Description { get; }

        /// <summary>
        ///     Gets the release notes of the NuGet package.
        /// </summary>
        [CanBeNull]
        string ReleaseNotes { get; }

        /// <summary>
        ///     Gets the type of source control software that the package's source code resides in.
        /// </summary>
        RepositoryType RepositoryType { get; }

        /// <summary>
        ///     Gets the URL for the location of the package's source code.
        /// </summary>
        [CanBeNull]
        string RepositoryUrl { get; }

        /// <summary>
        ///     Gets the source control commit the package is from.
        /// </summary>
        [CanBeNull]
        string RepositoryCommit { get; }

        /// <summary>
        ///     Gets the total number of downloads all versions of the package have in total.
        /// </summary>
        long TotalDownloads { get; }

        /// <summary>
        ///     Gets the URL for the location of the license of the NuGet package.
        /// </summary>
        [CanBeNull]
        string LicenseUrl { get; }

        /// <summary>
        ///     Gets the <see cref="INugetPackageSource" /> that contains this package.
        /// </summary>
        [NotNull]
        INugetPackageSource PackageSource { get; }

        /// <summary>
        ///     Gets the URL for the location of the package's source code.
        /// </summary>
        [CanBeNull]
        string ProjectUrl { get; }

        /// <summary>
        ///     Gets the summary of the NuGet package.
        /// </summary>
        [CanBeNull]
        string Summary { get; }

        /// <summary>
        ///     Gets the title (not ID) of the package. This is the "friendly" name that only appears in GUIs and on web-pages.
        /// </summary>
        [CanBeNull]
        string Title { get; }

        /// <summary>
        ///     Gets the icon for the package as a task returning a <see cref="Texture2D" />.
        /// </summary>
        [ItemCanBeNull]
        [CanBeNull]
        Task<Texture2D> IconTask { get; }

        /// <summary>
        ///     Asynchronously gets the NuGet packages that this NuGet package depends on grouped by target framework.
        /// </summary>
        /// <returns>The task that fetches <see cref="Dependencies" />.</returns>
        [NotNull]
        [ItemNotNull]
        Task<List<NugetFrameworkGroup>> GetDependenciesAsync();

        /// <summary>
        ///     Download the .nupkg file and store it inside a file at <paramref name="outputFilePath" />.
        /// </summary>
        /// <param name="outputFilePath">Path where the downloaded file is placed.</param>
        void DownloadNupkgToFile([NotNull] string outputFilePath);
    }
}
