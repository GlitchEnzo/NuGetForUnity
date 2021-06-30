using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NuGet.Editor.Util
{
    public struct UnityVersion : IComparable<UnityVersion>
    {
        public int Major;
        public int Minor;
        public int Revision;
        public char Release;
        public int Build;

        public static UnityVersion Current = new UnityVersion(Application.unityVersion);

        public UnityVersion(string version)
        {
            Match match = Regex.Match(version, @"(\d+)\.(\d+)\.(\d+)([fpba])(\d+)");
            if (!match.Success) { throw new ArgumentException("Invalid unity version"); }

            Major = int.Parse(match.Groups[1].Value);
            Minor = int.Parse(match.Groups[2].Value);
            Revision = int.Parse(match.Groups[3].Value);
            Release = match.Groups[4].Value[0];
            Build = int.Parse(match.Groups[5].Value);
        }

        public static int Compare(UnityVersion a, UnityVersion b)
        {

            if (a.Major < b.Major) { return -1; }
            if (a.Major > b.Major) { return 1; }

            if (a.Minor < b.Minor) { return -1; }
            if (a.Minor > b.Minor) { return 1; }

            if (a.Revision < b.Revision) { return -1; }
            if (a.Revision > b.Revision) { return 1; }

            if (a.Release < b.Release) { return -1; }
            if (a.Release > b.Release) { return 1; }

            if (a.Build < b.Build) { return -1; }
            if (a.Build > b.Build) { return 1; }

            return 0;
        }

        public int CompareTo(UnityVersion other)
        {
            return Compare(this, other);
        }
    }
}