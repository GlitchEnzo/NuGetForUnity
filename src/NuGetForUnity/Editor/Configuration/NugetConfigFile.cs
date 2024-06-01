#pragma warning disable SA1512,SA1124 // Single-line comments should not be followed by blank line

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using JetBrains.Annotations;
using NugetForUnity.Helper;
using NugetForUnity.PackageSource;
using UnityEngine;

#region No ReShaper

// ReSharper disable All
// needed because 'JetBrains.Annotations.NotNull' and 'System.Diagnostics.CodeAnalysis.NotNull' collide if this file is compiled with a never version of Unity / C#
using SuppressMessageAttribute = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;

// ReSharper restore All

#endregion

#pragma warning restore SA1512,SA1124 // Single-line comments should not be followed by blank line

namespace NugetForUnity.Configuration
{
    /// <summary>
    ///     Represents a NuGet.config file that stores the NuGet settings.
    ///     See here: https://docs.nuget.org/consume/nuget-config-file.
    /// </summary>
    public class NugetConfigFile
    {
        /// <summary>
        ///     The file name where the configuration is stored.
        /// </summary>
        public const string FileName = "NuGet.config";

        /// <summary>
        ///     The name of the attribute that is used to configure <see cref="NugetPackageSourceV3.PackageDownloadUrlTemplateOverwrite" /> of
        ///     <see cref="NugetPackageSourceV3" />.
        /// </summary>
        internal const string PackageDownloadUrlTemplateOverwriteAttributeName = "packageDownloadUrlTemplateOverwrite";

        /// <summary>
        ///     Default timeout in seconds for all web requests.
        /// </summary>
        private const int DefaultRequestTimeout = 10;

        private const string RequestTimeoutSecondsConfigKey = "RequestTimeoutSeconds";

        private const string PackagesConfigDirectoryPathConfigKey = "PackagesConfigDirectoryPath";

        private const string PreferNetStandardOverNetFrameworkConfigKey = "PreferNetStandardOverNetFramework";

        private const string ProtocolVersionAttributeName = "protocolVersion";

        private const string PasswordAttributeName = "password";

        private const string UpdateSearchBatchSizeAttributeName = "updateSearchBatchSize";

        private const string SupportsPackageIdSearchFilterAttributeName = "supportsPackageIdSearchFilter";

        [NotNull]
        private readonly string unityPackagesNugetInstallPath = Path.Combine(UnityPathHelper.AbsoluteUnityPackagesNugetPath, "InstalledPackages");

        [NotNull]
        private string configuredRepositoryPath = "Packages";

        [NotNull]
        private string packagesConfigDirectoryPath = Application.dataPath;

        [NotNull]
        private string repositoryPath = Path.GetFullPath(Path.Combine(Application.dataPath, "Packages"));

        /// <summary>
        ///     Gets the list of package sources that are defined in the NuGet.config file.
        /// </summary>
        /// <remarks>
        ///     The NuGet server protocol version defaults to version "2" when not pointing to a package source URL ending in .json (e.g.
        ///     https://api.nuget.org/v3/index.json).
        /// </remarks>
        [NotNull]
        public List<INugetPackageSource> PackageSources { get; } = new List<INugetPackageSource>();

        /// <summary>
        ///     Gets the currently active package source that is defined in the NuGet.config file.
        ///     Note: If the key/Name is set to "All" and the value/Path is set to "(Aggregate source)", all package sources are used.
        /// </summary>
        [CanBeNull]
        public INugetPackageSource ActivePackageSource { get; private set; }

        /// <summary>
        ///     Gets the absolute path where packages are to be installed.
        /// </summary>
        [NotNull]
        public string RepositoryPath
        {
            get => InstallLocation == PackageInstallLocation.InPackagesFolder ? unityPackagesNugetInstallPath : repositoryPath;

            private set => repositoryPath = value;
        }

        /// <summary>
        ///     Gets or sets the incomplete path that is saved.  The path is expanded and made public via the property above.
        /// </summary>
        [NotNull]
        public string ConfiguredRepositoryPath
        {
            get => configuredRepositoryPath;

            set
            {
                configuredRepositoryPath = value;

                var expandedPath = Environment.ExpandEnvironmentVariables(value);

                if (!Path.IsPathRooted(expandedPath))
                {
                    expandedPath = Path.Combine(Application.dataPath, expandedPath);
                }

                RepositoryPath = Path.GetFullPath(expandedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }
        }

        /// <summary>
        ///     Gets the default package source to push NuGet packages to.
        /// </summary>
        [CanBeNull]
        public string DefaultPushSource { get; private set; }

        /// <summary>
        ///     Gets or sets a value indicating whether to output verbose log messages to the console. False to output the normal level of messages.
        /// </summary>
        public bool Verbose { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether to skip installing dependencies and checking for pre-imported Unity libs
        ///     while auto-restoring.
        /// </summary>
        public bool SlimRestore { get; set; } = true;

        /// <summary>
        ///     Gets or sets a value indicating whether a package is installed from the cache (if present), or if it always downloads the package from the
        ///     server.
        /// </summary>
        public bool InstallFromCache { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether installed package files are set to read-only.
        /// </summary>
        public bool ReadOnlyPackageFiles { get; set; }

        /// <summary>
        ///     Gets or sets absolute path to directory containing packages.config file.
        /// </summary>
        [NotNull]
        public string PackagesConfigDirectoryPath
        {
            get =>
                InstallLocation == PackageInstallLocation.InPackagesFolder ?
                    UnityPathHelper.AbsoluteUnityPackagesNugetPath :
                    packagesConfigDirectoryPath;

            set => packagesConfigDirectoryPath = value;
        }

        /// <summary>
        ///     Gets the relative path to directory containing packages.config file. The path is relative to the folder containing the 'NuGet.config' file.
        /// </summary>
        [NotNull]
        public string RelativePackagesConfigDirectoryPath
        {
            get => PathHelper.GetRelativePath(Application.dataPath, PackagesConfigDirectoryPath);
            private set => PackagesConfigDirectoryPath = Path.GetFullPath(Path.Combine(Application.dataPath, value));
        }

        /// <summary>
        ///     Gets the path to the packages.config file.
        /// </summary>
        [NotNull]
        public string PackagesConfigFilePath => Path.Combine(PackagesConfigDirectoryPath, PackagesConfigFile.FileName);

        /// <summary>
        ///     Gets or sets the timeout in seconds used for all web requests to NuGet sources.
        /// </summary>
        public int RequestTimeoutSeconds { get; set; } = DefaultRequestTimeout;

        /// <summary>
        ///     Gets or sets the value indicating whether .NET Standard is preferred over .NET Framework as the TargetFramework.
        /// </summary>
        public bool PreferNetStandardOverNetFramework { get; set; }

        /// <summary>
        ///     Gets the value that tells the system how to determine where the packages are to be installed and configurations are to be stored.
        /// </summary>
        internal PackageInstallLocation InstallLocation { get; private set; }

        /// <summary>
        ///     Gets the list of enabled plugins.
        /// </summary>
        internal List<NugetForUnityPluginId> EnabledPlugins { get; } = new List<NugetForUnityPluginId>();

        /// <summary>
        ///     Loads a NuGet.config file at the given file-path.
        /// </summary>
        /// <param name="filePath">The full file-path to the NuGet.config file to load.</param>
        /// <returns>The newly loaded <see cref="NugetConfigFile" />.</returns>
        [NotNull]
        public static NugetConfigFile Load([NotNull] string filePath)
        {
            var configFile = new NugetConfigFile
            {
                InstallFromCache = true, ReadOnlyPackageFiles = false, RelativePackagesConfigDirectoryPath = ".",
            };

            var file = XDocument.Load(filePath);

            // read the full list of package sources (some may be disabled below)
            var packageSources = file.Root?.Element("packageSources");
            if (packageSources != null)
            {
                var adds = packageSources.Elements("add");
                foreach (var add in adds)
                {
                    var name = add.Attribute("key")?.Value ??
                               throw new InvalidOperationException($"packageSources misses 'key' attribute. Element:\n{add}");
                    var path = add.Attribute("value")?.Value ??
                               throw new InvalidOperationException($"packageSources misses 'value' attribute. Element:\n{add}");
                    var protocolVersion = add.Attribute(ProtocolVersionAttributeName)?.Value;
                    var newPackageSource = NugetPackageSourceCreator.CreatePackageSource(name, path, protocolVersion, null);
                    if (newPackageSource is NugetPackageSourceV3 sourceV3)
                    {
                        sourceV3.PackageDownloadUrlTemplateOverwrite = add.Attribute(PackageDownloadUrlTemplateOverwriteAttributeName)?.Value;

                        var updateSearchBatchSizeString = add.Attribute(UpdateSearchBatchSizeAttributeName)?.Value;
                        if (!string.IsNullOrEmpty(updateSearchBatchSizeString))
                        {
                            sourceV3.UpdateSearchBatchSize = Mathf.Clamp(int.Parse(updateSearchBatchSizeString), 1, int.MaxValue);
                        }

                        var supportsPackageIdSearchFilterString = add.Attribute(SupportsPackageIdSearchFilterAttributeName)?.Value;
                        if (bool.TryParse(supportsPackageIdSearchFilterString, out var supportsPackageIdSearchFilter))
                        {
                            sourceV3.SupportsPackageIdSearchFilter = supportsPackageIdSearchFilter;
                        }
                    }

                    configFile.PackageSources.Add(newPackageSource);
                }
            }

            // read the active package source (may be an aggregate of all enabled sources!)
            var activePackageSource = file.Root?.Element("activePackageSource");
            if (activePackageSource != null)
            {
                var add = activePackageSource.Element("add") ??
                          throw new InvalidOperationException($"activePackageSource misses 'add' element. Element:\n{activePackageSource}");
                var name = add.Attribute("key")?.Value ??
                           throw new InvalidOperationException($"activePackageSource misses 'key' attribute. Element:\n{add}");
                var path = add.Attribute("value")?.Value ??
                           throw new InvalidOperationException($"activePackageSource misses 'value' attribute. Element:\n{add}");
                configFile.ActivePackageSource = NugetPackageSourceCreator.CreatePackageSource(name, path, null, configFile.PackageSources);
            }

            // disable all listed disabled package sources
            var disabledPackageSources = file.Root?.Element("disabledPackageSources");
            if (disabledPackageSources != null)
            {
                var adds = disabledPackageSources.Elements("add");
                foreach (var add in adds)
                {
                    var name = add.Attribute("key")?.Value;
                    var disabled = add.Attribute("value")?.Value;
                    if (string.Equals(disabled, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        var source = configFile.PackageSources.Find(p => p.Name == name);
                        if (source != null)
                        {
                            source.IsEnabled = false;
                        }
                    }
                }
            }

            // read the list of enabled plugins
            var enabledPlugins = file.Root?.Element("enabledPlugins");
            if (enabledPlugins != null)
            {
                foreach (var add in enabledPlugins.Elements("add"))
                {
                    var name = add.Attribute("name")?.Value;
                    var path = add.Attribute("path")?.Value;
                    if (name != null && path != null)
                    {
                        configFile.EnabledPlugins.Add(new NugetForUnityPluginId(name, path));
                    }
                }
            }

            // set all listed passwords for package source credentials
            var packageSourceCredentials = file.Root?.Element("packageSourceCredentials");
            if (packageSourceCredentials != null)
            {
                foreach (var sourceElement in packageSourceCredentials.Elements())
                {
                    var name = XmlConvert.DecodeName(sourceElement.Name.LocalName);
                    var source = configFile.PackageSources.Find(p => p.Name == name);
                    if (source == null)
                    {
                        continue;
                    }

                    var adds = sourceElement.Elements("add");
                    foreach (var add in adds)
                    {
                        var keyName = add.Attribute("key")?.Value;
                        if (string.Equals(keyName, "userName", StringComparison.OrdinalIgnoreCase))
                        {
                            var userName = add.Attribute("value")?.Value;
                            source.UserName = userName;
                        }
                        else if (string.Equals(keyName, "clearTextPassword", StringComparison.OrdinalIgnoreCase))
                        {
                            var password = add.Attribute("value")?.Value;
                            source.SavedPassword = password;
                            source.SavedPasswordIsEncrypted = false;
                        }
                        else if (string.Equals(keyName, PasswordAttributeName, StringComparison.OrdinalIgnoreCase))
                        {
                            var encryptedPassword = add.Attribute("value")?.Value;
                            source.SavedPassword = encryptedPassword;
                            source.SavedPasswordIsEncrypted = true;
                        }
                    }
                }
            }

            // read the configuration data
            var config = file.Root?.Element("config");
            if (config == null)
            {
                return configFile;
            }

            var addElements = config.Elements("add");
            foreach (var add in addElements)
            {
                var key = add.Attribute("key")?.Value;
                var value = add.Attribute("value")?.Value ?? throw new InvalidOperationException($"config misses 'value' attribute. Element:\n{add}");

                if (string.Equals(key, "packageInstallLocation", StringComparison.OrdinalIgnoreCase))
                {
                    configFile.InstallLocation = (PackageInstallLocation)Enum.Parse(typeof(PackageInstallLocation), value);
                }
                else if (string.Equals(key, "repositoryPath", StringComparison.OrdinalIgnoreCase))
                {
                    configFile.ConfiguredRepositoryPath = value;
                }
                else if (string.Equals(key, "DefaultPushSource", StringComparison.OrdinalIgnoreCase))
                {
                    configFile.DefaultPushSource = value;
                }
                else if (string.Equals(key, "verbose", StringComparison.OrdinalIgnoreCase))
                {
                    configFile.Verbose = bool.Parse(value);
                }
                else if (string.Equals(key, "slimRestore", StringComparison.OrdinalIgnoreCase))
                {
                    configFile.SlimRestore = bool.Parse(value);
                }
                else if (string.Equals(key, "InstallFromCache", StringComparison.OrdinalIgnoreCase))
                {
                    configFile.InstallFromCache = bool.Parse(value);
                }
                else if (string.Equals(key, "ReadOnlyPackageFiles", StringComparison.OrdinalIgnoreCase))
                {
                    configFile.ReadOnlyPackageFiles = bool.Parse(value);
                }
                else if (string.Equals(key, RequestTimeoutSecondsConfigKey, StringComparison.OrdinalIgnoreCase))
                {
                    configFile.RequestTimeoutSeconds = int.Parse(value, CultureInfo.InvariantCulture);
                }
                else if (string.Equals(key, PackagesConfigDirectoryPathConfigKey, StringComparison.OrdinalIgnoreCase))
                {
                    configFile.RelativePackagesConfigDirectoryPath = value;
                }
                else if (string.Equals(key, PreferNetStandardOverNetFrameworkConfigKey, StringComparison.OrdinalIgnoreCase))
                {
                    configFile.PreferNetStandardOverNetFramework = bool.Parse(value);
                }
            }

            return configFile;
        }

        /// <summary>
        ///     Creates a NuGet.config file with the default settings at the given full file-path.
        /// </summary>
        /// <param name="filePath">The full file-path where to create the NuGet.config file.</param>
        /// <returns>The loaded <see cref="NugetConfigFile" /> loaded off of the newly created default file.</returns>
        [NotNull]
        public static NugetConfigFile CreateDefaultFile([NotNull] string filePath)
        {
            const string contents = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <clear />
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
  </packageSources>
  <disabledPackageSources />
  <activePackageSource>
    <add key=""All"" value=""(Aggregate source)"" />
  </activePackageSource>
  <config>
    <add key=""packageInstallLocation"" value=""CustomWithinAssets"" />
    <add key=""repositoryPath"" value=""./Packages"" />
    <add key=""PackagesConfigDirectoryPath"" value=""."" />
    <add key=""slimRestore"" value=""true"" />
    <add key=""PreferNetStandardOverNetFramework"" value=""true"" />
  </config>
</configuration>";

            File.WriteAllText(filePath, contents, new UTF8Encoding());

            return Load(filePath);
        }

        /// <summary>
        ///     Saves this NuGet.config file to disk.
        /// </summary>
        /// <param name="filePath">The file-path to where this NuGet.config will be saved.</param>
        [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "We intentionally use lower case.")]
        public void Save([NotNull] string filePath)
        {
            var configFile = new XDocument();

            var packageSources = new XElement("packageSources");
            var disabledPackageSources = new XElement("disabledPackageSources");
            var packageSourceCredentials = new XElement("packageSourceCredentials");

            XElement addElement;

            // save all enabled and disabled package sources
            foreach (var source in PackageSources)
            {
                addElement = new XElement("add");
                addElement.Add(new XAttribute("key", source.Name));
                addElement.Add(new XAttribute("value", source.SavedPath));
                if (!string.IsNullOrEmpty(source.SavedProtocolVersion))
                {
                    addElement.Add(new XAttribute(ProtocolVersionAttributeName, source.SavedProtocolVersion));
                }

                if (source is NugetPackageSourceV3 sourceV3)
                {
                    if (!string.IsNullOrEmpty(sourceV3.PackageDownloadUrlTemplateOverwrite))
                    {
                        addElement.Add(
                            new XAttribute(PackageDownloadUrlTemplateOverwriteAttributeName, sourceV3.PackageDownloadUrlTemplateOverwrite));
                    }

                    if (sourceV3.UpdateSearchBatchSize != NugetPackageSourceV3.DefaultUpdateSearchBatchSize)
                    {
                        addElement.Add(new XAttribute(UpdateSearchBatchSizeAttributeName, sourceV3.UpdateSearchBatchSize));
                    }

                    if (sourceV3.SupportsPackageIdSearchFilter != NugetPackageSourceV3.DefaultSupportsPackageIdSearchFilter)
                    {
                        addElement.Add(new XAttribute(SupportsPackageIdSearchFilterAttributeName, sourceV3.SupportsPackageIdSearchFilter));
                    }
                }

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
                    var sourceElement = new XElement(XmlConvert.EncodeName(source.Name) ?? string.Empty);
                    packageSourceCredentials.Add(sourceElement);

                    addElement = new XElement("add");
                    addElement.Add(new XAttribute("key", "userName"));
                    addElement.Add(new XAttribute("value", source.UserName ?? string.Empty));
                    sourceElement.Add(addElement);

                    addElement = new XElement("add");
                    addElement.Add(new XAttribute("key", source.SavedPasswordIsEncrypted ? PasswordAttributeName : "clearTextPassword"));
                    addElement.Add(new XAttribute("value", source.SavedPassword ?? string.Empty));
                    sourceElement.Add(addElement);
                }
            }

            // save the active package source (may be an aggregate)
            var activePackageSource = new XElement("activePackageSource");
            addElement = new XElement("add");
            addElement.Add(new XAttribute("key", "All"));
            addElement.Add(new XAttribute("value", "(Aggregate source)"));
            activePackageSource.Add(addElement);

            var config = new XElement("config");

            addElement = new XElement("add");
            addElement.Add(new XAttribute("key", "packageInstallLocation"));
            addElement.Add(new XAttribute("value", InstallLocation.ToString()));
            config.Add(addElement);

            if (!string.IsNullOrEmpty(ConfiguredRepositoryPath))
            {
                // save the un-expanded repository path
                addElement = new XElement("add");
                addElement.Add(new XAttribute("key", "repositoryPath"));
                addElement.Add(new XAttribute("value", ConfiguredRepositoryPath));
                config.Add(addElement);
            }

            addElement = new XElement("add");
            addElement.Add(new XAttribute("key", PackagesConfigDirectoryPathConfigKey));
            addElement.Add(new XAttribute("value", PathHelper.GetRelativePath(Application.dataPath, packagesConfigDirectoryPath)));
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
                addElement.Add(new XAttribute("value", Verbose.ToString().ToLowerInvariant()));
                config.Add(addElement);
            }

            addElement = new XElement("add");
            addElement.Add(new XAttribute("key", "slimRestore"));
            addElement.Add(new XAttribute("value", SlimRestore.ToString().ToLowerInvariant()));
            config.Add(addElement);

            if (!InstallFromCache)
            {
                addElement = new XElement("add");
                addElement.Add(new XAttribute("key", "InstallFromCache"));
                addElement.Add(new XAttribute("value", InstallFromCache.ToString().ToLowerInvariant()));
                config.Add(addElement);
            }

            if (ReadOnlyPackageFiles)
            {
                addElement = new XElement("add");
                addElement.Add(new XAttribute("key", "ReadOnlyPackageFiles"));
                addElement.Add(new XAttribute("value", ReadOnlyPackageFiles.ToString().ToLowerInvariant()));
                config.Add(addElement);
            }

            if (RequestTimeoutSeconds != DefaultRequestTimeout)
            {
                addElement = new XElement("add");
                addElement.Add(new XAttribute("key", RequestTimeoutSecondsConfigKey));
                addElement.Add(new XAttribute("value", RequestTimeoutSeconds));
                config.Add(addElement);
            }

            if (PreferNetStandardOverNetFramework)
            {
                addElement = new XElement("add");
                addElement.Add(new XAttribute("key", PreferNetStandardOverNetFrameworkConfigKey));
                addElement.Add(new XAttribute("value", PreferNetStandardOverNetFramework.ToString().ToLowerInvariant()));
                config.Add(addElement);
            }

            var configuration = new XElement("configuration");
            configuration.Add(packageSources);
            configuration.Add(disabledPackageSources);
            configuration.Add(packageSourceCredentials);
            configuration.Add(activePackageSource);

            if (EnabledPlugins.Count > 0)
            {
                var enabledPlugins = new XElement("enabledPlugins");
                foreach (var plugin in EnabledPlugins)
                {
                    addElement = new XElement("add");
                    addElement.Add(new XAttribute("name", plugin.Name));
                    addElement.Add(new XAttribute("path", plugin.Path));
                    enabledPlugins.Add(addElement);
                }

                configuration.Add(enabledPlugins);
            }

            configuration.Add(config);
            configFile.Add(configuration);

            var fileExists = File.Exists(filePath);

            // remove the read only flag on the file, if there is one.
            if (fileExists)
            {
                var attributes = File.GetAttributes(filePath);

                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    attributes &= ~FileAttributes.ReadOnly;
                    File.SetAttributes(filePath, attributes);
                }
            }

            configFile.Save(filePath);
        }

        /// <summary>
        ///     Changes the package install location config and also moves the packages.config to the new location.
        /// </summary>
        /// <param name="newInstallLocation">New install location to set.</param>
        internal void ChangeInstallLocation(PackageInstallLocation newInstallLocation)
        {
            if (newInstallLocation == InstallLocation)
            {
                return;
            }

            var oldPackagesConfigPath = PackagesConfigFilePath;
            InstallLocation = newInstallLocation;
            UnityPathHelper.EnsurePackageInstallDirectoryIsSetup();
            var newConfigPath = PackagesConfigFilePath;
            File.Move(oldPackagesConfigPath, newConfigPath);
            var configMeta = oldPackagesConfigPath + ".meta";
            if (File.Exists(configMeta))
            {
                File.Move(configMeta, newConfigPath + ".meta");
            }
        }
    }
}
