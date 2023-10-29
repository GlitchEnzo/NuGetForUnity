using NugetForUnity.PluginAPI.Models;

namespace NugetForUnity.PluginAPI.ExtensionPoints
{
    /// <summary>
    ///     Implement this interface to add additional buttons for each package in NugetForUnity window.
    /// </summary>
    public interface IPackageButtonsHandler
    {
        /// <summary>
        ///     This method will be called for each package that is rendered in NugetForUnity window.
        /// </summary>
        /// <param name="package">Package being renderer, either online package or installed package.</param>
        /// <param name="installedPackage">If package is installed this represents the installed version, otherwise it is null.</param>
        /// <param name="existsInUnity">True if package installation should be disabled because it is already included in Unity.</param>
        void DrawButtons(INugetPackage package, INugetPackage? installedPackage, bool existsInUnity);
    }
}
