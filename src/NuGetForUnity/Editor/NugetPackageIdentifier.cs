using System;
using UnityEngine;

namespace NugetForUnity
{
    /// <summary>
    ///     Represents an identifier for a NuGet package. It contains only an ID and a Version number.
    /// </summary>
    [Serializable]
    public class NugetPackageIdentifier : INugetPackageIdentifier, IEquatable<NugetPackageIdentifier>, IComparable<NugetPackageIdentifier>
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
            return CompareTo((INugetPackageIdentifier)other);
        }

        /// <summary>
        ///     Checks to see if this <see cref="NugetPackageIdentifier" /> is equal to the given one.
        /// </summary>
        /// <param name="other">The other <see cref="NugetPackageIdentifier" /> to check equality with.</param>
        /// <returns>True if the package identifiers are equal, otherwise false.</returns>
        public bool Equals(NugetPackageIdentifier other)
        {
            return Equals((INugetPackageIdentifier)other);
        }

        /// <inheritdoc />
        [field: SerializeField]
        public string Id { get; set; }

        /// <inheritdoc />
        [field: SerializeField]
        public NuGetPackageVersion PackageVersion { get; internal set; }

        /// <inheritdoc />
        [field: SerializeField]
        public bool IsManuallyInstalled { get; set; }

        /// <inheritdoc />
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
        public bool IsPrerelease => PackageVersion.IsPrerelease;

        /// <inheritdoc />
        public bool HasVersionRange => PackageVersion.HasVersionRange;

        /// <inheritdoc />
        public int CompareTo(INugetPackageIdentifier other)
        {
            var idCompareResult = string.Compare(Id, other.Id, StringComparison.OrdinalIgnoreCase);
            if (idCompareResult != 0)
            {
                return idCompareResult;
            }

            return PackageVersion.CompareTo(other.PackageVersion);
        }

        /// <summary>
        ///     Checks to see if this <see cref="INugetPackageIdentifier" /> is equal to the given one.
        /// </summary>
        /// <param name="other">The other <see cref="INugetPackageIdentifier" /> to check equality with.</param>
        /// <returns>True if the package identifiers are equal, otherwise false.</returns>
        public bool Equals(INugetPackageIdentifier other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return !(other is null) && other.Id.Equals(Id, StringComparison.OrdinalIgnoreCase) && other.PackageVersion.Equals(PackageVersion);
        }

        /// <inheritdoc />
        public bool InRange(INugetPackageIdentifier otherPackage)
        {
            return PackageVersion.InRange(otherPackage.PackageVersion);
        }

        /// <inheritdoc />
        public bool InRange(NuGetPackageVersion otherVersion)
        {
            return PackageVersion.InRange(otherVersion);
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
        public int CompareVersion(INugetPackageIdentifier other)
        {
            return PackageVersion.CompareTo(other.PackageVersion);
        }
    }
}
