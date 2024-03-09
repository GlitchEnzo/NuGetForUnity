#pragma warning disable SA1512,SA1124 // Single-line comments should not be followed by blank line

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    ///     Base class for NuGet packages containing information available from API v2.
    /// </summary>
    [Serializable]
    internal abstract class NugetPackageV2Base : NugetPackageIdentifier, INugetPackage, ISerializationCallbackReceiver
    {
        [ItemNotNull]
        [CanBeNull]
        [NonSerialized]
        private List<INugetPackageIdentifier> currentFrameworkDependencies;

        [CanBeNull]
        [SerializeField]
        [SuppressMessage("Usage", "CA2235:Mark all non-serializable fields", Justification = "It is a Unity object that can be serialized.")]
        private Texture2D icon;

        [ItemCanBeNull]
        [CanBeNull]
        [NonSerialized]
        private Task<Texture2D> iconTask;

        /// <summary>
        ///     Gets or sets the URL for the location of the actual (.nupkg) NuGet package.
        /// </summary>
        [CanBeNull]
        [field: SerializeField]
        public string DownloadUrl { get; set; }

        /// <summary>
        ///     Gets or sets the URL for the location of the icon of the NuGet package.
        /// </summary>
        [CanBeNull]
        [field: SerializeField]
        public string IconUrl { get; set; }

        /// <summary>
        ///     Gets or sets the source control branch the package is from.
        /// </summary>
        [CanBeNull]
        [field: SerializeField]
        public string RepositoryBranch { get; set; }

        /// <inheritdoc />
        [field: SerializeField]
        public List<NugetFrameworkGroup> Dependencies { get; private set; } = new List<NugetFrameworkGroup>();

        /// <inheritdoc />
        public abstract List<NugetPackageVersion> Versions { get; }

        /// <inheritdoc />
        [field: SerializeField]
        public List<string> Authors { get; set; } = new List<string>();

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

        /// <inheritdoc cref="INugetPackage.ProjectUrl" />
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
                    iconTask = NugetPackageTextureHelper.DownloadImageAsync(IconUrl);
                }

                return iconTask;
            }
        }

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

        /// <inheritdoc />
        IReadOnlyList<PluginAPI.Models.INugetPackageIdentifier> PluginAPI.Models.INugetPackage.CurrentFrameworkDependencies =>
            CurrentFrameworkDependencies;

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
        protected static void FillFromNuspec([NotNull] NuspecFile nuspec, [NotNull] NugetPackageV2Base package)
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
