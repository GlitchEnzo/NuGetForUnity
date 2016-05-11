namespace NugetForUnity
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml;
    using System.Xml.Linq;
    using Ionic.Zip;

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
        /// Gets or sets the title of the NuGet package.
        /// </summary>
        public string Title { get; set; }

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
        public List<NugetPackageIdentifier> Dependencies { get; set; }

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
        /// Loads the .nuspec file inside the .nupkg file at the given filepath.
        /// </summary>
        /// <param name="nupkgFilepath">The filepath to the .nupkg file to load.</param>
        /// <returns>The .nuspec file loaded from inside the .nupkg file.</returns>
        public static NuspecFile FromNupkgFile(string nupkgFilepath)
        {
            NuspecFile nuspec = new NuspecFile();

            if (File.Exists(nupkgFilepath))
            {
                // get the .nuspec file from inside the .nupkg
                using (ZipFile zip = ZipFile.Read(nupkgFilepath))
                {
                    //var entry = zip[string.Format("{0}.nuspec", packageId)];
                    var entry = zip.First(x => x.FileName.EndsWith(".nuspec"));

                    using (MemoryStream stream = new MemoryStream())
                    {
                        entry.Extract(stream);
                        stream.Position = 0;

                        nuspec = Load(stream);
                    }
                }
            }
            else
            {
                UnityEngine.Debug.LogErrorFormat("Package could not be read: {0}", nupkgFilepath);

                //nuspec.Id = packageId;
                //nuspec.Version = packageVersion;
                nuspec.Description = string.Format("COULD NOT LOAD {0}", nupkgFilepath);
            }

            return nuspec;
        }

        /// <summary>
        /// Loads a .nuspec file at the given filepath.
        /// </summary>
        /// <param name="filePath">The full filepath to the .nuspec file to load.</param>
        /// <returns>The newly loaded <see cref="NuspecFile"/>.</returns>
        public static NuspecFile Load(string filePath)
        {
            return Load(XDocument.Load(filePath));
        }

        /// <summary>
        /// Loads a .nuspec file inside the given stream.
        /// </summary>
        /// <param name="stream">The stream containing the .nuspec file to load.</param>
        /// <returns>The newly loaded <see cref="NuspecFile"/>.</returns>
        public static NuspecFile Load(Stream stream)
        {
            XmlReader reader = new XmlTextReader(stream);
            XDocument document = XDocument.Load(reader);
            return Load(document);
        }

        /// <summary>
        /// Loads a .nuspec file inside the given <see cref="XDocument"/>.
        /// </summary>
        /// <param name="nuspecDocument">The .nuspec file as an <see cref="XDocument"/>.</param>
        /// <returns>The newly loaded <see cref="NuspecFile"/>.</returns>
        public static NuspecFile Load(XDocument nuspecDocument)
        {
            NuspecFile nuspec = new NuspecFile();

            string nuspecNamespace = nuspecDocument.Root.GetDefaultNamespace().ToString();

            XElement package = nuspecDocument.Element(XName.Get("package", nuspecNamespace));
            XElement metadata = package.Element(XName.Get("metadata", nuspecNamespace));

            nuspec.Id = (string)metadata.Element(XName.Get("id", nuspecNamespace)) ?? string.Empty;
            nuspec.Version = (string)metadata.Element(XName.Get("version", nuspecNamespace)) ?? string.Empty;
            nuspec.Title = (string)metadata.Element(XName.Get("title", nuspecNamespace)) ?? string.Empty;
            nuspec.Authors = (string)metadata.Element(XName.Get("authors", nuspecNamespace)) ?? string.Empty;
            nuspec.Owners = (string)metadata.Element(XName.Get("owners", nuspecNamespace)) ?? string.Empty;
            nuspec.LicenseUrl = (string)metadata.Element(XName.Get("licenseUrl", nuspecNamespace)) ?? string.Empty;
            nuspec.ProjectUrl = (string)metadata.Element(XName.Get("projectUrl", nuspecNamespace)) ?? string.Empty;
            nuspec.IconUrl = (string)metadata.Element(XName.Get("iconUrl", nuspecNamespace)) ?? string.Empty;
            nuspec.RequireLicenseAcceptance = bool.Parse((string)metadata.Element(XName.Get("requireLicenseAcceptance", nuspecNamespace)) ?? "False");
            nuspec.Description = (string)metadata.Element(XName.Get("description", nuspecNamespace)) ?? string.Empty;
            nuspec.ReleaseNotes = (string)metadata.Element(XName.Get("releaseNotes", nuspecNamespace)) ?? string.Empty;
            nuspec.Copyright = (string)metadata.Element(XName.Get("copyright", nuspecNamespace));
            nuspec.Tags = (string)metadata.Element(XName.Get("tags", nuspecNamespace)) ?? string.Empty;

            nuspec.Dependencies = new List<NugetPackageIdentifier>();
            var dependenciesElement = metadata.Element(XName.Get("dependencies", nuspecNamespace));
            if (dependenciesElement != null)
            {
                foreach (var dependencyElement in dependenciesElement.Elements(XName.Get("dependency", nuspecNamespace)))
                {
                    NugetPackageIdentifier dependency = new NugetPackageIdentifier();
                    dependency.Id = (string)dependencyElement.Attribute("id") ?? string.Empty;
                    dependency.Version = (string)dependencyElement.Attribute("version") ?? string.Empty;
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
            // TODO: Set a namespace when saving

            XDocument file = new XDocument();
            file.Add(new XElement("package"));
            XElement metadata = new XElement("metadata");

            metadata.Add(new XElement("id", Id));
            metadata.Add(new XElement("version", Version));

            if (!string.IsNullOrEmpty(Title))
            {
                metadata.Add(new XElement("title", Title));
            }

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

            // remove the read only flag on the file, if there is one.
            if (File.Exists(filePath))
            {
                FileAttributes attributes = File.GetAttributes(filePath);

                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    attributes &= ~FileAttributes.ReadOnly;
                    File.SetAttributes(filePath, attributes);
                }
            }

            file.Save(filePath);
        }
    }
}