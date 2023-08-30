using System.Collections.Generic;
using System.Linq;
using NugetForUnity.Models;
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
        /// <param name="refreshAssets">True to force Unity to refresh its Assets folder.  False to temporarily ignore the change.  Defaults to true.</param>
        public static void Uninstall(INugetPackageIdentifier package, bool refreshAssets = true)
        {
            // Checking for pre-imported packages also ensures that the pre-imported package list is up-to-date before we uninstall packages.
            // Without this the pre-imported package list can contain the package as we delete the .dll before we call 'AssetDatabase.Refresh()'.
            if (UnityPreImportedLibraryResolver.IsAlreadyImportedInEngine(package, false))
            {
                Debug.LogWarning($"Uninstalling {package} makes no sense because it is a package that is 'pre-imported' by Unity.");
            }

            NugetLogger.LogVerbose("Uninstalling: {0} {1}", package.Id, package.Version);

            var foundPackage = package as INugetPackage ?? PackageCacheManager.GetPackageFromCacheOrSource(package);

            InstalledPackagesManager.RemovePackage(foundPackage);
            PackageContentManager.DeletePackageContentPackage(foundPackage);

            // uninstall all non manually installed dependencies that are not a dependency of another installed package
            var frameworkGroup = TargetFrameworkResolver.GetBestDependencyFrameworkGroupForCurrentSettings(foundPackage);
            foreach (var dependency in frameworkGroup.Dependencies)
            {
                if (InstalledPackagesManager.GetManuallyInstalledFlagFromConfiguration(dependency.Id))
                {
                    continue;
                }

                var hasMoreParents = InstalledPackagesManager.InstalledPackages
                    .Select(TargetFrameworkResolver.GetBestDependencyFrameworkGroupForCurrentSettings)
                    .Any(frameworkGrp => frameworkGrp.Dependencies.Any(dep => dep.Id == dependency.Id));

                if (!hasMoreParents)
                {
                    Uninstall(dependency, false);
                }
            }

            if (refreshAssets)
            {
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        ///     Uninstalls all of the currently installed packages.
        /// </summary>
        /// <param name="packagesToUninstall">The list of packages to uninstall.</param>
        internal static void UninstallAll(List<INugetPackage> packagesToUninstall)
        {
            foreach (var package in packagesToUninstall)
            {
                Uninstall(package, false);
            }

            AssetDatabase.Refresh();
        }
    }
}
