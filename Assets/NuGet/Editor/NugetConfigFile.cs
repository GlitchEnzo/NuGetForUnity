namespace NugetForUnity
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml.Linq;

    /// <summary>
    /// Represents a NuGet.config file that stores the NuGet settings.
    /// See here: https://docs.nuget.org/consume/nuget-config-file
    /// </summary>
    public class NugetConfigFile
    {
        /// <summary>
        /// Gets the list of package sources that are defined in the NuGet.config file.
        /// </summary>
        public List<NugetPackageSource> PackageSources { get; private set; }

        /// <summary>
        /// Gets the currectly active package source that is defined in the NuGet.config file.
        /// Note: If the key/Name is set to "All" and the value/Path is set to "(Aggregate source)", all package sources are used.
        /// </summary>
        public NugetPackageSource ActivePackageSource { get; private set; }

        /// <summary>
        /// Gets the local path where packages are to be installed.  It can be a full path or a relative path.
        /// </summary>
        public string RepositoryPath { get; private set; }

        /// <summary>
        /// Gets the default package source to push NuGet packages to.
        /// </summary>
        public string DefaultPushSource { get; private set; }

        /// <summary>
        /// True to output verbose log messages to the console.  False to output the normal level of messages.
        /// </summary>
        public bool Verbose { get; private set; }

        /// <summary>
        /// Loads a NuGet.config file at the given filepath.
        /// </summary>
        /// <param name="filePath">The full filepath to the NuGet.config file to load.</param>
        /// <returns>The newly loaded <see cref="NugetConfigFile"/>.</returns>
        public static NugetConfigFile Load(string filePath)
        {
            NugetConfigFile configFile = new NugetConfigFile();

            XDocument file = XDocument.Load(filePath);

            // read the full list of package sources (some may be disabled below)
            XElement packageSources = file.Root.Element("packageSources");
            if (packageSources != null)
            {
                configFile.PackageSources = new List<NugetPackageSource>();

                var adds = packageSources.Elements("add");
                foreach (var add in adds)
                {
                    configFile.PackageSources.Add(new NugetPackageSource(add.Attribute("key").Value, add.Attribute("value").Value));
                }
            }

            // read the active package source (may be an aggregate of all enabled sources!)
            XElement activePackageSource = file.Root.Element("activePackageSource");
            if (activePackageSource != null)
            {
                var add = activePackageSource.Element("add");
                configFile.ActivePackageSource = new NugetPackageSource(add.Attribute("key").Value, add.Attribute("value").Value);
            }

            // disable all listed disabled package sources
            XElement disabledPackageSources = file.Root.Element("disabledPackageSources");
            if (disabledPackageSources != null)
            {
                var adds = disabledPackageSources.Elements("add");
                foreach (var add in adds)
                {
                    string name = add.Attribute("key").Value;
                    string enabled = add.Attribute("value").Value;
                    if (enabled == "true")
                    {
                        var source = configFile.PackageSources.FirstOrDefault(p => p.Name == name);
                        if (source != null)
                        {
                            source.IsEnabled = false;
                        }
                    }
                }
            }

            // read the configuration data
            XElement config = file.Root.Element("config");
            if (config != null)
            {
                var adds = config.Elements("add");
                foreach (var add in adds)
                {
                    string key = add.Attribute("key").Value;
                    string value = add.Attribute("value").Value;

                    if (key == "repositoryPath")
                    {
                        configFile.RepositoryPath = value;

                        if (!Path.IsPathRooted(configFile.RepositoryPath))
                        {
                            if (configFile.RepositoryPath.StartsWith("./"))
                            {
                                // since ./ is just a relative path to the same directory, replace it
                                configFile.RepositoryPath = configFile.RepositoryPath.Replace("./", "\\");
                            }

                            configFile.RepositoryPath = Path.GetFullPath((UnityEngine.Application.dataPath + configFile.RepositoryPath).Replace('/', '\\'));
                        }
                    }
                    else if (key == "DefaultPushSource")
                    {
                        configFile.DefaultPushSource = value;
                    }
                    else if (key == "verbose")
                    {
                        configFile.Verbose = bool.Parse(value);
                    }
                }
            }

            return configFile;
        }

        /// <summary>
        /// Creates a NuGet.config file with the default settings at the given full filepath.
        /// </summary>
        /// <param name="filePath">The full filepath where to create the NuGet.config file.</param>
        /// <returns>The loaded <see cref="NugetConfigFile"/> loaded off of the newly created default file.</returns>
        public static NugetConfigFile CreateDefaultFile(string filePath)
        {
            const string contents =
@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
    <add key=""NuGet"" value=""http://www.nuget.org/api/v2/"" />
    </packageSources>
    <activePackageSource>
    <add key=""NuGet"" value=""http://www.nuget.org/api/v2/"" />
    </activePackageSource>
    <config>
    <add key=""repositoryPath"" value=""./Packages"" />
    <add key=""DefaultPushSource"" value=""http://www.nuget.org/api/v2/"" />
    </config>
</configuration>";

            File.WriteAllText(filePath, contents, new UTF8Encoding());

            return Load(filePath);
        }
    }
}