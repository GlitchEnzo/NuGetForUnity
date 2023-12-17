using System;
using System.Collections.Generic;
using System.IO.Compression;
using NugetForUnity.Configuration;
using NugetForUnity.Helper;
using NugetForUnity.PluginAPI;
using NugetForUnity.PluginAPI.ExtensionPoints;
using NugetForUnity.PluginAPI.Models;
using UnityEditor;
using UnityEngine;

namespace NugetForUnity.PluginSupport
{
    /// <summary>
    ///     Plugin Registry loads the plugins and provides methods for calling them.
    /// </summary>
    internal class PluginRegistry : INugetPluginRegistry,
        IPackageButtonsHandler,
        IPackageInstallFileHandler,
        IPackageUninstallHandler,
        IFoundInstalledPackageHandler
    {
        private readonly List<IFoundInstalledPackageHandler> foundInstalledPackageHandlers = new List<IFoundInstalledPackageHandler>();

        private readonly List<IPackageButtonsHandler> packageButtonsHandlers = new List<IPackageButtonsHandler>();

        private readonly List<IPackageInstallFileHandler> packageInstallFileHandlers = new List<IPackageInstallFileHandler>();

        private readonly List<IPackageUninstallHandler> packageUninstallHandlers = new List<IPackageUninstallHandler>();

        /// <summary>
        ///     Gets the static instance of PluginRegistry.
        /// </summary>
        public static PluginRegistry Instance { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether we are running from Unity or from CLI version.
        /// </summary>
        public bool IsRunningInUnity { get; } = SessionState.GetString(nameof(IsRunningInUnity), "true") == "true";

        /// <inheritdoc />
        public INugetPluginService PluginService { get; } = new NugetPluginService();

        /// <summary>
        ///     Reloads appropriate plugins after some are enabled or disabled.
        /// </summary>
        public static void Reinitialize()
        {
            if (Instance?.PluginService is IDisposable disposable)
            {
                disposable.Dispose();
            }

            Instance = null;
            InitPlugins();
        }

        /// <summary>
        ///     Loads the plug-ins that are enabled in preferences and initialized Instance.
        /// </summary>
        public static void InitPlugins()
        {
            if (Instance != null)
            {
                return;
            }

            Instance = new PluginRegistry();
            var loadedPlugins = new List<string>();
            var failedPlugins = new List<string>();
            foreach (var plugin in ConfigurationManager.NugetConfigFile.EnabledPlugins)
            {
                try
                {
                    var assembly = AssemblyLoader.Load(plugin);
                    var pluginLoaded = false;
                    foreach (var type in assembly.GetTypes())
                    {
                        if (typeof(INugetPlugin).IsAssignableFrom(type))
                        {
                            var pluginInstance = (INugetPlugin)Activator.CreateInstance(type);
                            pluginInstance.Register(Instance);
                            loadedPlugins.Add(plugin.Name);
                            pluginLoaded = true;
                            break;
                        }
                    }

                    if (!pluginLoaded)
                    {
                        failedPlugins.Add($"{plugin.Name}: No class implementing INugetPlugin found");
                    }
                }
                catch (Exception e)
                {
                    failedPlugins.Add($"{plugin.Name}: {e.GetType().Name} {e.Message}");
                }
            }

            if (loadedPlugins.Count > 0)
            {
                Debug.Log($"Loaded plugins: {string.Join(", ", loadedPlugins)}");
            }

            if (failedPlugins.Count > 0)
            {
                Debug.LogError($"Failed to load plugins: {string.Join("\n", failedPlugins)}");
            }
        }

        /// <inheritdoc />
        public void RegisterPackageButtonDrawer(IPackageButtonsHandler packageButtonsHandler)
        {
            packageButtonsHandlers.Add(packageButtonsHandler);
        }

        /// <inheritdoc />
        public void RegisterPackageInstallFileHandler(IPackageInstallFileHandler packageInstallFileHandler)
        {
            packageInstallFileHandlers.Add(packageInstallFileHandler);
        }

        /// <inheritdoc />
        public void RegisterPackageUninstallHandler(IPackageUninstallHandler packageUninstallHandler)
        {
            packageUninstallHandlers.Add(packageUninstallHandler);
        }

        /// <inheritdoc />
        public void RegisterFoundInstalledPackageHandler(IFoundInstalledPackageHandler foundInstalledPackageHandler)
        {
            foundInstalledPackageHandlers.Add(foundInstalledPackageHandler);
        }

        /// <inheritdoc />
        public void DrawButtons(INugetPackage package, INugetPackage installedPackage, bool existsInUnity)
        {
            foreach (var packageButtonsHandler in packageButtonsHandlers)
            {
                try
                {
                    packageButtonsHandler.DrawButtons(package, installedPackage, existsInUnity);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception while executing DrawButtons plugin handler {e}");
                }
            }
        }

        /// <inheritdoc />
        public bool HandleFileExtraction(INugetPackage package, ZipArchiveEntry entry, string extractDirectory)
        {
            foreach (var packageInstallFileHandler in packageInstallFileHandlers)
            {
                try
                {
                    if (packageInstallFileHandler.HandleFileExtraction(package, entry, extractDirectory))
                    {
                        return true;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception while executing file extraction plugin handler {e}");
                }
            }

            return false;
        }

        /// <inheritdoc />
        public void HandleUninstall(INugetPackage package, PackageUninstallReason uninstallReason)
        {
            foreach (var uninstallHandler in packageUninstallHandlers)
            {
                try
                {
                    uninstallHandler.HandleUninstall(package, uninstallReason);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception while executing Uninstall plugin handler {e}");
                }
            }
        }

        /// <inheritdoc />
        public void HandleUninstalledAll()
        {
            foreach (var uninstallHandler in packageUninstallHandlers)
            {
                try
                {
                    uninstallHandler.HandleUninstalledAll();
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception while executing UninstallAll plugin handler {e}");
                }
            }
        }

        /// <inheritdoc />
        public void ProcessInstalledPackage(INugetPackage installedPackage)
        {
            foreach (var foundInstalledPackageHandler in foundInstalledPackageHandlers)
            {
                try
                {
                    foundInstalledPackageHandler.ProcessInstalledPackage(installedPackage);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception while executing ProcessInstalledPackage plugin handler {e}");
                }
            }
        }
    }
}
