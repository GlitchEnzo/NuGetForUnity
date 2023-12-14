namespace NugetForUnity.PluginAPI.Models
{
    /// <summary>
    ///     Represents a .nuspec file used to store metadata for a NuGet package.
    /// </summary>
    public interface INuspecFile : INugetPackageIdentifier
    {
        /// <summary>
        ///     Gets or sets the Id of the package.
        /// </summary>
        new string Id { get; set; }

        /// <summary>
        ///     Gets or sets the source control branch the package is from.
        /// </summary>
        string? RepositoryBranch { get; set; }

        /// <summary>
        ///     Gets or sets the source control commit the package is from.
        /// </summary>
        string? RepositoryCommit { get; set; }

        /// <summary>
        ///     Gets or sets the type of source control software that the package's source code resides in.
        /// </summary>
        string? RepositoryType { get; set; }

        /// <summary>
        ///     Gets or sets the url for the location of the package's source code.
        /// </summary>
        string? RepositoryUrl { get; set; }

        /// <summary>
        ///     Gets or sets the title of the NuGet package.
        /// </summary>
        string? Title { get; set; }

        /// <summary>
        ///     Gets or sets the owners of the NuGet package.
        /// </summary>
        string? Owners { get; set; }

        /// <summary>
        ///     Gets or sets the URL for the location of the license of the NuGet package.
        /// </summary>
        string? LicenseUrl { get; set; }

        /// <summary>
        ///     Gets or sets the URL for the location of the project web-page of the NuGet package.
        /// </summary>
        string? ProjectUrl { get; set; }

        /// <summary>
        ///     Gets or sets the URL for the location of the icon of the NuGet package.
        /// </summary>
        string? IconUrl { get; set; }

        /// <summary>
        ///     Gets the path to a icon file. The path is relative to the root folder of the package. This is a alternative to using a URL <see cref="IconUrl" />
        ///     .
        /// </summary>
        string? Icon { get; }

        /// <summary>
        ///     Gets the full path to a icon file. This is only set if the .nuspec file contains a <see cref="Icon" />. This is a alternative to using a URL
        ///     <see cref="IconUrl" />.
        /// </summary>
        string? IconFilePath { get; }

        /// <summary>
        ///     Gets or sets a value indicating whether the license of the NuGet package needs to be accepted in order to use it.
        /// </summary>
        bool RequireLicenseAcceptance { get; set; }

        /// <summary>
        ///     Gets or sets the release notes of the NuGet package.
        /// </summary>
        string? ReleaseNotes { get; set; }

        /// <summary>
        ///     Gets or sets the copyright of the NuGet package.
        /// </summary>
        string? Copyright { get; set; }

        /// <summary>
        ///     Gets or sets the tags of the NuGet package.
        /// </summary>
        string? Tags { get; set; }

        /// <summary>
        ///     Gets or sets the description of the NuGet package.
        /// </summary>
        string? Description { get; set; }

        /// <summary>
        ///     Gets or sets the description of the NuGet package.
        /// </summary>
        string? Summary { get; set; }

        /// <summary>
        ///     Gets or sets the authors of the NuGet package.
        /// </summary>
        string Authors { get; set; }
    }
}
