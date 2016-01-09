namespace NugetForUnity
{
    using System;

    /// <summary>
    /// Represents an identifier for a NuGet package.  It contains only an ID and a Version number.
    /// </summary>
    public class NugetPackageIdentifier : IEquatable<NugetPackageIdentifier>
    {
        /// <summary>
        /// Gets or sets the ID of the NuGet package.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the version number of the NuGet package.
        /// </summary>
        public string Version { get; set; }

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

            return CompareVersions(first.Version, second.Version) < 0;
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

            return CompareVersions(first.Version, second.Version) > 0;
        }

        public static bool operator ==(NugetPackageIdentifier first, NugetPackageIdentifier second)
        {
            if (object.ReferenceEquals(first, null))
            {
                return object.ReferenceEquals(second, null);
            }

            return first.Equals(second);
        }

        public static bool operator !=(NugetPackageIdentifier first, NugetPackageIdentifier second)
        {
            if (object.ReferenceEquals(first, null))
            {
                return !object.ReferenceEquals(second, null);
            }

            return !first.Equals(second);
        }

        /// <summary>
        /// Compares two version numbers in the form "1.2.1". Returns:
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
                // TODO: Compare the prerelease beta/alpha tag
                versionA = versionA.Split('-')[0];
                string[] splitA = versionA.Split('.');
                int majorA = int.Parse(splitA[0]);
                int minorA = int.Parse(splitA[1]);
                int patchA = int.Parse(splitA[2]);
                int buildA = 0;
                if (splitA.Length == 4)
                {
                    buildA = int.Parse(splitA[3]);
                }

                versionB = versionB.Split('-')[0];
                string[] splitB = versionB.Split('.');
                int majorB = int.Parse(splitB[0]);
                int minorB = int.Parse(splitB[1]);
                int patchB = int.Parse(splitB[2]);
                int buildB = 0;
                if (splitB.Length == 4)
                {
                    buildB = int.Parse(splitB[3]);
                }

                int major = majorA < majorB ? -1 : majorA > majorB ? 1 : 0;
                int minor = minorA < minorB ? -1 : minorA > minorB ? 1 : 0;
                int patch = patchA < patchB ? -1 : patchA > patchB ? 1 : 0;
                int build = buildA < buildB ? -1 : buildA > buildB ? 1 : 0;

                if (major == 0)
                {
                    // if major versions are equal, compare minor versions
                    if (minor == 0)
                    {
                        if (patch == 0)
                        {
                            // if patch versions are equal, just use the build version
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
                return 0;
            }
        }
    }
}