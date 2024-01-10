using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NugetForUnity.Models;
using NugetForUnity.PluginAPI;
using NugetForUnity.PluginSupport;
using UnityEditor;
using UnityEngine;

namespace NugetForUnity
{
    /// <summary>
    ///     Uninstalls NuGet packages from the Unity project. Opposite of <see cref="NugetPackageInstaller" />.
    /// </summary>
    public static class NugetPackageUninstaller
    {
        /// <summary>
        ///     "Uninstalls" the given package by simply deleting its folder.
        /// </summary>
        /// <param name="package">The NugetPackage to uninstall.</param>
        /// <param name="uninstallReason">The reason uninstall is being called.</param>
        /// <param name="refreshAssets">True to force Unity to refresh its Assets folder.  False to temporarily ignore the change.  Defaults to true.</param>
        public static void Uninstall([NotNull] INugetPackageIdentifier package, PackageUninstallReason uninstallReason, bool refreshAssets = true)
        {
            // Checking for pre-imported packages also ensures that the pre-imported package list is up-to-date before we uninstall packages.
            // Without this the pre-imported package list can contain the package as we delete the .dll before we call 'AssetDatabase.Refresh()'.
            if (UnityPreImportedLibraryResolver.IsAlreadyImportedInEngine(package.Id, false))
            {
                Debug.LogWarning($"Uninstalling {package} makes no sense because it is a package that is 'pre-imported' by Unity.");
            }

            NugetLogger.LogVerbose("Uninstalling: {0} {1}", package.Id, package.Version);

            var foundPackage = package as INugetPackage ?? PackageCacheManager.GetPackageFromCacheOrSource(package);

            if (foundPackage is null)
            {
                return;
            }

            PluginRegistry.Instance.HandleUninstall(foundPackage, uninstallReason);

            InstalledPackagesManager.RemovePackage(foundPackage);
            PackageContentManager.DeletePackageContentPackage(foundPackage);

            // Since uninstall all will remove all packages we don't have to handle dependencies here.
            if (uninstallReason != PackageUninstallReason.UninstallAll)
            {
                // uninstall all non manually installed dependencies that are not a dependency of another installed package
                var frameworkDependencies = foundPackage.CurrentFrameworkDependencies;
                foreach (var dependency in frameworkDependencies)
                {
                    if (InstalledPackagesManager.GetManuallyInstalledFlagFromConfiguration(dependency.Id))
                    {
                        continue;
                    }

                    var hasMoreParents = InstalledPackagesManager.InstalledPackages.SelectMany(
                            installedPackage => installedPackage.CurrentFrameworkDependencies)
                        .Any(dep => dep.Id == dependency.Id);

                    if (!hasMoreParents)
                    {
                        Uninstall(dependency, uninstallReason, false);
                    }
                }
            }

            if (refreshAssets)
            {
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        ///     Uninstalls all given installed packages.
        /// </summary>
        /// <param name="packagesToUninstall">The list of packages to uninstall.</param>
        public static void UninstallAll([NotNull] [ItemNotNull] List<INugetPackage> packagesToUninstall)
        {
            foreach (var package in packagesToUninstall)
            {
                Uninstall(package, PackageUninstallReason.UninstallAll, false);
            }

            PluginRegistry.Instance.HandleUninstalledAll();

            AssetDatabase.Refresh();
        }

        /// <summary>
        ///     Uninstalls all of the currently installed packages.
        /// </summary>
        public static void UninstallAll()
        {
            UninstallAll(InstalledPackagesManager.InstalledPackages.ToList());
        }
    }
}
