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
        /// The path to the nuget.config file.
        /// </summary>
        public static readonly string NugetConfigFilePath = Path.Combine(Application.dataPath, "./NuGet.config");

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
        /// Backing field for the packages.config file.
        /// </summary>
        private static PackagesConfigFile packagesConfigFile;

        /// <summary>
        /// Gets the loaded packages.config file that hold the dependencies for the project.
        /// </summary>
        public static PackagesConfigFile PackagesConfigFile
        {
            get
            {
                if (packagesConfigFile == null)
                {
                    packagesConfigFile = PackagesConfigFile.Load(PackagesConfigFilePath);
                }

                return packagesConfigFile;
            }
        }

        /// <summary>
        /// The list of <see cref="NugetPackageSource"/>s to use.
        /// </summary>
        private static List<NugetPackageSource> packageSources = new List<NugetPackageSource>();

        /// <summary>
        /// The dictionary of currently installed <see cref="NugetPackage"/>s keyed off of their ID string.
        /// </summary>
        private static Dictionary<string, NugetPackage> installedPackages = new Dictionary<string, NugetPackage>();

        /// <summary>
        /// The current .NET version being used (2.0 [actually 3.5], 4.6, etc).
        /// </summary>
        internal static ApiCompatibilityLevel DotNetVersion;

        /// <summary>
        /// Static constructor used by Unity to initialize NuGet and restore packages defined in packages.config.
        /// </summary>
        static NugetHelper()
        {
            // if we are entering playmode, don't do anything
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            // get the .NET version being used, since we don't want a specific platform, send unknown
            //DotNetVersion = PlayerSettings.GetApiCompatibilityLevel(BuildTargetGroup.Unknown); // Unity 5.6+ way
            DotNetVersion = PlayerSettings.apiCompatibilityLevel; // Added in Unity 4.0 and marked as deprecated in Unity 5.6, but still exists and works in Unity 2017.1

            // Load the NuGet.config file
            LoadNugetConfigFile();

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

            // parse any command line arguments
            //LogVerbose("Command line: {0}", Environment.CommandLine);
            packageSources.Clear();
            bool readingSources = false;
            bool useCommandLineSources = false;
            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (readingSources)
                {
                    if (arg.StartsWith("-"))
                    {
                        readingSources = false;
                    }
                    else
                    {
                        NugetPackageSource source = new NugetPackageSource("CMD_LINE_SRC_" + packageSources.Count, arg);
                        LogVerbose("Adding command line package source {0} at {1}", "CMD_LINE_SRC_" + packageSources.Count, arg);
                        packageSources.Add(source);
                    }
                }

                if (arg == "-Source")
                {
                    // if the source is being forced, don't install packages from the cache
                    NugetConfigFile.InstallFromCache = false;
                    readingSources = true;
                    useCommandLineSources = true;
                }
            }

            // if there are not command line overrides, use the NuGet.config package sources
            if (!useCommandLineSources)
            {
                if (NugetConfigFile.ActivePackageSource.Path == "(Aggregate source)")
                {
                    packageSources.AddRange(NugetConfigFile.PackageSources);
                }
                else
                {
                    packageSources.Add(NugetConfigFile.ActivePackageSource);
                }
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
            // Try to find any nuget.exe in the package tools installation location
            string toolsPackagesFolder = Path.Combine(Application.dataPath, "../Packages");

            // create the folder to prevent an exception when getting the files
            Directory.CreateDirectory(toolsPackagesFolder);

            string[] files = Directory.GetFiles(toolsPackagesFolder, "nuget.exe", SearchOption.AllDirectories);
            if (files.Length > 1)
            {
                Debug.LogWarningFormat("More than one nuget.exe found. Using first one.");
            }
            else if (files.Length < 1)
            {
                Debug.LogWarningFormat("No nuget.exe found! Attemping to install the NuGet.CommandLine package.");
                InstallIdentifier(new NugetPackageIdentifier("NuGet.CommandLine", "2.8.6"));
                files = Directory.GetFiles(toolsPackagesFolder, "nuget.exe", SearchOption.AllDirectories);
                if (files.Length < 1)
                {
                    Debug.LogErrorFormat("nuget.exe still not found. Quiting...");
                    return;
                }
            }

            LogVerbose("Running: {0} \nArgs: {1}", files[0], arguments);

            Process process = new Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.FileName = files[0];
            process.StartInfo.Arguments = arguments;
            process.StartInfo.CreateNoWindow = true;
            //process.StartInfo.WorkingDirectory = Path.GetDirectoryName(files[0]);

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
            if (directoryPath.Contains("%20"))
            {
                LogVerbose("Removing %20 from {0}", directoryPath);
                Directory.Move(directoryPath, directoryPath.Replace("%20", " "));
                directoryPath = directoryPath.Replace("%20", " ");
            }

            string[] subdirectories = Directory.GetDirectories(directoryPath);
            foreach (var subDir in subdirectories)
            {
                FixSpaces(subDir);
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

            // Unity has no use for the build directory
            DeleteDirectory(packageInstallDirectory + "/build");

            // For now, delete Content.  We may use it later...
            DeleteDirectory(packageInstallDirectory + "/Content");

            // For now, delete src.  We may use it later...
            DeleteDirectory(packageInstallDirectory + "/src");

            // Delete documentation folders since they sometimes have HTML docs with JavaScript, which Unity tried to parse as "UnityScript"
            DeleteDirectory(packageInstallDirectory + "/docs");

            if (Directory.Exists(packageInstallDirectory + "/lib"))
            {
                int intDotNetVersion = (int)DotNetVersion; // c
                //bool using46 = DotNetVersion == ApiCompatibilityLevel.NET_4_6; // NET_4_6 option was added in Unity 5.6
                bool using46 = intDotNetVersion == 3; // NET_4_6 = 3 in Unity 5.6 and Unity 2017.1 - use the hard-coded int value to ensure it works in earlier versions of Unity
                var selectedDirectories = new List<string>();

                // go through the library folders in descending order (highest to lowest version)
                var libDirectories = Directory.GetDirectories(packageInstallDirectory + "/lib").Select(s => new DirectoryInfo(s)).OrderByDescending(di => di.Name.ToLower());
                foreach (var directory in libDirectories)
                {
                    string directoryName = directory.Name.ToLower();

                    // Select the highest .NET library available that is supported
                    // See here: https://docs.nuget.org/ndocs/schema/target-frameworks
                    if (using46 && directoryName == "net462")
                    {
                        selectedDirectories.Add(directory.FullName);
                        break;
                    }
                    else if (using46 && directoryName == "net461")
                    {
                        selectedDirectories.Add(directory.FullName);
                        break;
                    }
                    else if (using46 && directoryName == "net46")
                    {
                        selectedDirectories.Add(directory.FullName);
                        break;
                    }
                    else if (using46 && directoryName == "net452")
                    {
                        selectedDirectories.Add(directory.FullName);
                        break;
                    }
                    else if (using46 && directoryName == "net451")
                    {
                        selectedDirectories.Add(directory.FullName);
                        break;
                    }
                    else if (using46 && directoryName == "net45")
                    {
                        selectedDirectories.Add(directory.FullName);
                        break;
                    }
                    else if (using46 && directoryName == "net403")
                    {
                        selectedDirectories.Add(directory.FullName);
                        break;
                    }
                    else if (using46 && (directoryName == "net40" || directoryName == "net4"))
                    {
                        selectedDirectories.Add(directory.FullName);
                        break;
                    }
                    else if (
                        directoryName == "unity" ||
                        directoryName == "net35-unity full v3.5" ||
                        directoryName == "net35-unity subset v3.5")
                    {
                        // Keep all directories targeting Unity within a package
                        selectedDirectories.Add(Path.Combine(directory.Parent.FullName, "unity"));
                        selectedDirectories.Add(Path.Combine(directory.Parent.FullName, "net35-unity full v3.5"));
                        selectedDirectories.Add(Path.Combine(directory.Parent.FullName, "net35-unity subset v3.5"));
                        break;
                    }
                    else if (directoryName == "net35")
                    {
                        selectedDirectories.Add(directory.FullName);
                        break;
                    }
                    else if (directoryName == "net20")
                    {
                        selectedDirectories.Add(directory.FullName);
                        break;
                    }
                    else if (directoryName == "net11")
                    {
                        selectedDirectories.Add(directory.FullName);
                        break;
                    }
                }


                foreach (var dir in selectedDirectories)
                {
                    LogVerbose("Using {0}", dir);
                }

                // delete all of the libaries except for the selected one
                foreach (var directory in libDirectories)
                {
                    if (!selectedDirectories.Contains(directory.FullName))
                    {
                        DeleteDirectory(directory.FullName);
                    }
                }
            }

            if (Directory.Exists(packageInstallDirectory + "/tools"))
            {
                // Move the tools folder outside of the Unity Assets folder
                string toolsInstallDirectory = Path.Combine(Application.dataPath, string.Format("../Packages/{0}.{1}/tools", package.Id, package.Version));

                LogVerbose("Moving {0} to {1}", packageInstallDirectory + "/tools", toolsInstallDirectory);

                // create the directory to create any of the missing folders in the path
                Directory.CreateDirectory(toolsInstallDirectory);

                // delete the final directory to prevent the Move operation from throwing exceptions.
                DeleteDirectory(toolsInstallDirectory);

                Directory.Move(packageInstallDirectory + "/tools", toolsInstallDirectory);
            }

            // delete all PDB files since Unity uses Mono and requires MDB files, which causes it to output "missing MDB" errors
            DeleteAllFiles(packageInstallDirectory, "*.pdb");

            // if there are native DLLs, copy them to the Unity project root (1 up from Assets)
            if (Directory.Exists(packageInstallDirectory + "/output"))
            {
                string[] files = Directory.GetFiles(packageInstallDirectory + "/output");
                foreach (string file in files)
                {
                    string newFilePath = Directory.GetCurrentDirectory() + "/" + Path.GetFileName(file);
                    LogVerbose("Moving {0} to {1}", file, newFilePath);
                    DeleteFile(newFilePath);
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
                        DeleteFile(newFilePath);
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
                        DeleteFile(newFilePath);
                        File.Move(file, newFilePath);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarningFormat("{0} couldn't be moved. \n{1}", newFilePath, e.ToString());
                    }
                }

                // move the directories
                string[] directories = Directory.GetDirectories(packageInstallDirectory + "/StreamingAssets");
                foreach (string directory in directories)
                {
                    string newDirectoryPath = streamingAssetsDirectory + new DirectoryInfo(directory).Name;

                    try
                    {
                        LogVerbose("Moving {0} to {1}", directory, newDirectoryPath);
                        if (Directory.Exists(newDirectoryPath))
                        {
                            DeleteDirectory(newDirectoryPath);
                        }
                        Directory.Move(directory, newDirectoryPath);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarningFormat("{0} couldn't be moved. \n{1}", newDirectoryPath, e.ToString());
                    }
                }

                // delete the package's StreamingAssets folder and .meta file
                LogVerbose("Deleting {0}", packageInstallDirectory + "/StreamingAssets");
                DeleteDirectory(packageInstallDirectory + "/StreamingAssets");
                DeleteFile(packageInstallDirectory + "/StreamingAssets.meta");
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

            // Use -NoDefaultExcludes to allow files and folders that start with a . to be packed into the package
            // This is done because if you want a file/folder in a Unity project, but you want Unity to ignore it, it must start with a .
            // This is especially useful for .cs and .js files that you don't want Unity to compile as game scripts
            string arguments = string.Format("pack \"{0}\" -OutputDirectory \"{1}\" -NoDefaultExcludes", nuspecFilePath, PackOutputDirectory);

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
                File.SetAttributes(filePath, FileAttributes.Normal);
                File.Delete(filePath);
            }
        }

        /// <summary>
        /// Deletes all files in the given directory or in any sub-directory, with the given extension.
        /// </summary>
        /// <param name="directoryPath">The path to the directory to delete all files of the given extension from.</param>
        /// <param name="extension">The extension of the files to delete, in the form "*.ext"</param>
        private static void DeleteAllFiles(string directoryPath, string extension)
        {
            string[] files = Directory.GetFiles(directoryPath, extension, SearchOption.AllDirectories);
            foreach (var file in files)
            {
                DeleteFile(file);
            }
        }

        /// <summary>
        /// Uninstalls all of the currently installed packages.
        /// </summary>
        internal static void UninstallAll()
        {
            foreach (var package in installedPackages.Values.ToList())
            {
                Uninstall(package);
            }
        }

        /// <summary>
        /// "Uninstalls" the given package by simply deleting its folder.
        /// </summary>
        /// <param name="package">The NugetPackage to uninstall.</param>
        /// <param name="refreshAssets">True to force Unity to refesh its Assets folder.  False to temporarily ignore the change.  Defaults to true.</param>
        public static void Uninstall(NugetPackageIdentifier package, bool refreshAssets = true)
        {
            LogVerbose("Uninstalling: {0} {1}", package.Id, package.Version);

            // update the package.config file
            PackagesConfigFile.RemovePackage(package);
            PackagesConfigFile.Save(PackagesConfigFilePath);

            string packageInstallDirectory = Path.Combine(NugetConfigFile.RepositoryPath, string.Format("{0}.{1}", package.Id, package.Version));
            DeleteDirectory(packageInstallDirectory);

            string metaFile = Path.Combine(NugetConfigFile.RepositoryPath, string.Format("{0}.{1}.meta", package.Id, package.Version));
            DeleteFile(metaFile);

            string toolsInstallDirectory = Path.Combine(Application.dataPath, string.Format("../Packages/{0}.{1}", package.Id, package.Version));
            DeleteDirectory(toolsInstallDirectory);

            installedPackages.Remove(package.Id);

            if (refreshAssets)
                AssetDatabase.Refresh();
        }

        /// <summary>
        /// Updates a package by uninstalling the currently installed version and installing the "new" version.
        /// </summary>
        /// <param name="currentVersion">The current package to uninstall.</param>
        /// <param name="newVersion">The package to install.</param>
        /// <param name="refreshAssets">True to refresh the assets inside Unity.  False to ignore them (for now).  Defaults to true.</param>
        public static void Update(NugetPackageIdentifier currentVersion, NugetPackage newVersion, bool refreshAssets = true)
        {
            LogVerbose("Updating {0} {1} to {2}", currentVersion.Id, currentVersion.Version, newVersion.Version);
            Uninstall(currentVersion, false);
            Install(newVersion, refreshAssets);
        }

        /// <summary>
        /// Installs all of the given updates, and uninstalls the corresponding package that is already installed.
        /// </summary>
        /// <param name="updates">The list of all updates to install.</param>
        /// <param name="packagesToUpdate">The list of all packages currently installed.</param>
        public static void UpdateAll(IEnumerable<NugetPackage> updates, List<NugetPackage> packagesToUpdate)
        {
            float progressStep = 1.0f / updates.Count();
            float currentProgress = 0;

            foreach (NugetPackage update in updates)
            {
                EditorUtility.DisplayProgressBar(string.Format("Updating to {0} {1}", update.Id, update.Version), "Installing All Updates", currentProgress);

                NugetPackage installedPackage = packagesToUpdate.FirstOrDefault(p => p.Id == update.Id);
                if (installedPackage != null)
                {
                    Update(installedPackage, update, false);
                }
                else
                {
                    Debug.LogErrorFormat("Trying to update {0} to {1}, but no version is installed!", update.Id, update.Version);
                }

                currentProgress += progressStep;
            }

            AssetDatabase.Refresh();

            EditorUtility.ClearProgressBar();
        }

        /// <summary>
        /// Gets the dictionary of packages that are actually installed in the project, keyed off of the ID.
        /// </summary>
        /// <returns>A dictionary of installed <see cref="NugetPackage"/>s.</returns>
        public static Dictionary<string, NugetPackage> GetInstalledPackages()
        {
            LoadNugetConfigFile();

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            installedPackages.Clear();

            // loops through the packages that are actually installed in the project
            if (Directory.Exists(NugetConfigFile.RepositoryPath))
            {
                // a package that was installed via NuGet will have the .nupkg it came from inside the folder
                string[] nupkgFiles = Directory.GetFiles(NugetConfigFile.RepositoryPath, "*.nupkg", SearchOption.AllDirectories);
                foreach (string nupkgFile in nupkgFiles)
                {
                    NugetPackage package = NugetPackage.FromNupkgFile(nupkgFile);
                    if (!installedPackages.ContainsKey(package.Id))
                    {
                        installedPackages.Add(package.Id, package);
                    }
                    else
                    {
                        Debug.LogErrorFormat("Package is already in installed list: {0}", package.Id);
                    }
                }

                // if the source code & assets for a package are pulled directly into the project (ex: via a symlink/junction) it should have a .nuspec defining the package
                string[] nuspecFiles = Directory.GetFiles(NugetConfigFile.RepositoryPath, "*.nuspec", SearchOption.AllDirectories);
                foreach (string nuspecFile in nuspecFiles)
                {
                    NugetPackage package = NugetPackage.FromNuspec(NuspecFile.Load(nuspecFile));
                    if (!installedPackages.ContainsKey(package.Id))
                    {
                        installedPackages.Add(package.Id, package);
                    }
                    else
                    {
                        Debug.LogErrorFormat("Package is already in installed list: {0}", package.Id);
                    }
                }
            }

            stopwatch.Stop();
            LogVerbose("Getting installed packages took {0} ms", stopwatch.ElapsedMilliseconds);

            return installedPackages;
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

            // Loop through all active sources and combine them into a single list
            foreach (var source in packageSources.Where(s => s.IsEnabled))
            {
                var newPackages = source.Search(searchTerm, includeAllVersions, includePrerelease, numberToGet, numberToSkip);
                packages.AddRange(newPackages);
                packages = packages.Distinct().ToList();
            }

            return packages;
        }

        /// <summary>
        /// Queries the server with the given list of installed packages to get any updates that are available.
        /// </summary>
        /// <param name="packagesToUpdate">The list of currently installed packages.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="includeAllVersions">True to include older versions that are not the latest version.</param>
        /// <param name="targetFrameworks">The specific frameworks to target?</param>
        /// <param name="versionContraints">The version constraints?</param>
        /// <returns>A list of all updates available.</returns>
        public static List<NugetPackage> GetUpdates(List<NugetPackage> packagesToUpdate, bool includePrerelease = false, bool includeAllVersions = false, string targetFrameworks = "", string versionContraints = "")
        {
            List<NugetPackage> packages = new List<NugetPackage>();

            // Loop through all active sources and combine them into a single list
            foreach (var source in packageSources.Where(s => s.IsEnabled))
            {
                var newPackages = source.GetUpdates(packagesToUpdate, includePrerelease, includeAllVersions, targetFrameworks, versionContraints);
                packages.AddRange(newPackages);
                packages = packages.Distinct().ToList();
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
            // First look to see if the package is already installed
            NugetPackage package = GetInstalledPackage(packageId);

            if (package == null)
            {
                // That package isn't installed yet, so look in the cache next
                package = GetCachedPackage(packageId);
            }

            if (package == null)
            {
                // It's not in the cache, so we need to look in the active sources
                package = GetOnlinePackage(packageId);
            }

            return package;
        }

        /// <summary>
        /// Tries to find an already installed package that matches (or is in the range of) the given package ID.
        /// </summary>
        /// <param name="packageId">The <see cref="NugetPackageIdentifier"/> of the <see cref="NugetPackage"/> to find.</param>
        /// <returns>The best <see cref="NugetPackage"/> match, if there is one, otherwise null.</returns>
        private static NugetPackage GetInstalledPackage(NugetPackageIdentifier packageId)
        {
            NugetPackage installedPackage = null;

            if (installedPackages.TryGetValue(packageId.Id, out installedPackage))
            {
                if (packageId.Version != installedPackage.Version)
                {
                    if (packageId.InRange(installedPackage))
                    {
                        LogVerbose("Requested {0} {1}, but {2} is already installed, so using that.", packageId.Id, packageId.Version, installedPackage.Version);
                    }
                    else
                    {
                        LogVerbose("Requested {0} {1}. {2} is already installed, but it is out of range.", packageId.Id, packageId.Version, installedPackage.Version);
                        installedPackage = null;
                    }
                }
                else
                {
                    LogVerbose("Found exact package already installed: {0} {1}", installedPackage.Id, installedPackage.Version);
                }
            }


            return installedPackage;
        }

        /// <summary>
        /// Tries to find an already cached package that matches the given package ID.
        /// </summary>
        /// <param name="packageId">The <see cref="NugetPackageIdentifier"/> of the <see cref="NugetPackage"/> to find.</param>
        /// <returns>The best <see cref="NugetPackage"/> match, if there is one, otherwise null.</returns>
        private static NugetPackage GetCachedPackage(NugetPackageIdentifier packageId)
        {
            NugetPackage package = null;

            if (NugetHelper.NugetConfigFile.InstallFromCache)
            {
                string cachedPackagePath = System.IO.Path.Combine(NugetHelper.PackOutputDirectory, string.Format("./{0}.{1}.nupkg", packageId.Id, packageId.Version));

                if (File.Exists(cachedPackagePath))
                {
                    LogVerbose("Found exact package in the cache: {0}", cachedPackagePath);
                    package = NugetPackage.FromNupkgFile(cachedPackagePath);
                }
            }

            return package;
        }

        /// <summary>
        /// Tries to find an "online" (in the package sources - which could be local) package that matches (or is in the range of) the given package ID.
        /// </summary>
        /// <param name="packageId">The <see cref="NugetPackageIdentifier"/> of the <see cref="NugetPackage"/> to find.</param>
        /// <returns>The best <see cref="NugetPackage"/> match, if there is one, otherwise null.</returns>
        private static NugetPackage GetOnlinePackage(NugetPackageIdentifier packageId)
        {
            NugetPackage package = null;

            // Loop through all active sources and stop once the package is found
            foreach (var source in packageSources.Where(s => s.IsEnabled))
            {
                var foundPackage = source.GetSpecificPackage(packageId);
                if (foundPackage != null)
                {
                    if (package == null)
                    {
                        // if another package hasn't been found yet, use the current found one
                        LogVerbose("{0} {1} was found in {2}, and wanted {3}", foundPackage.Id, foundPackage.Version, source.Name, packageId.Version);
                        package = foundPackage;
                    }
                    else
                    {
                        // another package has been found previously, but neither match identically
                        if (foundPackage < package)
                        {
                            // use the new package if it's closer to the desired version
                            LogVerbose("{0} {1} was found in {2}, but wanted {3}", foundPackage.Id, foundPackage.Version, source.Name, packageId.Version);
                            package = foundPackage;
                        }
                    }
                }
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
        /// Installs the package given by the identifer.  It fetches the appropriate full package from the installed packages, package cache, or package sources and installs it.
        /// </summary>
        /// <param name="package">The identifer of the package to install.</param>
        internal static void InstallIdentifier(NugetPackageIdentifier package)
        {
            NugetPackage foundPackage = GetSpecificPackage(package);

            if (foundPackage != null)
            {
                Install(foundPackage, false);
            }
            else
            {
                Debug.LogErrorFormat("Could not find {0} {1} or greater.", package.Id, package.Version);
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
                // Application.stackTraceLogType was added in Unity 5.2
                // It was deprecated in Unity 5.4, but it still exists and works in Unity 2017.1
                // Continuing to use it here for backwards compatibility
                Application.stackTraceLogType = StackTraceLogType.None;
                Debug.LogFormat(format, args);
                Application.stackTraceLogType = StackTraceLogType.ScriptOnly;
            }
        }

        /// <summary>
        /// Installs the given package.
        /// </summary>
        /// <param name="package">The package to install.</param>
        /// <param name="refreshAssets">True to refresh the Unity asset database.  False to ignore the changes (temporarily).</param>
        public static void Install(NugetPackage package, bool refreshAssets = true)
        {
            try
            {
                LogVerbose("Installing: {0} {1}", package.Id, package.Version);

                // look to see if the package (any version) is already installed
                NugetPackage installedPackage = null;
                if (installedPackages.TryGetValue(package.Id, out installedPackage))
                {
                    if (installedPackage < package)
                    {
                        LogVerbose("{0} {1} is installed, but need {2} or greater. Updating to {3}", installedPackage.Id, installedPackage.Version, package.Version, package.Version);
                        Update(installedPackage, package, false);
                    }
                    else if (installedPackage > package)
                    {
                        LogVerbose("{0} {1} is installed. {2} or greater is needed, so using installed version.", installedPackage.Id, installedPackage.Version, package.Version);
                    }
                    else
                    {
                        LogVerbose("Already installed: {0} {1}", package.Id, package.Version);
                    }
                    return;
                }

                if (refreshAssets)
                    EditorUtility.DisplayProgressBar(string.Format("Installing {0} {1}", package.Id, package.Version), "Installing Dependencies", 0.1f);

                // install all dependencies
                foreach (var dependency in package.Dependencies)
                {
                    LogVerbose("Installing Dependency: {0} {1}", dependency.Id, dependency.Version);
                    InstallIdentifier(dependency);
                }

                // update packages.config
                PackagesConfigFile.AddPackage(package);
                PackagesConfigFile.Save(PackagesConfigFilePath);

                string cachedPackagePath = Path.Combine(PackOutputDirectory, string.Format("./{0}.{1}.nupkg", package.Id, package.Version));
                if (NugetConfigFile.InstallFromCache && File.Exists(cachedPackagePath))
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

                        Stream objStream = RequestUrl(package.DownloadUrl, package.PackageSource.Password, timeOut: null);
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

                // update the installed packages list
                installedPackages.Add(package.Id, package);
            }
            catch (Exception e)
            {
                Debug.LogErrorFormat("Unable to install package {0}\n{1}", package.Id, e.ToString());
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
        /// Get the specified URL from the web. Throws exceptions if the request fails.
        /// </summary>
        /// <param name="url">URL that will be loaded.</param>
        /// <param name="password">Password that will be passed in the Authorization header or the request. If null, authorization is omitted.</param>
        /// <param name="timeOut">Timeout in milliseconds or null to use the default timeout values of HttpWebRequest.</param>
        /// <returns>Stream containing the result.</returns>
        public static Stream RequestUrl(string url, string password, int? timeOut)
        {
            HttpWebRequest getRequest = (HttpWebRequest)WebRequest.Create(url);
            if (timeOut.HasValue)
            {
                getRequest.Timeout = timeOut.Value;
                getRequest.ReadWriteTimeout = timeOut.Value;
            }
            if (password != null)
            {
                // Send password as described by https://docs.microsoft.com/en-us/vsts/integrate/get-started/rest/basics.
                // This works with Visual Studio Team Services, but hasn't been tested with other authentication schemes so there may be additional work needed if there
                // are different kinds of authentication.
                getRequest.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", "", password))));
            }
            LogVerbose("HTTP GET {0}", url);
            Stream objStream = getRequest.GetResponse().GetResponseStream();
            return objStream;
        }

        /// <summary>
        /// Restores all packages defined in packages.config.
        /// </summary>
        public static void Restore()
        {
            GetInstalledPackages();

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                float progressStep = 1.0f / PackagesConfigFile.Packages.Count;
                float currentProgress = 0;

                // copy the list since the InstallIdentifier operation below changes the actual installed packages list
                var packagesToInstall = new List<NugetPackageIdentifier>(PackagesConfigFile.Packages);

                LogVerbose("Restoring {0} packages.", packagesToInstall.Count);

                foreach (var package in packagesToInstall)
                {
                    if (package != null)
                    {
                        EditorUtility.DisplayProgressBar("Restoring NuGet Packages", string.Format("Restoring {0} {1}", package.Id, package.Version), currentProgress);

                        if (!IsInstalled(package))
                        {
                            LogVerbose("---Restoring {0} {1}", package.Id, package.Version);
                            InstallIdentifier(package);
                        }
                        else
                        {
                            LogVerbose("---Already installed: {0} {1}", package.Id, package.Version);
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
                stopwatch.Stop();
                LogVerbose("Restoring packages took {0} ms", stopwatch.ElapsedMilliseconds);

                AssetDatabase.Refresh();
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Checks if a given package is installed.
        /// </summary>
        /// <param name="package">The package to check if is installed.</param>
        /// <returns>True if the given package is installed.  False if it is not.</returns>
        internal static bool IsInstalled(NugetPackageIdentifier package)
        {
            bool isInstalled = false;
            NugetPackage installedPackage = null;

            if (installedPackages.TryGetValue(package.Id, out installedPackage))
            {
                isInstalled = package.Version == installedPackage.Version;
            }

            return isInstalled;
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
                    request.Dispose();
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

            LogVerbose(
                timedout ? "Downloading image {0} timed out! Took more than 750ms." : "Downloading image {0} took {1} ms",
                url,
                stopwatch.ElapsedMilliseconds);

            return result;
        }
    }
}