using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NugetForUnity.Models;
using NugetForUnity.PluginAPI;
using UnityEditor;
using UnityEngine;

namespace NugetForUnity
{
    /// <summary>
    ///     Handles the updating of NuGet packages.
    /// </summary>
    public static class NugetPackageUpdater
    {
        /// <summary>
        ///     Updates a package by uninstalling the currently installed version and installing the "new" version.
        /// </summary>
        /// <param name="currentVersion">The current package to uninstall.</param>
        /// <param name="newVersion">The package to install.</param>
        /// <param name="refreshAssets">True to refresh the assets inside Unity. False to ignore them (for now). Defaults to true.</param>
        /// <param name="uninstallReason">The reason uninstall is being called.</param>
        /// <returns>True if the package was successfully updated. False otherwise.</returns>
        public static bool Update(
            [NotNull] INugetPackageIdentifier currentVersion,
            [NotNull] INugetPackage newVersion,
            bool refreshAssets = true,
            PackageUninstallReason uninstallReason = PackageUninstallReason.IndividualUpdate)
        {
            NugetLogger.LogVerbose("Updating {0} {1} to {2}", currentVersion.Id, currentVersion.Version, newVersion.Version);
            NugetPackageUninstaller.Uninstall(currentVersion, uninstallReason, false);
            newVersion.IsManuallyInstalled = newVersion.IsManuallyInstalled || currentVersion.IsManuallyInstalled;
            return NugetPackageInstaller.InstallIdentifier(newVersion, refreshAssets);
        }

        /// <summary>
        ///     Installs all of the given updates, and Uninstalls the corresponding package that is already installed.
        /// </summary>
        /// <param name="updates">The list of all updates to install.</param>
        /// <param name="packagesToUpdate">The list of all packages currently installed.</param>
        public static void UpdateAll(
            [NotNull] [ItemNotNull] IEnumerable<INugetPackage> updates,
            [NotNull] [ItemNotNull] IEnumerable<INugetPackage> packagesToUpdate)
        {
            var updatesCollection = updates as IReadOnlyCollection<INugetPackage> ?? updates.ToList();
            var packagesToUpdateCollection = packagesToUpdate as IReadOnlyCollection<INugetPackage> ?? packagesToUpdate.ToList();
            var progressStep = 1.0f / updatesCollection.Count;
            float currentProgress = 0;

            foreach (var update in updatesCollection)
            {
                EditorUtility.DisplayProgressBar($"Updating to {update.Id} {update.Version}", "Installing All Updates", currentProgress);

                var installedPackage = packagesToUpdateCollection.FirstOrDefault(p => p.Id == update.Id);
                if (installedPackage != null)
                {
                    Update(installedPackage, update, false, PackageUninstallReason.UpdateAll);
                }
                else
                {
                    Debug.LogErrorFormat("Trying to update {0} to {1}, but no version is installed!", update.Id, update.Version);
                }

                currentProgress += progressStep;
            }

            AssetDatabase.Refresh();

            EditorUtility.ClearProgressBar();
        }
    }
}
