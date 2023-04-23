using System;
using System.Linq;
using UnityEngine;

namespace NugetForUnity
{
    public class NuGetPackageVersion : IComparable<NuGetPackageVersion>, IEquatable<NuGetPackageVersion>
    {
        private readonly string fullString;

        private readonly string preReleaseIdentifier;

        private readonly string versionNumberPart;

        /// <summary>
        ///     Initializes a new instance of the <see cref="NuGetPackageVersion" /> class with the given Version string.
        /// </summary>
        /// <param name="version">The version number as string.</param>
        public NuGetPackageVersion(string version)
        {
            fullString = version.Trim();
            IsMinInclusive = fullString.StartsWith("[");
            HasVersionRange = fullString.StartsWith("(") || IsMinInclusive;
            if (HasVersionRange)
            {
                IsMaxInclusive = fullString.EndsWith("]");
                var minMax = fullString.TrimStart('[', '(').TrimEnd(']', ')').Split(',');
                MinimumVersion = minMax.FirstOrDefault()?.Trim();

                // if there is no MaxVersion specified, but the Max is Inclusive, then it is an EXACT version match with the stored MINIMUM
                MaximumVersion = minMax.Length == 2 ? minMax[1].Trim() : null;
            }

            var versionNumberString = HasVersionRange ? MaximumVersion ?? MinimumVersion : fullString;
            (versionNumberPart, preReleaseIdentifier) = SplitPreReleasIdentifier(versionNumberString);
        }

        /// <summary>
        ///     Gets a value indicating whether the minimum version number (only valid when HasVersionRange is true) is inclusive (true) or exclusive (false).
        /// </summary>
        public bool IsMinInclusive { get; }

        /// <summary>
        ///     Gets a value indicating whether the maximum version number (only valid when HasVersionRange is true) is inclusive (true) or exclusive (false).
        /// </summary>
        public bool IsMaxInclusive { get; }

        /// <summary>
        ///     Gets the minimum version number of the NuGet package. Only valid when HasVersionRange is true.
        /// </summary>
        public string MinimumVersion { get; }

        /// <summary>
        ///     Gets the maximum version number of the NuGet package. Only valid when HasVersionRange is true.
        /// </summary>
        public string MaximumVersion { get; }

        /// <summary>
        ///     Gets a value indicating whether this is a pre-release package or an official release package.
        /// </summary>
        public bool IsPrerelease => preReleaseIdentifier.Length > 0;

        /// <summary>
        ///     Gets a value indicating whether the version number specified is a range of values.
        /// </summary>
        public bool HasVersionRange { get; }

        /// <inheritdoc />
        public int CompareTo(NuGetPackageVersion other)
        {
            return CompareVersion((other.versionNumberPart, other.preReleaseIdentifier, other.HasVersionRange, other.fullString));
        }

        /// <summary>
        ///     Checks to see if this <see cref="NuGetPackageVersion" /> is equal to the given one.
        /// </summary>
        /// <param name="other">The other <see cref="NuGetPackageVersion" /> to check equality with.</param>
        /// <returns>True if the package identifiers are equal, otherwise false.</returns>
        public bool Equals(NuGetPackageVersion other)
        {
            return !(other is null) && other.fullString == fullString;
        }

        /// <summary>
        ///     Compares this version with the string representation of <paramref name="otherVersion" />.
        /// </summary>
        /// <param name="otherVersion">The other version number to check if it is grater or equal to this version.</param>
        /// <returns>
        ///     -1 if otherVersion is less than this.
        ///     0 if otherVersion is equal to this.
        ///     +1 if otherVersion is greater than this.
        /// </returns>
        public int CompareTo(string otherVersion)
        {
            var (versionPart, preReleasePart) = SplitPreReleasIdentifier(otherVersion);
            return CompareVersion((versionPart, preReleasePart, false, otherVersion));
        }

        /// <summary>
        ///     Checks to see if the first <see cref="NuGetPackageVersion" /> is less than the second.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is less than the second.</returns>
        public static bool operator <(NuGetPackageVersion first, NuGetPackageVersion second)
        {
            return first.CompareTo(second) < 0;
        }

        /// <summary>
        ///     Checks to see if the first <see cref="NuGetPackageVersion" /> is greater than the second.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is greater than the second.</returns>
        public static bool operator >(NuGetPackageVersion first, NuGetPackageVersion second)
        {
            return first.CompareTo(second) > 0;
        }

        /// <summary>
        ///     Checks to see if the first <see cref="NuGetPackageVersion" /> is less than or equal to the second.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is less than or equal to the second.</returns>
        public static bool operator <=(NuGetPackageVersion first, NuGetPackageVersion second)
        {
            return first.CompareTo(second) <= 0;
        }

        /// <summary>
        ///     Checks to see if the first <see cref="NuGetPackageVersion" /> is greater than or equal to the second.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is greater than or equal to the second.</returns>
        public static bool operator >=(NuGetPackageVersion first, NuGetPackageVersion second)
        {
            return first.CompareTo(second) >= 0;
        }

        /// <summary>
        ///     Checks to see if the first <see cref="NuGetPackageVersion" /> is equal to the second.
        ///     They are equal if the Id and the Version match.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is equal to the second.</returns>
        public static bool operator ==(NuGetPackageVersion first, NuGetPackageVersion second)
        {
            if (first is null)
            {
                return second is null;
            }

            return first.Equals(second);
        }

        /// <summary>
        ///     Checks to see if the first <see cref="NuGetPackageVersion" /> is not equal to the second.
        ///     They are not equal if the Id or the Version differ.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is not equal to the second.</returns>
        public static bool operator !=(NuGetPackageVersion first, NuGetPackageVersion second)
        {
            return !(first == second);
        }

        /// <summary>
        ///     Determines if the given version is in the version range of this <see cref="NuGetPackageVersion" />.
        ///     See here: https://docs.nuget.org/ndocs/create-packages/dependency-versions.
        /// </summary>
        /// <param name="other">The <see cref="NuGetPackageVersion" /> to check if is in the range.</param>
        /// <returns>True if the given version is in the range, otherwise false.</returns>
        public bool InRange(NuGetPackageVersion other)
        {
            var comparison = CompareVersion((other.versionNumberPart, other.preReleaseIdentifier, other.HasVersionRange, other.fullString));
            if (comparison == 0)
            {
                return true;
            }

            // if it has no version range specified (i.e. only a single version number) NuGet's specs
            // state that is the minimum version number, inclusive
            if (!HasVersionRange && comparison < 0)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Determines if a given object is equal to this <see cref="NuGetPackageVersion" />.
        /// </summary>
        /// <param name="obj">The object to check.</param>
        /// <returns>True if the given object is equal to this <see cref="NuGetPackageVersion" />, otherwise false.</returns>
        public override bool Equals(object obj)
        {
            return Equals(obj as NuGetPackageVersion);
        }

        /// <summary>
        ///     Gets the hash-code for this <see cref="NuGetPackageVersion" />.
        /// </summary>
        /// <returns>The hash-code for this instance.</returns>
        public override int GetHashCode()
        {
            return fullString.GetHashCode();
        }

        /// <summary>
        ///     Returns the string representation of this <see cref="NuGetPackageVersion" />.
        /// </summary>
        /// <returns>The version string.</returns>
        public override string ToString()
        {
            return fullString;
        }

        /// <summary>
        ///     Compares the given version string with the version range of this <see cref="NuGetPackageVersion" />.
        ///     See here: https://docs.nuget.org/ndocs/create-packages/dependency-versions.
        /// </summary>
        /// <param name="otherVersion">The version to check if is in the range.</param>
        /// <returns>
        ///     -1 if otherVersion is less than the version range. 0 if otherVersion is inside the version range. +1 if otherVersion is greater than the
        ///     version range.
        /// </returns>
        private int CompareVersion((string VersionNumberPart, string PreReleaseIdentifier, bool HasVersionRange, string FullString) otherVersion)
        {
            if (!HasVersionRange)
            {
                return CompareVersions(fullString, otherVersion);
            }

            if (!string.IsNullOrEmpty(MinimumVersion))
            {
                var compare = CompareVersions(MinimumVersion, otherVersion);

                // -1 = Min < other <-- Inclusive & Exclusive
                //  0 = Min = other <-- Inclusive Only
                // +1 = Min > other <-- OUT OF RANGE
                if (IsMinInclusive)
                {
                    if (compare > 0)
                    {
                        return -1;
                    }
                }
                else
                {
                    if (compare >= 0)
                    {
                        return -1;
                    }
                }
            }

            if (!string.IsNullOrEmpty(MaximumVersion))
            {
                var compare = CompareVersions(MaximumVersion, otherVersion);

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
                    return CompareVersions(MinimumVersion, otherVersion);
                }
            }

            return 0;
        }

        /// <summary>
        ///     Compares two version numbers in the form "1.2". Also supports an optional 3rd and 4th number as well as a pre-release tag, such as
        ///     "1.3.0.1-alpha2".
        ///     Returns:
        ///     -1 if versionA is less than versionB
        ///     0 if versionA is equal to versionB
        ///     +1 if versionA is greater than versionB.
        /// </summary>
        /// <param name="versionA">The first version number to compare.</param>
        /// <param name="versionB">The second version number to compare.</param>
        /// <returns>-1 if versionA is less than versionB. 0 if versionA is equal to versionB. +1 if versionA is greater than versionB.</returns>
        private static int CompareVersions(
            string versionA,
            (string VersionNumberPart, string PreReleaseIdentifier, bool HasVersionRange, string FullString) versionB)
        {
            if (versionB.HasVersionRange)
            {
                // this is more or less invalid / makes no sense.
                return string.Compare(versionA, versionB.FullString, StringComparison.Ordinal);
            }

            return CompareVersions(versionA, (versionB.VersionNumberPart, versionB.PreReleaseIdentifier));
        }

        private static int CompareVersions(string versionA, (string VersionNumberPart, string PreReleaseIdentifier) versionB)
        {
            try
            {
                var (versionNumberPartA, preReleaseIdentifierA) = SplitPreReleasIdentifier(versionA);
                versionA = versionNumberPartA;
                var prereleaseA = string.IsNullOrEmpty(preReleaseIdentifierA) ? "\uFFFF" : preReleaseIdentifierA;

                var splitA = versionA.Split('.');
                var majorA = int.Parse(splitA[0]);
                var minorA = int.Parse(splitA[1]);
                var patchA = 0;
                if (splitA.Length >= 3)
                {
                    patchA = int.Parse(splitA[2]);
                }

                var buildA = 0;
                if (splitA.Length >= 4)
                {
                    buildA = int.Parse(splitA[3]);
                }

                var versionStringB = versionB.VersionNumberPart;
                var prereleaseB = string.IsNullOrEmpty(versionB.PreReleaseIdentifier) ? "\uFFFF" : versionB.PreReleaseIdentifier;

                var splitB = versionStringB.Split('.');
                var majorB = int.Parse(splitB[0]);
                var minorB = int.Parse(splitB[1]);
                var patchB = 0;
                if (splitB.Length >= 3)
                {
                    patchB = int.Parse(splitB[2]);
                }

                var buildB = 0;
                if (splitB.Length >= 4)
                {
                    buildB = int.Parse(splitB[3]);
                }

                var major = majorA < majorB ? -1 :
                    majorA > majorB ? 1 : 0;
                var minor = minorA < minorB ? -1 :
                    minorA > minorB ? 1 : 0;
                var patch = patchA < patchB ? -1 :
                    patchA > patchB ? 1 : 0;
                var build = buildA < buildB ? -1 :
                    buildA > buildB ? 1 : 0;
                var prerelease = string.Compare(prereleaseA, prereleaseB, StringComparison.Ordinal);

                if (major == 0)
                {
                    // if major versions are equal, compare minor versions
                    if (minor == 0)
                    {
                        if (patch == 0)
                        {
                            // if patch versions are equal, compare build versions
                            if (build == 0)
                            {
                                // if the build versions are equal, just return the pre-release version comparison
                                return prerelease;
                            }

                            // the build versions are different, so use them
                            return build;
                        }

                        // the patch versions are different, so use them
                        return patch;
                    }

                    // the minor versions are different, so use them
                    return minor;
                }

                // the major versions are different, so use them
                return major;
            }
            catch (Exception)
            {
                Debug.LogErrorFormat("Compare Error: {0} {1}", versionA, versionB);
                return -1;
            }
        }

        private static (string VersionNumberPart, string PreReleaseIdentifier) SplitPreReleasIdentifier(string versionNumberString)
        {
            var preReleaseIdentifierStart = versionNumberString.IndexOf('-');
            var preReleaseIdentifier = preReleaseIdentifierStart == -1 ? string.Empty : versionNumberString.Substring(preReleaseIdentifierStart + 1);
            var versionNumberPart = preReleaseIdentifierStart == -1 ?
                versionNumberString :
                versionNumberString.Substring(0, preReleaseIdentifierStart);
            return (versionNumberPart, preReleaseIdentifier);
        }
    }
}
