using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace NugetForUnity
{
    /// <summary>
    ///     Represents a package available from NuGet.
    /// </summary>
    [Serializable]
    internal sealed class NugetPackage : NugetPackageIdentifier, INuGetPackage, IEquatable<NugetPackage>, IEqualityComparer<NugetPackage>
    {
        [SerializeField]
        private Texture2D icon;

        private Task<Texture2D> iconTask;

        [SerializeField]
        private LocalNuGetPackageSource localNuGetPackageSource;

        [SerializeField]
        private NuGetPackageSourceV2 packageSourceV2;

        /// <summary>
        ///     Initializes a new instance of the <see cref="NugetPackage" /> class.
        /// </summary>
        /// <param name="packageSourceV2">The source this package was downloaded with / provided by.</param>
        public NugetPackage(NuGetPackageSourceV2 packageSourceV2)
        {
            this.packageSourceV2 = packageSourceV2;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="NugetPackage" /> class.
        /// </summary>
        /// <param name="localNuGetPackageSource">The source this package was downloaded with / provided by.</param>
        public NugetPackage(LocalNuGetPackageSource localNuGetPackageSource)
        {
            this.localNuGetPackageSource = localNuGetPackageSource;
        }

        /// <summary>
        ///     Gets the NuGet packages that this NuGet package depends on.
        /// </summary>
        [field: SerializeField]
        public List<NugetFrameworkGroup> DependenciesV2 { get; private set; } = new List<NugetFrameworkGroup>();

        /// <summary>
        ///     Gets or sets the URL for the location of the actual (.nupkg) NuGet package.
        /// </summary>
        [field: SerializeField]
        public string DownloadUrlV2 { get; set; }

        /// <summary>
        ///     Gets or sets the URL for the location of the icon of the NuGet package.
        /// </summary>
        [field: SerializeField]
        public string IconUrl { get; set; }

        /// <summary>
        ///     Gets or sets the source control branch the package is from.
        /// </summary>
        [field: SerializeField]
        public string RepositoryBranchV2 { get; set; }

        /// <summary>
        ///     Checks to see if the two given <see cref="NugetPackage" />s are equal.
        /// </summary>
        /// <param name="x">The first <see cref="NugetPackage" /> to compare.</param>
        /// <param name="y">The second <see cref="NugetPackage" /> to compare.</param>
        /// <returns>True if the packages are equal, otherwise false.</returns>
        public bool Equals(NugetPackage x, NugetPackage y)
        {
            return x.Id == y.Id && x.Version == y.Version;
        }

        /// <summary>
        ///     Gets the hash-code for the given <see cref="NugetPackage" />.
        /// </summary>
        /// <param name="obj">The <see cref="NugetPackage" /> to generate the hash code.</param>
        /// <returns>The hash-code for the given <see cref="NugetPackage" />.</returns>
        public int GetHashCode(NugetPackage obj)
        {
            return obj.Id.GetHashCode() ^ obj.Version.GetHashCode();
        }

        /// <summary>
        ///     Checks to see if this <see cref="NugetPackage" /> is equal to the given one.
        /// </summary>
        /// <param name="other">The other <see cref="NugetPackage" /> to check equality with.</param>
        /// <returns>True if the packages are equal, otherwise false.</returns>
        public bool Equals(NugetPackage other)
        {
            return other.Id == Id && other.Version == Version;
        }

        /// <inheritdoc />
        public List<NuGetPackageVersion> Versions => null;

        /// <inheritdoc />
        [field: SerializeField]
        public List<string> Authors { get; set; }

        /// <inheritdoc />
        public Task<List<NugetFrameworkGroup>> Dependencies => Task.FromResult(DependenciesV2);

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
        public INuGetPackageSource PackageSource =>
            packageSourceV2 == null || string.IsNullOrEmpty(packageSourceV2.SavedPath) ?
                (INuGetPackageSource)localNuGetPackageSource :
                packageSourceV2;

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
                    iconTask = NuGetPackageTextureHelper.DownloadImage(IconUrl);
                }

                return iconTask;
            }
        }

        /// <inheritdoc />
        public void DownloadNupkgToFile(string outputFilePath)
        {
            PackageSource.DownloadNupkgToFile(this, outputFilePath, DownloadUrlV2);
        }

        /// <inheritdoc />
        public override void OnBeforeSerialize()
        {
            base.OnBeforeSerialize();
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

        /// <summary>
        ///     Creates a new <see cref="NugetPackage" /> from the given <see cref="NuspecFile" />.
        /// </summary>
        /// <param name="nuspec">The <see cref="NuspecFile" /> to use to create the <see cref="NugetPackage" />.</param>
        /// <param name="packageSource">The source this package was downloaded with / provided by.</param>
        /// <returns>The newly created <see cref="NugetPackage" />.</returns>
        public static NugetPackage FromNuspec(NuspecFile nuspec, LocalNuGetPackageSource packageSource)
        {
            var package = new NugetPackage(packageSource);
            return FillFromNuspec(nuspec, package);
        }

        /// <inheritdoc cref="FromNuspec(NuspecFile, LocalNuGetPackageSource)" />
        public static NugetPackage FromNuspec(NuspecFile nuspec, NuGetPackageSourceV2 packageSource)
        {
            var package = new NugetPackage(packageSource);
            return FillFromNuspec(nuspec, package);
        }

        private static NugetPackage FillFromNuspec(NuspecFile nuspec, NugetPackage package)
        {
            Enum.TryParse<RepositoryType>(nuspec.RepositoryType, true, out var repositoryType);
            package.Id = nuspec.Id;
            package.Version = nuspec.Version;
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
            package.RepositoryBranchV2 = nuspec.RepositoryBranch;
            package.RepositoryCommit = nuspec.RepositoryCommit;
            package.DependenciesV2 = nuspec.Dependencies;

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

            return package;
        }

        /// <summary>
        ///     Loads a <see cref="NugetPackage" /> from the .nupkg file at the given file-path.
        /// </summary>
        /// <param name="nupkgFilepath">The file-path to the .nupkg file to load.</param>
        /// <param name="packageSource">The source this package was downloaded with / provided by.</param>
        /// <returns>The <see cref="NugetPackage" /> loaded from the .nupkg file.</returns>
        public static NugetPackage FromNupkgFile(string nupkgFilepath, NuGetPackageSourceV2 packageSource)
        {
            var package = FromNuspec(NuspecFile.FromNupkgFile(nupkgFilepath), packageSource);
            package.DownloadUrlV2 = nupkgFilepath;
            return package;
        }

        /// <inheritdoc cref="FromNupkgFile(string, NuGetPackageSourceV2)" />
        public static NugetPackage FromNupkgFile(string nupkgFilepath, LocalNuGetPackageSource packageSource)
        {
            var package = FromNuspec(NuspecFile.FromNupkgFile(nupkgFilepath), packageSource);
            package.DownloadUrlV2 = nupkgFilepath;
            return package;
        }
    }
}
