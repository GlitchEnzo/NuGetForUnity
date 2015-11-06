using System.Collections.Generic;
using System.Diagnostics;
using System.Xml.Linq;

public class NuspecFile
{
    public string Id { get; set; }
    public string Version { get; set; }
    public string Authors { get; set; }
    public string Owners { get; set; }
    public string LicenseUrl { get; set; }
    public string ProjectUrl { get; set; }
    public string IconUrl { get; set; }
    public bool RequireLicenseAcceptance { get; set; }
    public List<NugetPackage> Dependencies { get; set; }
    public string Description { get; set; }
    public string ReleaseNotes { get; set; }
    public string Copyright { get; set; }
    public string Tags { get; set; }

    public static NuspecFile Load(string filePath)
    {
        NuspecFile nuspec = new NuspecFile();

        XDocument file = XDocument.Load(filePath);
        XElement metadata = file.Root.Element("metadata");

        nuspec.Id = (string)metadata.Element("id") ?? string.Empty;
        nuspec.Version = (string)metadata.Element("version") ?? string.Empty;
        nuspec.Authors = (string)metadata.Element("authors") ?? string.Empty;
        nuspec.Owners = (string)metadata.Element("owners") ?? string.Empty;
        nuspec.LicenseUrl = (string)metadata.Element("licenseUrl") ?? string.Empty;
        nuspec.ProjectUrl = (string)metadata.Element("projectUrl") ?? string.Empty;
        nuspec.IconUrl = (string)metadata.Element("iconUrl") ?? string.Empty;
        nuspec.RequireLicenseAcceptance = bool.Parse((string)metadata.Element("requireLicenseAcceptance") ?? "False");
        nuspec.Description = (string)metadata.Element("description") ?? string.Empty;
        nuspec.ReleaseNotes = (string)metadata.Element("releaseNotes") ?? string.Empty;
        nuspec.Copyright = (string)metadata.Element("copyright");
        nuspec.Tags = (string)metadata.Element("tags") ?? string.Empty;

        nuspec.Dependencies = new List<NugetPackage>();
        var dependenciesElement = metadata.Element("dependencies");
        if (dependenciesElement != null)
        {
            foreach (var dependencyElement in dependenciesElement.Elements("dependency"))
            {
                NugetPackage dependency = new NugetPackage();
                dependency.ID = (string)dependencyElement.Attribute("id") ?? string.Empty;
                dependency.Version = (string)dependencyElement.Attribute("version") ?? string.Empty;
                nuspec.Dependencies.Add(dependency);
            }
        }

        return nuspec;
    }

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
                dependencyElement.Add(new XAttribute("id", dependency.ID));
                dependencyElement.Add(new XAttribute("version", dependency.Version));
                dependenciesElement.Add(dependencyElement);
            }
            metadata.Add(dependenciesElement);
        }

        file.Root.Add(metadata);

        file.Save(filePath);
    }
}
