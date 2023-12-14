#pragma warning disable SA1512,SA1124 // Single-line comments should not be followed by blank line

using System;
using System.IO;
using JetBrains.Annotations;
using NugetForUnity.Configuration;
using UnityEngine;

#region No ReShaper

// ReSharper disable All
// needed because 'JetBrains.Annotations.NotNull' and 'System.Diagnostics.CodeAnalysis.NotNull' collide if this file is compiled with a never version of Unity / C#
using SuppressMessageAttribute = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;

// ReSharper restore All

#endregion

#pragma warning restore SA1512,SA1124 // Single-line comments should not be followed by blank line
namespace NugetForUnity.Models
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
            PackageVersion = new NugetPackageVersion();
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="NugetPackageIdentifier" /> class with the given ID and Version.
        /// </summary>
        /// <param name="id">The ID of the package.</param>
        /// <param name="version">The version number of the package.</param>
        public NugetPackageIdentifier([NotNull] string id, [CanBeNull] string version)
        {
            Id = id;
            PackageVersion = new NugetPackageVersion(version);
        }

        /// <inheritdoc />
        [field: SerializeField]
        public string Id { get; set; }

        /// <inheritdoc />
        [field: SerializeField]
        public NugetPackageVersion PackageVersion { get; set; }

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

                PackageVersion = new NugetPackageVersion(value);
            }
        }

        /// <inheritdoc />
        public string PackageFileName => $"{Id}.{Version}.nupkg";

        /// <inheritdoc />
        public string SpecificationFileName => $"{Id}.nuspec";

        /// <inheritdoc />
        public bool IsPrerelease => PackageVersion.IsPrerelease;

        /// <inheritdoc />
        public bool HasVersionRange => PackageVersion.HasVersionRange;

        /// <summary>
        ///     Checks to see if the first <see cref="NugetPackageIdentifier" /> is less than the second.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is less than the second.</returns>
        public static bool operator <([CanBeNull] NugetPackageIdentifier first, [CanBeNull] NugetPackageIdentifier second)
        {
            if (first is null)
            {
                return true;
            }

            return first.CompareTo(second) < 0;
        }

        /// <summary>
        ///     Checks to see if the first <see cref="NugetPackageIdentifier" /> is greater than the second.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is greater than the second.</returns>
        public static bool operator >([CanBeNull] NugetPackageIdentifier first, [CanBeNull] NugetPackageIdentifier second)
        {
            if (first is null)
            {
                return false;
            }

            return first.CompareTo(second) > 0;
        }

        /// <summary>
        ///     Checks to see if the first <see cref="NugetPackageIdentifier" /> is less than or equal to the second.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is less than or equal to the second.</returns>
        public static bool operator <=([CanBeNull] NugetPackageIdentifier first, [CanBeNull] NugetPackageIdentifier second)
        {
            if (first is null)
            {
                return second is null;
            }

            return first.CompareTo(second) <= 0;
        }

        /// <summary>
        ///     Checks to see if the first <see cref="NugetPackageIdentifier" /> is greater than or equal to the second.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is greater than or equal to the second.</returns>
        public static bool operator >=([CanBeNull] NugetPackageIdentifier first, [CanBeNull] NugetPackageIdentifier second)
        {
            if (first is null)
            {
                return second is null;
            }

            return first.CompareTo(second) >= 0;
        }

        /// <summary>
        ///     Checks to see if the first <see cref="NugetPackageIdentifier" /> is equal to the second.
        ///     They are equal if the Id and the Version match.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is equal to the second.</returns>
        public static bool operator ==([CanBeNull] NugetPackageIdentifier first, [CanBeNull] NugetPackageIdentifier second)
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
        public static bool operator !=([CanBeNull] NugetPackageIdentifier first, [CanBeNull] NugetPackageIdentifier second)
        {
            return !(first == second);
        }

        /// <inheritdoc />
        public string GetPackageInstallPath(string prefix = "")
        {
            return Path.Combine(ConfigurationManager.NugetConfigFile.RepositoryPath, $"{prefix}{Id}.{Version}");
        }

        /// <inheritdoc />
        public int CompareTo([CanBeNull] NugetPackageIdentifier other)
        {
            return CompareTo(other as INugetPackageIdentifier);
        }

        /// <inheritdoc />
        public int CompareTo([CanBeNull] INugetPackageIdentifier other)
        {
            if (other is null)
            {
                return -1;
            }

            var idCompareResult = string.Compare(Id, other.Id, StringComparison.OrdinalIgnoreCase);
            if (idCompareResult != 0)
            {
                return idCompareResult;
            }

            return PackageVersion.CompareTo(other.PackageVersion);
        }

        /// <inheritdoc />
        public bool InRange(INugetPackageIdentifier otherPackage)
        {
            return PackageVersion.InRange(otherPackage.PackageVersion);
        }

        /// <inheritdoc />
        public bool InRange(NugetPackageVersion otherVersion)
        {
            return PackageVersion.InRange(otherVersion);
        }

        /// <summary>
        ///     Checks to see if this <see cref="NugetPackageIdentifier" /> is equal to the given one.
        /// </summary>
        /// <param name="other">The other <see cref="NugetPackageIdentifier" /> to check equality with.</param>
        /// <returns>True if the package identifiers are equal, otherwise false.</returns>
        public bool Equals(NugetPackageIdentifier other)
        {
            return Equals(other as INugetPackageIdentifier);
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

            return !(other is null) && string.Equals(other.Id, Id, StringComparison.OrdinalIgnoreCase) && other.PackageVersion.Equals(PackageVersion);
        }

        /// <summary>
        ///     Determines if a given object is equal to this <see cref="NugetPackageIdentifier" />.
        /// </summary>
        /// <param name="obj">The object to check.</param>
        /// <returns>True if the given object is equal to this <see cref="NugetPackageIdentifier" />, otherwise false.</returns>
        public override bool Equals(object obj)
        {
            return Equals(obj as INugetPackageIdentifier);
        }

        /// <summary>
        ///     Gets the hash-code for this <see cref="NugetPackageIdentifier" />.
        /// </summary>
        /// <returns>The hash-code for this instance.</returns>
        [SuppressMessage(
            "ReSharper",
            "NonReadonlyMemberInGetHashCode",
            Justification = "We only edit the version / id before we use the hash (stroe it in a dictionary).")]
        public override int GetHashCode()
        {
            return GetHashCodeOfId() ^ PackageVersion.GetHashCode();
        }

        /// <summary>
        ///     Returns the string representation of this <see cref="NugetPackageIdentifier" /> in the form "{ID}.{Version}".
        /// </summary>
        /// <returns>A string in the form "{ID}.{Version}".</returns>
        public override string ToString()
        {
            return $"{Id}.{Version}";
        }

        private int GetHashCodeOfId()
        {
#if UNITY_2021_2_OR_NEWER
            return Id.GetHashCode(StringComparison.OrdinalIgnoreCase);
#else
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Id);
#endif
        }
    }
}
