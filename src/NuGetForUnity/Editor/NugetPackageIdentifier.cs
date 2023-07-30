using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace NugetForUnity
{
    /// <summary>
    ///     Represents an identifier for a NuGet package. It contains only an ID and a Version number.
    /// </summary>
    [Serializable]
    public class NugetPackageIdentifier : INuGetPackageIdentifier, IEquatable<NugetPackageIdentifier>, IComparable<NugetPackageIdentifier>
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="NugetPackageIdentifier" /> class with empty ID and Version.
        /// </summary>
        public NugetPackageIdentifier()
        {
            Id = string.Empty;
            PackageVersion = new NuGetPackageVersion();
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="NugetPackageIdentifier" /> class with the given ID and Version.
        /// </summary>
        /// <param name="id">The ID of the package.</param>
        /// <param name="version">The version number of the package.</param>
        public NugetPackageIdentifier(string id, string version)
        {
            Id = id;
            PackageVersion = new NuGetPackageVersion(version);
        }

        /// <inheritdoc />
        public int CompareTo(NugetPackageIdentifier other)
        {
            return CompareTo((INuGetPackageIdentifier)other);
        }

        /// <summary>
        ///     Checks to see if this <see cref="NugetPackageIdentifier" /> is equal to the given one.
        /// </summary>
        /// <param name="other">The other <see cref="NugetPackageIdentifier" /> to check equality with.</param>
        /// <returns>True if the package identifiers are equal, otherwise false.</returns>
        public bool Equals(NugetPackageIdentifier other)
        {
            return Equals((INuGetPackageIdentifier)other);
        }

        /// <summary>
        ///     Gets or sets whether this package was installed manually or just as a dependency.
        /// </summary>
        [field: SerializeField]
        public bool IsManuallyInstalled { get; set; }

        /// <inheritdoc />
        [field: SerializeField]
        public string Id { get; set; }

        /// <summary>
        ///     Gets or sets the version number of the NuGet package.
        ///     This is the normalized version number without build-metadata e.g. <b>1.0.0+b3a8</b> is normalized to <b>1.0.0</b>.
        /// </summary>
        public string Version
        {
            get => PackageVersion.NormalizedVersion;

            set
            {
                if (PackageVersion.FullVersion == value)
                {
                    return;
                }

                PackageVersion = new NuGetPackageVersion(value);
            }
        }

        /// <inheritdoc />
        public string PackageFileName => $"{Id}.{Version}.nupkg";

        /// <inheritdoc />
        public string SpecificationFileName => $"{Id}.{Version}.nuspec";

        /// <inheritdoc />
        public int CompareTo(INuGetPackageIdentifier other)
        {
            var idCompareResult = string.Compare(Id, other.Id, StringComparison.OrdinalIgnoreCase);
            if (idCompareResult != 0)
            {
                return idCompareResult;
            }

            return PackageVersion.CompareTo(other.PackageVersion);
        }

        /// <inheritdoc />
        public bool Equals(INuGetPackageIdentifier other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return !(other is null) && other.Id.Equals(Id, StringComparison.OrdinalIgnoreCase) && other.PackageVersion.Equals(PackageVersion);
        }

        /// <inheritdoc />
        [field: SerializeField]
        public NuGetPackageVersion PackageVersion { get; internal set; }

        /// <inheritdoc />
        public bool IsPrerelease => PackageVersion.IsPrerelease;

        /// <inheritdoc />
        public bool HasVersionRange => PackageVersion.HasVersionRange;

        /// <inheritdoc />
        public bool InRange(INuGetPackageIdentifier otherPackage)
        {
            return PackageVersion.InRange(otherPackage.PackageVersion);
        }

        /// <inheritdoc />
        public bool InRange(NuGetPackageVersion otherVersion)
        {
            return PackageVersion.InRange(otherVersion);
        }

        /// <summary>
        ///     Full filename, including specified path, of this NuGet package's file.
        /// </summary>
        /// <remarks>
        ///     Do not use this method when attempting to find a package file in a local repository; use
        ///     <see
        ///         cref="GetLocalPackageFilePath" />
        ///     instead. The existence of the file is not verified.
        /// </remarks>
        /// <param name="baseDirectoryPath">Path in which the package file will be found.</param>
        /// <returns>Base package filename prefixed by the indicated path.</returns>
        public string GetPackageFilePath(string baseDirectoryPath)
        {
            return Path.Combine(baseDirectoryPath, PackageFileName);
        }

        /// <summary>
        ///     Full filename, including full path, of this NuGet package's file in a local NuGet repository.
        /// </summary>
        /// <remarks>
        ///     Use this method when attempting to find a package file in a local repository. Do not use
        ///     <see cref="GetPackageFilePath(string)" /> for this purpose. The existence of the file is verified.
        /// </remarks>
        /// <param name="baseDirectoryPath">Path to the local repository's root directory.</param>
        /// <returns>The full path to the file, if it exists in the repository, or <c>null</c> otherwise.</returns>
        public string GetLocalPackageFilePath(string baseDirectoryPath)
        {
            // Find this package's file in the repository.
            var files = Directory.GetFiles(baseDirectoryPath, PackageFileName, SearchOption.AllDirectories);

            // If we found any, return the first found; otherwise return null.
            return files.FirstOrDefault();
        }

        /// <summary>
        ///     Checks to see if the first <see cref="NugetPackageIdentifier" /> is less than the second.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is less than the second.</returns>
        public static bool operator <(NugetPackageIdentifier first, NugetPackageIdentifier second)
        {
            return first.CompareTo(second) < 0;
        }

        /// <summary>
        ///     Checks to see if the first <see cref="NugetPackageIdentifier" /> is greater than the second.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is greater than the second.</returns>
        public static bool operator >(NugetPackageIdentifier first, NugetPackageIdentifier second)
        {
            return first.CompareTo(second) > 0;
        }

        /// <summary>
        ///     Checks to see if the first <see cref="NugetPackageIdentifier" /> is less than or equal to the second.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is less than or equal to the second.</returns>
        public static bool operator <=(NugetPackageIdentifier first, NugetPackageIdentifier second)
        {
            return first.CompareTo(second) <= 0;
        }

        /// <summary>
        ///     Checks to see if the first <see cref="NugetPackageIdentifier" /> is greater than or equal to the second.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is greater than or equal to the second.</returns>
        public static bool operator >=(NugetPackageIdentifier first, NugetPackageIdentifier second)
        {
            return first.CompareTo(second) >= 0;
        }

        /// <summary>
        ///     Checks to see if the first <see cref="NugetPackageIdentifier" /> is equal to the second.
        ///     They are equal if the Id and the Version match.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is equal to the second.</returns>
        public static bool operator ==(NugetPackageIdentifier first, NugetPackageIdentifier second)
        {
            if (ReferenceEquals(first, second))
            {
                return true;
            }

            if (first is null)
            {
                return false;
            }

            return first.Equals(second);
        }

        /// <summary>
        ///     Checks to see if the first <see cref="NugetPackageIdentifier" /> is not equal to the second.
        ///     They are not equal if the Id or the Version differ.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is not equal to the second.</returns>
        public static bool operator !=(NugetPackageIdentifier first, NugetPackageIdentifier second)
        {
            return !(first == second);
        }

        /// <summary>
        ///     Determines if a given object is equal to this <see cref="NugetPackageIdentifier" />.
        /// </summary>
        /// <param name="obj">The object to check.</param>
        /// <returns>True if the given object is equal to this <see cref="NugetPackageIdentifier" />, otherwise false.</returns>
        public override bool Equals(object obj)
        {
            return Equals(obj as NugetPackageIdentifier);
        }

        /// <summary>
        ///     Gets the hash-code for this <see cref="NugetPackageIdentifier" />.
        /// </summary>
        /// <returns>The hash-code for this instance.</returns>
        public override int GetHashCode()
        {
            return Id.GetHashCode() ^ PackageVersion.GetHashCode();
        }

        /// <summary>
        ///     Returns the string representation of this <see cref="NugetPackageIdentifier" /> in the form "{ID}.{Version}".
        /// </summary>
        /// <returns>A string in the form "{ID}.{Version}".</returns>
        public override string ToString()
        {
            return $"{Id}.{Version}";
        }

        /// <summary>
        ///     Compares the given version string with the version range of this <see cref="NugetPackageIdentifier" />.
        ///     See here: https://docs.nuget.org/ndocs/create-packages/dependency-versions.
        /// </summary>
        /// <param name="other">The package of witch the version to check if its version is grater or less then the <see cref="PackageVersion" />.</param>
        /// <returns>
        ///     -1 if the version of the other package is less than the version of this package.
        ///     0 if the version of the other package equals the version of this package.
        ///     +1 if the version of the other package is greater than the version of this package.
        /// </returns>
        public int CompareVersion(INuGetPackageIdentifier other)
        {
            return PackageVersion.CompareTo(other.PackageVersion);
        }
    }
}
