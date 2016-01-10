namespace NugetForUnity
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.ServiceModel.Syndication;
    using System.Text;
    using System.Xml;
    using System.Xml.Linq;
    using Ionic.Zip;
    using UnityEditor;
    using UnityEngine;
    using Debug = UnityEngine.Debug;

    /// <summary>
    /// A set of helper methods that act as a wrapper around nuget.exe
    /// 
    /// TIP: It's incredibly useful to associate .nupkg files as compressed folder in Windows (View like .zip files).  To do this:
    ///      1) Open a command prompt as admin (Press Windows key. Type "cmd".  Right click on the icon and choose "Run as Administrator"
    ///      2) Enter this command: cmd /c assoc .nupkg=CompressedFolder
    /// </summary>
    [InitializeOnLoad]
    public static class NugetHelper
    {
        /// <summary>
        /// The path to the directory that contains nuget.exe and nuget.config.
        /// </summary>
        private static readonly string NugetPath = Path.Combine(Application.dataPath, "./NuGet");

        /// <summary>
        /// The path to the nuget.config file.
        /// </summary>
        private static readonly string NugetConfigFilePath = Path.Combine(NugetPath, "../NuGet.config");

        /// <summary>
        /// The path to the nuget.exe file.
        /// </summary>
        private static readonly string NugetExeFilePath = Path.Combine(NugetPath, "./nuget.exe");

        /// <summary>
        /// The path to the packages.config file.
        /// </summary>
        private static readonly string PackagesConfigFilePath = Path.Combine(Application.dataPath, "./packages.config");

        /// <summary>
        /// The path where to put created (packed) and downloaded (not installed yet) .nupkg files.
        /// </summary>
        private static readonly string PackOutputDirectory = Path.Combine(Application.dataPath, "../nupkgs");

        /// <summary>
        /// The amount of time, in milliseconds, before the nuget.exe process times out and is killed.
        /// </summary>
        private const int TimeOut = 60000;

        /// <summary>
        /// The loaded NuGet.config file that holds the settings for NuGet.
        /// </summary>
        private static readonly NugetConfigFile NugetConfigFile;

        /// <summary>
        /// Static constructor used by Unity to restore packages defined in packages.config.
        /// </summary>
        static NugetHelper()
        {
            // Load the NuGet.config file
            if (File.Exists(NugetConfigFilePath))
            {
                NugetConfigFile = NugetConfigFile.Load(NugetConfigFilePath);
            }
            else
            {
                Debug.LogFormat("No NuGet.config file found. Creating default at {0}", NugetConfigFilePath);

                NugetConfigFile = NugetConfigFile.CreateDefaultFile(NugetConfigFilePath);
                AssetDatabase.Refresh();
            }

            // create the nupkgs directory, if it doesn't exist
            if (!Directory.Exists(PackOutputDirectory))
            {
                Directory.CreateDirectory(PackOutputDirectory);
            }

            // restore packages - this will be called EVERY time the project is loaded or a code-file changes
            Restore();
        }

        /// <summary>
        /// Runs nuget.exe using the given arguments.
        /// </summary>
        /// <param name="arguments">The arguments to run nuget.exe with.</param>
        /// <param name="logOuput">True to output debug information to the Unity console.  Defaults to true.</param>
        /// <returns>The string of text that was output from nuget.exe following its execution.</returns>
        private static void RunNugetProcess(string arguments, bool logOuput = true)
        {
            LogVerbose("Running NuGet.exe with args = {0}", arguments);

            Process process = new Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.FileName = NugetExeFilePath;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WorkingDirectory = NugetPath;

            // http://stackoverflow.com/questions/16803748/how-to-decode-cmd-output-correctly
            // Default = 65533, ASCII = ?, Unicode = nothing works at all, UTF-8 = 65533, UTF-7 = 242 = WORKS!, UTF-32 = nothing works at all
            process.StartInfo.StandardOutputEncoding = Encoding.GetEncoding(850);
            process.Start();

            if (!process.WaitForExit(TimeOut))
            {
                Debug.LogWarning("NuGet took too long to finish.  Killing operation.");
                process.Kill();
            }

            string error = process.StandardError.ReadToEnd();
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError(error);
            }

            string output = process.StandardOutput.ReadToEnd();
            if (logOuput && !string.IsNullOrEmpty(output))
            {
                Debug.Log(output);
            }
        }

        /// <summary>
        /// Cleans up a package after it has been installed.
        /// Since we are in Unity, we can make certain assumptions on which files will NOT be used, so we can delete them.
        /// </summary>
        /// <param name="package">The NugetPackage to clean.</param>
        private static void Clean(NugetPackageIdentifier package)
        {
            string packageInstallDirectory = Path.Combine(NugetConfigFile.RepositoryPath, string.Format("{0}.{1}", package.Id, package.Version));

            LogVerbose("Cleaning {0}", packageInstallDirectory);

            // delete a remnant .meta file that may exist from packages created by Unity
            DeleteFile(packageInstallDirectory + "/" + package.Id + ".nuspec.meta");

            // delete directories & files that NuGet normally deletes, but since we are installing "manually" they exist
            DeleteDirectory(packageInstallDirectory + "/_rels");
            DeleteDirectory(packageInstallDirectory + "/package");
            DeleteFile(packageInstallDirectory + "/" + package.Id + ".nuspec");
            DeleteFile(packageInstallDirectory + "/[Content_Types].xml");

            // Unity has no use for the tools or build directories
            DeleteDirectory(packageInstallDirectory + "/tools");
            DeleteDirectory(packageInstallDirectory + "/build");

            // For now, delete Content.  We may use it later...
            DeleteDirectory(packageInstallDirectory + "/Content");

            // Delete documentation folders since they sometimes have HTML docs with JavaScript, which Unity tried to parse as "UnityScript"
            DeleteDirectory(packageInstallDirectory + "/docs");

            // Unity can only use .NET 3.5 or lower, so delete everything else
            if (Directory.Exists(packageInstallDirectory + "/lib"))
            {
                //bool has20 = Directory.Exists(packageInstallDirectory + "/lib/net20");
                bool has30 = Directory.Exists(packageInstallDirectory + "/lib/net30");
                bool has35 = Directory.Exists(packageInstallDirectory + "/lib/net35");

                List<string> directoriesToDelete = new List<string>
                {
                    "net40",
                    "net45",
                    "netcore45",
                    "net4",
                    "cf", // compact framework
                    "wp", // windows phone
                    "sl", // silverlight
                    "windowsphone"
                };

                string[] libDirectories = Directory.GetDirectories(packageInstallDirectory + "/lib");
                foreach (var directory in libDirectories)
                {
                    if (directoriesToDelete.Any(s => directory.Contains(s)))
                    {
                        LogVerbose("Deleting {0}", directory);
                        DeleteDirectory(directory);
                    }
                    else if (directory.Contains("net20") && (has30 || has35))
                    {
                        // if .NET 2.0 exists, keep it, unless there is also a .NET 3.0 or 3.5 version as well
                        LogVerbose("Deleting {0}", directory);
                        DeleteDirectory(directory);
                    }
                    else if (directory.Contains("net30") && has35)
                    {
                        // if .NET 3.0 exists, keep it, unless there is also a .NET 3.5 version as well
                        LogVerbose("Deleting {0}", directory);
                        DeleteDirectory(directory);
                    }
                }
            }
        }

        /// <summary>
        /// Calls "nuget.exe pack" to create a .nupkg file based on the given .nuspec file.
        /// </summary>
        /// <param name="nuspecFilePath">The full filepath to the .nuspec file to use.</param>
        public static void Pack(string nuspecFilePath)
        {
            if (!Directory.Exists(PackOutputDirectory))
            {
                Directory.CreateDirectory(PackOutputDirectory);
            }

            string arguments = string.Format("pack \"{0}\" -OutputDirectory \"{1}\"", nuspecFilePath, PackOutputDirectory);

            RunNugetProcess(arguments);
        }

        /// <summary>
        /// Calls "nuget.exe push" to push a .nupkf file to the the server location defined in the NuGet.config file.
        /// Note: This differs slightly from NuGet's Push command by automatically calling Pack if the .nupkg doesn't already exist.
        /// </summary>
        /// <param name="nuspec">The NuspecFile which defines the package to push.  Only the ID and Version are used.</param>
        /// <param name="nuspecFilePath">The full filepath to the .nuspec file to use.  This is required by NuGet's Push command.</param>
        /// /// <param name="apiKey">The API key to use when pushing a package to the server.  This is optional.</param>
        public static void Push(NuspecFile nuspec, string nuspecFilePath, string apiKey = "")
        {
            string packagePath = Path.Combine(PackOutputDirectory, string.Format("{0}.{1}.nupkg", nuspec.Id, nuspec.Version));
            if (!File.Exists(packagePath))
            {
                LogVerbose("Attempting to Pack.");
                Pack(nuspecFilePath);

                if (!File.Exists(packagePath))
                {
                    Debug.LogErrorFormat("NuGet package not found: {0}", packagePath);
                    return;
                }
            }

            string arguments = string.Format("push \"{0}\" {1} -configfile \"{2}\"", packagePath, apiKey, NugetConfigFilePath);

            RunNugetProcess(arguments);
        }

        /// <summary>
        /// Recursively deletes the folder at the given path.
        /// NOTE: Directory.Delete() doesn't delete Read-Only files, whereas this does.
        /// </summary>
        /// <param name="directoryPath">The path of the folder to delete.</param>
        private static void DeleteDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
                return;

            var directoryInfo = new DirectoryInfo(directoryPath);

            // delete any sub-folders first
            foreach (var childInfo in directoryInfo.GetFileSystemInfos())
            {
                DeleteDirectory(childInfo.FullName);
            }

            // remove the read-only flag on all files
            var files = directoryInfo.GetFiles();
            foreach (var file in files)
            {
                file.Attributes = FileAttributes.Normal;
            }

            // remove the read-only flag on the directory
            directoryInfo.Attributes = FileAttributes.Normal;

            // recursively delete the directory
            directoryInfo.Delete(true);
        }

        /// <summary>
        /// Deletes a file at the given filepath.
        /// </summary>
        /// <param name="filePath">The filepath to the file to delete.</param>
        private static void DeleteFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        /// <summary>
        /// "Uninstalls" the given package by simply deleting its folder.
        /// </summary>
        /// <param name="package">The NugetPackage to uninstall.</param>
        /// <param name="refreshAssets">True to force Unity to refesh its Assets folder.  False to temporarily ignore the change.  Defaults to true.</param>
        public static void Uninstall(NugetPackageIdentifier package, bool refreshAssets = true)
        {
            string packageInstallDirectory = Path.Combine(NugetConfigFile.RepositoryPath, string.Format("{0}.{1}", package.Id, package.Version));
            DeleteDirectory(packageInstallDirectory);

            RemoveInstalledPackage(package);

            if (refreshAssets)
                AssetDatabase.Refresh();
        }

        /// <summary>
        /// Updates a package by uninstalling the currently installed version and installing the "new" version.
        /// </summary>
        /// <param name="currentVersion">The current package to uninstall.</param>
        /// <param name="newVersion">The package to install.</param>
        public static void Update(NugetPackageIdentifier currentVersion, NugetPackage newVersion)
        {
            Uninstall(currentVersion, false);
            Install(newVersion);
        }

        /// <summary>
        /// Gets a list of all currently installed packages by reading the packages.config file AND querying the server for the FULL details.
        /// NOTE: This retrieves all of the data for the package by querying the server for info.
        /// </summary>
        /// <returns>A list of installed NugetPackages.</returns>
        public static List<NugetPackage> GetFullInstalledPackages()
        {
            List<NugetPackageIdentifier> packages = LoadInstalledPackages();

            List<NugetPackage> fullPackages = new List<NugetPackage>(packages.Count);
            fullPackages.AddRange(packages.Select(package => GetSpecificPackage(package)));

            return fullPackages;
        }

        /// <summary>
        /// Loads a list of all currently installed packages by reading the packages.config file.
        /// NOTE: This only retrieves the ID and Version for the package, nothing else.
        /// </summary>
        /// <returns>A list of installed NugetPackages.</returns>
        private static List<NugetPackageIdentifier> LoadInstalledPackages()
        {
            // Create a package.config file, if there isn't already one in the project
            if (!File.Exists(PackagesConfigFilePath))
            {
                Debug.LogFormat("No packages.config file found. Creating default at {0}", PackagesConfigFilePath);

                SaveInstalledPackages(new List<NugetPackageIdentifier>());
                AssetDatabase.Refresh();
            }

            List<NugetPackageIdentifier> packages = new List<NugetPackageIdentifier>();

            XDocument packagesFile = XDocument.Load(PackagesConfigFilePath);
            foreach (var packageElement in packagesFile.Root.Elements())
            {
                NugetPackage package = new NugetPackage();
                package.Id = packageElement.Attribute("id").Value;
                package.Version = packageElement.Attribute("version").Value;
                packages.Add(package);
            }

            return packages;
        }

        /// <summary>
        /// Adds a package to the packages.config file.
        /// </summary>
        /// <param name="package">The NugetPackage to add to the packages.config file.</param>
        private static void AddInstalledPackage(NugetPackageIdentifier package)
        {
            List<NugetPackageIdentifier> packages = LoadInstalledPackages();

            if (!packages.Contains(package))
            {
                packages.Add(package);
                SaveInstalledPackages(packages);
            }
        }

        /// <summary>
        /// Removes a package from the packages.config file.
        /// </summary>
        /// <param name="package">The NugetPackage to remove from the packages.config file.</param>
        private static void RemoveInstalledPackage(NugetPackageIdentifier package)
        {
            List<NugetPackageIdentifier> packages = LoadInstalledPackages();
            packages.Remove(package);
            SaveInstalledPackages(packages);
        }

        /// <summary>
        /// Saves the packages.config file and populates it with given installed NugetPackages.
        /// </summary>
        /// <param name="packages">The list of currently installed NugetPackages to write to the packages.config file.</param>
        private static void SaveInstalledPackages(IEnumerable<NugetPackageIdentifier> packages)
        {
            XDocument packagesFile = new XDocument();
            packagesFile.Add(new XElement("packages"));
            foreach (var package in packages)
            {
                XElement packageElement = new XElement("package");
                packageElement.Add(new XAttribute("id", package.Id));
                packageElement.Add(new XAttribute("version", package.Version));
                packagesFile.Root.Add(packageElement);
            }

            // remove the read only flag on the file, if there is one.
            if (File.Exists(PackagesConfigFilePath))
            {
                FileAttributes attributes = File.GetAttributes(PackagesConfigFilePath);

                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    attributes &= ~FileAttributes.ReadOnly;
                    File.SetAttributes(PackagesConfigFilePath, attributes);
                } 
            }

            packagesFile.Save(PackagesConfigFilePath);
        }

        /// <summary>
        /// Gets a list of NuGetPackages via the HTTP Search() function defined by NuGet.Server and NuGet Gallery.
        /// This allows searching for partial IDs or even the empty string (the default) to list ALL packages.
        /// 
        /// NOTE: See the functions and parameters defined here: https://www.nuget.org/api/v2/$metadata
        /// </summary>
        /// <param name="searchTerm"></param>
        /// <param name="includeAllVersions"></param>
        /// <param name="includePrerelease"></param>
        /// <param name="numberToGet"></param>
        /// <param name="numberToSkip"></param>
        /// <returns></returns>
        public static List<NugetPackage> Search(string searchTerm = "", bool includeAllVersions = false, bool includePrerelease = false, int numberToGet = 15, int numberToSkip = 0)
        {
            //Example URL: "http://www.nuget.org/api/v2/Search()?$filter=IsLatestVersion&$orderby=Id&$skip=0&$top=30&searchTerm='newtonsoft'&targetFramework=''&includePrerelease=false";

            string url = NugetConfigFile.ActivePackageSource.Path;

            // call the search method
            url += "Search()?";

            // filter results
            if (!includeAllVersions)
            {
                if (!includePrerelease)
                {
                    url += "$filter=IsLatestVersion&";
                }
                else
                {
                    url += "$filter=IsAbsoluteLatestVersion&";
                }
            }

            // order results
            //url += "$orderby=Id&";
            //url += "$orderby=LastUpdated&";
            url += "$orderby=DownloadCount&";

            // skip a certain number of entries
            url += string.Format("$skip={0}&", numberToSkip);

            // show a certain number of entries
            url += string.Format("$top={0}&", numberToGet);

            // apply the search term
            url += string.Format("searchTerm='{0}'&", searchTerm);

            // apply the target framework filters
            url += "targetFramework=''&";

            // should we include prerelease packages?
            url += string.Format("includePrerelease={0}", includePrerelease.ToString().ToLower());

            return GetPackagesFromUrl(url);
        }

        /// <summary>
        /// Gets a list of NuGetPackages via the HTTP FindPackagesById() function defined by NuGet.Server and NuGet Gallery.
        /// The package ID passed in MUST be an actual, full ID of a package otherwise it will return an empty list.
        /// The list will always be sorted in a descending order by Version #.  This means the first entry will be the latest version.
        /// 
        /// NOTE: See the functions and parameters defined here: https://www.nuget.org/api/v2/$metadata
        /// </summary>
        /// <param name="packageId"></param>
        /// <param name="includeAllVersions"></param>
        /// <param name="includePrerelease"></param>
        /// <returns></returns>
        private static List<NugetPackage> FindPackagesById(string packageId, bool includeAllVersions = false, bool includePrerelease = false)
        {
            //Example URL: "http://www.nuget.org/api/v2/FindPackagesById()?$filter=IsLatestVersion&$orderby=Version desc&id='Newtonsoft.Json'";

            string url = NugetConfigFile.ActivePackageSource.Path;

            // call the search method
            url += "FindPackagesById()?";

            // filter results
            if (!includeAllVersions)
            {
                if (!includePrerelease)
                {
                    url += "$filter=IsLatestVersion&";
                }
                else
                {
                    url += "$filter=IsAbsoluteLatestVersion&";
                }
            }

            // order results by version number, in descending order
            url += "$orderby=Version desc&";

            // set the package ID to retreive
            url += string.Format("id='{0}'", packageId);

            return GetPackagesFromUrl(url);
        }

        /// <summary>
        /// Queries the server with the given list of installed packages to get any updates that are available.
        /// </summary>
        /// <param name="installedPackages">The list of currently installed packages.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="includeAllVersions">True to include older versions that are not the latest version.</param>
        /// <param name="targetFrameworks">The specific frameworks to target?</param>
        /// <param name="versionContraints">The version constraints?</param>
        /// <returns>A list of all updates available.</returns>
        public static List<NugetPackage> GetUpdates(IEnumerable<NugetPackage> installedPackages, bool includePrerelease = false, bool includeAllVersions = false, string targetFrameworks = "", string versionContraints = "")
        {
            string packageIds = string.Empty;
            string versions = string.Empty;

            foreach (var package in installedPackages)
            {
                if (string.IsNullOrEmpty(packageIds))
                {
                    packageIds += package.Id;
                }
                else
                {
                    packageIds += "|" + package.Id;
                }

                if (string.IsNullOrEmpty(versions))
                {
                    versions += package.Version;
                }
                else
                {
                    versions += "|" + package.Version;
                }
            }

            string url = string.Format("{0}GetUpdates()?packageIds='{1}'&versions='{2}'&includePrerelease={3}&includeAllVersions={4}&targetFrameworks='{5}'&versionConstraints='{6}'", NugetConfigFile.ActivePackageSource.Path, packageIds, versions, includePrerelease.ToString().ToLower(), includeAllVersions.ToString().ToLower(), targetFrameworks, versionContraints);

            var updates = GetPackagesFromUrl(url);

            LogVerbose("{0} updates found.", updates.Count);

            return updates;
        }

        /// <summary>
        /// Gets a NugetPackage from the NuGet server with the exact ID and Version given.
        /// If an exact match isn't found, it selects the next closest version available.
        /// </summary>
        /// <param name="package">The <see cref="NugetPackageIdentifier"/> containing the ID and Version of the package to get.</param>
        /// <returns>The retrieved package, if there is one.  Null if no matching package was found.</returns>
        private static NugetPackage GetSpecificPackage(NugetPackageIdentifier package)
        {
            string url = string.Format("{0}FindPackagesById()?$filter=Version ge '{1}'&$orderby=Version asc&id='{2}'", NugetConfigFile.ActivePackageSource.Path, package.Version, package.Id);

            var foundPackage = GetPackagesFromUrl(url).FirstOrDefault();

            if (foundPackage == null)
            {
                Debug.LogErrorFormat("Could not find specific package: {0} - {1}", package.Id, package.Version);
            }
            else if (foundPackage.Version != package.Version)
            {
                Debug.LogWarningFormat("Requested {0} version {1}, but instead using {2}", package.Id, package.Version, foundPackage.Version);
            }

            return foundPackage;
        }

        /// <summary>
        /// Builds a list of NugetPackages from the XML returned from the HTTP GET request issued at the given URL.
        /// Note that NuGet uses an Atom-feed (XML Syndicaton) superset called OData.
        /// See here http://www.odata.org/documentation/odata-version-2-0/uri-conventions/
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private static List<NugetPackage> GetPackagesFromUrl(string url)
        {
            LogVerbose("Getting packages from: {0}", url);

            WebRequest getRequest = WebRequest.Create(url);
            Stream responseStream = getRequest.GetResponse().GetResponseStream();
            StreamReader objReader = new StreamReader(responseStream);
            SyndicationFeed atomFeed = SyndicationFeed.Load(XmlReader.Create(objReader));

            List<NugetPackage> packages = new List<NugetPackage>();

            foreach (var item in atomFeed.Items)
            {
                var propertiesExtension = item.ElementExtensions.First();
                var reader = propertiesExtension.GetReader();
                var properties = (XElement)XDocument.ReadFrom(reader);

                NugetPackage package = new NugetPackage();
                package.DownloadUrl = ((UrlSyndicationContent)item.Content).Url.ToString();
                package.Id = item.Title.Text;
                package.Title = (string)properties.Element(XName.Get("Title", "http://schemas.microsoft.com/ado/2007/08/dataservices")) ?? string.Empty;
                package.Version = (string)properties.Element(XName.Get("Version", "http://schemas.microsoft.com/ado/2007/08/dataservices")) ?? string.Empty;
                package.Description = (string)properties.Element(XName.Get("Description", "http://schemas.microsoft.com/ado/2007/08/dataservices")) ?? string.Empty;
                package.LicenseUrl = (string)properties.Element(XName.Get("LicenseUrl", "http://schemas.microsoft.com/ado/2007/08/dataservices")) ?? string.Empty;

                string iconUrl = (string)properties.Element(XName.Get("IconUrl", "http://schemas.microsoft.com/ado/2007/08/dataservices")) ?? string.Empty;
                if (!string.IsNullOrEmpty(iconUrl))
                {
                    package.Icon = DownloadImage(iconUrl);
                }

                // if there is no title, just use the ID as the title
                if (string.IsNullOrEmpty(package.Title))
                {
                    package.Title = package.Id;
                }

                // Get dependencies
                package.Dependencies = new List<NugetPackageIdentifier>();
                string rawDependencies = (string)properties.Element(XName.Get("Dependencies", "http://schemas.microsoft.com/ado/2007/08/dataservices")) ?? string.Empty;
                if (!string.IsNullOrEmpty(rawDependencies))
                {
                    string[] dependencies = rawDependencies.Split('|');
                    foreach (var dependencyString in dependencies)
                    {
                        string[] details = dependencyString.Split(':');

                        // some packages (ex: FSharp.Data - 2.1.0) have inproper "semi-empty" dependencies such as:
                        // "Zlib.Portable:1.10.0:portable-net40+sl50+wp80+win80|::net40"
                        // so we need to only add valid dependencies and skip invalid ones
                        if (!string.IsNullOrEmpty(details[0]) && !string.IsNullOrEmpty(details[1]))
                        {
                            NugetPackageIdentifier dependency = new NugetPackageIdentifier();
                            dependency.Id = details[0];
                            dependency.Version = details[1];

                            package.Dependencies.Add(dependency);
                        }
                    }
                }

                packages.Add(package);
            }

            LogVerbose("Retreived {0} packages", packages.Count);

            return packages;
        }

        /// <summary>
        /// Copies the contents of input to output. Doesn't close either stream.
        /// </summary>
        private static void CopyStream(Stream input, Stream output)
        {
            byte[] buffer = new byte[8 * 1024];
            int len;
            while ((len = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, len);
            }
        }

        /// <summary>
        /// Installs the package given by the identifer.  It fetches the appropriate full package from the server and installs it.
        /// </summary>
        /// <param name="package">The identifer of the package to install.</param>
        /// <param name="refreshAssets">True to force Unity to refrehs the asset database.  False to temporarily ignore the change.</param>
        private static void InstallIdentifier(NugetPackageIdentifier package, bool refreshAssets = true)
        {
            // TODO: Look to see if the package, or a later version, is already installed and use it.
            //       If an earlier version is installed, update it.
            //       If not installed, look on the server for specific version
            //       If specific version not found on server, use the next version up (not latest)

            var installedPackages = LoadInstalledPackages();
            foreach (var installedPackage in installedPackages)
            {
                if (installedPackage.Id == package.Id)
                {
                    if (installedPackage < package)
                    {
                        // the installed version is older than the version to install, so update it
                        //NugetPackage oldPackage = GetSpecificPackage(installedPackage);
                        NugetPackage newPackage = GetSpecificPackage(package);

                        LogVerbose("{0} {1} is installed, but need {2}.  Updating to {3}", installedPackage.Id, installedPackage.Version, package.Version, newPackage.Version);
                        Update(installedPackage, newPackage);
                        return;
                    }
                    else
                    {
                        // Either case below is true
                        // the installed version is newer than the version to install, so use it
                        // the installed version is equal to the version to install, so use it
                        LogVerbose("{0} {1} is installed. {2} is needed, so just using installed version.", installedPackage.Id, installedPackage.Version, package.Version);
                        return;
                    }
                }
            }

            NugetPackage foundPackage = GetSpecificPackage(package);
            
            if (foundPackage != null)
            {
                Install(foundPackage, refreshAssets);
            }
        }

        /// <summary>
        /// Outputs the given message to the log only if verbose mode is active.  Otherwise it does nothing.
        /// </summary>
        /// <param name="format">The formatted message string.</param>
        /// <param name="args">The arguments for the formattted message string.</param>
        private static void LogVerbose(string format, params object[] args)
        {
            if (NugetConfigFile.Verbose)
            {
                Debug.LogFormat(format, args);
            }
        }

        /// <summary>
        /// Installs the given package via the HTTP server API.
        /// </summary>
        /// <param name="package">The package to install.</param>
        /// <param name="refreshAssets">True to refresh the Unity asset database.  False to ignore the changes (temporarily).</param>
        public static void Install(NugetPackage package, bool refreshAssets = true)
        {
            LogVerbose("Installing: {0} - {1}", package.Id, package.Version);

            // Mono doesn't have a Certificate Authority, so we have to provide all validation manually.  Currently just accept anything.
            // See here: http://stackoverflow.com/questions/4926676/mono-webrequest-fails-with-https
            ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, policyErrors) => true;

            //if (string.IsNullOrEmpty(package.DownloadUrl))
            //{
            //    // some packages don't have a DownloadURL attached (package.config, dependencies), so we have to query the server to get the full information
            //    package = GetSpecificPackage(package.Id, package.Version);
            //}

            foreach (var dependency in package.Dependencies)
            {
                LogVerbose("Installing Dependency: {0} - {1}", dependency.Id, dependency.Version);

                // TODO: Do all of the appropriate dependency version range checking instead of grabbing the specific version.
                InstallIdentifier(dependency, false);
            }

            HttpWebRequest getRequest = (HttpWebRequest)WebRequest.Create(package.DownloadUrl);

            // TODO: Get the local packages location from the config file
            Stream objStream = getRequest.GetResponse().GetResponseStream();
            string localPackagePath = Path.Combine(PackOutputDirectory, string.Format("./{0}.{1}.nupkg", package.Id, package.Version));
            using (Stream file = File.Create(localPackagePath))
            {
                CopyStream(objStream, file);
            }

            // unzip the package
            using (ZipFile zip = ZipFile.Read(localPackagePath))
            {
                foreach (ZipEntry entry in zip)
                {
                    entry.Extract(Path.Combine(NugetConfigFile.RepositoryPath, string.Format("{0}.{1}", package.Id, package.Version)), ExtractExistingFileAction.OverwriteSilently);
                }
            }

            // clean
            Clean(package);

            // update packages.config
            AddInstalledPackage(package);

            if (refreshAssets)
                AssetDatabase.Refresh();
        }

        /// <summary>
        /// Restores all packages defined in packages.config.
        /// </summary>
        public static void Restore()
        {
            var packages = LoadInstalledPackages();
            foreach (var package in packages)
            {
                if (package != null && !IsInstalled(package))
                {
                    InstallIdentifier(package);
                }
            }
        }

        /// <summary>
        /// Checks if a given package is installed.
        /// </summary>
        /// <param name="package">The package to check if is installed.</param>
        /// <returns>True if the given package is installed.  False if it is not.</returns>
        private static bool IsInstalled(NugetPackageIdentifier package)
        {
            string packageInstallDirectory = Path.Combine(NugetConfigFile.RepositoryPath, string.Format("{0}.{1}", package.Id, package.Version));

            return Directory.Exists(packageInstallDirectory);
        }

        /// <summary>
        /// Downloads an image at the given URL and converts it to a Unity Texture2D.
        /// </summary>
        /// <param name="url">The URL of the image to download.</param>
        /// <returns>The image as a Unity Texture2D object.</returns>
        private static Texture2D DownloadImage(string url)
        {
            bool timedout = false;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            WWW request = new WWW(url);
            while (!request.isDone)
            {
                if (stopwatch.ElapsedMilliseconds >= 750)
                {
                    LogVerbose("Timed out!");

                    request.Dispose();
                    stopwatch.Stop();
                    timedout = true;
                    break;
                }
            }

            if (!timedout && !string.IsNullOrEmpty(request.error))
            {
                LogVerbose(request.error);
            }

            Texture2D result = null;

            if (!timedout && string.IsNullOrEmpty(request.error))
            {
                result = request.texture;
            }

            return result;
        }
    }
}
