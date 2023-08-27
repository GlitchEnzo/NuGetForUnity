using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NugetForUnity.Helper;
using NugetForUnity.PackageSource;
using UnityEngine;

namespace NugetForUnity.Models
{
    /// <summary>
    ///     Represents a NuGet package that was downloaded from a NuGet server using NuGet API v3.
    /// </summary>
    [Serializable]
    internal sealed class NugetPackageV3 : NugetPackageIdentifier, INugetPackage, ISerializationCallbackReceiver
    {
        [SerializeField]
        private List<NugetFrameworkGroup> dependencies;

        private bool dependenciesFetched;

        private Task<List<NugetFrameworkGroup>> dependenciesTask;

        [SerializeField]
        private Texture2D icon;

        private Task<Texture2D> iconTask;

        [SerializeField]
        private string iconUrl;

        [SerializeField]
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
            string id,
            string version,
            List<string> authors,
            string description,
            long totalDownloads,
            string licenseUrl,
            NugetPackageSourceV3 packageSource,
            string projectUrl,
            string summary,
            string title,
            string iconUrl,
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

                return Task.Run(() => GetDependenciesCoreAsync()).GetAwaiter().GetResult();
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
                    iconTask = NugetPackageTextureHelper.DownloadImage(iconUrl);
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
            packageSource.DownloadNupkgToFile(this, outputFilePath, null);
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

        private async Task<List<NugetFrameworkGroup>> GetDependenciesCoreAsync()
        {
            NugetLogger.LogVerbose("Fetching dependencies for {0}", this);
            dependencies = await packageSource.GetPackageDetails(this);
            NugetLogger.LogVerbose("Fetched dependencies for {0}", this);
            dependenciesFetched = true;
            return dependencies;
        }
    }
}
