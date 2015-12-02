namespace NugetForUnity
{
    using System.Collections.Generic;
    using System.Xml.Linq;

    /// <summary>
    /// Represents a .nuspec file used to store metadata for a NuGet package.
    /// </summary>
    public class NuspecFile
    {
        /// <summary>
        /// Gets or sets the ID of the NuGet package.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the version number of the NuGet package.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the authors of the NuGet package.
        /// </summary>
        public string Authors { get; set; }

        /// <summary>
        /// Gets or sets the owners of the NuGet package.
        /// </summary>
        public string Owners { get; set; }

        /// <summary>
        /// Gets or sets the URL for the location of the license of the NuGet package.
        /// </summary>
        public string LicenseUrl { get; set; }

        /// <summary>
        /// Gets or sets the URL for the location of the project webpage of the NuGet package.
        /// </summary>
        public string ProjectUrl { get; set; }

        /// <summary>
        /// Gets or sets the URL for the location of the icon of the NuGet package.
        /// </summary>
        public string IconUrl { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the license of the NuGet package needs to be accepted in order to use it.
        /// </summary>
        public bool RequireLicenseAcceptance { get; set; }

        /// <summary>
        /// Gets or sets the NuGet packages that this NuGet package depends on.
        /// </summary>
        public List<NugetPackage> Dependencies { get; set; }

        /// <summary>
        /// Gets or sets the description of the NuGet package.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the release notes of the NuGet package.
        /// </summary>
        public string ReleaseNotes { get; set; }

        /// <summary>
        /// Gets or sets the copyright of the NuGet package.
        /// </summary>
        public string Copyright { get; set; }

        /// <summary>
        /// Gets or sets the tags of the NuGet package.
        /// </summary>
        public string Tags { get; set; }

        /// <summary>
        /// Loads a .nuspec file at the given filepath.
        /// </summary>
        /// <param name="filePath">The full filepath to the .nuspec file to load.</param>
        /// <returns>The newly loaded <see cref="NuspecFile"/>.</returns>
        public static NuspecFile Load(string filePath)
        {
            NuspecFile nuspec = new NuspecFile();

            XDocument file = XDocument.Load(filePath);
            XElement metadata = file.Root.Element("metadata");

            nuspec.Id = (string) metadata.Element("id") ?? string.Empty;
            nuspec.Version = (string) metadata.Element("version") ?? string.Empty;
            nuspec.Authors = (string) metadata.Element("authors") ?? string.Empty;
            nuspec.Owners = (string) metadata.Element("owners") ?? string.Empty;
            nuspec.LicenseUrl = (string) metadata.Element("licenseUrl") ?? string.Empty;
            nuspec.ProjectUrl = (string) metadata.Element("projectUrl") ?? string.Empty;
            nuspec.IconUrl = (string) metadata.Element("iconUrl") ?? string.Empty;
            nuspec.RequireLicenseAcceptance = bool.Parse((string) metadata.Element("requireLicenseAcceptance") ?? "False");
            nuspec.Description = (string) metadata.Element("description") ?? string.Empty;
            nuspec.ReleaseNotes = (string) metadata.Element("releaseNotes") ?? string.Empty;
            nuspec.Copyright = (string) metadata.Element("copyright");
            nuspec.Tags = (string) metadata.Element("tags") ?? string.Empty;

            nuspec.Dependencies = new List<NugetPackage>();
            var dependenciesElement = metadata.Element("dependencies");
            if (dependenciesElement != null)
            {
                foreach (var dependencyElement in dependenciesElement.Elements("dependency"))
                {
                    NugetPackage dependency = new NugetPackage();
                    dependency.Id = (string) dependencyElement.Attribute("id") ?? string.Empty;
                    dependency.Version = (string) dependencyElement.Attribute("version") ?? string.Empty;
                    nuspec.Dependencies.Add(dependency);
                }
            }

            return nuspec;
        }

        /// <summary>
        /// Saves a <see cref="NuspecFile"/> to the given filepath, automatically overwriting.
        /// </summary>
        /// <param name="filePath">The full filepath to the .nuspec file to save.</param>
        public void Save(string filePath)
        {
            XDocument file = new XDocument();
            file.Add(new XElement("package"));
            XElement metadata = new XElement("metadata");

            metadata.Add(new XElement("id", Id));
            metadata.Add(new XElement("version", Version));
            metadata.Add(new XElement("authors", Authors));
            metadata.Add(new XElement("owners", Owners));
            metadata.Add(new XElement("licenseUrl", LicenseUrl));
            metadata.Add(new XElement("projectUrl", ProjectUrl));
            metadata.Add(new XElement("iconUrl", IconUrl));
            metadata.Add(new XElement("requireLicenseAcceptance", RequireLicenseAcceptance));
            metadata.Add(new XElement("description", Description));
            metadata.Add(new XElement("releaseNotes", ReleaseNotes));
            metadata.Add(new XElement("copyright", Copyright));
            metadata.Add(new XElement("tags", Tags));

            if (Dependencies != null && Dependencies.Count > 0)
            {
                //UnityEngine.Debug.Log("Saving dependencies!");
                var dependenciesElement = new XElement("dependencies");
                foreach (var dependency in Dependencies)
                {
                    var dependencyElement = new XElement("dependency");
                    dependencyElement.Add(new XAttribute("id", dependency.Id));
                    dependencyElement.Add(new XAttribute("version", dependency.Version));
                    dependenciesElement.Add(dependencyElement);
                }
                metadata.Add(dependenciesElement);
            }

            file.Root.Add(metadata);

            file.Save(filePath);
        }
    }
}