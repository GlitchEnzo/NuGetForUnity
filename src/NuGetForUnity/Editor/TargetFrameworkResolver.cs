﻿#pragma warning disable SA1512,SA1124 // Single-line comments should not be followed by blank line

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using NugetForUnity.Models;
using UnityEditor;
using UnityEngine;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.Build;
#endif

#region No ReShaper

// ReSharper disable All
// needed because 'JetBrains.Annotations.NotNull' and 'System.Diagnostics.CodeAnalysis.NotNull' collide if this file is compiled with a never version of Unity / C#
using SuppressMessageAttribute = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;

// ReSharper restore All

#endregion

#pragma warning restore SA1512,SA1124 // Single-line comments should not be followed by blank line
namespace NugetForUnity
{
    /// <summary>
    ///     Helper to select the best target-framework.
    /// </summary>
    internal static class TargetFrameworkResolver
    {
        // highest priority first. We use values without '.' for easier comparison.
        [NotNull]
        private static readonly TargetFrameworkSupport[] PrioritizedTargetFrameworks =
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

        /// <summary>
        ///     Gets the <see cref="ApiCompatibilityLevel" /> of the current selected build target.
        /// </summary>
        [SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Local", Justification = "Property setter needed for unit test")]
        internal static Lazy<ApiCompatibilityLevel> CurrentBuildTargetApiCompatibilityLevel { get; private set; } = new Lazy<ApiCompatibilityLevel>(
            () =>
            {
#if UNITY_2021_2_OR_NEWER
                return PlayerSettings.GetApiCompatibilityLevel(
                    NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup));
#else
                return PlayerSettings.GetApiCompatibilityLevel(EditorUserBuildSettings.selectedBuildTargetGroup);
#endif
            });

        private static DotnetVersionCompatibilityLevel CurrentBuildTargetDotnetVersionCompatibilityLevel
        {
            get
            {
                switch (CurrentBuildTargetApiCompatibilityLevel.Value)
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
        ///     Select the highest .NET library available that is supported by Unity.
        ///     See here: https://docs.nuget.org/ndocs/schema/target-frameworks.
        /// </summary>
        /// <param name="availableTargetFrameworks">The list of available target-frameworks.</param>
        /// <returns>The best matching target-framework.</returns>
        [CanBeNull]
        public static string TryGetBestTargetFramework([NotNull] [ItemNotNull] IReadOnlyCollection<string> availableTargetFrameworks)
        {
            return TryGetBestTargetFramework(availableTargetFrameworks, targetFramework => targetFramework);
        }

        /// <summary>
        ///     Select the highest .NET library available that is supported by Unity.
        ///     See here: https://docs.nuget.org/ndocs/schema/target-frameworks.
        /// </summary>
        /// <typeparam name="T">The type of the target-framework.</typeparam>
        /// <param name="availableTargetFrameworks">The list of available target-frameworks.</param>
        /// <param name="getTargetFrameworkString">A function to get the target-framework string.</param>
        /// <returns>The best matching target-framework.</returns>
        [CanBeNull]
        public static T TryGetBestTargetFramework<T>(
            [NotNull] [ItemNotNull] IReadOnlyCollection<T> availableTargetFrameworks,
            [NotNull] Func<T, string> getTargetFrameworkString)
        {
            var currentDotnetVersion = CurrentBuildTargetDotnetVersionCompatibilityLevel;
            var currentUnityVersion = UnityVersion.Current;
            foreach (var targetFrameworkSupport in PrioritizedTargetFrameworks)
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
                        var availableString = getTargetFrameworkString(availableTargetFramework).Replace(".", string.Empty);
                        return availableString.Equals(targetFrameworkSupport.Name, StringComparison.OrdinalIgnoreCase);
                    });

                if (!Equals(bestMatch, default(T)))
                {
                    return bestMatch;
                }
            }

            return default;
        }

        /// <summary>
        ///     Select the best target-framework group of a NuGet package.
        /// </summary>
        /// <param name="packageDependencies">The available frameworks.</param>
        /// <returns>The selected target framework group or null if non is matching.</returns>
        [CanBeNull]
        internal static NugetFrameworkGroup GetNullableBestDependencyFrameworkGroupForCurrentSettings(
            [NotNull] [ItemNotNull] List<NugetFrameworkGroup> packageDependencies)
        {
            var bestTargetFramework = TryGetBestTargetFramework(packageDependencies, frameworkGroup => frameworkGroup.TargetFramework);
            NugetLogger.LogVerbose(
                "Selecting {0} as the best target framework for current settings",
                bestTargetFramework?.TargetFramework ?? "(null)");
            return bestTargetFramework;
        }

        /// <summary>
        ///     Select the best target-framework group of a NuGet package.
        /// </summary>
        /// <param name="packageDependencies">The available frameworks.</param>
        /// <returns>The selected target framework group or a empty group if non is matching.</returns>
        [NotNull]
        internal static NugetFrameworkGroup GetBestDependencyFrameworkGroupForCurrentSettings(
            [NotNull] [ItemNotNull] List<NugetFrameworkGroup> packageDependencies)
        {
            return GetNullableBestDependencyFrameworkGroupForCurrentSettings(packageDependencies) ?? new NugetFrameworkGroup();
        }

        /// <summary>
        ///     Select the best target-framework group of a NuGet package.
        /// </summary>
        /// <param name="nuspec">The package of witch the dependencies are selected.</param>
        /// <returns>The selected target framework group or a empty group if non is matching.</returns>
        [NotNull]
        internal static NugetFrameworkGroup GetBestDependencyFrameworkGroupForCurrentSettings([NotNull] NuspecFile nuspec)
        {
            return GetBestDependencyFrameworkGroupForCurrentSettings(nuspec.Dependencies);
        }

        /// <summary>
        ///     Select the best target-framework name.
        /// </summary>
        /// <param name="targetFrameworks">The available frameworks.</param>
        /// <returns>The selected target framework or null if non is matching.</returns>
        [CanBeNull]
        internal static string TryGetBestTargetFrameworkForCurrentSettings([NotNull] [ItemNotNull] IReadOnlyCollection<string> targetFrameworks)
        {
            var result = TryGetBestTargetFramework(targetFrameworks);
            NugetLogger.LogVerbose("Selecting {0} as the best target framework for current settings", result ?? "(null)");
            return result;
        }

        [SuppressMessage(
            "StyleCop.CSharp.OrderingRules",
            "SA1201:Elements should appear in the correct order",
            Justification = "We like private enums at the botom of the file.")]
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

        // Ignore Spelling: dotnet
        private readonly struct TargetFrameworkSupport
        {
            public TargetFrameworkSupport(
                string name,
                UnityVersion? minimumUnityVersion = null,
                params DotnetVersionCompatibilityLevel[] supportedDotnetVersions)
            {
                Name = name;
                MinimumUnityVersion = minimumUnityVersion;
                SupportedDotnetVersions = supportedDotnetVersions;
            }

            public UnityVersion? MinimumUnityVersion { get; }

            public string Name { get; }

            public DotnetVersionCompatibilityLevel[] SupportedDotnetVersions { get; }
        }

        private readonly struct UnityVersion : IComparable<UnityVersion>
        {
            private readonly int build;

            private readonly int major;

            private readonly int minor;

            private readonly char release;

            private readonly int revision;

            public UnityVersion(int major, int minor, int revision, char release, int build)
            {
                this.major = major;
                this.minor = minor;
                this.revision = revision;
                this.release = release;
                this.build = build;
            }

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
}
