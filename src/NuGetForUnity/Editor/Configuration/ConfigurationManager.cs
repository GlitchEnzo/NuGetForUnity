using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NugetForUnity.Helper;
using NugetForUnity.Models;
using NugetForUnity.PackageSource;
using NugetForUnity.PluginSupport;
using UnityEngine;

[assembly: InternalsVisibleTo("NuGetForUnity.Editor.Tests")]

namespace NugetForUnity.Configuration
{
    /// <summary>
    ///     Manages the active configuration of NuGetForUnity <see cref="NugetConfigFile" />.
    /// </summary>
    public static class ConfigurationManager
    {
        /// <summary>
        ///     The <see cref="INugetPackageSource" /> to use.
        /// </summary>
        [CanBeNull]
        private static INugetPackageSource activePackageSource;

        /// <summary>
        ///     Backing field for the NuGet.config file.
        /// </summary>
        [CanBeNull]
        private static NugetConfigFile nugetConfigFile;

        static ConfigurationManager()
        {
            NugetConfigFilePath = Path.Combine(UnityPathHelper.AbsoluteUnityPackagesNugetPath, NugetConfigFile.FileName);
            if (File.Exists(NugetConfigFilePath))
            {
                NugetConfigFileDirectoryPath = UnityPathHelper.AbsoluteUnityPackagesNugetPath;
            }
            else
            {
                NugetConfigFilePath = Path.Combine(UnityPathHelper.AbsoluteAssetsPath, NugetConfigFile.FileName);
                NugetConfigFileDirectoryPath = UnityPathHelper.AbsoluteAssetsPath;
            }
        }

        /// <summary>
        ///     Gets the path to the nuget.config file.
        /// </summary>
        /// <remarks>
        ///     <see cref="NugetConfigFile" />.
        /// </remarks>
        [NotNull]
        public static string NugetConfigFilePath { get; private set; }

        /// <summary>
        ///     Gets the loaded NuGet.config file that holds the settings for NuGet.
        /// </summary>
        [NotNull]
        public static NugetConfigFile NugetConfigFile
        {
            get
            {
                if (nugetConfigFile is null)
                {
                    LoadNugetConfigFile();
                }

                Debug.Assert(nugetConfigFile != null, nameof(nugetConfigFile) + " != null");
                return nugetConfigFile;
            }
        }

        /// <summary>
        ///     Gets the path to the directory containing the NuGet.config file.
        /// </summary>
        [NotNull]
        internal static string NugetConfigFileDirectoryPath { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether verbose logging is enabled.
        ///     This can be set in the NuGet.config file.
        ///     But this will not load the NuGet.config file to prevent endless loops wen we log while we load the <c>NuGet.config</c> file.
        /// </summary>
        internal static bool IsVerboseLoggingEnabled => nugetConfigFile?.Verbose ?? false;

        /// <summary>
        ///     Gets the value indicating whether .NET Standard is preferred over .NET Framework as the TargetFramework.
        /// </summary>
        internal static bool PreferNetStandardOverNetFramework => nugetConfigFile?.PreferNetStandardOverNetFramework ?? false;

        /// <summary>
        ///     Gets the <see cref="INugetPackageSource" /> to use.
        /// </summary>
        [NotNull]
        private static INugetPackageSource ActivePackageSource
        {
            get
            {
                if (activePackageSource is null)
                {
                    LoadNugetConfigFile();
                }

                Debug.Assert(activePackageSource != null, nameof(activePackageSource) + " != null");
                return activePackageSource;
            }
        }

        /// <summary>
        ///     Loads the NuGet.config file.
        /// </summary>
        public static void LoadNugetConfigFile()
        {
            if (File.Exists(NugetConfigFilePath))
            {
                nugetConfigFile = NugetConfigFile.Load(NugetConfigFilePath);
            }
            else
            {
                Debug.LogFormat("No NuGet.config file found. Creating default at {0}", NugetConfigFilePath);

                nugetConfigFile = NugetConfigFile.CreateDefaultFile(NugetConfigFilePath);
            }

            // parse any command line arguments
            var packageSourcesFromCommandLine = new List<INugetPackageSource>();
            var readingSources = false;
            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (readingSources)
                {
                    if (arg.StartsWith("-", StringComparison.Ordinal))
                    {
                        readingSources = false;
                    }
                    else
                    {
                        var source = NugetPackageSourceCreator.CreatePackageSource(
                            $"CMD_LINE_SRC_{packageSourcesFromCommandLine.Count}",
                            arg,
                            null,
                            null);
                        NugetLogger.LogVerbose("Adding command line package source {0} at {1}", source.Name, arg);
                        packageSourcesFromCommandLine.Add(source);
                    }
                }

                if (arg.Equals("-Source", StringComparison.OrdinalIgnoreCase))
                {
                    // if the source is being forced, don't install packages from the cache
                    NugetConfigFile.InstallFromCache = false;
                    readingSources = true;
                }
            }

            if (packageSourcesFromCommandLine.Count == 1)
            {
                activePackageSource = packageSourcesFromCommandLine[0];
            }
            else if (packageSourcesFromCommandLine.Count > 1)
            {
                activePackageSource = new NugetPackageSourceCombined(packageSourcesFromCommandLine);
            }
            else
            {
                // if there are not command line overrides, use the NuGet.config package sources
                activePackageSource = NugetConfigFile.ActivePackageSource;
            }

            PluginRegistry.InitPlugins();
        }

        /// <summary>
        ///     Gets a list of NugetPackages from all active package source's.
        ///     This allows searching for partial IDs or even the empty string (the default) to list ALL packages.
        /// </summary>
        /// <param name="searchTerm">The search term to use to filter packages. Defaults to the empty string.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="numberToGet">The number of packages to fetch.</param>
        /// <param name="numberToSkip">The number of packages to skip before fetching.</param>
        /// <param name="cancellationToken">Token that can be used to cancel the asynchronous task.</param>
        /// <returns>The list of available packages.</returns>
        [NotNull]
        [ItemNotNull]
        public static Task<List<INugetPackage>> SearchAsync(
            [NotNull] string searchTerm = "",
            bool includePrerelease = false,
            int numberToGet = 15,
            int numberToSkip = 0,
            CancellationToken cancellationToken = default)
        {
            return ActivePackageSource.SearchAsync(searchTerm, includePrerelease, numberToGet, numberToSkip, cancellationToken);
        }

        /// <summary>
        ///     Queries all active nuget package source's with the given list of installed packages to get any updates that are available.
        /// </summary>
        /// <param name="packagesToUpdate">The list of currently installed packages for witch updates are searched.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="targetFrameworks">The specific frameworks to target?.</param>
        /// <param name="versionConstraints">The version constraints?.</param>
        /// <returns>A list of all updates available.</returns>
        [NotNull]
        [ItemNotNull]
        public static List<INugetPackage> GetUpdates(
            [NotNull] IEnumerable<INugetPackage> packagesToUpdate,
            bool includePrerelease = false,
            string targetFrameworks = "",
            string versionConstraints = "")
        {
            return ActivePackageSource.GetUpdates(packagesToUpdate, includePrerelease, targetFrameworks, versionConstraints);
        }

        /// <inheritdoc cref="INugetPackageSource.GetSpecificPackage(INugetPackageIdentifier)" />
        [CanBeNull]
        public static INugetPackage GetSpecificPackage([NotNull] INugetPackageIdentifier nugetPackageIdentifier)
        {
            return ActivePackageSource.GetSpecificPackage(nugetPackageIdentifier);
        }

        /// <summary>
        ///     Moves the Nuget.config under newPlacement and updated local properties to point to it.
        /// </summary>
        /// <param name="newInstallLocation">New placement for configs.</param>
        internal static void MoveConfig(PackageInstallLocation newInstallLocation)
        {
            NugetConfigFile.ChangeInstallLocation(newInstallLocation);
            var newConfigsPath = newInstallLocation == PackageInstallLocation.InPackagesFolder ?
                UnityPathHelper.AbsoluteUnityPackagesNugetPath :
                UnityPathHelper.AbsoluteAssetsPath;
            var newConfigFilePath = Path.Combine(newConfigsPath, NugetConfigFile.FileName);

            File.Move(NugetConfigFilePath, newConfigFilePath);
            var configMeta = NugetConfigFilePath + ".meta";
            if (File.Exists(configMeta))
            {
                File.Move(configMeta, newConfigFilePath + ".meta");
            }

            NugetConfigFilePath = newConfigFilePath;
            NugetConfigFileDirectoryPath = newConfigsPath;
        }
    }
}
