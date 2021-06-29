using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace NugetForUnity
{
    /// <summary>
    /// Provides helper methods for parsing a NuGet server OData response.
    /// OData is a superset of the Atom API.
    /// </summary>
    public static class NugetODataResponse
    {
        private static string AtomNamespace = "http://www.w3.org/2005/Atom";

        private static string DataServicesNamespace = "http://schemas.microsoft.com/ado/2007/08/dataservices";

        private static string MetaDataNamespace = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";

        /// <summary>
        /// Gets the string value of a NuGet metadata property from the given properties element and property name.
        /// </summary>
        /// <param name="properties">The properties element.</param>
        /// <param name="name">The name of the property to get.</param>
        /// <returns>The string value of the property.</returns>
        private static string GetProperty(this XElement properties, string name)
        {
            return (string)properties.Element(XName.Get(name, DataServicesNamespace)) ?? string.Empty;
        }

        /// <summary>
        /// Gets the <see cref="XElement"/> within the Atom namespace with the given name.
        /// </summary>
        /// <param name="element">The element containing the Atom element.</param>
        /// <param name="name">The name of the Atom element</param>
        /// <returns>The Atom element.</returns>
        private static XElement GetAtomElement(this XElement element, string name)
        {
            return element.Element(XName.Get(name, AtomNamespace));
        }

        /// <summary>
        /// Parses the given <see cref="XDocument"/> and returns the list of <see cref="NugetPackage"/>s contained within.
        /// </summary>
        /// <param name="document">The <see cref="XDocument"/> that is the OData XML response from the NuGet server.</param>
        /// <returns>The list of <see cref="NugetPackage"/>s read from the given XML.</returns>
        public static List<NugetPackage> Parse(XDocument document)
        {
            List<NugetPackage> packages = new List<NugetPackage>();

            IEnumerable<XElement> packageEntries;
            if (document.Root.Name.Equals(XName.Get("entry", AtomNamespace)))
            {
                packageEntries = Enumerable.Repeat(document.Root, 1);
            }
            else
            {
                packageEntries = document.Root.Elements(XName.Get("entry", AtomNamespace));
            }
                
            foreach (var entry in packageEntries)
            {
                NugetPackage package = new NugetPackage();
                package.Id = entry.GetAtomElement("title").Value;
                package.DownloadUrl = entry.GetAtomElement("content").Attribute("src").Value;

                var entryProperties = entry.Element(XName.Get("properties", MetaDataNamespace));
                package.Title = entryProperties.GetProperty("Title");
                package.Version = entryProperties.GetProperty("Version");
                package.Description = entryProperties.GetProperty("Description");
                package.Summary = entryProperties.GetProperty("Summary");
                package.ReleaseNotes = entryProperties.GetProperty("ReleaseNotes");
                package.LicenseUrl = entryProperties.GetProperty("LicenseUrl");
                package.ProjectUrl = entryProperties.GetProperty("ProjectUrl");
                package.Authors = entryProperties.GetProperty("Authors");
                package.DownloadCount = int.Parse(entryProperties.GetProperty("DownloadCount"));

                string iconUrl = entryProperties.GetProperty("IconUrl");
                if (!string.IsNullOrEmpty(iconUrl))
                {
                    package.Icon = NugetHelper.DownloadImage(iconUrl);
                }

                // if there is no title, just use the ID as the title
                if (string.IsNullOrEmpty(package.Title))
                {
                    package.Title = package.Id;
                }

                // Get dependencies
                string rawDependencies = entryProperties.GetProperty("Dependencies");
                if (!string.IsNullOrEmpty(rawDependencies))
                {
                    var dependencyGroups = new Dictionary<string, NugetFrameworkGroup>();

                    string[] dependencies = rawDependencies.Split('|');
                    foreach (var dependencyString in dependencies)
                    {
                        string[] details = dependencyString.Split(':');
                        
                        string framework = string.Empty;
                        if (details.Length > 2)
                        {
                            framework = details[2];
                        }

                        NugetFrameworkGroup group;
                        if (!dependencyGroups.TryGetValue(framework, out group))
                        {
                            group = new NugetFrameworkGroup();
                            group.TargetFramework = framework;
                            dependencyGroups.Add(framework, group);
                        }

                        var dependency = new NugetPackageIdentifier(details[0], details[1]);
                        // some packages (ex: FSharp.Data - 2.1.0) have inproper "semi-empty" dependencies such as:
                        // "Zlib.Portable:1.10.0:portable-net40+sl50+wp80+win80|::net40"
                        // so we need to only add valid dependencies and skip invalid ones
                        if (!string.IsNullOrEmpty(dependency.Id) && !string.IsNullOrEmpty(dependency.Version))
                        {
                            group.Dependencies.Add(dependency);
                        }
                    }

                    foreach (var group in dependencyGroups.Values)
                    {
                        package.Dependencies.Add(group);
                    }
                }

                packages.Add(package);
            }

            return packages;
        }
    }
}
