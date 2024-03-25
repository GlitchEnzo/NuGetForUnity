using System;
using System.Diagnostics;
using NugetForUnity.Helper;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace NugetForUnity
{
    /// <summary>
    ///     Restores all packages defined in packages.config.
    /// </summary>
    public static class PackageRestorer
    {
        /// <summary>
        ///     Restores all packages defined in packages.config.
        /// </summary>
        /// <param name="slimRestore">True if we want to skip installing dependencies and checking if the lib is imported in Unity.</param>
        public static void Restore(bool slimRestore)
        {
            UnityPathHelper.EnsurePackageInstallDirectoryIsSetup();
            InstalledPackagesManager.UpdateInstalledPackages();

            var stopwatch = Stopwatch.StartNew();
            var somethingChanged = false;
            try
            {
                var packagesToInstall =
                    InstalledPackagesManager.PackagesConfigFile.Packages.FindAll(
                        package => !InstalledPackagesManager.IsInstalled(package, !slimRestore));
                if (packagesToInstall.Count > 0)
                {
                    var progressStep = 1.0f / packagesToInstall.Count;
                    float currentProgress = 0;

                    NugetLogger.LogVerbose("Restoring {0} packages.", packagesToInstall.Count);

                    foreach (var package in packagesToInstall)
                    {
                        EditorUtility.DisplayProgressBar("Restoring NuGet Packages", $"Restoring {package.Id} {package.Version}", currentProgress);
                        NugetLogger.LogVerbose("---Restoring {0} {1}", package.Id, package.Version);
                        NugetPackageInstaller.InstallIdentifier(package, false, slimRestore);
                        somethingChanged = true;
                        currentProgress += progressStep;
                    }
                }
                else
                {
                    NugetLogger.LogVerbose("No packages need restoring.");
                }

                if (InstalledPackagesManager.RemoveUnnecessaryPackages())
                {
                    somethingChanged = true;
                }
            }
            catch (Exception e)
            {
                Debug.LogErrorFormat("{0}", e);
            }
            finally
            {
                NugetLogger.LogVerbose("Restoring packages took {0} ms", stopwatch.ElapsedMilliseconds);

                if (somethingChanged)
                {
                    AssetDatabase.Refresh();
                }

                EditorUtility.ClearProgressBar();
            }
        }
    }
}
