#pragma warning disable SA1512,SA1124 // Single-line comments should not be followed by blank line

using System;
using System.Globalization;
using System.Text;
using JetBrains.Annotations;
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
    ///     Represents a NuGet package version number.
    /// </summary>
    [Serializable]
    public sealed class NugetPackageVersion : IEquatable<NugetPackageVersion>, IComparable<NugetPackageVersion>, ISerializationCallbackReceiver
    {
        [NonSerialized]
        private SemVer2Version? maximumSemVer2Version;

        [NonSerialized]
        private SemVer2Version? minimumSemVer2Version;

        [NonSerialized]
        private SemVer2Version semVer2Version;

        /// <summary>
        ///     Initializes a new instance of the <see cref="NugetPackageVersion" /> class.
        /// </summary>
        public NugetPackageVersion()
        {
            SetFromString(null);
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="NugetPackageVersion" /> class with the given Version string.
        /// </summary>
        /// <param name="version">The version number as string.</param>
        public NugetPackageVersion([CanBeNull] string version)
        {
            SetFromString(version);
        }

        /// <summary>
        ///     Gets the normalized version number of the NuGet package.
        ///     This is the normalized version number without build-metadata e.g. <b>1.0.0+b3a8</b> is normalized to <b>1.0.0</b>.
        /// </summary>
        [NotNull]
        public string NormalizedVersion { get; private set; } = string.Empty;

        /// <summary>
        ///     Gets the full version number of the NuGet package.
        ///     This contains the build-metadata e.g. <b>1.0.0+b3a8</b>.
        /// </summary>
        [field: SerializeField]
        [NotNull]
        public string FullVersion { get; private set; } = string.Empty;

        /// <summary>
        ///     Gets a value indicating whether the minimum version number (only valid when HasVersionRange is true) is inclusive (true) or exclusive (false).
        /// </summary>
        public bool IsMinInclusive { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the maximum version number (only valid when HasVersionRange is true) is inclusive (true) or exclusive (false).
        /// </summary>
        public bool IsMaxInclusive { get; private set; }

        /// <summary>
        ///     Gets the minimum version number of the NuGet package. Only valid when HasVersionRange is true.
        /// </summary>
        [CanBeNull]
        public string MinimumVersion { get; private set; }

        /// <summary>
        ///     Gets the maximum version number of the NuGet package. Only valid when HasVersionRange is true.
        /// </summary>
        [CanBeNull]
        public string MaximumVersion => maximumSemVer2Version?.ToString();

        /// <summary>
        ///     Gets a value indicating whether this is a pre-release package or an official release package.
        /// </summary>
        public bool IsPrerelease { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the version number specified is a range of values.
        /// </summary>
        public bool HasVersionRange { get; private set; }

        /// <summary>
        ///     Checks to see if the first <see cref="NugetPackageVersion" /> is less than the second.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is less than the second.</returns>
        public static bool operator <([CanBeNull] NugetPackageVersion first, [CanBeNull] NugetPackageVersion second)
        {
            if (first is null)
            {
                return true;
            }

            return first.CompareTo(second) < 0;
        }

        /// <summary>
        ///     Checks to see if the first <see cref="NugetPackageVersion" /> is greater than the second.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is greater than the second.</returns>
        public static bool operator >([CanBeNull] NugetPackageVersion first, [CanBeNull] NugetPackageVersion second)
        {
            if (first is null)
            {
                return false;
            }

            return first.CompareTo(second) > 0;
        }

        /// <summary>
        ///     Checks to see if the first <see cref="NugetPackageVersion" /> is less than or equal to the second.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is less than or equal to the second.</returns>
        public static bool operator <=([CanBeNull] NugetPackageVersion first, [CanBeNull] NugetPackageVersion second)
        {
            if (first is null)
            {
                return second is null;
            }

            return first.CompareTo(second) <= 0;
        }

        /// <summary>
        ///     Checks to see if the first <see cref="NugetPackageVersion" /> is greater than or equal to the second.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is greater than or equal to the second.</returns>
        public static bool operator >=([CanBeNull] NugetPackageVersion first, [CanBeNull] NugetPackageVersion second)
        {
            if (first is null)
            {
                return second is null;
            }

            return first.CompareTo(second) >= 0;
        }

        /// <summary>
        ///     Checks to see if the first <see cref="NugetPackageVersion" /> is equal to the second.
        ///     They are equal if the Id and the Version match.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is equal to the second.</returns>
        public static bool operator ==([CanBeNull] NugetPackageVersion first, [CanBeNull] NugetPackageVersion second)
        {
            if (first is null)
            {
                return second is null;
            }

            return first.Equals(second);
        }

        /// <summary>
        ///     Checks to see if the first <see cref="NugetPackageVersion" /> is not equal to the second.
        ///     They are not equal if the Id or the Version differ.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is not equal to the second.</returns>
        public static bool operator !=(NugetPackageVersion first, NugetPackageVersion second)
        {
            return !(first == second);
        }

        /// <summary>
        ///     Compares this version with the string representation of <paramref name="other" />.
        /// </summary>
        /// <param name="other">The other version number to check if it is grater or equal to this version.</param>
        /// <returns>
        ///     -1 if other is less than this.
        ///     0 if other is equal to this.
        ///     +1 if other is greater than this.
        /// </returns>
        public int CompareTo([CanBeNull] NugetPackageVersion other)
        {
            if (other is null)
            {
                return -1;
            }

            if (HasVersionRange || other.HasVersionRange)
            {
                return string.Compare(NormalizedVersion, other.NormalizedVersion, StringComparison.OrdinalIgnoreCase);
            }

            return semVer2Version.Compare(other.semVer2Version);
        }

        /// <inheritdoc />
        public void OnBeforeSerialize()
        {
            // nothing to do
        }

        /// <inheritdoc />
        public void OnAfterDeserialize()
        {
            SetFromString(FullVersion);
        }

        /// <summary>
        ///     Determines if the given version is in the version range of this <see cref="NugetPackageVersion" />.
        ///     See here: https://docs.nuget.org/ndocs/create-packages/dependency-versions.
        /// </summary>
        /// <param name="other">The <see cref="NugetPackageVersion" /> to check if is in the range.</param>
        /// <returns>True if the given version is in the range, otherwise false.</returns>
        public bool InRange([NotNull] NugetPackageVersion other)
        {
            if (string.IsNullOrEmpty(FullVersion))
            {
                // if this has no version specified, it matches everything
                return true;
            }

            if (other.HasVersionRange)
            {
                if (!HasVersionRange)
                {
                    // A 'single' version can't be in range of a version range.
                    return false;
                }

                // We have two version ranges. We check if they intersect.
                return (other.minimumSemVer2Version.HasValue &&
                        CompareVersion(other.minimumSemVer2Version.Value) == 0 &&
                        (other.IsMinInclusive || !other.minimumSemVer2Version.Equals(maximumSemVer2Version))) ||
                       (other.maximumSemVer2Version.HasValue &&
                        CompareVersion(other.maximumSemVer2Version.Value) == 0 &&
                        (other.IsMaxInclusive || !other.maximumSemVer2Version.Equals(minimumSemVer2Version)));
            }

            if (!HasVersionRange)
            {
                // if it has no version range specified (e.g. only a single version number)
                // The NuGet's specs state that it is the minimum version number, inclusive
                return semVer2Version.Compare(other.semVer2Version) <= 0;
            }

            // version comparison with respect to the version range
            return CompareVersion(other.semVer2Version) == 0;
        }

        /// <summary>
        ///     Determines if a given object is equal to this <see cref="NugetPackageVersion" />.
        /// </summary>
        /// <param name="obj">The object to check.</param>
        /// <returns>True if the given object is equal to this <see cref="NugetPackageVersion" />, otherwise false.</returns>
        public override bool Equals(object obj)
        {
            return Equals(obj as NugetPackageVersion);
        }

        /// <summary>
        ///     Checks to see if this <see cref="NugetPackageVersion" /> is equal to the given one.
        /// </summary>
        /// <param name="other">The other <see cref="NugetPackageVersion" /> to check equality with.</param>
        /// <returns>True if the package versions are equal, otherwise false.</returns>
        public bool Equals(NugetPackageVersion other)
        {
            if (other is null)
            {
                return false;
            }

            return string.Equals(NormalizedVersion, other.NormalizedVersion, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        ///     Gets the hash-code for this <see cref="NugetPackageVersion" />.
        /// </summary>
        /// <returns>The hash-code for this instance.</returns>
        [SuppressMessage(
            "ReSharper",
            "NonReadonlyMemberInGetHashCode",
            Justification = "We only edit the version before we use the hash (stroe it in a dictionary).")]
        public override int GetHashCode()
        {
#if UNITY_2021_2_OR_NEWER
            return NormalizedVersion.GetHashCode(StringComparison.OrdinalIgnoreCase);
#else
            return StringComparer.OrdinalIgnoreCase.GetHashCode(NormalizedVersion);
#endif
        }

        /// <summary>
        ///     Returns the string representation of this <see cref="NugetPackageVersion" />.
        /// </summary>
        /// <returns>The version string.</returns>
        public override string ToString()
        {
            return FullVersion;
        }

        /// <summary>
        ///     Compares the given version string with the version range of this <see cref="NugetPackageVersion" />.
        ///     See here: https://docs.nuget.org/ndocs/create-packages/dependency-versions.
        /// </summary>
        /// <param name="otherSemVer2">The version to check if is in the range.</param>
        /// <returns>
        ///     -1 if other is less than the version range. 0 if other is inside the version range. +1 if other is greater than the
        ///     version range.
        /// </returns>
        private int CompareVersion(in SemVer2Version otherSemVer2)
        {
            if (!HasVersionRange)
            {
                return semVer2Version.Compare(otherSemVer2);
            }

            var compareMinimum = 0;
            if (minimumSemVer2Version.HasValue)
            {
                compareMinimum = minimumSemVer2Version.Value.Compare(otherSemVer2);

                // -1 = Min < other <-- Inclusive & Exclusive
                //  0 = Min = other <-- Inclusive Only
                // +1 = Min > other <-- OUT OF RANGE
                if (IsMinInclusive)
                {
                    if (compareMinimum > 0)
                    {
                        return -1;
                    }
                }
                else
                {
                    if (compareMinimum >= 0)
                    {
                        return -1;
                    }
                }
            }

            if (maximumSemVer2Version.HasValue)
            {
                var compare = maximumSemVer2Version.Value.Compare(otherSemVer2);

                // -1 = Max < other <-- OUT OF RANGE
                //  0 = Max = other <-- Inclusive Only
                // +1 = Max > other <-- Inclusive & Exclusive
                if (IsMaxInclusive)
                {
                    if (compare < 0)
                    {
                        return 1;
                    }
                }
                else
                {
                    if (compare <= 0)
                    {
                        return 1;
                    }
                }
            }
            else
            {
                if (IsMaxInclusive)
                {
                    // if there is no MaxVersion specified, but the Max is Inclusive, then it is an EXACT version match with the stored MINIMUM
                    return compareMinimum;
                }
            }

            return 0;
        }

        private void SetFromString([CanBeNull] string version)
        {
            minimumSemVer2Version = null;
            maximumSemVer2Version = null;
            if (string.IsNullOrWhiteSpace(version))
            {
                NormalizedVersion = string.Empty;
                FullVersion = string.Empty;
                semVer2Version = new SemVer2Version(false);
                return;
            }

            version = version.Trim();
            FullVersion = version;
            IsMinInclusive = version.StartsWith("[", StringComparison.Ordinal);
            HasVersionRange = IsMinInclusive || version.StartsWith("(", StringComparison.Ordinal);
            if (HasVersionRange)
            {
                semVer2Version = new SemVer2Version(false);
                NormalizedVersion = version;
                IsPrerelease = version.IndexOf('-') >= 0;
                IsMaxInclusive = version.EndsWith("]", StringComparison.Ordinal);

                // if there is no MaxVersion specified, but the Max is Inclusive, then it is an EXACT version match with the stored MINIMUM
                var minMax = version.TrimStart('[', '(').TrimEnd(']', ')').Split(',');
                var minimumVersion = minMax[0].Trim();
                minimumSemVer2Version = string.IsNullOrEmpty(minimumVersion) ? (SemVer2Version?)null : new SemVer2Version(minimumVersion);
                var maximumVersion = minMax.Length == 2 ? minMax[1].Trim() : null;
                maximumSemVer2Version = string.IsNullOrEmpty(maximumVersion) ? (SemVer2Version?)null : new SemVer2Version(maximumVersion);
            }
            else
            {
                semVer2Version = new SemVer2Version(version);
                NormalizedVersion = semVer2Version.ToString(); // normalize the version string
                IsPrerelease = semVer2Version.PreRelease != null;
                IsMaxInclusive = false;
            }
        }

        /// <summary>
        ///     SemVer2 <see href="https://semver.org/" />.
        ///     Complying with NuGet comparison rules <see href="https://learn.microsoft.com/en-us/nuget/concepts/package-versioning" />.
        /// </summary>
        /// Ignore spelling: SemVer, Sem, Ver
        private readonly struct SemVer2Version
        {
            [CanBeNull]
            private readonly string buildMetadata;

            private readonly int major;

            private readonly int minor;

            private readonly int patch;

            [CanBeNull]
            private readonly string[] preReleaseLabels;

            private readonly int revision;

            /// <summary>
            ///     Initializes a new instance of the <see cref="SemVer2Version" /> struct.
            /// </summary>
            /// <param name="dummy">Dummy to allow calling this constructor.</param>
            public SemVer2Version(bool dummy)
            {
                _ = dummy;
                buildMetadata = null;
                PreRelease = null;
                major = -1;
                minor = -1;
                patch = -1;
                revision = -1;
                preReleaseLabels = null;
            }

            /// <summary>
            ///     Initializes a new instance of the <see cref="SemVer2Version" /> struct.
            /// </summary>
            /// <param name="version">The version number as string.</param>
            public SemVer2Version([CanBeNull] string version)
            {
                if (!string.IsNullOrWhiteSpace(version))
                {
                    try
                    {
                        buildMetadata = null;
                        var buildMetadataStartIndex = version.IndexOf('+');
                        if (buildMetadataStartIndex > 0)
                        {
                            buildMetadata = version.Substring(buildMetadataStartIndex + 1);
                            version = version.Substring(0, buildMetadataStartIndex);
                        }

                        PreRelease = null;
                        preReleaseLabels = null;
                        var preReleaseStartIndex = version.IndexOf('-');
                        if (preReleaseStartIndex > 0)
                        {
                            PreRelease = version.Substring(preReleaseStartIndex + 1);
                            preReleaseLabels = PreRelease.Split('.');

                            version = version.Substring(0, preReleaseStartIndex);
                        }

                        var split = version.Split('.');
                        major = int.Parse(split[0], CultureInfo.InvariantCulture);
                        minor = 0;
                        if (split.Length >= 2)
                        {
                            minor = int.Parse(split[1], CultureInfo.InvariantCulture);
                        }

                        patch = 0;
                        if (split.Length >= 3)
                        {
                            patch = int.Parse(split[2], CultureInfo.InvariantCulture);
                        }

                        revision = 0;
                        if (split.Length >= 4)
                        {
                            revision = int.Parse(split[3], CultureInfo.InvariantCulture);
                        }

                        return;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogErrorFormat("Invalid version number: '{0}'\n{1}", version, ex);
                    }
                }

                buildMetadata = null;
                PreRelease = null;
                preReleaseLabels = null;
                major = -1;
                minor = -1;
                patch = -1;
                revision = -1;
            }

            [CanBeNull]
            public string PreRelease { get; }

            /// <summary>
            ///     Compares two version numbers in the form "1.2". Also supports an optional 3rd and 4th number as well as a prerelease tag, such as
            ///     "1.3.0.1-alpha2".
            ///     Returns:
            ///     -1 if this is less than other
            ///     0 if this is equal to other
            ///     +1 if this is greater than other.
            /// </summary>
            /// <param name="other">The second version number to compare.</param>
            /// <returns>-1 if this is less than other. 0 if this is equal to other. +1 if this is greater than other.</returns>
            public int Compare(in SemVer2Version other)
            {
                try
                {
                    var majorComparison = major.CompareTo(other.major);
                    if (majorComparison == 0)
                    {
                        // if major versions are equal, compare minor versions
                        var minorComparison = minor.CompareTo(other.minor);
                        if (minorComparison == 0)
                        {
                            var patchNumber = patch;
                            var otherPatch = other.patch;
                            var patchComparison = patchNumber.CompareTo(otherPatch);
                            if (patchComparison == 0)
                            {
                                // if patch versions are equal, compare revision of the versions
                                var revisionNumber = revision;
                                var otherRevision = other.revision;
                                var revisionComparison = revisionNumber.CompareTo(otherRevision);
                                if (revisionComparison == 0)
                                {
                                    // if the build versions are equal, just return the prerelease version comparison
                                    if (preReleaseLabels == null && other.preReleaseLabels == null)
                                    {
                                        // no pre-release and the rest is equal
                                        return 0;
                                    }

                                    if (preReleaseLabels != null && other.preReleaseLabels == null)
                                    {
                                        // pre-release versions are always after release versions
                                        return -1;
                                    }

                                    if (preReleaseLabels == null && other.preReleaseLabels != null)
                                    {
                                        return 1;
                                    }

                                    var prereleaseComparison = ComparePreReleaseLabels(preReleaseLabels, other.preReleaseLabels);
                                    return prereleaseComparison;
                                }

                                // the build versions are different, so use them
                                return revisionComparison;
                            }

                            // the patch versions are different, so use them
                            return patchComparison;
                        }

                        // the minor versions are different, so use them
                        return minorComparison;
                    }

                    // the major versions are different, so use them
                    return majorComparison;
                }
                catch (Exception exception)
                {
                    Debug.LogErrorFormat("Error: {0} while comparing '{1}' with '{2}'.", exception, this, other);
                    return -1;
                }
            }

            /// <inheritdoc />
            public override string ToString()
            {
                return ToString(false);
            }

            /// <summary>
            ///     Returns the version number as a string in the form <see cref="major" />.<see cref="minor" />.<see cref="patch" />-<see cref="PreRelease" />+
            ///     <see cref="buildMetadata" />.
            ///     The <see cref="buildMetadata" /> can be removed.
            /// </summary>
            /// <param name="withBuildMetadata">If <c>true</c> the <see cref="buildMetadata" /> is included.</param>
            /// <returns>The formatted string.</returns>
            [NotNull]
            public string ToString(bool withBuildMetadata)
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.Append(major);
                stringBuilder.Append('.');
                stringBuilder.Append(minor);
                stringBuilder.Append('.');
                stringBuilder.Append(patch);

                if (revision != 0)
                {
                    stringBuilder.Append('.');
                    stringBuilder.Append(revision);
                }

                if (!string.IsNullOrEmpty(PreRelease))
                {
                    stringBuilder.Append('-');
                    stringBuilder.Append(PreRelease);
                }

                if (withBuildMetadata && !string.IsNullOrEmpty(buildMetadata))
                {
                    stringBuilder.Append('+');
                    stringBuilder.Append(buildMetadata);
                }

                return stringBuilder.ToString();
            }

            /// <summary>
            ///     Compares sets of Pre-Release labels (<see cref="PreRelease" /> splitted by '.').
            /// </summary>
            private static int ComparePreReleaseLabels([NotNull] string[] releaseLabels1, [NotNull] string[] releaseLabels2)
            {
                var result = 0;

                var count = Math.Max(releaseLabels1.Length, releaseLabels2.Length);

                for (var i = 0; i < count; i++)
                {
                    var hasLabel1 = i < releaseLabels1.Length;
                    var hasLabel2 = i < releaseLabels2.Length;

                    if (!hasLabel1 && hasLabel2)
                    {
                        return -1;
                    }

                    if (hasLabel1 && !hasLabel2)
                    {
                        return 1;
                    }

                    // compare the labels
                    result = ComparePreReleaseLabel(releaseLabels1[i], releaseLabels2[i]);

                    if (result != 0)
                    {
                        return result;
                    }
                }

                return result;
            }

            /// <summary>
            ///     Pre-Release labels are compared as numbers if they are numeric, otherwise they will be compared as strings (case insensitive).
            /// </summary>
            private static int ComparePreReleaseLabel(string releaseLabel1, string releaseLabel2)
            {
                var label1IsNumeric = int.TryParse(releaseLabel1, out var releaseLabel1Number);
                var label2IsNumeric = int.TryParse(releaseLabel2, out var releaseLabel2Number);

                if (label1IsNumeric && label2IsNumeric)
                {
                    // if both are numeric compare them as numbers
                    return releaseLabel1Number.CompareTo(releaseLabel2Number);
                }

                if (label1IsNumeric || label2IsNumeric)
                {
                    // numeric labels come before alpha labels
                    return label1IsNumeric ? -1 : 1;
                }

                // Everything will be compared case insensitively.
                return string.Compare(releaseLabel1, releaseLabel2, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
