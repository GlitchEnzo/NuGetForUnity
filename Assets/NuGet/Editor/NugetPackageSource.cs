namespace NugetForUnity
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.ServiceModel.Syndication;
    using System.Xml;
    using System.Xml.Linq;
    using UnityEngine;
    using Debug = UnityEngine.Debug;

    /// <summary>
    /// Represents a NuGet Package Source (a "server").
    /// </summary>
    public class NugetPackageSource
    {
        /// <summary>
        /// Gets or sets the name of the package source.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets or sets the path of the package source.
        /// </summary>
        public string Path { get; private set; }

        /// <summary>
        /// Gets or sets a value indicated whether the path is a local path or a remote path.
        /// </summary>
        public bool IsLocalPath { get; private set; }

        /// <summary>
        /// Gets or sets a value indicated whether this source is enabled or not.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="NugetPackageSource"/> class.
        /// </summary>
        /// <param name="name">The name of the package source.</param>
        /// <param name="path">The path to the package source.</param>
        public NugetPackageSource(string name, string path)
        {
            Name = name;
            Path = path;
            IsLocalPath = !Path.StartsWith("http");
            IsEnabled = true;
        }

        /// <summary>
        /// Gets a NugetPackage from the NuGet server with the exact ID and Version given.
        /// If an exact match isn't found, it selects the next closest version available.
        /// </summary>
        /// <param name="package">The <see cref="NugetPackageIdentifier"/> containing the ID and Version of the package to get.</param>
        /// <returns>The retrieved package, if there is one.  Null if no matching package was found.</returns>
        public NugetPackage GetSpecificPackage(NugetPackageIdentifier package)
        {
            NugetPackage foundPackage = null;

            string cachedPackagePath = System.IO.Path.Combine(NugetHelper.PackOutputDirectory, string.Format("./{0}.{1}.nupkg", package.Id, package.Version));

            if (NugetPreferences.UseCache && File.Exists(cachedPackagePath))
            {
                NugetHelper.LogVerbose("Getting specific package from the cache: {0}", cachedPackagePath);
                foundPackage = NugetPackage.FromNupkgFile(cachedPackagePath);
            }
            else
            {
                if (IsLocalPath)
                {
                    string localPackagePath = System.IO.Path.Combine(Path, string.Format("./{0}.{1}.nupkg", package.Id, package.Version));
                    if (File.Exists(localPackagePath))
                    {
                        foundPackage = NugetPackage.FromNupkgFile(localPackagePath);
                    }
                    else
                    {
                        // TODO: Sort the local packages?  Currently assuming they are in alphabetical order due to the filesystem.

                        // Try to find later versions of the same package
                        var packages = GetLocalPackages(package.Id, true, true);
                        foundPackage = packages.SkipWhile(x => x < package).FirstOrDefault();

                        if (foundPackage == null)
                        {
                            Debug.LogErrorFormat("Could not find specific local package: {0} - {1}", package.Id, package.Version);
                        }
                        else if (foundPackage.Version != package.Version)
                        {
                            Debug.LogWarningFormat("Requested {0} version {1}, but instead using {2}", package.Id, package.Version, foundPackage.Version);
                        }
                    }
                }
                else
                {
                    string url = string.Format("{0}FindPackagesById()?$filter=Version ge '{1}'&$orderby=Version asc&id='{2}'", Path, package.Version, package.Id);

                    foundPackage = GetPackagesFromUrl(url).FirstOrDefault();

                    if (foundPackage == null)
                    {
                        Debug.LogErrorFormat("Could not find specific package: {0} - {1}", package.Id, package.Version);
                    }
                    else if (foundPackage.Version != package.Version)
                    {
                        Debug.LogWarningFormat("Requested {0} version {1}, but instead using {2}", package.Id, package.Version, foundPackage.Version);
                    }
                }
            }

            return foundPackage;
        }

        /// <summary>
        /// Gets a list of NuGetPackages from this package source.
        /// This allows searching for partial IDs or even the empty string (the default) to list ALL packages.
        /// 
        /// NOTE: See the functions and parameters defined here: https://www.nuget.org/api/v2/$metadata
        /// </summary>
        /// <param name="searchTerm">The search term to use to filter packages. Defaults to the empty string.</param>
        /// <param name="includeAllVersions">True to include older versions that are not the latest version.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="numberToGet">The number of packages to fetch.</param>
        /// <param name="numberToSkip">The number of packages to skip before fetching.</param>
        /// <returns>The list of available packages.</returns>
        public List<NugetPackage> Search(string searchTerm = "", bool includeAllVersions = false, bool includePrerelease = false, int numberToGet = 15, int numberToSkip = 0)
        {
            if (IsLocalPath)
            {
                return GetLocalPackages(searchTerm, includeAllVersions, includePrerelease, numberToGet, numberToSkip);
            }

            //Example URL: "http://www.nuget.org/api/v2/Search()?$filter=IsLatestVersion&$orderby=Id&$skip=0&$top=30&searchTerm='newtonsoft'&targetFramework=''&includePrerelease=false";

            string url = Path;

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
            url += "$orderby=DownloadCount desc&";

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
        /// Gets a list of all available packages from a local source (not a web server) that match the given filters.
        /// </summary>
        /// <param name="searchTerm">The search term to use to filter packages. Defaults to the empty string.</param>
        /// <param name="includeAllVersions">True to include older versions that are not the latest version.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="numberToGet">The number of packages to fetch.</param>
        /// <param name="numberToSkip">The number of packages to skip before fetching.</param>
        /// <returns>The list of available packages.</returns>
        private List<NugetPackage> GetLocalPackages(string searchTerm = "", bool includeAllVersions = false, bool includePrerelease = false, int numberToGet = 15, int numberToSkip = 0)
        {
            List<NugetPackage> localPackages = new List<NugetPackage>();

            if (numberToSkip != 0)
            {
                // we return the entire list the first time, so no more to add
                return localPackages;
            }

            string path = Path;

            if (Directory.Exists(path))
            {
                string[] packagePaths = Directory.GetFiles(path, string.Format("*{0}*.nupkg", searchTerm));

                foreach (var packagePath in packagePaths)
                {
                    var package = NugetPackage.FromNupkgFile(packagePath);
                    package.PackageSource = this;

                    if (package.IsPrerelease && !includePrerelease)
                    {
                        // if it's a prerelease package and we aren't supposed to return prerelease packages, just skip it
                        continue;
                    }

                    if (includeAllVersions)
                    {
                        // if all versions are being included, simply add it and move on
                        localPackages.Add(package);
                        //LogVerbose("Adding {0} {1}", package.Id, package.Version);
                        continue;
                    }

                    var existingPackage = localPackages.FirstOrDefault(x => x.Id == package.Id);
                    if (existingPackage != null)
                    {
                        // there is already a package with the same ID in the list
                        if (existingPackage < package)
                        {
                            // if the current package is newer than the existing package, swap them
                            localPackages.Remove(existingPackage);
                            localPackages.Add(package);
                        }
                    }
                    else
                    {
                        // there is no package with the same ID in the list yet
                        localPackages.Add(package);
                    }
                }
            }
            else
            {
                Debug.LogErrorFormat("Local folder not found: {0}", path);
            }

            return localPackages;
        }

        /// <summary>
        /// Builds a list of NugetPackages from the XML returned from the HTTP GET request issued at the given URL.
        /// Note that NuGet uses an Atom-feed (XML Syndicaton) superset called OData.
        /// See here http://www.odata.org/documentation/odata-version-2-0/uri-conventions/
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private List<NugetPackage> GetPackagesFromUrl(string url)
        {
            NugetHelper.LogVerbose("Getting packages from: {0}", url);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            WebRequest getRequest = WebRequest.Create(url);
            getRequest.Timeout = 5000;
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
                package.PackageSource = this;
                package.DownloadUrl = ((UrlSyndicationContent)item.Content).Url.ToString();
                package.Id = item.Title.Text;
                package.Title = (string)properties.Element(XName.Get("Title", "http://schemas.microsoft.com/ado/2007/08/dataservices")) ?? string.Empty;
                package.Version = (string)properties.Element(XName.Get("Version", "http://schemas.microsoft.com/ado/2007/08/dataservices")) ?? string.Empty;
                package.Description = (string)properties.Element(XName.Get("Description", "http://schemas.microsoft.com/ado/2007/08/dataservices")) ?? string.Empty;
                package.LicenseUrl = (string)properties.Element(XName.Get("LicenseUrl", "http://schemas.microsoft.com/ado/2007/08/dataservices")) ?? string.Empty;

                string iconUrl = (string)properties.Element(XName.Get("IconUrl", "http://schemas.microsoft.com/ado/2007/08/dataservices")) ?? string.Empty;
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
                package.Dependencies = new List<NugetPackageIdentifier>();
                string rawDependencies = (string)properties.Element(XName.Get("Dependencies", "http://schemas.microsoft.com/ado/2007/08/dataservices")) ?? string.Empty;
                if (!string.IsNullOrEmpty(rawDependencies))
                {
                    string[] dependencies = rawDependencies.Split('|');
                    foreach (var dependencyString in dependencies)
                    {
                        string[] details = dependencyString.Split(':');
                        string id = details[0];
                        string version = details[1];
                        string framework = string.Empty;

                        if (details.Length > 2)
                        {
                            framework = details[2];
                        }

                        // some packages (ex: FSharp.Data - 2.1.0) have inproper "semi-empty" dependencies such as:
                        // "Zlib.Portable:1.10.0:portable-net40+sl50+wp80+win80|::net40"
                        // so we need to only add valid dependencies and skip invalid ones
                        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(version))
                        {
                            // only use the dependency if there is no framework specified, or it is explicitly .NET 3.0
                            if (string.IsNullOrEmpty(framework) || framework == "net30")
                            {
                                NugetPackageIdentifier dependency = new NugetPackageIdentifier();
                                dependency.Id = id;
                                dependency.Version = version;

                                package.Dependencies.Add(dependency);
                            }
                        }
                    }
                }

                packages.Add(package);
            }

            stopwatch.Stop();
            NugetHelper.LogVerbose("Retreived {0} packages in {1} ms", packages.Count, stopwatch.ElapsedMilliseconds);

            return packages;
        }

        /// <summary>
        /// Gets a list of available packages from a local source (not a web server) that are upgrades for the given list of installed packages.
        /// </summary>
        /// <param name="installedPackages">The list of currently installed packages to use to find updates.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="includeAllVersions">True to include older versions that are not the latest version.</param>
        /// <returns>A list of all updates available.</returns>
        private List<NugetPackage> GetLocalUpdates(IEnumerable<NugetPackage> installedPackages, bool includePrerelease = false, bool includeAllVersions = false)
        {
            List<NugetPackage> updates = new List<NugetPackage>();

            var availablePackages = GetLocalPackages(string.Empty, includeAllVersions, includePrerelease);
            foreach (var installedPackage in installedPackages)
            {
                foreach (var availablePackage in availablePackages)
                {
                    if (installedPackage.Id == availablePackage.Id)
                    {
                        if (installedPackage < availablePackage)
                        {
                            updates.Add(availablePackage);
                        }
                    }
                }
            }

            return updates;
        }

        /// <summary>
        /// Queries the source with the given list of installed packages to get any updates that are available.
        /// </summary>
        /// <param name="installedPackages">The list of currently installed packages.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="includeAllVersions">True to include older versions that are not the latest version.</param>
        /// <param name="targetFrameworks">The specific frameworks to target?</param>
        /// <param name="versionContraints">The version constraints?</param>
        /// <returns>A list of all updates available.</returns>
        public List<NugetPackage> GetUpdates(IEnumerable<NugetPackage> installedPackages, bool includePrerelease = false, bool includeAllVersions = false, string targetFrameworks = "", string versionContraints = "")
        {
            if (IsLocalPath)
            {
                return GetLocalUpdates(installedPackages, includePrerelease, includeAllVersions);
            }

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

            string url = string.Format("{0}GetUpdates()?packageIds='{1}'&versions='{2}'&includePrerelease={3}&includeAllVersions={4}&targetFrameworks='{5}'&versionConstraints='{6}'", Path, packageIds, versions, includePrerelease.ToString().ToLower(), includeAllVersions.ToString().ToLower(), targetFrameworks, versionContraints);

            var updates = GetPackagesFromUrl(url);

            // sort alphabetically
            updates.Sort(delegate(NugetPackage x, NugetPackage y)
            {
                if (x.Id == null && y.Id == null)
                    return 0;
                else if (x.Id == null)
                    return -1;
                else if (y.Id == null)
                    return 1;
                else if (x.Id == y.Id)
                    return x.Version.CompareTo(y.Version);
                else
                    return x.Id.CompareTo(y.Id);
            });

            return updates;
        }
    }
}