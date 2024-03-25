using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using JetBrains.Annotations;
using NugetForUnity.Models;
using UnityEditor;
using UnityEngine;

namespace NugetForUnity.Configuration
{
    /// <summary>
    ///     Represents a package.config file that holds the NuGet package dependencies for the project.
    /// </summary>
    public class PackagesConfigFile
    {
        /// <summary>
        ///     The file name where the configuration is stored.
        /// </summary>
        public const string FileName = "packages.config";

        private const string AutoReferencedAttributeName = "autoReferenced";

        private const string TargetFrameworkAttributeName = "targetFramework";

        [CanBeNull]
        private string contentIsSameAsInFilePath;

        /// <summary>
        ///     Gets the <see cref="NugetPackageIdentifier" />s contained in the package.config file.
        /// </summary>
        [NotNull]
        public List<PackageConfig> Packages { get; private set; } = new List<PackageConfig>();

        /// <summary>
        ///     Loads a list of all currently installed packages by reading the packages.config file.
        /// </summary>
        /// <returns>A newly created <see cref="PackagesConfigFile" />.</returns>
        [NotNull]
        public static PackagesConfigFile Load()
        {
            var filePath = ConfigurationManager.NugetConfigFile.PackagesConfigFilePath;
            var configFile = new PackagesConfigFile { Packages = new List<PackageConfig>() };

            // Create a package.config file, if there isn't already one in the project
            if (!File.Exists(filePath))
            {
                Debug.LogFormat("No packages.config file found. Creating default at {0}", filePath);

                configFile.Save();
            }

            var packagesFile = XDocument.Load(filePath);
            if (packagesFile.Root is null)
            {
                throw new InvalidOperationException("XML has no root element.");
            }

            foreach (var packageElement in packagesFile.Root.Elements())
            {
                var package = new PackageConfig
                {
                    Id =
                        packageElement.Attribute("id")?.Value ??
                        throw new InvalidOperationException($"package element misses 'id' attribute. Element:\n{packageElement}"),
                    Version =
                        packageElement.Attribute("version")?.Value ??
                        throw new InvalidOperationException($"package element misses 'version' attribute. Element:\n{packageElement}"),
                    IsManuallyInstalled =
                        packageElement.Attribute("manuallyInstalled")?.Value.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false,
                    AutoReferenced = (bool)(packageElement.Attributes(AutoReferencedAttributeName).FirstOrDefault() ??
                                            new XAttribute(AutoReferencedAttributeName, true)),
                    TargetFramework = packageElement.Attribute(TargetFrameworkAttributeName)?.Value,
                };
                configFile.Packages.Add(package);
            }

            configFile.contentIsSameAsInFilePath = filePath;
            return configFile;
        }

        /// <summary>
        ///     Adds a package to the packages.config file.
        /// </summary>
        /// <param name="package">The NugetPackage to add to the packages.config file.</param>
        /// <returns>The newly added or allready existing config entry from the packages.config file.</returns>
        public PackageConfig AddPackage([NotNull] INugetPackageIdentifier package)
        {
            return AddPackage(new PackageConfig { Id = package.Id, Version = package.Version, IsManuallyInstalled = package.IsManuallyInstalled });
        }

        /// <summary>
        ///     Removes a package from the packages.config file.
        /// </summary>
        /// <param name="package">The NugetPackage to remove from the packages.config file.</param>
        /// <returns>True if the package was removed, false otherwise.</returns>
        public bool RemovePackage([NotNull] INugetPackageIdentifier package)
        {
            var removed = Packages.RemoveAll(p => p.Equals(package));
            if (removed > 0)
            {
                MarkAsModified();
            }

            return removed > 0;
        }

        /// <summary>
        ///     Saves the packages.config file and populates it with given installed NugetPackages.
        /// </summary>
        public void Save()
        {
            var filePath = ConfigurationManager.NugetConfigFile.PackagesConfigFilePath;
            if (contentIsSameAsInFilePath == filePath)
            {
                return;
            }

            Packages.Sort(
                (x, y) =>
                {
                    if (x.Id == y.Id)
                    {
                        return x.PackageVersion.CompareTo(y.PackageVersion);
                    }

                    return string.Compare(x.Id, y.Id, StringComparison.Ordinal);
                });

            var packagesFile = new XDocument();
            packagesFile.Add(new XElement("packages"));
            foreach (var package in Packages)
            {
                var packageElement = new XElement("package");
                packageElement.Add(new XAttribute("id", package.Id));
                packageElement.Add(new XAttribute("version", package.Version));

                if (package.IsManuallyInstalled)
                {
                    packageElement.Add(new XAttribute("manuallyInstalled", "true"));
                }

                if (!package.AutoReferenced)
                {
                    packageElement.Add(new XAttribute(AutoReferencedAttributeName, package.AutoReferenced));
                }

                if (!string.IsNullOrEmpty(package.TargetFramework))
                {
                    packageElement.Add(new XAttribute(TargetFrameworkAttributeName, package.TargetFramework));
                }

                packagesFile.Root?.Add(packageElement);
            }

            // remove the read only flag on the file, if there is one.
            var packageExists = File.Exists(filePath);
            if (packageExists)
            {
                var attributes = File.GetAttributes(filePath);

                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    attributes &= ~FileAttributes.ReadOnly;
                    File.SetAttributes(filePath, attributes);
                }
            }

            packagesFile.Save(filePath);
            contentIsSameAsInFilePath = filePath;
        }

        /// <summary>
        ///     Moves the packages.config file and its corresponding meta file to the given path. If there is no packages.config file on the current
        ///     path, makes a default version of the file on the new path.
        /// </summary>
        /// <param name="newPath">Path to move packages.config file to.</param>
        internal static void Move([NotNull] string newPath)
        {
            var nugetConfig = ConfigurationManager.NugetConfigFile;
            var oldFilePath = nugetConfig.PackagesConfigFilePath;
            var oldPath = nugetConfig.PackagesConfigDirectoryPath;

            // We need to make sure saved path is using forward slashes so it works on all systems
            nugetConfig.PackagesConfigDirectoryPath = newPath.Replace("\\", "/");
            var newFilePath = Path.GetFullPath(Path.Combine(newPath, FileName));
            try
            {
                if (!File.Exists(oldFilePath))
                {
                    Debug.LogFormat("No packages.config file found. Creating default at {0}", newPath);
                    var configFile = new PackagesConfigFile { Packages = new List<PackageConfig>() };
                    configFile.Save();
                    AssetDatabase.Refresh();
                    return;
                }

                Directory.CreateDirectory(newPath);

                // moving config to the new path
                File.Move(oldFilePath, newFilePath);
            }
            catch (Exception e)
            {
                // usually unauthorized access or IO exception (trying to move to a folder where the same file exists)
                Debug.LogException(e);
                nugetConfig.PackagesConfigDirectoryPath = oldPath;
                return;
            }

            // manually moving meta file to suppress Unity warning
            if (File.Exists($"{oldFilePath}.meta"))
            {
                File.Move($"{oldFilePath}.meta", $"{newFilePath}.meta");
            }

            // if the old path is now an empty directory, delete it
            if (!Directory.EnumerateFileSystemEntries(oldPath).Any())
            {
                Directory.Delete(oldPath);

                // also delete its meta file if it exists
                if (File.Exists($"{oldPath}.meta"))
                {
                    File.Delete($"{oldPath}.meta");
                }
            }
        }

        /// <summary>
        ///     Sets the manually installed flag for the given package.
        /// </summary>
        /// <param name="package">The package to mark as manually installed.</param>
        internal void SetManuallyInstalledFlag([NotNull] INugetPackageIdentifier package)
        {
            package.IsManuallyInstalled = true;
            var packageConfig = GetPackageConfigurationById(package);
            if (packageConfig != null && !packageConfig.IsManuallyInstalled)
            {
                packageConfig.IsManuallyInstalled = true;
                MarkAsModified();
            }
        }

        /// <summary>
        ///     Gets the <see cref="PackageConfig" /> entry of the NuGet package with the id <paramref name="package" />.
        /// </summary>
        /// <param name="package">The package id to serch.</param>
        /// <returns>The configuration of the NuGet package or null if the package is not in the <see cref="Packages" /> collection.</returns>
        [CanBeNull]
        internal PackageConfig GetPackageConfigurationById([NotNull] INugetPackageIdentifier package)
        {
            return GetPackageConfigurationById(package.Id);
        }

        /// <summary>
        ///     Gets the <see cref="PackageConfig" /> entry of the NuGet package with the id <paramref name="packageId" />.
        /// </summary>
        /// <param name="packageId">The package id to serch.</param>
        /// <returns>The configuration of the NuGet package or null if the package is not in the <see cref="Packages" /> collection.</returns>
        [CanBeNull]
        internal PackageConfig GetPackageConfigurationById([NotNull] string packageId)
        {
            return Packages.Find(p => string.Equals(p.Id, packageId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        ///     Adds a package to the packages.config file.
        /// </summary>
        /// <param name="package">The NugetPackage to add to the packages.config file.</param>
        /// <returns>The newly added or allready existing config entry from the packages.config file.</returns>
        private PackageConfig AddPackage([NotNull] PackageConfig package)
        {
            var existingPackage = GetPackageConfigurationById(package);
            if (existingPackage is null)
            {
                Packages.Add(package);
                MarkAsModified();
                return package;
            }

            var compared = existingPackage.CompareTo(package);
            if (compared < 0)
            {
                Debug.LogWarningFormat(
                    "{0} {1} is already listed in the packages.config file.  Updating to {2}",
                    existingPackage.Id,
                    existingPackage.Version,
                    package.Version);
                Packages.Remove(existingPackage);
                Packages.Add(package);
                MarkAsModified();
                return package;
            }

            if (compared > 0)
            {
                Debug.LogWarningFormat(
                    "Trying to add {0} {1} to the packages.config file.  {2} is already listed, so using that.",
                    package.Id,
                    package.Version,
                    existingPackage.Version);
                return existingPackage;
            }

            return existingPackage;
        }

        private void MarkAsModified()
        {
            contentIsSameAsInFilePath = null;
        }
    }
}
