#pragma warning disable SA1512,SA1124 // Single-line comments should not be followed by blank line

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NugetForUnity.Helper;
using NugetForUnity.PackageSource;
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
    ///     Represents a NuGet package that was downloaded from a NuGet server using NuGet API v3.
    /// </summary>
    [Serializable]
    internal sealed class NugetPackageV3 : NugetPackageIdentifier, INugetPackage, ISerializationCallbackReceiver
    {
        [ItemNotNull]
        [CanBeNull]
        [NonSerialized]
        private IReadOnlyList<INugetPackageIdentifier> currentFrameworkDependencies;

        [SerializeField]
        private List<NugetFrameworkGroup> dependencies = new List<NugetFrameworkGroup>();

        [SerializeField]
        private bool dependenciesFetched;

        [CanBeNull]
        [NonSerialized]
        private Task<List<NugetFrameworkGroup>> dependenciesTask;

        [CanBeNull]
        [SerializeField]
        [SuppressMessage("Usage", "CA2235:Mark all non-serializable fields", Justification = "It is a Unity object that can be serialized.")]
        private Texture2D icon;

        [ItemCanBeNull]
        [CanBeNull]
        [NonSerialized]
        private Task<Texture2D> iconTask;

        [CanBeNull]
        [SerializeField]
        private string iconUrl;

        [SerializeField]
        [NotNull]
        private NugetPackageSourceV3 packageSource;

        /// <summary>
        ///     Initializes a new instance of the <see cref="NugetPackageV3" /> class.
        /// </summary>
        /// <param name="id">The id of the package.</param>
        /// <param name="version">The version.</param>
        /// <param name="authors">The authors.</param>
        /// <param name="description">The description.</param>
        /// <param name="totalDownloads">The total number of downloads.</param>
        /// <param name="licenseUrl">The license URL.</param>
        /// <param name="packageSource">The source used to receive the package.</param>
        /// <param name="projectUrl">The URL of the project.</param>
        /// <param name="summary">The short summary.</param>
        /// <param name="title">The human readable title.</param>
        /// <param name="iconUrl">The URL where the icon can be downloaded.</param>
        /// <param name="versions">All available versions.</param>
        public NugetPackageV3(
            [NotNull] string id,
            [NotNull] string version,
            [NotNull] List<string> authors,
            [CanBeNull] string description,
            long totalDownloads,
            [CanBeNull] string licenseUrl,
            [NotNull] NugetPackageSourceV3 packageSource,
            [CanBeNull] string projectUrl,
            [CanBeNull] string summary,
            [CanBeNull] string title,
            [CanBeNull] string iconUrl,
            List<NugetPackageVersion> versions)
            : base(id, version)
        {
            Authors = authors;
            Description = description;
            TotalDownloads = totalDownloads;
            LicenseUrl = licenseUrl;
            this.packageSource = packageSource;
            ProjectUrl = projectUrl;
            Summary = summary;
            Title = title;
            this.iconUrl = iconUrl;
            Versions = versions;
        }

        /// <inheritdoc />
        [field: SerializeField]
        public List<NugetPackageVersion> Versions { get; private set; }

        /// <inheritdoc />
        [field: SerializeField]
        public List<string> Authors { get; private set; }

        /// <inheritdoc />
        public List<NugetFrameworkGroup> Dependencies
        {
            get
            {
                if (dependenciesFetched)
                {
                    return dependencies;
                }

                return Task.Run(GetDependenciesCoreAsync).GetAwaiter().GetResult();
            }

            set
            {
                dependencies = value;
                dependenciesFetched = true;
            }
        }

        /// <inheritdoc />
        [field: SerializeField]
        public string Description { get; private set; }

        /// <inheritdoc />
        [field: SerializeField]
        public long TotalDownloads { get; private set; }

        /// <inheritdoc />
        [field: SerializeField]
        public string LicenseUrl { get; private set; }

        /// <inheritdoc />
        public INugetPackageSource PackageSource => packageSource;

        /// <inheritdoc />
        [field: SerializeField]
        public string ProjectUrl { get; private set; }

        /// <inheritdoc />
        [field: SerializeField]
        public string Summary { get; private set; }

        /// <inheritdoc />
        [field: SerializeField]
        public string Title { get; private set; }

        /// <inheritdoc />
        public Task<Texture2D> IconTask
        {
            get
            {
                if (iconTask != null)
                {
                    return iconTask;
                }

                if (!string.IsNullOrEmpty(iconUrl))
                {
                    iconTask = NugetPackageTextureHelper.DownloadImageAsync(iconUrl);
                }

                return iconTask;
            }
        }

        /// <inheritdoc />
        public string ReleaseNotes => string.Empty;

        /// <inheritdoc />
        public RepositoryType RepositoryType => RepositoryType.NotSpecified;

        /// <inheritdoc />
        public string RepositoryUrl => string.Empty;

        /// <inheritdoc />
        public string RepositoryCommit => string.Empty;

        /// <inheritdoc />
        public IReadOnlyList<INugetPackageIdentifier> CurrentFrameworkDependencies
        {
            get
            {
                if (currentFrameworkDependencies == null)
                {
                    currentFrameworkDependencies = TargetFrameworkResolver.GetBestDependencyFrameworkGroupForCurrentSettings(
                            Dependencies,
                            InstalledPackagesManager.GetPackageConfigurationById(Id)?.TargetFramework)
                        .Dependencies;
                }

                return currentFrameworkDependencies;
            }
        }

        /// <summary>
        ///     Gets or sets the URL for the location of the actual (.nupkg) NuGet package.
        /// </summary>
        [CanBeNull]
        [field: SerializeField]
        public string DownloadUrl { get; set; }

        /// <inheritdoc />
        IReadOnlyList<PluginAPI.Models.INugetPackageIdentifier> PluginAPI.Models.INugetPackage.CurrentFrameworkDependencies =>
            CurrentFrameworkDependencies;

        /// <inheritdoc />
        public Task<List<NugetFrameworkGroup>> GetDependenciesAsync()
        {
            if (dependenciesFetched)
            {
                return Task.FromResult(dependencies);
            }

            if (dependenciesTask != null)
            {
                return dependenciesTask;
            }

            dependenciesTask = GetDependenciesCoreAsync();

            return dependenciesTask;
        }

        /// <inheritdoc />
        public void DownloadNupkgToFile(string outputFilePath)
        {
            packageSource.DownloadNupkgToFile(this, outputFilePath, DownloadUrl);
        }

        /// <inheritdoc />
        public void OnBeforeSerialize()
        {
            if (iconTask != null)
            {
                icon = iconTask.IsCompleted ? iconTask.Result : null;
            }
        }

        /// <inheritdoc />
        public void OnAfterDeserialize()
        {
            if (icon != null)
            {
                iconTask = Task.FromResult(icon);
            }
        }

        [NotNull]
        [ItemNotNull]
        private async Task<List<NugetFrameworkGroup>> GetDependenciesCoreAsync()
        {
            NugetLogger.LogVerbose("Fetching dependencies for {0}", this);
            dependencies = await packageSource.GetPackageDetailsAsync(this);
            NugetLogger.LogVerbose("Fetched dependencies for {0}", this);
            dependenciesFetched = true;
            return dependencies;
        }
    }
}
