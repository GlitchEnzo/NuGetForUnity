using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace NugetForUnity
{
    /// <summary>
    ///     Represents a package available from NuGet.
    /// </summary>
    [Serializable]
    public class NugetPackage : NugetPackageIdentifier, IEquatable<NugetPackage>, IEqualityComparer<NugetPackage>, ISerializationCallbackReceiver
    {
        /// <summary>
        ///     Gets or sets the authors of the package.
        /// </summary>
        public string Authors;

        /// <summary>
        ///     Gets or sets the NuGet packages that this NuGet package depends on.
        /// </summary>
        public List<NugetFrameworkGroup> Dependencies = new List<NugetFrameworkGroup>();

        /// <summary>
        ///     Gets or sets the description of the NuGet package.
        /// </summary>
        public string Description;

        /// <summary>
        ///     Gets or sets the DownloadCount.
        /// </summary>
        public long DownloadCount;

        /// <summary>
        ///     Gets or sets the URL for the location of the actual (.nupkg) NuGet package.
        /// </summary>
        public string DownloadUrl;

        [SerializeField]
        private Texture2D icon;

        private Task<Texture2D> iconTask;

        /// <summary>
        ///     Gets or sets the URL for the location of the icon of the NuGet package.
        /// </summary>
        public string IconUrl;

        /// <summary>
        ///     Gets or sets the URL for the location of the license of the NuGet package.
        /// </summary>
        public string LicenseUrl;

        /// <summary>
        ///     Gets or sets the <see cref="NugetPackageSource" /> that contains this package.
        /// </summary>
        public NugetPackageSource PackageSource;

        /// <summary>
        ///     Gets or sets the URL for the location of the package's source code.
        /// </summary>
        public string ProjectUrl;

        /// <summary>
        ///     Gets or sets the release notes of the NuGet package.
        /// </summary>
        public string ReleaseNotes;

        /// <summary>
        ///     Gets or sets the source control branch the package is from.
        /// </summary>
        public string RepositoryBranch;

        /// <summary>
        ///     Gets or sets the source control commit the package is from.
        /// </summary>
        public string RepositoryCommit;

        /// <summary>
        ///     Gets or sets the type of source control software that the package's source code resides in.
        /// </summary>
        public RepositoryType RepositoryType;

        /// <summary>
        ///     Gets or sets the URL for the location of the package's source code.
        /// </summary>
        public string RepositoryUrl;

        /// <summary>
        ///     Gets or sets the summary of the NuGet package.
        /// </summary>
        public string Summary;

        /// <summary>
        ///     Gets or sets the title (not ID) of the package. This is the "friendly" name that only appears in GUIs and on web-pages.
        /// </summary>
        public string Title;

        /// <summary>
        ///     Gets the icon for the package as a task returning a <see cref="Texture2D" />.
        /// </summary>
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
        ///     Creates a new <see cref="NugetPackage" /> from the given <see cref="NuspecFile" />.
        /// </summary>
        /// <param name="nuspec">The <see cref="NuspecFile" /> to use to create the <see cref="NugetPackage" />.</param>
        /// <returns>The newly created <see cref="NugetPackage" />.</returns>
        public static NugetPackage FromNuspec(NuspecFile nuspec)
        {
            var package = new NugetPackage();

            package.Id = nuspec.Id;
            package.Version = nuspec.Version;
            package.Title = nuspec.Title;
            package.Authors = nuspec.Authors;
            package.Description = nuspec.Description;
            package.Summary = nuspec.Summary;
            package.ReleaseNotes = nuspec.ReleaseNotes;
            package.LicenseUrl = nuspec.LicenseUrl;
            package.ProjectUrl = nuspec.ProjectUrl;
            package.IconUrl = nuspec.IconUrl;
            package.RepositoryUrl = nuspec.RepositoryUrl;
            Enum.TryParse(nuspec.RepositoryType, true, out package.RepositoryType);
            package.RepositoryBranch = nuspec.RepositoryBranch;
            package.RepositoryCommit = nuspec.RepositoryCommit;

            // if there is no title, just use the ID as the title
            if (string.IsNullOrEmpty(package.Title))
            {
                package.Title = package.Id;
            }

            package.Dependencies = nuspec.Dependencies;

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
        /// <returns>The <see cref="NugetPackage" /> loaded from the .nupkg file.</returns>
        public static NugetPackage FromNupkgFile(string nupkgFilepath)
        {
            var package = FromNuspec(NuspecFile.FromNupkgFile(nupkgFilepath));
            package.DownloadUrl = nupkgFilepath;
            return package;
        }
    }
}
