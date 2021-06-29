﻿namespace NugetForUnity
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml.Linq;
    using UnityEditor;

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
        public bool Verbose { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a package is installed from the cache (if present), or if it always downloads the package from the server.
        /// </summary>
        public bool InstallFromCache { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether installed package files are set to read-only.
        /// </summary>
        public bool ReadOnlyPackageFiles { get; set; }

        /// <summary>
        /// The incomplete path that is saved.  The path is expanded and made public via the property above.
        /// </summary>
        private string savedRepositoryPath;

        /// <summary>
        /// Saves this NuGet.config file to disk.
        /// </summary>
        /// <param name="filepath">The filepath to where this NuGet.config will be saved.</param>
        public void Save(string filepath)
        {
            XDocument configFile = new XDocument();

            XElement packageSources = new XElement("packageSources");
            XElement disabledPackageSources = new XElement("disabledPackageSources");
            XElement packageSourceCredentials = new XElement("packageSourceCredentials");

            XElement addElement;

            // save all enabled and disabled package sources 
            foreach (var source in PackageSources)
            {
                addElement = new XElement("add");
                addElement.Add(new XAttribute("key", source.Name));
                addElement.Add(new XAttribute("value", source.SavedPath));
                packageSources.Add(addElement);

                if (!source.IsEnabled)
                {
                    addElement = new XElement("add");
                    addElement.Add(new XAttribute("key", source.Name));
                    addElement.Add(new XAttribute("value", "true"));
                    disabledPackageSources.Add(addElement);
                }

                if (source.HasPassword)
                {
                    XElement sourceElement = new XElement(source.Name);
                    packageSourceCredentials.Add(sourceElement);

                    addElement = new XElement("add");
                    addElement.Add(new XAttribute("key", "userName"));
                    addElement.Add(new XAttribute("value", source.UserName ?? string.Empty));
                    sourceElement.Add(addElement);

                    addElement = new XElement("add");
                    addElement.Add(new XAttribute("key", "clearTextPassword"));
                    addElement.Add(new XAttribute("value", source.SavedPassword));
                    sourceElement.Add(addElement);
                }
            }

            // save the active package source (may be an aggregate)
            XElement activePackageSource = new XElement("activePackageSource");
            addElement = new XElement("add");
            addElement.Add(new XAttribute("key", "All"));
            addElement.Add(new XAttribute("value", "(Aggregate source)"));
            activePackageSource.Add(addElement);

            XElement config = new XElement("config");

            // save the un-expanded respository path
            addElement = new XElement("add");
            addElement.Add(new XAttribute("key", "repositoryPath"));
            addElement.Add(new XAttribute("value", savedRepositoryPath));
            config.Add(addElement);

            // save the default push source
            if (DefaultPushSource != null)
            {
                addElement = new XElement("add");
                addElement.Add(new XAttribute("key", "DefaultPushSource"));
                addElement.Add(new XAttribute("value", DefaultPushSource));
                config.Add(addElement);
            }

            if (Verbose)
            {
                addElement = new XElement("add");
                addElement.Add(new XAttribute("key", "verbose"));
                addElement.Add(new XAttribute("value", Verbose.ToString().ToLower()));
                config.Add(addElement);
            }

            if (!InstallFromCache)
            {
                addElement = new XElement("add");
                addElement.Add(new XAttribute("key", "InstallFromCache"));
                addElement.Add(new XAttribute("value", InstallFromCache.ToString().ToLower()));
                config.Add(addElement);
            }

            if (!ReadOnlyPackageFiles)
            {
                addElement = new XElement("add");
                addElement.Add(new XAttribute("key", "ReadOnlyPackageFiles"));
                addElement.Add(new XAttribute("value", ReadOnlyPackageFiles.ToString().ToLower()));
                config.Add(addElement);
            }

            XElement configuration = new XElement("configuration");
            configuration.Add(packageSources);
            configuration.Add(disabledPackageSources);
            configuration.Add(packageSourceCredentials);
            configuration.Add(activePackageSource);
            configuration.Add(config);

            configFile.Add(configuration);

            bool fileExists = File.Exists(filepath);
            // remove the read only flag on the file, if there is one.
            if (fileExists)
            {
                FileAttributes attributes = File.GetAttributes(filepath);

                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    attributes &= ~FileAttributes.ReadOnly;
                    File.SetAttributes(filepath, attributes);
                }
            }

            configFile.Save(filepath);

            NugetHelper.DisableWSAPExportSetting(filepath, fileExists);
        }

        /// <summary>
        /// Loads a NuGet.config file at the given filepath.
        /// </summary>
        /// <param name="filePath">The full filepath to the NuGet.config file to load.</param>
        /// <returns>The newly loaded <see cref="NugetConfigFile"/>.</returns>
        public static NugetConfigFile Load(string filePath)
        {
            NugetConfigFile configFile = new NugetConfigFile();
            configFile.PackageSources = new List<NugetPackageSource>();
            configFile.InstallFromCache = true;
            configFile.ReadOnlyPackageFiles = false;

            XDocument file = XDocument.Load(filePath);

            // Force disable
            NugetHelper.DisableWSAPExportSetting(filePath, false);

            // read the full list of package sources (some may be disabled below)
            XElement packageSources = file.Root.Element("packageSources");
            if (packageSources != null)
            {
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
                    string disabled = add.Attribute("value").Value;
                    if (String.Equals(disabled, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        var source = configFile.PackageSources.FirstOrDefault(p => p.Name == name);
                        if (source != null)
                        {
                            source.IsEnabled = false;
                        }
                    }
                }
            }

            // set all listed passwords for package source credentials
            XElement packageSourceCredentials = file.Root.Element("packageSourceCredentials");
            if (packageSourceCredentials != null)
            {
                foreach (var sourceElement in packageSourceCredentials.Elements())
                {
                    string name = sourceElement.Name.LocalName;
                    var source = configFile.PackageSources.FirstOrDefault(p => p.Name == name);
                    if (source != null)
                    {
                        var adds = sourceElement.Elements("add");
                        foreach (var add in adds)
                        {
                            if (string.Equals(add.Attribute("key").Value, "userName", StringComparison.OrdinalIgnoreCase))
                            {
                                string userName = add.Attribute("value").Value;
                                source.UserName = userName;
                            }

                            if (string.Equals(add.Attribute("key").Value, "clearTextPassword", StringComparison.OrdinalIgnoreCase))
                            {
                                string password = add.Attribute("value").Value;
                                source.SavedPassword = password;
                            }
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

                    if (String.Equals(key, "repositoryPath", StringComparison.OrdinalIgnoreCase))
                    {
                        configFile.savedRepositoryPath = value;
                        configFile.RepositoryPath = Environment.ExpandEnvironmentVariables(value);

                        if (!Path.IsPathRooted(configFile.RepositoryPath))
                        {
                            string repositoryPath = Path.Combine(UnityEngine.Application.dataPath, configFile.RepositoryPath);
                            repositoryPath = Path.GetFullPath(repositoryPath);

                            configFile.RepositoryPath = repositoryPath;
                        }
                    }
                    else if (String.Equals(key, "DefaultPushSource", StringComparison.OrdinalIgnoreCase))
                    {
                        configFile.DefaultPushSource = value;
                    }
                    else if (String.Equals(key, "verbose", StringComparison.OrdinalIgnoreCase))
                    {
                        configFile.Verbose = bool.Parse(value);
                    }
                    else if (String.Equals(key, "InstallFromCache", StringComparison.OrdinalIgnoreCase))
                    {
                        configFile.InstallFromCache = bool.Parse(value);
                    }
                    else if (String.Equals(key, "ReadOnlyPackageFiles", StringComparison.OrdinalIgnoreCase))
                    {
                        configFile.ReadOnlyPackageFiles = bool.Parse(value);
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
       <clear/>
       <add key=""NuGet"" value=""http://www.nuget.org/api/v2/"" />
    </packageSources>
    <disabledPackageSources />
    <activePackageSource>
       <add key=""All"" value=""(Aggregate source)"" />
    </activePackageSource>
    <config>
       <add key=""repositoryPath"" value=""./Packages"" />
       <add key=""DefaultPushSource"" value=""http://www.nuget.org/api/v2/"" />
    </config>
</configuration>";

            File.WriteAllText(filePath, contents, new UTF8Encoding());

            AssetDatabase.Refresh();

            NugetHelper.DisableWSAPExportSetting(filePath, false);

            return Load(filePath);
        }
    }
}