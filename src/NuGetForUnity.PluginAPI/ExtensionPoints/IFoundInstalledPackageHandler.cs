using NugetForUnity.PluginAPI.Models;

namespace NugetForUnity.PluginAPI.ExtensionPoints
{
    /// <summary>
    ///     Implement this interface to add additional handling for each found installed package.
    /// </summary>
    public interface IFoundInstalledPackageHandler
    {
        /// <summary>
        ///     This will be called for each found installed package in the project.
        /// </summary>
        /// <param name="installedPackage">The installedPackage created from found nuspec file.</param>
        void ProcessInstalledPackage(INugetPackage installedPackage);
    }
}
