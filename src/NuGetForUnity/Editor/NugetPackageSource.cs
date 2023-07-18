using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using Debug = UnityEngine.Debug;

namespace NugetForUnity
{
    /// <summary>
    ///     Represents a NuGet Package Source (a "server").
    /// </summary>
    [Serializable]
    public class NugetPackageSource
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="NugetPackageSource" /> class.
        /// </summary>
        /// <param name="name">The name of the package source.</param>
        /// <param name="path">The path to the package source.</param>
        public NugetPackageSource(string name, string path)
        {
            Name = name;
            SavedPath = path;
            IsLocalPath = !ExpandedPath.StartsWith("http");
            IsEnabled = true;
        }

        /// <summary>
        ///     Gets or sets the name of the package source.
        /// </summary>
        public string Name;

        /// <summary>
        ///     Gets or sets the path of the package source.
        /// </summary>
        public string SavedPath;

        /// <summary>
        ///     Gets path, with the values of environment variables expanded.
        /// </summary>
        public string ExpandedPath
        {
            get
            {
                var path = Environment.ExpandEnvironmentVariables(SavedPath);
                if (!path.StartsWith("http") && path != "(Aggregate source)" && !Path.IsPathRooted(path))
                {
                    path = Path.Combine(Path.GetDirectoryName(NugetHelper.NugetConfigFilePath), path);
                }

                return path;
            }
        }

        public string UserName;

        /// <summary>
        ///     Gets or sets the password used to access the feed. Null indicates that no password is used.
        /// </summary>
        public string SavedPassword;

        /// <summary>
        ///     Gets password, with the values of environment variables expanded.
        /// </summary>
        public string ExpandedPassword => SavedPassword != null ? Environment.ExpandEnvironmentVariables(SavedPassword) : null;

        public bool HasPassword
        {
            get => SavedPassword != null;

            set
            {
                if (value)
                {
                    if (SavedPassword == null)
                    {
                        SavedPassword = string.Empty; // Initialize newly-enabled password to empty string.
                    }
                }
                else
                {
                    SavedPassword = null; // Clear password to null when disabled.
                }
            }
        }

        /// <summary>
        ///     Gets or sets a value indicated whether the path is a local path or a remote path.
        /// </summary>
        public bool IsLocalPath;

        /// <summary>
        ///     Gets or sets a value indicated whether this source is enabled or not.
        /// </summary>
        public bool IsEnabled;

        /// <summary>
        ///     Gets a NugetPackage from the NuGet server that matches (or is in range of) the <see cref="NugetPackageIdentifier" /> given.
        /// </summary>
        /// <param name="package">The <see cref="NugetPackageIdentifier" /> containing the ID and Version of the package to get.</param>
        /// <returns>The retrieved package, if there is one.  Null if no matching package was found.</returns>
        public List<NugetPackage> FindPackagesById(NugetPackageIdentifier package)
        {
            List<NugetPackage> foundPackages = null;

            if (IsLocalPath)
            {
                if (!package.HasVersionRange)
                {
                    var localPackagePath = Path.Combine(ExpandedPath, $"{package.Id}.{package.Version}.nupkg");

                    if (!File.Exists(localPackagePath))
                    {
                        // Hierarchical folder structures are supported in NuGet 3.3+.
                        // └─<packageID>
                        //   └─<version>
                        //     └─<packageID>.<version>.nupkg
                        localPackagePath = Path.Combine(ExpandedPath, package.Id, package.Version, $"{package.Id}.{package.Version}.nupkg");
                    }

                    if (File.Exists(localPackagePath))
                    {
                        var localPackage = NugetPackage.FromNupkgFile(localPackagePath);
                        foundPackages = new List<NugetPackage> { localPackage };
                    }
                    else
                    {
                        foundPackages = new List<NugetPackage>();
                    }
                }
                else
                {
                    // TODO: Optimize to no longer use GetLocalPackages, since that loads the .nupkg itself
                    foundPackages = GetLocalPackages($"{package.Id}*", true, true);
                }
            }
            else
            {
                // See here: http://www.odata.org/documentation/odata-version-2-0/uri-conventions/
                var url = string.Format("{0}FindPackagesById()?id='{1}'", ExpandedPath, package.Id);

                // Are we looking for a specific package?
                if (!package.HasVersionRange)
                {
                    url = string.Format("{0}&$filter=Version eq '{1}'", url, package.Version);
                }
                else
                {
                    // As we can't search for a specific Version we need to fetch all versions and remove the non matching versions.
                    // As packages can have so many versions to find the correct version we fetch up to 1000 instead of the default 100.
                    url = string.Format("{0}&$top=1000", url);
                }

                try
                {
                    foundPackages = GetPackagesFromUrl(url, UserName, ExpandedPassword);
                }
                catch (Exception e)
                {
                    foundPackages = new List<NugetPackage>();
                    Debug.LogErrorFormat("Unable to retrieve package list from {0}\n{1}", url, e);
                }
            }

            if (foundPackages != null)
            {
                // Return all the packages in the range of versions specified by 'package'.
                foundPackages.RemoveAll(p => !package.InRange(p));
                foundPackages.Sort();

                foreach (var foundPackage in foundPackages)
                {
                    foundPackage.PackageSource = this;
                }
            }

            return foundPackages;
        }

        /// <summary>
        ///     Gets a NugetPackage from the NuGet server that matches (or is in range of) the <see cref="NugetPackageIdentifier" /> given.
        /// </summary>
        /// <param name="package">The <see cref="NugetPackageIdentifier" /> containing the ID and Version of the package to get.</param>
        /// <returns>The retrieved package, if there is one.  Null if no matching package was found.</returns>
        public NugetPackage GetSpecificPackage(NugetPackageIdentifier package)
        {
            if (package.HasVersionRange)
            {
                // if multiple match we use the lowest version
                return FindPackagesById(package).FirstOrDefault();
            }

            if (IsLocalPath)
            {
                var localPackagePath = Path.Combine(ExpandedPath, string.Format("./{0}.{1}.nupkg", package.Id, package.Version));
                if (File.Exists(localPackagePath))
                {
                    var localPackage = NugetPackage.FromNupkgFile(localPackagePath);
                    localPackage.PackageSource = this;
                    return localPackage;
                }

                return null;
            }

            var url = string.Format("{0}Packages(Id='{1}',Version='{2}')", ExpandedPath, package.Id, package.Version);
            try
            {
                return GetPackagesFromUrl(url, UserName, ExpandedPassword).First();
            }
            catch (Exception e)
            {
                Debug.LogErrorFormat("Unable to retrieve package from {0}\n{1}", url, e);
                return null;
            }
        }

        /// <summary>
        ///     Gets a list of NuGetPackages from this package source.
        ///     This allows searching for partial IDs or even the empty string (the default) to list ALL packages.
        ///     NOTE: See the functions and parameters defined here: https://www.nuget.org/api/v2/$metadata
        /// </summary>
        /// <param name="searchTerm">The search term to use to filter packages. Defaults to the empty string.</param>
        /// <param name="includeAllVersions">True to include older versions that are not the latest version.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="numberToGet">The number of packages to fetch.</param>
        /// <param name="numberToSkip">The number of packages to skip before fetching.</param>
        /// <returns>The list of available packages.</returns>
        public List<NugetPackage> Search(string searchTerm = "",
            bool includeAllVersions = false,
            bool includePrerelease = false,
            int numberToGet = 15,
            int numberToSkip = 0)
        {
            if (IsLocalPath)
            {
                return GetLocalPackages($"*{searchTerm}*", includeAllVersions, includePrerelease, numberToSkip);
            }

            //Example URL: "http://www.nuget.org/api/v2/Search()?$filter=IsLatestVersion&$orderby=Id&$skip=0&$top=30&searchTerm='newtonsoft'&targetFramework=''&includePrerelease=false";

            var url = ExpandedPath;

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

            try
            {
                return GetPackagesFromUrl(url, UserName, ExpandedPassword);
            }
            catch (Exception e)
            {
                Debug.LogErrorFormat("Unable to retrieve package list from {0}\n{1}", url, e);
                return new List<NugetPackage>();
            }
        }

        /// <summary>
        ///     Gets a list of all available packages from a local source (not a web server) that match the given filters.
        /// </summary>
        /// <param name="searchTerm">The search term to use to filter packages. Defaults to the empty string.</param>
        /// <param name="includeAllVersions">True to include older versions that are not the latest version.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="numberToSkip">The number of packages to skip before fetching.</param>
        /// <returns>The list of available packages.</returns>
        private List<NugetPackage> GetLocalPackages(string searchTerm = "",
            bool includeAllVersions = false,
            bool includePrerelease = false,
            int numberToSkip = 0)
        {
            if (string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = "*";
            }

            var localPackages = new List<NugetPackage>();

            if (numberToSkip != 0)
            {
                // we return the entire list the first time, so no more to add
                return localPackages;
            }

            var path = ExpandedPath;

            if (Directory.Exists(path))
            {
                var packagePaths = Directory.GetFiles(path, $"{searchTerm}.nupkg");

                // Hierarchical folder structures are supported in NuGet 3.3+.
                // └─<packageID>
                //   └─<version>
                //     └─<packageID>.<version>.nupkg
                var packagesFromFolders = Directory.GetDirectories(path, searchTerm)
                    .SelectMany(nameFolder => Directory.GetDirectories(nameFolder))
                    .SelectMany(versionFolder => Directory.GetFiles(versionFolder, "*.nupkg"));
                foreach (var packagePath in packagePaths.Concat(packagesFromFolders))
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
        ///     Builds a list of NugetPackages from the XML returned from the HTTP GET request issued at the given URL.
        ///     Note that NuGet uses an Atom-feed (XML Syndicaton) superset called OData.
        ///     See here http://www.odata.org/documentation/odata-version-2-0/uri-conventions/
        /// </summary>
        /// <param name="url"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        private List<NugetPackage> GetPackagesFromUrl(string url, string username, string password)
        {
            NugetHelper.LogVerbose("Getting packages from: {0}", url);

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var packages = new List<NugetPackage>();
            using (var responseStream = NugetHelper.RequestUrl(url, username, password, 10000))
            {
                using (var streamReader = new StreamReader(responseStream))
                {
                    packages = NugetODataResponse.Parse(XDocument.Load(streamReader));
                    foreach (var package in packages)
                    {
                        package.PackageSource = this;
                    }
                }
            }

            stopwatch.Stop();
            NugetHelper.LogVerbose("Received {0} packages in {1} ms", packages.Count, stopwatch.ElapsedMilliseconds);

            return packages;
        }

        /// <summary>
        ///     Gets a list of available packages from a local source (not a web server) that are upgrades for the given list of installed packages.
        /// </summary>
        /// <param name="installedPackages">The list of currently installed packages to use to find updates.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="includeAllVersions">True to include older versions that are not the latest version.</param>
        /// <returns>A list of all updates available.</returns>
        private List<NugetPackage> GetLocalUpdates(IEnumerable<NugetPackage> installedPackages,
            bool includePrerelease = false,
            bool includeAllVersions = false)
        {
            var updates = new List<NugetPackage>();

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
        ///     Queries the source with the given list of installed packages to get any updates that are available.
        /// </summary>
        /// <param name="installedPackages">The list of currently installed packages.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="includeAllVersions">True to include older versions that are not the latest version.</param>
        /// <param name="targetFrameworks">The specific frameworks to target?</param>
        /// <param name="versionContraints">The version constraints?</param>
        /// <returns>A list of all updates available.</returns>
        public List<NugetPackage> GetUpdates(IEnumerable<NugetPackage> installedPackages,
            bool includePrerelease = false,
            bool includeAllVersions = false,
            string targetFrameworks = "",
            string versionContraints = "")
        {
            if (IsLocalPath)
            {
                return GetLocalUpdates(installedPackages, includePrerelease, includeAllVersions);
            }

            var updates = new List<NugetPackage>();

            // check for updates in groups of 10 instead of all of them, since that causes servers to throw errors for queries that are too long
            for (var i = 0; i < installedPackages.Count(); i += 10)
            {
                var packageGroup = installedPackages.Skip(i).Take(10);

                var packageIds = string.Empty;
                var versions = string.Empty;

                foreach (var package in packageGroup)
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

                var url = string.Format(
                    "{0}GetUpdates()?packageIds='{1}'&versions='{2}'&includePrerelease={3}&includeAllVersions={4}&targetFrameworks='{5}'&versionConstraints='{6}'",
                    ExpandedPath,
                    packageIds,
                    versions,
                    includePrerelease.ToString().ToLower(),
                    includeAllVersions.ToString().ToLower(),
                    targetFrameworks,
                    versionContraints);

                try
                {
                    var newPackages = GetPackagesFromUrl(url, UserName, ExpandedPassword);
                    updates.AddRange(newPackages);
                }
                catch (Exception e)
                {
                    var webException = e as WebException;
                    var webResponse = webException != null ? webException.Response as HttpWebResponse : null;
                    if (webResponse != null && webResponse.StatusCode == HttpStatusCode.NotFound)
                    {
                        // Some web services, such as VSTS don't support the GetUpdates API. Attempt to retrieve updates via FindPackagesById.
                        NugetHelper.LogVerbose("{0} not found. Falling back to FindPackagesById.", url);
                        return GetUpdatesFallback(installedPackages, includePrerelease, includeAllVersions, targetFrameworks, versionContraints);
                    }

                    Debug.LogErrorFormat("Unable to retrieve package list from {0}\n{1}", url, e);
                }
            }

            // sort alphabetically, then by version descending
            updates.Sort(
                delegate(NugetPackage x, NugetPackage y)
                {
                    if (x.Id == y.Id)
                    {
                        return -1 * x.CompareVersion(y.Version);
                    }

                    return x.Id.CompareTo(y.Id);
                });

#if TEST_GET_UPDATES_FALLBACK
            // Enable this define in order to test that GetUpdatesFallback is working as intended. This tests that it returns the same set of packages
            // that are returned by the GetUpdates API. Since GetUpdates isn't available when using a Visual Studio Team Services feed, the intention
            // is that this test would be conducted by using nuget.org's feed where both paths can be compared.
            List<NugetPackage> updatesReplacement =
 GetUpdatesFallback(installedPackages, includePrerelease, includeAllVersions, targetFrameworks, versionContraints);
            ComparePackageLists(updates, updatesReplacement, "GetUpdatesFallback doesn't match GetUpdates API");
#endif

            return updates;
        }

#if TEST_GET_UPDATES_FALLBACK
        private static void ComparePackageLists(List<NugetPackage> updates,
            List<NugetPackage> updatesReplacement,
            string errorMessageToDisplayIfListsDoNotMatch)
        {
            var matchingComparison = new StringBuilder();
            var missingComparison = new StringBuilder();
            foreach (var package in updates)
            {
                if (updatesReplacement.Contains(package))
                {
                    matchingComparison.Append(matchingComparison.Length == 0 ? "Matching: " : ", ");
                    matchingComparison.Append(package);
                }
                else
                {
                    missingComparison.Append(missingComparison.Length == 0 ? "Missing: " : ", ");
                    missingComparison.Append(package);
                }
            }

            var extraComparison = new StringBuilder();
            foreach (var package in updatesReplacement)
            {
                if (!updates.Contains(package))
                {
                    extraComparison.Append(extraComparison.Length == 0 ? "Extra: " : ", ");
                    extraComparison.Append(package);
                }
            }

            if (missingComparison.Length > 0 || extraComparison.Length > 0)
            {
                Debug.LogWarningFormat(
                    "{0}\n{1}\n{2}\n{3}",
                    errorMessageToDisplayIfListsDoNotMatch,
                    matchingComparison,
                    missingComparison,
                    extraComparison);
            }
        }
#endif

        /// <summary>
        ///     Some NuGet feeds such as Visual Studio Team Services do not implement the GetUpdates function.
        ///     In that case this fallback function can be used to retrieve updates by using the FindPackagesById function.
        /// </summary>
        /// <param name="installedPackages">The list of currently installed packages.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="includeAllVersions">True to include older versions that are not the latest version.</param>
        /// <param name="targetFrameworks">The specific frameworks to target?</param>
        /// <param name="versionContraints">The version constraints?</param>
        /// <returns>A list of all updates available.</returns>
        private List<NugetPackage> GetUpdatesFallback(IEnumerable<NugetPackage> installedPackages,
            bool includePrerelease = false,
            bool includeAllVersions = false,
            string targetFrameworks = "",
            string versionContraints = "")
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            Debug.Assert(
                string.IsNullOrEmpty(targetFrameworks) &&
                string.IsNullOrEmpty(versionContraints)); // These features are not supported by this version of GetUpdates.

            var updates = new List<NugetPackage>();
            foreach (var installedPackage in installedPackages)
            {
                var versionRange = string.Format(
                    "({0},)",
                    installedPackage.Version); // Minimum of Current ID (exclusive) with no maximum (exclusive).
                var id = new NugetPackageIdentifier(installedPackage.Id, versionRange);
                var packageUpdates = FindPackagesById(id);

                if (!includePrerelease)
                {
                    packageUpdates.RemoveAll(p => p.IsPrerelease);
                }

                if (packageUpdates.Count == 0)
                {
                    continue;
                }

                var skip = includeAllVersions ? 0 : packageUpdates.Count - 1;
                updates.AddRange(packageUpdates.Skip(skip));
            }

            NugetHelper.LogVerbose("NugetPackageSource.GetUpdatesFallback took {0} ms", stopwatch.ElapsedMilliseconds);
            return updates;
        }
    }
}
