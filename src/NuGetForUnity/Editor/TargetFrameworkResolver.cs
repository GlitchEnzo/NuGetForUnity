using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace NugetForUnity
{
    /// <summary>
    ///     Helper to select the best target-framework.
    /// </summary>
    internal static class TargetFrameworkResolver
    {
        // highest priority first. We use values without '.' for easier comparison.
        private static readonly TargetFrameworkSupport[] prioritizedTargetFrameworks =
        {
            new TargetFrameworkSupport("unity"),

            // .net framework (as we don't support unity < 2018 we can expect that at least .net framework 4.4 is supported)
            new TargetFrameworkSupport("net48", new UnityVersion(2021, 2, 0, 'f', 0), DotnetVersionCompatibilityLevel.NetFramework46Or48),
            new TargetFrameworkSupport("net472", null, DotnetVersionCompatibilityLevel.NetFramework46Or48),
            new TargetFrameworkSupport("net471", null, DotnetVersionCompatibilityLevel.NetFramework46Or48),
            new TargetFrameworkSupport("net47", null, DotnetVersionCompatibilityLevel.NetFramework46Or48),
            new TargetFrameworkSupport("net462", null, DotnetVersionCompatibilityLevel.NetFramework46Or48),
            new TargetFrameworkSupport("net461", null, DotnetVersionCompatibilityLevel.NetFramework46Or48),
            new TargetFrameworkSupport("net46", null, DotnetVersionCompatibilityLevel.NetFramework46Or48),
            new TargetFrameworkSupport("net452", null, DotnetVersionCompatibilityLevel.NetFramework46Or48),
            new TargetFrameworkSupport("net451", null, DotnetVersionCompatibilityLevel.NetFramework46Or48),
            new TargetFrameworkSupport("net45", null, DotnetVersionCompatibilityLevel.NetFramework46Or48),
            new TargetFrameworkSupport("net403", null, DotnetVersionCompatibilityLevel.NetFramework46Or48),
            new TargetFrameworkSupport("net40", null, DotnetVersionCompatibilityLevel.NetFramework46Or48),
            new TargetFrameworkSupport("net4", null, DotnetVersionCompatibilityLevel.NetFramework46Or48),
            new TargetFrameworkSupport("net35-unity full v35", null, DotnetVersionCompatibilityLevel.NetFramework46Or48),
            new TargetFrameworkSupport("net35-unity subset v35", null, DotnetVersionCompatibilityLevel.NetFramework46Or48),
            new TargetFrameworkSupport("net35", null, DotnetVersionCompatibilityLevel.NetFramework46Or48),
            new TargetFrameworkSupport("net20", null, DotnetVersionCompatibilityLevel.NetFramework46Or48),
            new TargetFrameworkSupport("net11", null, DotnetVersionCompatibilityLevel.NetFramework46Or48),

            // .net standard
            new TargetFrameworkSupport(
                "netstandard21",
                new UnityVersion(2021, 2, 0, 'f', 0),
                DotnetVersionCompatibilityLevel.NetStandard20Or21,
                DotnetVersionCompatibilityLevel.NetFramework46Or48),
            new TargetFrameworkSupport(
                "netstandard20",
                null,
                DotnetVersionCompatibilityLevel.NetStandard20Or21,
                DotnetVersionCompatibilityLevel.NetFramework46Or48),
            new TargetFrameworkSupport(
                "netstandard16",
                null,
                DotnetVersionCompatibilityLevel.NetStandard20Or21,
                DotnetVersionCompatibilityLevel.NetFramework46Or48),
            new TargetFrameworkSupport(
                "netstandard15",
                null,
                DotnetVersionCompatibilityLevel.NetStandard20Or21,
                DotnetVersionCompatibilityLevel.NetFramework46Or48),
            new TargetFrameworkSupport(
                "netstandard14",
                null,
                DotnetVersionCompatibilityLevel.NetStandard20Or21,
                DotnetVersionCompatibilityLevel.NetFramework46Or48),
            new TargetFrameworkSupport(
                "netstandard13",
                null,
                DotnetVersionCompatibilityLevel.NetStandard20Or21,
                DotnetVersionCompatibilityLevel.NetFramework46Or48),
            new TargetFrameworkSupport(
                "netstandard12",
                null,
                DotnetVersionCompatibilityLevel.NetStandard20Or21,
                DotnetVersionCompatibilityLevel.NetFramework46Or48),
            new TargetFrameworkSupport(
                "netstandard11",
                null,
                DotnetVersionCompatibilityLevel.NetStandard20Or21,
                DotnetVersionCompatibilityLevel.NetFramework46Or48),
            new TargetFrameworkSupport(
                "netstandard10",
                null,
                DotnetVersionCompatibilityLevel.NetStandard20Or21,
                DotnetVersionCompatibilityLevel.NetFramework46Or48),

            // fallback if there is one with empty string
            new TargetFrameworkSupport(string.Empty),
        };

        private static DotnetVersionCompatibilityLevel CurrentBuildTargetDotnetVersionCompatibilityLevel
        {
            get
            {
                switch (PlayerSettings.GetApiCompatibilityLevel(EditorUserBuildSettings.selectedBuildTargetGroup))
                {
                    case ApiCompatibilityLevel.NET_4_6:
                        return DotnetVersionCompatibilityLevel.NetFramework46Or48;
                    case ApiCompatibilityLevel.NET_Standard_2_0:
                        return DotnetVersionCompatibilityLevel.NetStandard20Or21;
                    default:
                        return DotnetVersionCompatibilityLevel.None;
                }
            }
        }

        /// <summary>
        ///     Select the highest .NET library available that is supported
        ///     See here: https://docs.nuget.org/ndocs/schema/target-frameworks
        /// </summary>
        /// <param name="availableTargetFrameworks"></param>
        /// <returns></returns>
        public static string TryGetBestTargetFramework(IReadOnlyCollection<string> availableTargetFrameworks)
        {
            return TryGetBestTargetFramework(availableTargetFrameworks, targetFramework => targetFramework);
        }

        public static T TryGetBestTargetFramework<T>(IReadOnlyCollection<T> availableTargetFrameworks, Func<T, string> getTargetFrameworkString)
        {
            var currentDotnetVersion = CurrentBuildTargetDotnetVersionCompatibilityLevel;
            var currentUnityVersion = UnityVersion.Current;
            foreach (var targetFrameworkSupport in prioritizedTargetFrameworks)
            {
                if (targetFrameworkSupport.SupportedDotnetVersions.Length != 0 &&
                    !targetFrameworkSupport.SupportedDotnetVersions.Contains(currentDotnetVersion))
                {
                    continue;
                }

                if (targetFrameworkSupport.MinimumUnityVersion != null && currentUnityVersion < targetFrameworkSupport.MinimumUnityVersion.Value)
                {
                    continue;
                }

                var bestMatch = availableTargetFrameworks.FirstOrDefault(
                    availableTargetFramework =>
                    {
                        var availableString = getTargetFrameworkString(availableTargetFramework).Replace(".", "");
                        return availableString.Equals(targetFrameworkSupport.Name, StringComparison.OrdinalIgnoreCase);
                    });

                if (bestMatch != null)
                {
                    return bestMatch;
                }
            }

            return default;
        }

        private enum DotnetVersionCompatibilityLevel
        {
            None = 0,

            /// <summary>
            ///     The full .net framework (not .net core or normal .net since (.net 5.0)).
            ///     .net framework 4.6 available since Unity 5.6 and Unity >= 2017.1.
            ///     .net framework 4.8 since Unity 2021.2.
            /// </summary>
            NetFramework46Or48,

            /// <summary>
            ///     .NET Standard is a formal specification of .NET APIs that are available on multiple .NET implementations.
            ///     Unity supports .net standard 2.0 and since Unity 2021.2 .net standard 2.1.
            /// </summary>
            NetStandard20Or21,
        }

        private readonly struct TargetFrameworkSupport
        {
            public readonly string Name;

            public readonly UnityVersion? MinimumUnityVersion;

            public readonly DotnetVersionCompatibilityLevel[] SupportedDotnetVersions;

            public TargetFrameworkSupport(string name,
                UnityVersion? minimumUnityVersion = null,
                params DotnetVersionCompatibilityLevel[] supportedDotnetVersions)
            {
                Name = name;
                MinimumUnityVersion = minimumUnityVersion;
                SupportedDotnetVersions = supportedDotnetVersions;
            }
        }

        private readonly struct UnityVersion : IComparable<UnityVersion>
        {
            public readonly int Major;

            public readonly int Minor;

            public readonly int Revision;

            public readonly char Release;

            public readonly int Build;

            [SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Local", Justification = "Property setter needed for unit test")]
            public static UnityVersion Current { get; private set; } = new UnityVersion(Application.unityVersion);

            public UnityVersion(string version)
            {
                var match = Regex.Match(version, @"(\d+)\.(\d+)\.(\d+)([fpba])(\d+)");
                if (!match.Success)
                {
                    throw new ArgumentException("Invalid unity version");
                }

                Major = int.Parse(match.Groups[1].Value);
                Minor = int.Parse(match.Groups[2].Value);
                Revision = int.Parse(match.Groups[3].Value);
                Release = match.Groups[4].Value[0];
                Build = int.Parse(match.Groups[5].Value);
            }

            public UnityVersion(int major, int minor, int revision, char release, int build)
            {
                Major = major;
                Minor = minor;
                Revision = revision;
                Release = release;
                Build = build;
            }

            public static int Compare(UnityVersion a, UnityVersion b)
            {
                if (a.Major < b.Major)
                {
                    return -1;
                }

                if (a.Major > b.Major)
                {
                    return 1;
                }

                if (a.Minor < b.Minor)
                {
                    return -1;
                }

                if (a.Minor > b.Minor)
                {
                    return 1;
                }

                if (a.Revision < b.Revision)
                {
                    return -1;
                }

                if (a.Revision > b.Revision)
                {
                    return 1;
                }

                if (a.Release < b.Release)
                {
                    return -1;
                }

                if (a.Release > b.Release)
                {
                    return 1;
                }

                if (a.Build < b.Build)
                {
                    return -1;
                }

                if (a.Build > b.Build)
                {
                    return 1;
                }

                return 0;
            }

            public int CompareTo(UnityVersion other)
            {
                return Compare(this, other);
            }

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
        }
    }
}
