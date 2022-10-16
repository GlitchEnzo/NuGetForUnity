namespace NugetForUnity
{
    using System.Collections.Generic;
    using System.IO;
    using System.Xml.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Represents a package.config file that holds the NuGet package dependencies for the project.
    /// </summary>
    public class PackagesConfigFile
    {
        /// <summary>
        /// Gets the <see cref="NugetPackageIdentifier"/>s contained in the package.config file.
        /// </summary>
        public List<NugetPackageIdentifier> Packages { get; private set; }

        /// <summary>
        /// Adds a package to the packages.config file.
        /// </summary>
        /// <param name="package">The NugetPackage to add to the packages.config file.</param>
        public void AddPackage(NugetPackageIdentifier package)
        {
            NugetPackageIdentifier existingPackage = Packages.Find(p => p.Id.ToLower() == package.Id.ToLower());
            if (existingPackage != null)
            {
                if (existingPackage < package)
                {
                    Debug.LogWarningFormat("{0} {1} is already listed in the packages.config file.  Updating to {2}", existingPackage.Id, existingPackage.Version, package.Version);
                    Packages.Remove(existingPackage);
                    Packages.Add(package);
                }
                else if (existingPackage > package)
                {
                    Debug.LogWarningFormat("Trying to add {0} {1} to the packages.config file.  {2} is already listed, so using that.", package.Id, package.Version, existingPackage.Version);
                }
            }
            else
            {
                Packages.Add(package);
            }
        }

        /// <summary>
        /// Removes a package from the packages.config file.
        /// </summary>
        /// <param name="package">The NugetPackage to remove from the packages.config file.</param>
        public void RemovePackage(NugetPackageIdentifier package)
        {
            Packages.RemoveAll(p => p.CompareTo(package) == 0);
        }

        /// <summary>
        /// Loads a list of all currently installed packages by reading the packages.config file.
        /// </summary>
        /// <returns>A newly created <see cref="PackagesConfigFile"/>.</returns>
        public static PackagesConfigFile Load(string filepath)
        {
            PackagesConfigFile configFile = new PackagesConfigFile
            {
                Packages = new List<NugetPackageIdentifier>()
            };

            // Create a package.config file, if there isn't already one in the project
            if (!File.Exists(filepath))
            {
                Debug.LogFormat("No packages.config file found. Creating default at {0}", filepath);

                configFile.Save(filepath);

                AssetDatabase.Refresh();
            }

            XDocument packagesFile = XDocument.Load(filepath);

            // Force disable
            NugetHelper.DisableWSAPExportSetting(filepath, false);

            foreach (XElement packageElement in packagesFile.Root.Elements())
            {
                NugetPackage package = new NugetPackage
                {
                    Id = packageElement.Attribute("id").Value,
                    Version = packageElement.Attribute("version").Value
                };
                configFile.Packages.Add(package);
            }

            return configFile;
        }

        /// <summary>
        /// Saves the packages.config file and populates it with given installed NugetPackages.
        /// </summary>
        /// <param name="filepath">The filepath to where this packages.config will be saved.</param>
        public void Save(string filepath)
        {
            Packages.Sort(delegate (NugetPackageIdentifier x, NugetPackageIdentifier y)
            {
                if (x.Id == null && y.Id == null)
                {
                    return 0;
                }
                else if (x.Id == null)
                {
                    return -1;
                }
                else if (y.Id == null)
                {
                    return 1;
                }
                else if (x.Id == y.Id)
                {
                    return x.Version.CompareTo(y.Version);
                }
                else
                {
                    return x.Id.CompareTo(y.Id);
                }
            });

            XDocument packagesFile = new XDocument();
            packagesFile.Add(new XElement("packages"));
            foreach (NugetPackageIdentifier package in Packages)
            {
                XElement packageElement = new XElement("package");
                packageElement.Add(new XAttribute("id", package.Id));
                packageElement.Add(new XAttribute("version", package.Version));
                packagesFile.Root.Add(packageElement);
            }

            // remove the read only flag on the file, if there is one.
            bool packageExists = File.Exists(filepath);
            if (packageExists)
            {
                FileAttributes attributes = File.GetAttributes(filepath);

                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    attributes &= ~FileAttributes.ReadOnly;
                    File.SetAttributes(filepath, attributes);
                }
            }

            packagesFile.Save(filepath);

            NugetHelper.DisableWSAPExportSetting(filepath, packageExists);
        }
    }
}