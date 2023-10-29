using NugetForUnity.PluginAPI.Models;

namespace NugetForUnity.PluginAPI.ExtensionPoints
{
    /// <summary>
    ///     Implement this interface to add additional handling when nupkg is being uninstalled.
    /// </summary>
    public interface IPackageUninstallHandler
    {
        /// <summary>
        ///     This method will be called for each package being uninstalled. Note that uninstall is also done for old version
        ///     when package is being updated.
        /// </summary>
        /// <param name="package">The package being uninstalled.</param>
        /// <param name="uninstallReason">The reason uninstall is being called.</param>
        void HandleUninstall(INugetPackage package, PackageUninstallReason uninstallReason);

        /// <summary>
        ///     This method will be called when all packages have been uninstalled using uninstall all method.
        /// </summary>
        void HandleUninstalledAll();
    }
}
