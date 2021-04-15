namespace NugetForUnity
{
    using System;

    /// <summary>
    /// Represents an identifier for a NuGet package.  It contains only an ID and a Version number.
    /// </summary>
    public class NugetPackageIdentifier : IEquatable<NugetPackageIdentifier>, IComparable<NugetPackageIdentifier>
    {
        /// <summary>
        /// Gets or sets the ID of the NuGet package.
        /// </summary>
        public string Id;

        /// <summary>
        /// Gets or sets the version number of the NuGet package.
        /// </summary>
        public string Version;

        /// <summary>
        /// Gets a value indicating whether this is a prerelease package or an official release package.
        /// </summary>
        public bool IsPrerelease { get { return Version.Contains("-"); } }

        /// <summary>
        /// Gets a value indicating whether the version number specified is a range of values.
        /// </summary>
        public bool HasVersionRange { get { return Version.StartsWith("(") || Version.StartsWith("["); } }

        /// <summary>
        /// Gets a value indicating whether the minimum version number (only valid when HasVersionRange is true) is inclusive (true) or exclusive (false).
        /// </summary>
        public bool IsMinInclusive { get { return Version.StartsWith("["); } }

        /// <summary>
        /// Gets a value indicating whether the maximum version number (only valid when HasVersionRange is true) is inclusive (true) or exclusive (false).
        /// </summary>
        public bool IsMaxInclusive { get { return Version.EndsWith("]"); } }

        /// <summary>
        /// Gets the minimum version number of the NuGet package. Only valid when HasVersionRange is true.
        /// </summary>
        public string MinimumVersion { get { return Version.TrimStart(new[] { '[', '(' }).TrimEnd(new[] { ']', ')' }).Split(new[] { ',' })[0].Trim(); } }

        /// <summary>
        /// Gets the maximum version number of the NuGet package. Only valid when HasVersionRange is true.
        /// </summary>
        public string MaximumVersion 
        {
            get 
            {
                // if there is no MaxVersion specified, but the Max is Inclusive, then it is an EXACT version match with the stored MINIMUM
                string[] minMax = Version.TrimStart(new[] { '[', '(' }).TrimEnd(new[] { ']', ')' }).Split(new[] { ',' });
                return minMax.Length == 2 ? minMax[1].Trim() : null; 
            } 
        }

        /// <summary>
        /// Initializes a new instance of a <see cref="NugetPackageIdentifider"/> with empty ID and Version.
        /// </summary>
        public NugetPackageIdentifier()
        {
            Id = string.Empty;
            Version = string.Empty;
        }

        /// <summary>
        /// Initializes a new instance of a <see cref="NugetPackageIdentifider"/> with the given ID and Version.
        /// </summary>
        /// <param name="id">The ID of the package.</param>
        /// <param name="version">The version number of the package.</param>
        public NugetPackageIdentifier(string id, string version)
        {
            Id = id;
            Version = version;
        }

        /// <summary>
        /// Checks to see if this <see cref="NugetPackageIdentifier"/> is equal to the given one.
        /// </summary>
        /// <param name="other">The other <see cref="NugetPackageIdentifier"/> to check equality with.</param>
        /// <returns>True if the package identifiers are equal, otherwise false.</returns>
        public bool Equals(NugetPackageIdentifier other)
        {
            return other != null && other.Id == Id && other.Version == Version;
        }

        /// <summary>
        /// Checks to see if the first <see cref="NugetPackageIdentifier"/> is less than the second.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is less than the second.</returns>
        public static bool operator <(NugetPackageIdentifier first, NugetPackageIdentifier second)
        {
            if (first.Id != second.Id)
            {
                return string.Compare(first.Id, second.Id) < 0;
            }

            return first.CompareVersion(second.Version) < 0;
        }

        /// <summary>
        /// Checks to see if the first <see cref="NugetPackageIdentifier"/> is greater than the second.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is greater than the second.</returns>
        public static bool operator >(NugetPackageIdentifier first, NugetPackageIdentifier second)
        {
            if (first.Id != second.Id)
            {
                return string.Compare(first.Id, second.Id) > 0;
            }

            return first.CompareVersion(second.Version) > 0;
        }

        /// <summary>
        /// Checks to see if the first <see cref="NugetPackageIdentifier"/> is less than or equal to the second.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is less than or equal to the second.</returns>
        public static bool operator <=(NugetPackageIdentifier first, NugetPackageIdentifier second)
        {
            if (first.Id != second.Id)
            {
                return string.Compare(first.Id, second.Id) <= 0;
            }

            return first.CompareVersion(second.Version) <= 0;
        }

        /// <summary>
        /// Checks to see if the first <see cref="NugetPackageIdentifier"/> is greater than or equal to the second.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is greater than or equal to the second.</returns>
        public static bool operator >=(NugetPackageIdentifier first, NugetPackageIdentifier second)
        {
            if (first.Id != second.Id)
            {
                return string.Compare(first.Id, second.Id) >= 0;
            }

            return first.CompareVersion(second.Version) >= 0;
        }

        /// <summary>
        /// Checks to see if the first <see cref="NugetPackageIdentifier"/> is equal to the second.
        /// They are equal if the Id and the Version match.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is equal to the second.</returns>
        public static bool operator ==(NugetPackageIdentifier first, NugetPackageIdentifier second)
        {
            if (ReferenceEquals(first, null))
            {
                return ReferenceEquals(second, null);
            }

            return first.Equals(second);
        }

        /// <summary>
        /// Checks to see if the first <see cref="NugetPackageIdentifier"/> is not equal to the second.
        /// They are not equal if the Id or the Version differ.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is not equal to the second.</returns>
        public static bool operator !=(NugetPackageIdentifier first, NugetPackageIdentifier second)
        {
            if (ReferenceEquals(first, null))
            {
                return !ReferenceEquals(second, null);
            }

            return !first.Equals(second);
        }

        /// <summary>
        /// Determines if a given object is equal to this <see cref="NugetPackageIdentifier"/>.
        /// </summary>
        /// <param name="obj">The object to check.</param>
        /// <returns>True if the given object is equal to this <see cref="NugetPackageIdentifier"/>, otherwise false.</returns>
        public override bool Equals(object obj)
        {
            // If parameter is null return false.
            if (obj == null)
            {
                return false;
            }

            // If parameter cannot be cast to NugetPackageIdentifier return false.
            NugetPackageIdentifier p = obj as NugetPackageIdentifier;
            if ((object)p == null)
            {
                return false;
            }

            // Return true if the fields match:
            return (Id == p.Id) && (Version == p.Version);
        }

        /// <summary>
        /// Gets the hashcode for this <see cref="NugetPackageIdentifier"/>.
        /// </summary>
        /// <returns>The hashcode for this instance.</returns>
        public override int GetHashCode()
        {
            return Id.GetHashCode() ^ Version.GetHashCode();
        }

        /// <summary>
        /// Returns the string representation of this <see cref="NugetPackageIdentifer"/> in the form "{ID}.{Version}".
        /// </summary>
        /// <returns>A string in the form "{ID}.{Version}".</returns>
        public override string ToString()
        {
            return string.Format("{0}.{1}", Id, Version);
        }

        /// <summary>
        /// Determines if the given <see cref="NugetPackageIdentifier"/>'s version is in the version range of this <see cref="NugetPackageIdentifier"/>.
        /// See here: https://docs.nuget.org/ndocs/create-packages/dependency-versions
        /// </summary>
        /// <param name="otherVersion">The <see cref="NugetPackageIdentifier"/> whose version to check if is in the range.</param>
        /// <returns>True if the given version is in the range, otherwise false.</returns>
        public bool InRange(NugetPackageIdentifier otherPackage)
        {
            return InRange(otherPackage.Version);
        }

        /// <summary>
        /// Determines if the given version is in the version range of this <see cref="NugetPackageIdentifier"/>.
        /// See here: https://docs.nuget.org/ndocs/create-packages/dependency-versions
        /// </summary>
        /// <param name="otherVersion">The version to check if is in the range.</param>
        /// <returns>True if the given version is in the range, otherwise false.</returns>
        public bool InRange(string otherVersion)
        {
            int comparison = CompareVersion(otherVersion);
            if (comparison == 0) { return true; }

            // if it has no version range specified (ie only a single version number) NuGet's specs
            // state that that is the minimum version number, inclusive
            if (!HasVersionRange && comparison < 0) { return true; }

            return false;
        }

        /// <summary>
        /// Compares the given version string with the version range of this <see cref="NugetPackageIdentifier"/>.
        /// See here: https://docs.nuget.org/ndocs/create-packages/dependency-versions
        /// </summary>
        /// <param name="otherVersion">The version to check if is in the range.</param>
        /// <returns>-1 if otherVersion is less than the version range. 0 if otherVersion is inside the version range. +1 if otherVersion is greater than the version range.</returns>
        public int CompareVersion(string otherVersion)
        {
            if (!HasVersionRange)
            {
                return CompareVersions(Version, otherVersion);
            }

            if (!string.IsNullOrEmpty(MinimumVersion))
            {
                int compare = CompareVersions(MinimumVersion, otherVersion);
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
                int compare = CompareVersions(MaximumVersion, otherVersion);
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
        /// Compares two version numbers in the form "1.2". Also supports an optional 3rd and 4th number as well as a prerelease tag, such as "1.3.0.1-alpha2".
        /// Returns:
        /// -1 if versionA is less than versionB
        ///  0 if versionA is equal to versionB
        /// +1 if versionA is greater than versionB
        /// </summary>
        /// <param name="versionA">The first version number to compare.</param>
        /// <param name="versionB">The second version number to compare.</param>
        /// <returns>-1 if versionA is less than versionB. 0 if versionA is equal to versionB. +1 if versionA is greater than versionB</returns>
        private static int CompareVersions(string versionA, string versionB)
        {
            try
            {
                string[] splitStringsA = versionA.Split('-');
                versionA = splitStringsA[0];
                string prereleaseA = "\uFFFF";

                if (splitStringsA.Length > 1)
                {
                    prereleaseA = splitStringsA[1];
                    for (int i = 2; i < splitStringsA.Length; i++)
                    {
                        prereleaseA += "-" + splitStringsA[i];
                    }
                }

                string[] splitA = versionA.Split('.');
                int majorA = int.Parse(splitA[0]);
                int minorA = int.Parse(splitA[1]);
                int patchA = 0;
                if (splitA.Length >= 3)
                {
                    patchA = int.Parse(splitA[2]);
                }
                int buildA = 0;
                if (splitA.Length >= 4)
                {
                    buildA = int.Parse(splitA[3]);
                }

                string[] splitStringsB = versionB.Split('-');
                versionB = splitStringsB[0];
                string prereleaseB = "\uFFFF";

                if (splitStringsB.Length > 1)
                {
                    prereleaseB = splitStringsB[1];
                    for (int i = 2; i < splitStringsB.Length; i++)
                    {
                        prereleaseB += "-" + splitStringsB[i];
                    }
                }

                string[] splitB = versionB.Split('.');
                int majorB = int.Parse(splitB[0]);
                int minorB = int.Parse(splitB[1]);
                int patchB = 0;
                if (splitB.Length >= 3)
                {
                    patchB = int.Parse(splitB[2]);
                }
                int buildB = 0;
                if (splitB.Length >= 4)
                {
                    buildB = int.Parse(splitB[3]);
                }

                int major = majorA < majorB ? -1 : majorA > majorB ? 1 : 0;
                int minor = minorA < minorB ? -1 : minorA > minorB ? 1 : 0;
                int patch = patchA < patchB ? -1 : patchA > patchB ? 1 : 0;
                int build = buildA < buildB ? -1 : buildA > buildB ? 1 : 0;
                int prerelease = string.Compare(prereleaseA, prereleaseB, StringComparison.Ordinal);

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
                                // if the build versions are equal, just return the prerelease version comparison
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
                UnityEngine.Debug.LogErrorFormat("Compare Error: {0} {1}", versionA, versionB);
                return -1;
            }
        }

        public int CompareTo(NugetPackageIdentifier other)
        {
            if (this.Id != other.Id)
            {
                return string.Compare(this.Id, other.Id);
            }

            return CompareVersion(other.Version);
        }
    }
}