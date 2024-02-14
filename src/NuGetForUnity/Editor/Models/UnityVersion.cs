using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NugetForUnity.Models
{
    /// <summary>
    ///    Represents a unity version.
    /// </summary>
    internal readonly struct UnityVersion : IComparable<UnityVersion>
    {
        private readonly int build;

        private readonly int major;

        private readonly int minor;

        private readonly char release;

        private readonly int revision;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnityVersion"/> struct.
        /// </summary>
        /// <param name="major">Major version number.</param>
        /// <param name="minor">Minor version number.</param>
        /// <param name="revision">Revision number.</param>
        /// <param name="release">Release flag. If 'f', official release. If 'p' patch release.</param>
        /// <param name="build">Build number.</param>
        public UnityVersion(int major, int minor, int revision, char release, int build)
        {
            this.major = major;
            this.minor = minor;
            this.revision = revision;
            this.release = release;
            this.build = build;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UnityVersion"/> struct.
        /// </summary>
        /// <param name="version">A string representation of Unity version.</param>
        /// <exception cref="ArgumentException">Cannot parse version.</exception>
        [SuppressMessage("ReSharper", "MemberCanBePrivate.Local", Justification = "Called by Unit Test.")]
        public UnityVersion(string version)
        {
            var match = Regex.Match(version, @"(\d+)\.(\d+)\.(\d+)([fpba])(\d+)");
            if (!match.Success)
            {
                throw new ArgumentException("Invalid unity version");
            }

            major = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            minor = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            revision = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            release = match.Groups[4].Value[0];
            build = int.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        ///     Gets current version from Application.unityVersion.
        /// </summary>
        [SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Local", Justification = "Property setter needed for unit test")]
        public static UnityVersion Current { get; private set; } = new UnityVersion(Application.unityVersion);

        public static bool operator <(UnityVersion left, UnityVersion right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(UnityVersion left, UnityVersion right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(UnityVersion left, UnityVersion right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(UnityVersion left, UnityVersion right)
        {
            return left.CompareTo(right) >= 0;
        }

        public int CompareTo(UnityVersion other)
        {
            return Compare(this, other);
        }

        private static int Compare(UnityVersion a, UnityVersion b)
        {
            if (a.major < b.major)
            {
                return -1;
            }

            if (a.major > b.major)
            {
                return 1;
            }

            if (a.minor < b.minor)
            {
                return -1;
            }

            if (a.minor > b.minor)
            {
                return 1;
            }

            if (a.revision < b.revision)
            {
                return -1;
            }

            if (a.revision > b.revision)
            {
                return 1;
            }

            if (a.release < b.release)
            {
                return -1;
            }

            if (a.release > b.release)
            {
                return 1;
            }

            if (a.build < b.build)
            {
                return -1;
            }

            if (a.build > b.build)
            {
                return 1;
            }

            return 0;
        }
    }
}
