using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace NugetForUnity
{
    /// <summary>
    ///     Represents a NuGet package that was downloaded from a NuGet server using NuGet API v3.
    /// </summary>
    [Serializable]
    internal sealed class NuGetPackageV3 : NugetPackageIdentifier, INuGetPackage
    {
        private List<NugetFrameworkGroup> dependencies;

        [SerializeField]
        private Texture2D icon;

        private Task<Texture2D> iconTask;

        [SerializeField]
        private string iconUrl;

        [SerializeField]
        private NuGetPackageSourceV3 packageSource;

        /// <summary>
        ///     Initializes a new instance of the <see cref="NuGetPackageV3" /> class.
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
        /// <param name="title">The human readably title.</param>
        /// <param name="iconUrl">The URL where the icon can be downloaded.</param>
        /// <param name="versions">All available versions.</param>
        public NuGetPackageV3(
            string id,
            string version,
            List<string> authors,
            string description,
            long totalDownloads,
            string licenseUrl,
            NuGetPackageSourceV3 packageSource,
            string projectUrl,
            string summary,
            string title,
            string iconUrl,
            List<NuGetPackageVersion> versions)
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
        public List<NuGetPackageVersion> Versions { get; private set; }

        /// <inheritdoc />
        [field: SerializeField]
        public List<string> Authors { get; private set; }

        /// <inheritdoc />
        public Task<List<NugetFrameworkGroup>> Dependencies => dependencies != null ? Task.FromResult(dependencies) : GetDependenciesAsync();

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
        public INuGetPackageSource PackageSource => packageSource;

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
                    iconTask = NuGetPackageTextureHelper.DownloadImage(iconUrl);
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
        public void DownloadNupkgToFile(string outputFilePath)
        {
            packageSource.DownloadNupkgToFile(this, outputFilePath, null);
        }

        /// <inheritdoc />
        public override void OnBeforeSerialize()
        {
            if (iconTask != null)
            {
                icon = iconTask.IsCompleted ? iconTask.Result : null;
            }

            base.OnBeforeSerialize();
        }

        /// <inheritdoc />
        public override void OnAfterDeserialize()
        {
            if (icon != null)
            {
                iconTask = Task.FromResult(icon);
            }

            base.OnAfterDeserialize();
        }

        private async Task<List<NugetFrameworkGroup>> GetDependenciesAsync()
        {
            dependencies = await packageSource.GetPackageDetails(this);
            return dependencies;
        }
    }
}
