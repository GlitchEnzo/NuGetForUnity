using NugetForUnity.PluginAPI.ExtensionPoints;
using NugetForUnity.PluginAPI.Models;

namespace NugetForUnity.PluginAPI
{
    /// <summary>
    ///     NugetForUnity will pass an instance of this interface to INugetPlugin.Register method that plugins can use
    ///     to register additional functionalities.
    /// </summary>
    public interface INugetPluginRegistry
    {
        /// <summary>
        ///     Gets a value indicating whether we are currently running in Unity or from CLI.
        /// </summary>
        bool IsRunningInUnity { get; }

        /// <summary>
        ///     Gets the methods that NugetForUnity provides to the plugin, like logging methods.
        /// </summary>
        INugetPluginService PluginService { get; }

        /// <summary>
        ///     Register a class that will be used to draw additional buttons for each package in NugetForUnity editor window.
        /// </summary>
        /// <param name="packageButtonsHandler">The package buttons handler to register.</param>
        void RegisterPackageButtonDrawer(IPackageButtonsHandler packageButtonsHandler);

        /// <summary>
        ///     Register a class that will be called for each file that is extracted from the nupkg that is being installed.
        /// </summary>
        /// <param name="packageInstallFileHandler">The file handler to register.</param>
        void RegisterPackageInstallFileHandler(IPackageInstallFileHandler packageInstallFileHandler);

        /// <summary>
        ///     Register a class that will be called when uninstalling some package.
        /// </summary>
        /// <param name="packageUninstallHandler">The package uninstall handler to register.</param>
        void RegisterPackageUninstallHandler(IPackageUninstallHandler packageUninstallHandler);

        /// <summary>
        ///     Register a class that will be called when installed package is found.
        /// </summary>
        /// <param name="foundInstalledPackageHandler">The found installed package handler to register.</param>
        void RegisterFoundInstalledPackageHandler(IFoundInstalledPackageHandler foundInstalledPackageHandler);
    }
}
