using System.Collections.Generic;

namespace NugetForUnity.PluginAPI.Models
{
    /// <summary>
    ///     Represents a NuGet package.
    /// </summary>
    public interface INugetPackage : INugetPackageIdentifier
    {
        /// <summary>
        ///     Gets the title (not ID) of the package. This is the "friendly" name that only appears in GUIs and on web-pages.
        /// </summary>
        string? Title { get; }

        /// <summary>
        ///     Gets the URL for the location of the package's source code.
        /// </summary>
        string? ProjectUrl { get; }

        /// <summary>
        ///     Gets the list of dependencies for the framework that best matches what is available in Unity.
        /// </summary>
        /// <returns>List of dependencies.</returns>
        IReadOnlyList<INugetPackageIdentifier> CurrentFrameworkDependencies { get; }
    }
}
