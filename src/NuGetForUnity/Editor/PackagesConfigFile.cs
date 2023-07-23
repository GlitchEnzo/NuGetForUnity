using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

namespace NugetForUnity
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

        private string contentIsSameAsInFilePath;

        /// <summary>
        ///     Gets the <see cref="NugetPackageIdentifier" />s contained in the package.config file.
        /// </summary>
        public List<PackageConfig> Packages { get; private set; }

        /// <summary>
        ///     Adds a package to the packages.config file.
        /// </summary>
        /// <param name="package">The NugetPackage to add to the packages.config file.</param>
        public void AddPackage(PackageConfig package)
        {
            var existingPackage = Packages.Find(p => p.Id.Equals(package.Id, StringComparison.OrdinalIgnoreCase));
            if (existingPackage != null)
            {
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
                }
                else if (compared > 0)
                {
                    Debug.LogWarningFormat(
                        "Trying to add {0} {1} to the packages.config file.  {2} is already listed, so using that.",
                        package.Id,
                        package.Version,
                        existingPackage.Version);
                }
            }
            else
            {
                Packages.Add(package);
                MarkAsModified();
            }
        }

        /// <summary>
        ///     Adds a package to the packages.config file.
        /// </summary>
        /// <param name="package">The NugetPackage to add to the packages.config file.</param>
        public void AddPackage(NugetPackageIdentifier package)
        {
            AddPackage(new PackageConfig { Id = package.Id, Version = package.Version, IsManuallyInstalled = package.IsManuallyInstalled });
        }

        /// <summary>
        ///     Removes a package from the packages.config file.
        /// </summary>
        /// <param name="package">The NugetPackage to remove from the packages.config file.</param>
        public bool RemovePackage(NugetPackageIdentifier package)
        {
            var removed = Packages.RemoveAll(p => p.CompareTo(package) == 0);
            if (removed > 0)
            {
                MarkAsModified();
            }

            return removed > 0;
        }

        internal void SetManuallyInstalledFlag(NugetPackageIdentifier package)
        {
            package.IsManuallyInstalled = true;
            var packageConfig = Packages.Find(p => p.Id.Equals(package.Id, StringComparison.OrdinalIgnoreCase));
            if (packageConfig != null)
            {
                packageConfig.IsManuallyInstalled = true;
                MarkAsModified();
            }
        }

        /// <summary>
        ///     Loads a list of all currently installed packages by reading the packages.config file.
        /// </summary>
        /// <returns>A newly created <see cref="PackagesConfigFile" />.</returns>
        public static PackagesConfigFile Load(string filepath)
        {
            var configFile = new PackagesConfigFile { Packages = new List<PackageConfig>() };

            // Create a package.config file, if there isn't already one in the project
            if (!File.Exists(filepath))
            {
                Debug.LogFormat("No packages.config file found. Creating default at {0}", filepath);

                configFile.Save(filepath);
            }

            var packagesFile = XDocument.Load(filepath);
            foreach (var packageElement in packagesFile.Root.Elements())
            {
                var package = new PackageConfig
                {
                    Id = packageElement.Attribute("id").Value,
                    Version = packageElement.Attribute("version").Value,
                    IsManuallyInstalled =
                        packageElement.Attribute("manuallyInstalled")?.Value.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false,
                    AutoReferenced = (bool)(packageElement.Attributes(AutoReferencedAttributeName).FirstOrDefault() ??
                                            new XAttribute(AutoReferencedAttributeName, true)),
                };
                configFile.Packages.Add(package);
            }

            configFile.contentIsSameAsInFilePath = filepath;
            return configFile;
        }

        /// <summary>
        ///     Saves the packages.config file and populates it with given installed NugetPackages.
        /// </summary>
        /// <param name="filepath">The filepath to where this packages.config will be saved.</param>
        public void Save(string filepath)
        {
            if (contentIsSameAsInFilePath == filepath)
            {
                return;
            }

            Packages.Sort(
                delegate(PackageConfig x, PackageConfig y)
                {
                    if (x.Id == null && y.Id == null)
                    {
                        return 0;
                    }

                    if (x.Id == null)
                    {
                        return -1;
                    }

                    if (y.Id == null)
                    {
                        return 1;
                    }

                    if (x.Id == y.Id)
                    {
                        return x.Version.CompareTo(y.Version);
                    }

                    return x.Id.CompareTo(y.Id);
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

                packagesFile.Root?.Add(packageElement);
            }

            // remove the read only flag on the file, if there is one.
            var packageExists = File.Exists(filepath);
            if (packageExists)
            {
                var attributes = File.GetAttributes(filepath);

                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    attributes &= ~FileAttributes.ReadOnly;
                    File.SetAttributes(filepath, attributes);
                }
            }

            packagesFile.Save(filepath);
            contentIsSameAsInFilePath = filepath;
        }

        private void MarkAsModified()
        {
            contentIsSameAsInFilePath = null;
        }
    }
}
