﻿using System;

namespace NugetForUnity
{
    /// <summary>
    ///     Interface for a versioned NuGet package.
    /// </summary>
    public interface INuGetPackageIdentifier : IEquatable<INuGetPackageIdentifier>, IComparable<INuGetPackageIdentifier>
    {
        /// <summary>
        ///     Gets or sets a value indicating whether this package was installed manually or just as a dependency.
        /// </summary>
        bool IsManuallyInstalled { get; set; }

        /// <summary>
        ///     Gets the ID of the NuGet package.
        /// </summary>
        string Id { get; }

        /// <summary>
        ///     Gets the normalized version number of the NuGet package.
        /// </summary>
        string Version { get; }

        /// <summary>
        ///     Gets a value indicating whether the version number specified is a range of values.
        /// </summary>
        bool HasVersionRange { get; }

        /// <summary>
        ///     Gets a value indicating whether this is a prerelease package or an official release package.
        /// </summary>
        bool IsPrerelease { get; }

        /// <summary>
        ///     Gets the typed version number of the NuGet package.
        /// </summary>
        NuGetPackageVersion PackageVersion { get; }

        /// <summary>
        ///     Gets the name of the '.nupkg' file that contains the whole package content as a ZIP.
        /// </summary>
        string PackageFileName { get; }

        /// <summary>
        ///     Gets the name of the '.nuspec' file that contains metadata of this NuGet package's.
        /// </summary>
        string SpecificationFileName { get; }

        /// <summary>
        ///     Determines if the given <see cref="INuGetPackageIdentifier" />'s version <paramref name="otherPackage" /> is in the version range of this
        ///     <see cref="INuGetPackageIdentifier" />.
        ///     See here: https://docs.nuget.org/ndocs/create-packages/dependency-versions.
        /// </summary>
        /// <param name="otherPackage">The <see cref="INuGetPackageIdentifier" /> whose version to check if is in the range.</param>
        /// <returns>True if the given version is in the range, otherwise false.</returns>
        bool InRange(INuGetPackageIdentifier otherPackage);

        /// <summary>
        ///     Determines if the given <paramref name="otherVersion" /> is in the version range of this <see cref="INuGetPackageIdentifier" />.
        ///     See here: https://docs.nuget.org/ndocs/create-packages/dependency-versions.
        /// </summary>
        /// <param name="otherVersion">The <see cref="NuGetPackageVersion" /> to check if is in the range.</param>
        /// <returns>True if the given version is in the range, otherwise false.</returns>
        bool InRange(NuGetPackageVersion otherVersion);
    }
}
