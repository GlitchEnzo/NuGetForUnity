using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NugetForUnity.Helper;
using NugetForUnity.PackageSource;
using UnityEngine;

namespace NugetForUnity.Models
{
    /// <summary>
    ///     Base class for NuGet packages containing information available from API v2.
    /// </summary>
    [Serializable]
    internal abstract class NugetPackageV2Base : NugetPackageIdentifier, INugetPackage, ISerializationCallbackReceiver
    {
        [SerializeField]
        private Texture2D icon;

        private Task<Texture2D> iconTask;

        /// <summary>
        ///     Gets or sets the URL for the location of the actual (.nupkg) NuGet package.
        /// </summary>
        [field: SerializeField]
        public string DownloadUrl { get; set; }

        /// <summary>
        ///     Gets or sets the URL for the location of the icon of the NuGet package.
        /// </summary>
        [field: SerializeField]
        public string IconUrl { get; set; }

        /// <summary>
        ///     Gets or sets the source control branch the package is from.
        /// </summary>
        [field: SerializeField]
        public string RepositoryBranch { get; set; }

        /// <inheritdoc />
        [field: SerializeField]
        public List<NugetFrameworkGroup> Dependencies { get; private set; } = new List<NugetFrameworkGroup>();

        /// <inheritdoc />
        public abstract List<NugetPackageVersion> Versions { get; }

        /// <inheritdoc />
        [field: SerializeField]
        public List<string> Authors { get; set; }

        /// <inheritdoc />
        [field: SerializeField]
        public string Description { get; set; }

        /// <inheritdoc />
        [field: SerializeField]
        public long TotalDownloads { get; set; }

        /// <inheritdoc />
        [field: SerializeField]
        public string LicenseUrl { get; set; }

        /// <inheritdoc />
        public abstract INugetPackageSource PackageSource { get; }

        /// <inheritdoc />
        [field: SerializeField]
        public string ProjectUrl { get; set; }

        /// <inheritdoc />
        [field: SerializeField]
        public string ReleaseNotes { get; set; }

        /// <inheritdoc />
        [field: SerializeField]
        public string RepositoryCommit { get; set; }

        /// <inheritdoc />
        [field: SerializeField]
        public RepositoryType RepositoryType { get; set; }

        /// <inheritdoc />
        [field: SerializeField]
        public string RepositoryUrl { get; set; }

        /// <inheritdoc />
        [field: SerializeField]
        public string Summary { get; set; }

        /// <inheritdoc />
        [field: SerializeField]
        public string Title { get; set; }

        /// <inheritdoc />
        public Task<Texture2D> IconTask
        {
            get
            {
                if (iconTask != null)
                {
                    return iconTask;
                }

                if (!string.IsNullOrEmpty(IconUrl))
                {
                    iconTask = NugetPackageTextureHelper.DownloadImage(IconUrl);
                }

                return iconTask;
            }
        }

        /// <inheritdoc />
        public Task<List<NugetFrameworkGroup>> GetDependenciesAsync()
        {
            return Task.FromResult(Dependencies);
        }

        /// <inheritdoc />
        public void DownloadNupkgToFile(string outputFilePath)
        {
            PackageSource.DownloadNupkgToFile(this, outputFilePath, DownloadUrl);
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

        /// <summary>
        ///     Fills the <see cref="NugetPackageV2Base" /> with the information from the <see cref="NuspecFile" />.
        /// </summary>
        /// <param name="nuspec">The information form the <see cref="NuspecFile" />.</param>
        /// <param name="package">The package to fill with the data from <paramref name="nuspec" />.</param>
        protected static void FillFromNuspec(NuspecFile nuspec, NugetPackageV2Base package)
        {
            Enum.TryParse<RepositoryType>(nuspec.RepositoryType, true, out var repositoryType);
            package.Id = nuspec.Id;
            package.PackageVersion = nuspec.PackageVersion;
            package.Versions.Add(package.PackageVersion);
            package.Title = nuspec.Title;
            package.Authors = nuspec.Authors.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            package.Description = nuspec.Description;
            package.Summary = nuspec.Summary;
            package.ReleaseNotes = nuspec.ReleaseNotes;
            package.LicenseUrl = nuspec.LicenseUrl;
            package.ProjectUrl = nuspec.ProjectUrl;
            package.IconUrl = nuspec.IconUrl;
            package.RepositoryUrl = nuspec.RepositoryUrl;
            package.RepositoryType = repositoryType;
            package.RepositoryBranch = nuspec.RepositoryBranch;
            package.RepositoryCommit = nuspec.RepositoryCommit;
            package.Dependencies = nuspec.Dependencies;

            // if there is no title, just use the ID as the title
            if (string.IsNullOrEmpty(package.Title))
            {
                package.Title = package.Id;
            }

            // handle local icon files, preferred if the file exists.
            if (!string.IsNullOrEmpty(nuspec.IconFilePath) && (string.IsNullOrEmpty(nuspec.IconUrl) || File.Exists(nuspec.IconFilePath)))
            {
                package.IconUrl = $"file:///{nuspec.IconFilePath}";
            }
        }
    }
}
