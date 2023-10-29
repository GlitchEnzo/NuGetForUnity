#pragma warning disable SA1512,SA1124 // Single-line comments should not be followed by blank line

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using JetBrains.Annotations;
using NugetForUnity.Models;
using NugetForUnity.PluginAPI.Models;
using UnityEngine;

#region No ReShaper

// ReSharper disable All
// needed because 'JetBrains.Annotations.NotNull' and 'System.Diagnostics.CodeAnalysis.NotNull' collide if this file is compiled with a never version of Unity / C#
using SuppressMessageAttribute = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;

// ReSharper restore All

#endregion

#pragma warning restore SA1512,SA1124 // Single-line comments should not be followed by blank line

namespace NugetForUnity
{
    /// <summary>
    ///     Represents a .nuspec file used to store metadata for a NuGet package.
    /// </summary>
    /// <remarks>
    ///     At a minimum, Id, Version, Description, and Authors is required.  Everything else is optional.
    ///     See more info here: https://docs.microsoft.com/en-us/nuget/schema/nuspec.
    /// </remarks>
    public class NuspecFile : NugetPackageIdentifier, INuspecFile
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="NuspecFile" /> class.
        /// </summary>
        public NuspecFile()
        {
            Dependencies = new List<NugetFrameworkGroup>();
            Files = new List<NuspecContentFile>();
        }

        /// <summary>
        ///     Project can register its own method that sets default values for newly created nuspec file.
        /// </summary>
        public static event Action<NuspecFile> ProjectSpecificNuspecInitializer;

        /// <summary>
        ///     Gets or sets the source control branch the package is from.
        /// </summary>
        public string RepositoryBranch { get; set; }

        /// <summary>
        ///     Gets or sets the source control commit the package is from.
        /// </summary>
        public string RepositoryCommit { get; set; }

        /// <summary>
        ///     Gets or sets the type of source control software that the package's source code resides in.
        /// </summary>
        public string RepositoryType { get; set; }

        /// <summary>
        ///     Gets or sets the url for the location of the package's source code.
        /// </summary>
        public string RepositoryUrl { get; set; }

        /// <summary>
        ///     Gets or sets the title of the NuGet package.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        ///     Gets or sets the owners of the NuGet package.
        /// </summary>
        public string Owners { get; set; }

        /// <summary>
        ///     Gets or sets the URL for the location of the license of the NuGet package.
        /// </summary>
        public string LicenseUrl { get; set; }

        /// <summary>
        ///     Gets or sets the URL for the location of the project web-page of the NuGet package.
        /// </summary>
        public string ProjectUrl { get; set; }

        /// <summary>
        ///     Gets or sets the URL for the location of the icon of the NuGet package.
        /// </summary>
        public string IconUrl { get; set; }

        /// <summary>
        ///     Gets the path to a icon file. The path is relative to the root folder of the package. This is a alternative to using a URL <see cref="IconUrl" />
        ///     .
        /// </summary>
        public string Icon { get; private set; }

        /// <summary>
        ///     Gets the full path to a icon file. This is only set if the .nuspec file contains a <see cref="Icon" />. This is a alternative to using a URL
        ///     <see cref="IconUrl" />.
        /// </summary>
        public string IconFilePath { get; private set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the license of the NuGet package needs to be accepted in order to use it.
        /// </summary>
        public bool RequireLicenseAcceptance { get; set; }

        /// <summary>
        ///     Gets the NuGet packages that this NuGet package depends on.
        /// </summary>
        [NotNull]
        public List<NugetFrameworkGroup> Dependencies { get; }

        /// <summary>
        ///     Gets or sets the release notes of the NuGet package.
        /// </summary>
        public string ReleaseNotes { get; set; }

        /// <summary>
        ///     Gets or sets the copyright of the NuGet package.
        /// </summary>
        public string Copyright { get; set; }

        /// <summary>
        ///     Gets or sets the tags of the NuGet package.
        /// </summary>
        public string Tags { get; set; }

        /// <summary>
        ///     Gets the list of content files listed in the .nuspec file.
        /// </summary>
        [NotNull]
        public List<NuspecContentFile> Files { get; }

        /// <summary>
        ///     Gets or sets the description of the NuGet package.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        ///     Gets or sets the description of the NuGet package.
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        ///     Gets or sets the authors of the NuGet package.
        /// </summary>
        public string Authors { get; set; } = string.Empty;

        /// <summary>
        ///     Creates a new nuspec file with default values for a new package.
        /// </summary>
        /// <param name="packageName">Optional name for the new package. Defaults to "MyPackage".</param>
        /// <returns>Newly created nuspec file.</returns>
        public static NuspecFile CreateDefault(string packageName = "MyPackage")
        {
            var result = new NuspecFile
            {
                Id = "CompanyName." + packageName,
                Title = packageName,
                Version = "0.0.1",
                Authors = "Your Name",
                Owners = "Your Name",
                LicenseUrl = "http://your_license_url_here",
                ProjectUrl = "http://your_project_url_here",
                Description = "A description of what this package is and does.",
                Summary = "A brief description of what this package is and does.",
                ReleaseNotes = "Notes for this specific release",
                Copyright = "Copyright 2017",
                IconUrl = "https://www.nuget.org/Content/Images/packageDefaultIcon-50x50.png",
            };

            ProjectSpecificNuspecInitializer?.Invoke(result);

            return result;
        }

        /// <summary>
        ///     Loads the .nuspec file inside the .nupkg file at the given file path.
        /// </summary>
        /// <param name="nupkgFilePath">The file path to the .nupkg file to load.</param>
        /// <returns>The .nuspec file loaded from inside the .nupkg file.</returns>
        [NotNull]
        public static NuspecFile FromNupkgFile([NotNull] string nupkgFilePath)
        {
            var nuspec = new NuspecFile();

            try
            {
                // get the .nuspec file from inside the .nupkg
                using (var zip = ZipFile.OpenRead(nupkgFilePath))
                {
                    var entry = zip.Entries.First(x => x.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));

                    using (var stream = entry.Open())
                    {
                        nuspec = Load(stream).SetIconFilePath(nupkgFilePath);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogErrorFormat("Package could not be read: {0}", nupkgFilePath);
                nuspec.Description = $"COULD NOT LOAD {nupkgFilePath}";
            }

            return nuspec;
        }

        /// <summary>
        ///     Loads a .nuspec file at the given file path.
        /// </summary>
        /// <param name="filePath">The full file path to the .nuspec file to load.</param>
        /// <returns>The newly loaded <see cref="NuspecFile" />.</returns>
        [NotNull]
        public static NuspecFile Load([NotNull] string filePath)
        {
            return Load(XDocument.Load(filePath)).SetIconFilePath(filePath);
        }

        /// <summary>
        ///     Loads a .nuspec file inside the given stream.
        /// </summary>
        /// <param name="stream">The stream containing the .nuspec file to load.</param>
        /// <returns>The newly loaded <see cref="NuspecFile" />.</returns>
        [NotNull]
        public static NuspecFile Load([NotNull] Stream stream)
        {
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings { XmlResolver = null }))
            {
                var document = XDocument.Load(reader);
                return Load(document);
            }
        }

        /// <summary>
        ///     Loads a .nuspec file inside the given <see cref="XDocument" />.
        /// </summary>
        /// <param name="nuspecDocument">The .nuspec file as an <see cref="XDocument" />.</param>
        /// <returns>The newly loaded <see cref="NuspecFile" />.</returns>
        [NotNull]
        public static NuspecFile Load([NotNull] XDocument nuspecDocument)
        {
            var nuspec = new NuspecFile();

            var nuspecNamespace = nuspecDocument.Root?.GetDefaultNamespace().ToString() ?? string.Empty;

            var package = nuspecDocument.Element(XName.Get("package", nuspecNamespace));
            var metadata = package?.Element(XName.Get("metadata", nuspecNamespace)) ??
                           throw new InvalidOperationException("There is on package->metadata element inside the nuspec file.");

            nuspec.Id = (string)metadata.Element(XName.Get("id", nuspecNamespace)) ?? string.Empty;
            nuspec.Version = (string)metadata.Element(XName.Get("version", nuspecNamespace)) ?? string.Empty;
            nuspec.Title = (string)metadata.Element(XName.Get("title", nuspecNamespace)) ?? string.Empty;
            nuspec.Authors = (string)metadata.Element(XName.Get("authors", nuspecNamespace)) ?? string.Empty;
            nuspec.Owners = (string)metadata.Element(XName.Get("owners", nuspecNamespace)) ?? string.Empty;
            nuspec.LicenseUrl = (string)metadata.Element(XName.Get("licenseUrl", nuspecNamespace)) ?? string.Empty;
            nuspec.ProjectUrl = (string)metadata.Element(XName.Get("projectUrl", nuspecNamespace)) ?? string.Empty;
            nuspec.IconUrl = (string)metadata.Element(XName.Get("iconUrl", nuspecNamespace)) ?? string.Empty;
            nuspec.Icon = (string)metadata.Element(XName.Get("icon", nuspecNamespace)) ?? string.Empty;
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
                    var group = new NugetFrameworkGroup
                    {
                        TargetFramework =
                            ConvertFromNupkgTargetFrameworkName((string)frameworkGroup.Attribute("targetFramework") ?? string.Empty),
                    };

                    foreach (var dependencyElement in frameworkGroup.Elements(XName.Get("dependency", nuspecNamespace)))
                    {
                        var id = (string)dependencyElement.Attribute("id") ?? string.Empty;
                        var version = (string)dependencyElement.Attribute("version") ?? string.Empty;
                        var dependency = new NugetPackageIdentifier(id, version);
                        group.Dependencies.Add(dependency);
                    }

                    nuspec.Dependencies.Add(group);
                }

                // Flat dependency list
                if (nuspec.Dependencies.Count == 0)
                {
                    var group = new NugetFrameworkGroup();
                    foreach (var dependencyElement in dependenciesElement.Elements(XName.Get("dependency", nuspecNamespace)))
                    {
                        var id = (string)dependencyElement.Attribute("id") ?? string.Empty;
                        var version = (string)dependencyElement.Attribute("version") ?? string.Empty;
                        var dependency = new NugetPackageIdentifier(id, version);
                        group.Dependencies.Add(dependency);
                    }

                    nuspec.Dependencies.Add(group);
                }
            }

            var filesElement = package.Element(XName.Get("files", nuspecNamespace));
            if (filesElement != null)
            {
                foreach (var fileElement in filesElement.Elements(XName.Get("file", nuspecNamespace)))
                {
                    var file = new NuspecContentFile
                    {
                        Source = (string)fileElement.Attribute("src") ?? string.Empty,
                        Target = (string)fileElement.Attribute("target") ?? string.Empty,
                    };
                    nuspec.Files.Add(file);
                }
            }

            return nuspec;
        }

        /// <summary>
        ///     Full filename, including full path, of this NuGet package's file in a local NuGet repository.
        /// </summary>
        /// <remarks>
        ///     The existence of the file is verified.
        /// </remarks>
        /// <param name="baseDirectoryPath">Path to the local repository's root directory.</param>
        /// <returns>The full path to the file, if it exists in the repository, or <c>null</c> otherwise.</returns>
        [CanBeNull]
        public string GetLocalPackageFilePath([NotNull] string baseDirectoryPath)
        {
            // Find this package's file in the repository.
            var files = Directory.GetFiles(baseDirectoryPath, PackageFileName, SearchOption.AllDirectories);

            // If we found any, return the first found; otherwise return null.
            return files.FirstOrDefault();
        }

        /// <summary>
        ///     Saves a <see cref="NuspecFile" /> to the given file-path, automatically overwriting.
        /// </summary>
        /// <param name="filePath">The full file-path to the .nuspec file to save.</param>
        public void Save([NotNull] string filePath)
        {
            var file = new XDocument();
            var packageElement = new XElement("package");
            file.Add(packageElement);
            var metadata = new XElement("metadata");

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
                var attributes = File.GetAttributes(filePath);

                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    attributes &= ~FileAttributes.ReadOnly;
                    File.SetAttributes(filePath, attributes);
                }
            }

            file.Save(filePath);
        }

        [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "We intentionally use lower case.")]
        [NotNull]
        private static string ConvertFromNupkgTargetFrameworkName([NotNull] string targetFramework)
        {
            var convertedTargetFramework = targetFramework.ToLowerInvariant().Replace(".netstandard", "netstandard").Replace("native0.0", "native");

            convertedTargetFramework = convertedTargetFramework.StartsWith(".netframework", StringComparison.Ordinal) ?
                convertedTargetFramework.Replace(".netframework", "net").Replace(".", string.Empty) :
                convertedTargetFramework;

            return convertedTargetFramework;
        }

        [NotNull]
        private NuspecFile SetIconFilePath([NotNull] string containingFilePath)
        {
            if (!string.IsNullOrEmpty(Icon))
            {
                IconFilePath = Path.Combine(
                    Path.GetDirectoryName(Path.GetFullPath(containingFilePath)) ??
                    throw new ArgumentException($"Path doesn't contain a directory name '{containingFilePath}'.", nameof(containingFilePath)),
                    Icon);
            }

            return this;
        }
    }
}
