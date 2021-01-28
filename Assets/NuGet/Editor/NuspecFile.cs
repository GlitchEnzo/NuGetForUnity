namespace NugetForUnity
{
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Xml;
    using System.Xml.Linq;

    /// <summary>
    /// Represents a .nuspec file used to store metadata for a NuGet package.
    /// </summary>
    /// <remarks>
    /// At a minumum, Id, Version, Description, and Authors is required.  Everything else is optional.
    /// See more info here: https://docs.microsoft.com/en-us/nuget/schema/nuspec
    /// </remarks>
    public class NuspecFile
    {
        public NuspecFile()
        {
            Dependencies = new List<NugetFrameworkGroup>();
            Files = new List<NuspecContentFile>();
        }

        #region Required
        /// <summary>
        /// Gets or sets the ID of the NuGet package.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the version number of the NuGet package.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the description of the NuGet package.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the description of the NuGet package.
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        /// Gets or sets the authors of the NuGet package.
        /// </summary>
        public string Authors { get; set; }
        #endregion

        /// <summary>
        /// Gets or sets the title of the NuGet package.
        /// </summary>
        public string Title { get; set; }

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
        public List<NugetFrameworkGroup> Dependencies { get; set; }

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
        /// Gets or sets the url for the location of the package's source code.
        /// </summary>
        public string RepositoryUrl;

        /// <summary>
        /// Gets or sets the type of source control software that the package's source code resides in.
        /// </summary>
        public string RepositoryType;

        /// <summary>
        /// Gets or sets the source control branch the package is from.
        /// </summary>
        public string RepositoryBranch;

        /// <summary>
        /// Gets or sets the source control commit the package is from.
        /// </summary>
        public string RepositoryCommit;

        /// <summary>
        /// Gets or sets the list of content files listed in the .nuspec file.
        /// </summary>
        public List<NuspecContentFile> Files { get; set; }

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
                using (ZipArchive zip = ZipFile.OpenRead(nupkgFilepath))
                {
                    //var entry = zip[string.Format("{0}.nuspec", packageId)];
                    var entry = zip.Entries.First(x => x.FullName.EndsWith(".nuspec"));

                    using (Stream stream = entry.Open())
                    {
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
            nuspec.Summary = (string)metadata.Element(XName.Get("summary", nuspecNamespace)) ?? string.Empty;
            nuspec.ReleaseNotes = (string)metadata.Element(XName.Get("releaseNotes", nuspecNamespace)) ?? string.Empty;
            nuspec.Copyright = (string)metadata.Element(XName.Get("copyright", nuspecNamespace));
            nuspec.Tags = (string)metadata.Element(XName.Get("tags", nuspecNamespace)) ?? string.Empty;

            var repositoryElement = metadata.Element(XName.Get("repository", nuspecNamespace));

            if (repositoryElement != null)
            {
                nuspec.RepositoryType = (string)repositoryElement.Attribute(XName.Get("type")) ?? string.Empty;
                nuspec.RepositoryUrl = (string)repositoryElement.Attribute(XName.Get("url")) ?? string.Empty;
                nuspec.RepositoryBranch = (string)repositoryElement.Attribute(XName.Get("branch")) ?? string.Empty;
                nuspec.RepositoryCommit = (string)repositoryElement.Attribute(XName.Get("commit")) ?? string.Empty;
            }

            var dependenciesElement = metadata.Element(XName.Get("dependencies", nuspecNamespace));
            if (dependenciesElement != null)
            {
                // Dependencies specified for specific target frameworks
                foreach (var frameworkGroup in dependenciesElement.Elements(XName.Get("group", nuspecNamespace)))
                {
                    NugetFrameworkGroup group = new NugetFrameworkGroup();
                    group.TargetFramework = ConvertFromNupkgTargetFrameworkName((string)frameworkGroup.Attribute("targetFramework") ?? string.Empty);

                    foreach (var dependencyElement in frameworkGroup.Elements(XName.Get("dependency", nuspecNamespace)))
                    {
                        NugetPackageIdentifier dependency = new NugetPackageIdentifier();

                        dependency.Id = (string)dependencyElement.Attribute("id") ?? string.Empty;
                        dependency.Version = (string)dependencyElement.Attribute("version") ?? string.Empty;
                        group.Dependencies.Add(dependency);
                    }

                    nuspec.Dependencies.Add(group);
                }

                // Flat dependency list
                if (nuspec.Dependencies.Count == 0)
                {
                    NugetFrameworkGroup group = new NugetFrameworkGroup();
                    foreach (var dependencyElement in dependenciesElement.Elements(XName.Get("dependency", nuspecNamespace)))
                    {
                        NugetPackageIdentifier dependency = new NugetPackageIdentifier();
                        dependency.Id = (string)dependencyElement.Attribute("id") ?? string.Empty;
                        dependency.Version = (string)dependencyElement.Attribute("version") ?? string.Empty;
                        group.Dependencies.Add(dependency);
                    }

                    nuspec.Dependencies.Add(group);
                }
            }

            var filesElement = package.Element(XName.Get("files", nuspecNamespace));
            if (filesElement != null)
            {
                //UnityEngine.Debug.Log("Loading files!");
                foreach (var fileElement in filesElement.Elements(XName.Get("file", nuspecNamespace)))
                {
                    NuspecContentFile file = new NuspecContentFile();
                    file.Source = (string)fileElement.Attribute("src") ?? string.Empty;
                    file.Target = (string)fileElement.Attribute("target") ?? string.Empty;
                    nuspec.Files.Add(file);
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
            XElement packageElement = new XElement("package");
            file.Add(packageElement);
            XElement metadata = new XElement("metadata");

            // required
            metadata.Add(new XElement("id", Id));
            metadata.Add(new XElement("version", Version));
            metadata.Add(new XElement("description", Description));
            metadata.Add(new XElement("authors", Authors));

            if (!string.IsNullOrEmpty(Title))
            {
                metadata.Add(new XElement("title", Title));
            }

            if (!string.IsNullOrEmpty(Owners))
            {
                metadata.Add(new XElement("owners", Owners));
            }

            if (!string.IsNullOrEmpty(LicenseUrl))
            {
                metadata.Add(new XElement("licenseUrl", LicenseUrl));
            }

            if (!string.IsNullOrEmpty(ProjectUrl))
            {
                metadata.Add(new XElement("projectUrl", ProjectUrl));
            }

            if (!string.IsNullOrEmpty(IconUrl))
            {
                metadata.Add(new XElement("iconUrl", IconUrl));
            }

            if (RequireLicenseAcceptance)
            {
                metadata.Add(new XElement("requireLicenseAcceptance", RequireLicenseAcceptance));
            }

            if (!string.IsNullOrEmpty(ReleaseNotes))
            {
                metadata.Add(new XElement("releaseNotes", ReleaseNotes));
            }

            if (!string.IsNullOrEmpty(Copyright))
            {
                metadata.Add(new XElement("copyright", Copyright));
            }

            if (!string.IsNullOrEmpty(Tags))
            {
                metadata.Add(new XElement("tags", Tags));
            }

            if (Dependencies.Count > 0)
            {
                //UnityEngine.Debug.Log("Saving dependencies!");
                var dependenciesElement = new XElement("dependencies");
                foreach (var frameworkGroup in Dependencies)
                {
                    var group = new XElement("group");
                    if (!string.IsNullOrEmpty(frameworkGroup.TargetFramework))
                    {
                        group.Add(new XAttribute("targetFramework", frameworkGroup.TargetFramework));
                    }

                    foreach (var dependency in frameworkGroup.Dependencies)
                    {
                        var dependencyElement = new XElement("dependency");
                        dependencyElement.Add(new XAttribute("id", dependency.Id));
                        dependencyElement.Add(new XAttribute("version", dependency.Version));
                        dependenciesElement.Add(dependencyElement);
                    }
                }
                metadata.Add(dependenciesElement);
            }

            file.Root.Add(metadata);

            if (Files.Count > 0)
            {
                //UnityEngine.Debug.Log("Saving files!");
                var filesElement = new XElement("files");
                foreach (var contentFile in Files)
                {
                    var fileElement = new XElement("file");
                    fileElement.Add(new XAttribute("src", contentFile.Source));
                    fileElement.Add(new XAttribute("target", contentFile.Target));
                    filesElement.Add(fileElement);
                }
                packageElement.Add(filesElement);
            }

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

        static string ConvertFromNupkgTargetFrameworkName(string targetFramework)
        {
            var convertedTargetFramework = targetFramework
                .ToLower()
                .Replace(".netstandard", "netstandard")
                .Replace("native0.0", "native");

            convertedTargetFramework = convertedTargetFramework.StartsWith(".netframework") ?
                convertedTargetFramework.Replace(".netframework", "net").Replace(".", "") :
                convertedTargetFramework;

            return convertedTargetFramework;
        }
    }
}