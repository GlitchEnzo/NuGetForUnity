namespace NugetForUnity.PluginAPI.Models
{
    /// <summary>
    ///     Interface for a versioned NuGet package.
    /// </summary>
    public interface INugetPackageIdentifier
    {
        /// <summary>
        ///     Gets the Id of the package.
        /// </summary>
        string Id { get; }

        /// <summary>
        ///     Gets or sets the normalized version number of the NuGet package.
        ///     This is the normalized version number without build-metadata e.g. <b>1.0.0+b3a8</b> is normalized to <b>1.0.0</b>.
        /// </summary>
        string Version { get; set; }

        /// <summary>
        ///     Returns the folder path where this package is or will be installed.
        /// </summary>
        /// <param name="prefix">
        ///     In case you need to manipulate the folder to a bit different name you can provide
        ///     the prefix you want to add to folder name here.
        /// </param>
        /// <returns>
        ///     Folder path where this package is or will be installed with an optional prefix to
        ///     final path segment.
        /// </returns>
        string GetPackageInstallPath(string prefix = "");
    }
}
