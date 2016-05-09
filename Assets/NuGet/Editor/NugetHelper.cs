namespace NugetForUnity
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
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
        public static readonly string NugetConfigFilePath = Path.Combine(NugetPath, "../NuGet.config");

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
        public static readonly string PackOutputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\NuGet\\Cache";

        /// <summary>
        /// The amount of time, in milliseconds, before the nuget.exe process times out and is killed.
        /// </summary>
        private const int TimeOut = 60000;

        /// <summary>
        /// The loaded NuGet.config file that holds the settings for NuGet.
        /// </summary>
        public static NugetConfigFile NugetConfigFile { get; private set; }

        /// <summary>
        /// Gets the loaded packages.config file that hold the dependencies for the project.
        /// </summary>
        public static PackagesConfigFile PackagesConfigFile { get; private set; }

        /// <summary>
        /// Static constructor used by Unity to restore packages defined in packages.config.
        /// </summary>
        static NugetHelper()
        {
            // Load the NuGet.config file
            LoadNugetConfigFile();

            PackagesConfigFile = PackagesConfigFile.Load(PackagesConfigFilePath);

            // create the nupkgs directory, if it doesn't exist
            if (!Directory.Exists(PackOutputDirectory))
            {
                Directory.CreateDirectory(PackOutputDirectory);
            }

            // restore packages - this will be called EVERY time the project is loaded or a code-file changes
            Restore();
        }

        /// <summary>
        /// Loads the NuGet.config file.
        /// </summary>
        public static void LoadNugetConfigFile()
        {
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
        /// Replace all %20 encodings with a normal space.
        /// </summary>
        /// <param name="directoryPath">The path to the directory.</param>
        private static void FixSpaces(string directoryPath)
        {
            string[] subdirectories = Directory.GetDirectories(directoryPath);
            foreach (var subDir in subdirectories)
            {
                FixSpaces(subDir);
            }

            if (directoryPath.Contains("%20"))
            {
                LogVerbose("Removing %20 from {0}", directoryPath);
                Directory.Move(directoryPath, directoryPath.Replace("%20", " "));
                directoryPath = directoryPath.Replace("%20", " ");
            }

            string[] files = Directory.GetFiles(directoryPath);
            foreach (var file in files)
            {
                if (file.Contains("%20"))
                {
                    LogVerbose("Removing %20 from {0}", file);
                    File.Move(file, file.Replace("%20", " "));
                }
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

            FixSpaces(packageInstallDirectory);

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
                    string directoryName = new DirectoryInfo(directory).Name;
                    if (directoriesToDelete.Any(directoryName.Contains))
                    {
                        LogVerbose("Deleting unused directory: {0}", directory);
                        DeleteDirectory(directory);
                    }
                    else if (directoryName.Contains("net20") && (has30 || has35))
                    {
                        // if .NET 2.0 exists, keep it, unless there is also a .NET 3.0 or 3.5 version as well
                        LogVerbose("Deleting net20: {0}", directory);
                        DeleteDirectory(directory);
                    }
                    else if (directoryName.Contains("net30") && has35)
                    {
                        // if .NET 3.0 exists, keep it, unless there is also a .NET 3.5 version as well
                        LogVerbose("Deleting net30: {0}", directory);
                        DeleteDirectory(directory);
                    }
                }
            }

            // if there are native DLLs, copy them to the Unity project root (1 up from Assets)
            if (Directory.Exists(packageInstallDirectory + "/output"))
            {
                string[] files = Directory.GetFiles(packageInstallDirectory + "/output");
                foreach (string file in files)
                {
                    string newFilePath = Directory.GetCurrentDirectory() + "/" + Path.GetFileName(file);
                    LogVerbose("Moving {0} to {1}", file, newFilePath);
                    if (File.Exists(newFilePath))
                    {
                        File.Delete(newFilePath);
                    }
                    File.Move(file, newFilePath);
                }

                LogVerbose("Deleting {0}", packageInstallDirectory + "/output");

                DeleteDirectory(packageInstallDirectory + "/output");
            }

            // if there are Unity plugin DLLs, copy them to the Unity Plugins folder (Assets/Plugins)
            if (Directory.Exists(packageInstallDirectory + "/unityplugin"))
            {
                string pluginsDirectory = Application.dataPath + "/Plugins/";

                if (!Directory.Exists(pluginsDirectory))
                {
                    Directory.CreateDirectory(pluginsDirectory);
                }

                string[] files = Directory.GetFiles(packageInstallDirectory + "/unityplugin");
                foreach (string file in files)
                {
                    string newFilePath = pluginsDirectory + Path.GetFileName(file);

                    try
                    {
                        LogVerbose("Moving {0} to {1}", file, newFilePath);
                        if (File.Exists(newFilePath))
                        {
                            File.Delete(newFilePath);
                        }
                        File.Move(file, newFilePath);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Debug.LogWarningFormat("{0} couldn't be overwritten. It may be a native plugin already locked by Unity. Please close Unity and manually delete it.", newFilePath);
                    }
                }

                LogVerbose("Deleting {0}", packageInstallDirectory + "/unityplugin");

                DeleteDirectory(packageInstallDirectory + "/unityplugin");
            }

            // if there are Unity StreamingAssets, copy them to the Unity StreamingAssets folder (Assets/StreamingAssets)
            if (Directory.Exists(packageInstallDirectory + "/StreamingAssets"))
            {
                string streamingAssetsDirectory = Application.dataPath + "/StreamingAssets/";

                if (!Directory.Exists(streamingAssetsDirectory))
                {
                    Directory.CreateDirectory(streamingAssetsDirectory);
                }

                // move the files
                string[] files = Directory.GetFiles(packageInstallDirectory + "/StreamingAssets");
                foreach (string file in files)
                {
                    string newFilePath = streamingAssetsDirectory + Path.GetFileName(file);

                    try
                    {
                        LogVerbose("Moving {0} to {1}", file, newFilePath);
                        if (File.Exists(newFilePath))
                        {
                            File.Delete(newFilePath);
                        }
                        File.Move(file, newFilePath);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarningFormat("{0} couldn't be overwritten. {1}", newFilePath, e.ToString());
                    }
                }

                // move the directories
                string[] directories = Directory.GetDirectories(packageInstallDirectory + "/StreamingAssets");
                foreach (string directory in directories)
                {
                    string newDirectoryPath = streamingAssetsDirectory + Path.GetFileName(Path.GetDirectoryName(directory));

                    try
                    {
                        LogVerbose("Moving {0} to {1}", directory, newDirectoryPath);
                        if (Directory.Exists(newDirectoryPath))
                        {
                            Directory.Delete(newDirectoryPath);
                        }
                        Directory.Move(directory, newDirectoryPath);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarningFormat("{0} couldn't be moved. {1}", newDirectoryPath, e.ToString());
                    }
                }

                LogVerbose("Deleting {0}", packageInstallDirectory + "/StreamingAssets");

                DeleteDirectory(packageInstallDirectory + "/StreamingAssets");
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

            string metaFile = Path.Combine(NugetConfigFile.RepositoryPath, string.Format("{0}.{1}.meta", package.Id, package.Version));
            DeleteFile(metaFile);

            PackagesConfigFile.RemovePackage(package);
            PackagesConfigFile.Save(PackagesConfigFilePath);

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
        /// Gets a list of all currently installed packages by reading the packages.config file and then reading the .nuspec file inside the .nupkg file.
        /// </summary>
        /// <returns>A list of installed NugetPackages.</returns>
        public static List<NugetPackage> GetInstalledPackages()
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            List<NugetPackage> fullPackages = new List<NugetPackage>(PackagesConfigFile.Packages.Count);

            foreach (var package in PackagesConfigFile.Packages)
            {
                string installedPackagePath = Path.Combine(NugetConfigFile.RepositoryPath, string.Format("{0}.{1}/{0}.{1}.nupkg", package.Id, package.Version));

                // if the .nupkg file doesn't exist in the installed directory, it was probably installed via an older version.  Copy it now from the cached location, if it exists.
                if (!File.Exists(installedPackagePath))
                {
                    string cachedPackagePath = Path.Combine(PackOutputDirectory, string.Format("./{0}.{1}.nupkg", package.Id, package.Version));
                    if (File.Exists(cachedPackagePath) && Directory.Exists(Path.GetDirectoryName(installedPackagePath)))
                    {
                        File.Copy(cachedPackagePath, installedPackagePath);
                    }
                }

                // get the NugetPackage via the .nuspec file inside the .nupkg file
                fullPackages.Add(NugetPackage.FromNupkgFile(installedPackagePath));
            }

            // sort alphabetically
            fullPackages.Sort(delegate(NugetPackage x, NugetPackage y)
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

            stopwatch.Stop();
            LogVerbose("Getting installed packages locally took {0} ms", stopwatch.ElapsedMilliseconds);

            return fullPackages;
        }

        /// <summary>
        /// Gets a list of NuGetPackages via the HTTP Search() function defined by NuGet.Server and NuGet Gallery.
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
        public static List<NugetPackage> Search(string searchTerm = "", bool includeAllVersions = false, bool includePrerelease = false, int numberToGet = 15, int numberToSkip = 0)
        {
            List<NugetPackage> packages = new List<NugetPackage>();

            if (NugetConfigFile.ActivePackageSource.Path == "(Aggregate source)")
            {
                // Loop through all active sources and combine them into a single list
                foreach (var source in NugetConfigFile.PackageSources.Where(s => s.IsEnabled))
                {
                    var newPackages = source.Search(searchTerm, includeAllVersions, includePrerelease, numberToGet, numberToSkip);
                    packages.AddRange(newPackages);
                    packages = packages.Distinct().ToList();
                }
            }
            else
            {
                packages = NugetConfigFile.ActivePackageSource.Search(searchTerm, includeAllVersions, includePrerelease, numberToGet, numberToSkip);
            }

            return packages;
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
        public static List<NugetPackage> GetUpdates(List<NugetPackage> installedPackages, bool includePrerelease = false, bool includeAllVersions = false, string targetFrameworks = "", string versionContraints = "")
        {
            List<NugetPackage> packages = new List<NugetPackage>();

            if (NugetConfigFile.ActivePackageSource.Path == "(Aggregate source)")
            {
                // Loop through all active sources and combine them into a single list
                foreach (var source in NugetConfigFile.PackageSources.Where(s => s.IsEnabled))
                {
                    var newPackages = source.GetUpdates(installedPackages, includePrerelease, includeAllVersions, targetFrameworks, versionContraints);
                    packages.AddRange(newPackages);
                    packages = packages.Distinct().ToList();
                }
            }
            else
            {
                packages = NugetConfigFile.ActivePackageSource.GetUpdates(installedPackages, includePrerelease, includeAllVersions, targetFrameworks, versionContraints);
            }

            return packages;
        }

        /// <summary>
        /// Gets a NugetPackage from the NuGet server with the exact ID and Version given.
        /// If an exact match isn't found, it selects the next closest version available.
        /// </summary>
        /// <param name="packageId">The <see cref="NugetPackageIdentifier"/> containing the ID and Version of the package to get.</param>
        /// <returns>The retrieved package, if there is one.  Null if no matching package was found.</returns>
        private static NugetPackage GetSpecificPackage(NugetPackageIdentifier packageId)
        {
            NugetPackage package = null;

            if (NugetConfigFile.ActivePackageSource.Path == "(Aggregate source)")
            {
                // Loop through all active sources and stop once the package is found
                foreach (var source in NugetConfigFile.PackageSources.Where(s => s.IsEnabled))
                {
                    var foundPackage = source.GetSpecificPackage(packageId);
                    if (foundPackage != null)
                    {
                        if (foundPackage == packageId)
                        {
                            // the found package matches the ID identically
                            package = foundPackage;
                            break;
                        }
                        
                        if (foundPackage > packageId)
                        {
                            // the found package does NOT match the ID identically
                            if (package == null)
                            {
                                // if another package hasn't been found yet, use the new one
                                package = foundPackage;
                            }
                            else
                            {
                                // another package has been found previously, but neither match identically
                                if (foundPackage < package)
                                {
                                    // use the new package if it's closer to the desired version
                                    package = foundPackage;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                package = NugetConfigFile.ActivePackageSource.GetSpecificPackage(packageId);
            }

            return package;
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
            // If the package, or a later version, is already installed and use it.
            // If an earlier version is installed, update it.
            // If not installed, look on the server for specific version
            // If specific version not found on server, use the next version up (not latest)

            // copy the list since the Update operation below changes the actual installed packages list
            var installedPackages = new List<NugetPackageIdentifier>(PackagesConfigFile.Packages);

            foreach (var installedPackage in installedPackages)
            {
                if (installedPackage.Id == package.Id && IsInstalled(installedPackage))
                {
                    if (installedPackage < package)
                    {
                        // the installed version is older than the version to install, so update it
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
        public static void LogVerbose(string format, params object[] args)
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
            try
            {
                LogVerbose("Installing: {0} {1}", package.Id, package.Version);

                if (refreshAssets)
                    EditorUtility.DisplayProgressBar(string.Format("Installing {0} {1}", package.Id, package.Version), "Installing Dependencies", 0.1f);

                foreach (var dependency in package.Dependencies)
                {
                    bool alreadyListed = false;

                    // look in the packages.config file to see if the dependency, the same or newer version, is already listed
                    foreach (var installedPackage in PackagesConfigFile.Packages)
                    {
                        if (installedPackage.Id == package.Id)
                        {
                            alreadyListed = installedPackage >= package;
                            break;
                        }
                    }

                    if (!alreadyListed)
                    {
                        LogVerbose("Installing Dependency: {0} {1}", dependency.Id, dependency.Version);
                        InstallIdentifier(dependency, false);
                    }
                    else
                    {
                        LogVerbose("Skipping Dependency: {0} {1}", dependency.Id, dependency.Version);
                    }
                }

                string cachedPackagePath = Path.Combine(PackOutputDirectory, string.Format("./{0}.{1}.nupkg", package.Id, package.Version));
                if (NugetPreferences.UseCache && File.Exists(cachedPackagePath))
                {
                    LogVerbose("Cached package found for {0} {1}", package.Id, package.Version);
                }
                else
                {
                    if (package.PackageSource.IsLocalPath)
                    {
                        LogVerbose("Caching local package {0} {1}", package.Id, package.Version);

                        // copy the .nupkg from the local path to the cache
                        File.Copy(Path.Combine(package.PackageSource.Path, string.Format("./{0}.{1}.nupkg", package.Id, package.Version)), cachedPackagePath, true);
                    }
                    else
                    {
                        // Mono doesn't have a Certificate Authority, so we have to provide all validation manually.  Currently just accept anything.
                        // See here: http://stackoverflow.com/questions/4926676/mono-webrequest-fails-with-https

                        // remove all handlers
                        //if (ServicePointManager.ServerCertificateValidationCallback != null)
                        //    foreach (var d in ServicePointManager.ServerCertificateValidationCallback.GetInvocationList())
                        //        ServicePointManager.ServerCertificateValidationCallback -= (d as System.Net.Security.RemoteCertificateValidationCallback);
                        ServicePointManager.ServerCertificateValidationCallback = null;

                        // add anonymous handler
                        ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, policyErrors) => true;

                        LogVerbose("Downloading package {0} {1}", package.Id, package.Version);

                        if (refreshAssets)
                            EditorUtility.DisplayProgressBar(string.Format("Installing {0} {1}", package.Id, package.Version), "Downloading Package", 0.3f);

                        HttpWebRequest getRequest = (HttpWebRequest) WebRequest.Create(package.DownloadUrl);
                        Stream objStream = getRequest.GetResponse().GetResponseStream();

                        using (Stream file = File.Create(cachedPackagePath))
                        {
                            CopyStream(objStream, file);
                        }
                    }
                }

                if (refreshAssets)
                    EditorUtility.DisplayProgressBar(string.Format("Installing {0} {1}", package.Id, package.Version), "Extracting Package", 0.6f);

                if (File.Exists(cachedPackagePath))
                {
                    // unzip the package
                    using (ZipFile zip = ZipFile.Read(cachedPackagePath))
                    {
                        foreach (ZipEntry entry in zip)
                        {
                            entry.Extract(Path.Combine(NugetConfigFile.RepositoryPath, string.Format("{0}.{1}", package.Id, package.Version)), ExtractExistingFileAction.OverwriteSilently);
                        }
                    }

                    // copy the .nupkg inside the Unity project
                    File.Copy(cachedPackagePath, Path.Combine(NugetConfigFile.RepositoryPath, string.Format("{0}.{1}/{0}.{1}.nupkg", package.Id, package.Version)), true);
                }
                else
                {
                    Debug.LogErrorFormat("File not found: {0}", cachedPackagePath);
                }

                if (refreshAssets)
                    EditorUtility.DisplayProgressBar(string.Format("Installing {0} {1}", package.Id, package.Version), "Cleaning Package", 0.9f);

                // clean
                Clean(package);

                // update packages.config
                PackagesConfigFile.AddPackage(package);
                PackagesConfigFile.Save(PackagesConfigFilePath);
            }
            catch (Exception e)
            {
                Debug.LogErrorFormat("{0}", e.ToString());
            }
            finally
            {
                if (refreshAssets)
                {
                    EditorUtility.DisplayProgressBar(string.Format("Installing {0} {1}", package.Id, package.Version), "Importing Package", 0.95f);
                    AssetDatabase.Refresh();
                    EditorUtility.ClearProgressBar();
                }
            }
        }

        /// <summary>
        /// Restores all packages defined in packages.config.
        /// </summary>
        public static void Restore()
        {
            try
            {
                // TODO: Is this reload needed?
                PackagesConfigFile = PackagesConfigFile.Load(PackagesConfigFilePath);

                float progressStep = 1.0f / PackagesConfigFile.Packages.Count;
                float currentProgress = 0;

                // copy the list since the InstallIdentifier operation below changes the actual installed packages list
                var installedPackages = new List<NugetPackageIdentifier>(PackagesConfigFile.Packages);

                LogVerbose("Restoring {0} packages.", installedPackages.Count);

                foreach (var package in installedPackages)
                {
                    if (package != null)
                    {
                        EditorUtility.DisplayProgressBar("Restoring NuGet Packages", string.Format("Restoring {0}", package.Id), currentProgress);
                        
                        if (!IsInstalled(package))
                        {
                            LogVerbose("   Installing {0}", package.Id);
                            InstallIdentifier(package, false);
                        }
                        else
                        {
                            LogVerbose("   Already installed: {0}", package.Id);
                        }
                    }

                    currentProgress += progressStep;
                }
            }
            catch (Exception e)
            {
                Debug.LogErrorFormat("{0}", e.ToString());
            }
            finally
            {
                AssetDatabase.Refresh();
                EditorUtility.ClearProgressBar();
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
        public static Texture2D DownloadImage(string url)
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
