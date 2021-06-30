using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using NuGet.Editor.Models;
using NuGet.Editor.Nuget;
using NuGet.Editor.Services;
using UnityEditor;
using UnityEngine;

namespace NuGet.Editor.Util
{
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
        private static bool insideInitializeOnLoad = false;

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
        public static readonly string PackOutputDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            Path.Combine("NuGet", "Cache")
            );

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
        /// The dictionary of cached credentials retrieved by credential providers, keyed by feed URI.
        /// </summary>
        private static Dictionary<Uri, CredentialProviderResponse?> cachedCredentialsByFeedUri = new Dictionary<Uri, CredentialProviderResponse?>();

        /// <summary>
        /// The current .NET version being used (2.0 [actually 3.5], 4.6, etc).
        /// </summary>
        private static ApiCompatibilityLevel DotNetVersion
        {
            get
            {
#if UNITY_5_6_OR_NEWER
                return PlayerSettings.GetApiCompatibilityLevel(EditorUserBuildSettings.selectedBuildTargetGroup);
#else
                return PlayerSettings.apiCompatibilityLevel;
#endif
            }
        }

        private static IFileHelper fileHelper = new FileHelper();

        private static IDownloadHelper downloadHelper = new DownloadHelper(fileHelper);

        private static INugetService nugetService = new NugetService();

        /// <summary>
        /// Static constructor used by Unity to initialize NuGet and restore packages defined in packages.config.
        /// </summary>
        static NugetHelper()
        {
            insideInitializeOnLoad = true;
            try
            {
                // if we are entering playmode, don't do anything
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    return;
                }

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
            finally
            {
                insideInitializeOnLoad = false;
            }
        }

        /// <summary>
        /// Loads the NuGet.config file.
        /// </summary>
        public static void LoadNugetConfigFile()
        {
            if (File.Exists(NugetConfigFilePath))
            {
                NugetConfigFile = nugetService.Load(NugetConfigFilePath);
            }
            else
            {
                Debug.LogFormat("No NuGet.config file found. Creating default at {0}", NugetConfigFilePath);

                NugetConfigFile = nugetService.CreateDefaultFile(NugetConfigFilePath);
                AssetDatabase.Refresh();
            }

            // parse any command line arguments
            //LogVerbose("Command line: {0}", Environment.CommandLine);
            packageSources.Clear();
            bool readingSources = false;
            bool useCommandLineSources = false;
            foreach (string arg in Environment.GetCommandLineArgs())
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
                if (NugetConfigFile.ActivePackageSource.ExpandedPath == "(Aggregate source)")
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
            foreach (string subDir in subdirectories)
            {
                FixSpaces(subDir);
            }

            string[] files = Directory.GetFiles(directoryPath);
            foreach (string file in files)
            {
                if (file.Contains("%20"))
                {
                    LogVerbose("Removing %20 from {0}", file);
                    File.Move(file, file.Replace("%20", " "));
                }
            }
        }

        /// <summary>
        /// Checks the file importer settings and disables the export to WSA Platform setting.
        /// </summary>
        /// <param name="filePath">The path to the .config file.</param>
        /// <param name="notifyOfUpdate">Whether or not to log a warning of the update.</param>
        public static void DisableWSAPExportSetting(string filePath, bool notifyOfUpdate)
        {
            string[] unityVersionParts = Application.unityVersion.Split('.');
            int unityMajorVersion;
            if (int.TryParse(unityVersionParts[0], out unityMajorVersion) && unityMajorVersion <= 2017)
            {
                return;
            }

            filePath = Path.GetFullPath(filePath);

            string assetsLocalPath = filePath.Replace(Path.GetFullPath(Application.dataPath), "Assets");
            PluginImporter importer = AssetImporter.GetAtPath(assetsLocalPath) as PluginImporter;

            if (importer == null)
            {
                if (!insideInitializeOnLoad)
                {
                    Debug.LogError(string.Format("Couldn't get importer for '{0}'.", filePath));
                }
                return;
            }

            if (importer.GetCompatibleWithPlatform(BuildTarget.WSAPlayer))
            {
                if (notifyOfUpdate)
                {
                    Debug.LogWarning(string.Format("Disabling WSA platform on asset settings for {0}", filePath));
                }
                else
                {
                    LogVerbose("Disabling WSA platform on asset settings for {0}", filePath);
                }

                importer.SetCompatibleWithPlatform(BuildTarget.WSAPlayer, false);
            }
        }

        private static bool FrameworkNamesAreEqual(string tfm1, string tfm2)
        {
            return tfm1.Equals(tfm2, StringComparison.InvariantCultureIgnoreCase);
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
            fileHelper.DeleteFile(packageInstallDirectory + "/" + package.Id + ".nuspec.meta");

            // delete directories & files that NuGet normally deletes, but since we are installing "manually" they exist
            fileHelper.DeleteDirectory(packageInstallDirectory + "/_rels");
            fileHelper.DeleteDirectory(packageInstallDirectory + "/package");
            fileHelper.DeleteFile(packageInstallDirectory + "/" + package.Id + ".nuspec");
            fileHelper.DeleteFile(packageInstallDirectory + "/[Content_Types].xml");

            // Unity has no use for the build directory
            fileHelper.DeleteDirectory(packageInstallDirectory + "/build");

            // For now, delete src.  We may use it later...
            fileHelper.DeleteDirectory(packageInstallDirectory + "/src");

            // Since we don't automatically fix up the runtime dll platforms, remove them until we improve support
            // for this newer feature of nuget packages.
            fileHelper.DeleteDirectory(Path.Combine(packageInstallDirectory, "runtimes"));

            // Delete documentation folders since they sometimes have HTML docs with JavaScript, which Unity tried to parse as "UnityScript"
            fileHelper.DeleteDirectory(packageInstallDirectory + "/docs");

            // Delete ref folder, as it is just used for compile-time reference and does not contain implementations.
            // Leaving it results in "assembly loading" and "multiple pre-compiled assemblies with same name" errors
            fileHelper.DeleteDirectory(packageInstallDirectory + "/ref");

            if (Directory.Exists(packageInstallDirectory + "/lib"))
            {
                List<string> selectedDirectories = new List<string>();

                // go through the library folders in descending order (highest to lowest version)
                IEnumerable<DirectoryInfo> libDirectories = Directory.GetDirectories(packageInstallDirectory + "/lib").Select(s => new DirectoryInfo(s));
                var targetFrameworks = libDirectories
                    .Select(x => x.Name.ToLower());

                bool isAlreadyImported = IsAlreadyImportedInEngine(package);
                string bestTargetFramework = TryGetBestTargetFrameworkForCurrentSettings(targetFrameworks);
                if (!isAlreadyImported && (bestTargetFramework != null))
                {
                    DirectoryInfo bestLibDirectory = libDirectories
                        .First(x => FrameworkNamesAreEqual(x.Name, bestTargetFramework));

                    if (bestTargetFramework == "unity" ||
                        bestTargetFramework == "net35-unity full v3.5" ||
                        bestTargetFramework == "net35-unity subset v3.5")
                    {
                        selectedDirectories.Add(Path.Combine(bestLibDirectory.Parent.FullName, "unity"));
                        selectedDirectories.Add(Path.Combine(bestLibDirectory.Parent.FullName, "net35-unity full v3.5"));
                        selectedDirectories.Add(Path.Combine(bestLibDirectory.Parent.FullName, "net35-unity subset v3.5"));
                    }
                    else
                    {
                        selectedDirectories.Add(bestLibDirectory.FullName);
                    }
                }

                foreach (string directory in selectedDirectories)
                {
                    LogVerbose("Using {0}", directory);
                }

                // delete all of the libaries except for the selected one
                foreach (DirectoryInfo directory in libDirectories)
                {
                    bool validDirectory = selectedDirectories
                        .Where(d => string.Compare(d, directory.FullName, ignoreCase: true) == 0)
                        .Any();

                    if (!validDirectory)
                    {
                        fileHelper.DeleteDirectory(directory.FullName);
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
                fileHelper.DeleteDirectory(toolsInstallDirectory);

                Directory.Move(packageInstallDirectory + "/tools", toolsInstallDirectory);
            }

            // delete all PDB files since Unity uses Mono and requires MDB files, which causes it to output "missing MDB" errors
            fileHelper.DeleteAllFiles(packageInstallDirectory, "*.pdb");

            // if there are native DLLs, copy them to the Unity project root (1 up from Assets)
            if (Directory.Exists(packageInstallDirectory + "/output"))
            {
                string[] files = Directory.GetFiles(packageInstallDirectory + "/output");
                foreach (string file in files)
                {
                    string newFilePath = Directory.GetCurrentDirectory() + "/" + Path.GetFileName(file);
                    LogVerbose("Moving {0} to {1}", file, newFilePath);
                    fileHelper.DeleteFile(newFilePath);
                    File.Move(file, newFilePath);
                }

                LogVerbose("Deleting {0}", packageInstallDirectory + "/output");

                fileHelper.DeleteDirectory(packageInstallDirectory + "/output");
            }

            // if there are Unity plugin DLLs, copy them to the Unity Plugins folder (Assets/Plugins)
            if (Directory.Exists(packageInstallDirectory + "/unityplugin"))
            {
                string pluginsDirectory = Application.dataPath + "/Plugins/";

                fileHelper.DirectoryCopy(packageInstallDirectory + "/unityplugin", pluginsDirectory);

                LogVerbose("Deleting {0}", packageInstallDirectory + "/unityplugin");

                fileHelper.DeleteDirectory(packageInstallDirectory + "/unityplugin");
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
                        fileHelper.DeleteFile(newFilePath);
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
                            fileHelper.DeleteDirectory(newDirectoryPath);
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
                fileHelper.DeleteDirectory(packageInstallDirectory + "/StreamingAssets");
                fileHelper.DeleteFile(packageInstallDirectory + "/StreamingAssets.meta");
            }
        }

        private static bool IsAlreadyImportedInEngine(NugetPackageIdentifier package)
        {
            HashSet<string> alreadyImportedLibs = GetAlreadyImportedLibs();
            bool isAlreadyImported = alreadyImportedLibs.Contains(package.Id);
            LogVerbose("Is package '{0}' already imported? {1}", package.Id, isAlreadyImported);
            return isAlreadyImported;
        }

        private static HashSet<string> alreadyImportedLibs = null;
        private static HashSet<string> GetAlreadyImportedLibs()
        {
            if (alreadyImportedLibs == null)
            {
                IEnumerable<string> lookupPaths = GetAllLookupPaths();
                IEnumerable<string> libNames = lookupPaths
                    .SelectMany(directory => Directory.EnumerateFiles(directory, "*.dll", SearchOption.AllDirectories))
                    .Select(Path.GetFileName)
                    .Select(p => Path.ChangeExtension(p, null));
                alreadyImportedLibs = new HashSet<string>(libNames);
                LogVerbose("Already imported libs: {0}", string.Join(", ", alreadyImportedLibs));
            }

            return alreadyImportedLibs;
        }

        private static IEnumerable<string> GetAllLookupPaths()
        {
            string executablePath = EditorApplication.applicationPath;
            string[] roots = new[] {
                // MacOS directory layout
                Path.Combine(executablePath, "Contents"),
                // Windows directory layout
                Path.Combine(Directory.GetParent(executablePath).FullName, "Data")
            };
            string[] relativePaths = new[] {
                Path.Combine("NetStandard",  "compat"),
                Path.Combine("MonoBleedingEdge", "lib", "mono")
            };
            IEnumerable<string> allPossiblePaths = roots
                .SelectMany(root => relativePaths
                    .Select(relativePath => Path.Combine(root, relativePath)));
            string[] existingPaths = allPossiblePaths
                .Where(Directory.Exists)
                .ToArray();
            LogVerbose("All existing path to dependency lookup are: {0}", string.Join(", ", existingPaths));
            return existingPaths;
        }

        public static NugetFrameworkGroup GetBestDependencyFrameworkGroupForCurrentSettings(NugetPackage package)
        {
            IEnumerable<string> targetFrameworks = package.Dependencies
                .Select(x => x.TargetFramework);

            string bestTargetFramework = TryGetBestTargetFrameworkForCurrentSettings(targetFrameworks);
            return package.Dependencies
                .FirstOrDefault(x => FrameworkNamesAreEqual(x.TargetFramework, bestTargetFramework)) ?? new NugetFrameworkGroup();
        }

        public static NugetFrameworkGroup GetBestDependencyFrameworkGroupForCurrentSettings(NuspecFile nuspec)
        {
            var targetFrameworks = nuspec.Dependencies
                .Select(x => x.TargetFramework);

            string bestTargetFramework = TryGetBestTargetFrameworkForCurrentSettings(targetFrameworks);
            return nuspec.Dependencies
                .FirstOrDefault(x => FrameworkNamesAreEqual(x.TargetFramework, bestTargetFramework)) ?? new NugetFrameworkGroup();
        }

        private struct PriorityFramework { public int Priority; public string Framework; }
        private static readonly string[] unityFrameworks = new string[] { "unity" };
        private static readonly string[] netStandardFrameworks = new string[] {
            "netstandard20", "netstandard16", "netstandard15", "netstandard14", "netstandard13", "netstandard12", "netstandard11", "netstandard10" };
        private static readonly string[] net4Unity2018Frameworks = new string[] { "net471", "net47" };
        private static readonly string[] net4Unity2017Frameworks = new string[] { "net462", "net461", "net46", "net452", "net451", "net45", "net403", "net40", "net4" };
        private static readonly string[] net3Frameworks = new string[] { "net35-unity full v3.5", "net35-unity subset v3.5", "net35", "net20", "net11" };
        private static readonly string[] defaultFrameworks = new string[] { string.Empty };

        public static string TryGetBestTargetFrameworkForCurrentSettings(IEnumerable<string> targetFrameworks)
        {
            int intDotNetVersion = (int)DotNetVersion; // c
            //bool using46 = DotNetVersion == ApiCompatibilityLevel.NET_4_6; // NET_4_6 option was added in Unity 5.6
            bool using46 = intDotNetVersion == 3; // NET_4_6 = 3 in Unity 5.6 and Unity 2017.1 - use the hard-coded int value to ensure it works in earlier versions of Unity
            bool usingStandard2 = intDotNetVersion == 6; // using .net standard 2.0

            var frameworkGroups = new List<string[]> { unityFrameworks };

            if (usingStandard2)
            {
                frameworkGroups.Add(netStandardFrameworks);
            }
            else if (using46)
            {
                if (UnityVersion.Current.Major >= 2018)
                {
                    frameworkGroups.Add(net4Unity2018Frameworks);
                }

                if (UnityVersion.Current.Major >= 2017)
                {
                    frameworkGroups.Add(net4Unity2017Frameworks);
                }

                frameworkGroups.Add(net3Frameworks);
                frameworkGroups.Add(netStandardFrameworks);
            }
            else
            {
                frameworkGroups.Add(net3Frameworks);
            }

            frameworkGroups.Add(defaultFrameworks);

            Func<string, int> getTfmPriority = (string tfm) =>
            {
                for (int i = 0; i < frameworkGroups.Count; ++i)
                {
                    int index = Array.FindIndex(frameworkGroups[i], test =>
                    {
                        if (test.Equals(tfm, StringComparison.InvariantCultureIgnoreCase)) { return true; }
                        if (test.Equals(tfm.Replace(".", string.Empty), StringComparison.InvariantCultureIgnoreCase)) { return true; }
                        return false;
                    });

                    if (index >= 0)
                    {
                        return i * 1000 + index;
                    }
                }

                return int.MaxValue;
            };

            // Select the highest .NET library available that is supported
            // See here: https://docs.nuget.org/ndocs/schema/target-frameworks
            string result = targetFrameworks
                .Select(tfm => new NugetHelper.PriorityFramework { Priority = getTfmPriority(tfm), Framework = tfm })
                .Where(pfm => pfm.Priority != int.MaxValue)
                .ToArray() // Ensure we don't search for priorities again when sorting
                .OrderBy(pfm => pfm.Priority)
                .Select(pfm => pfm.Framework)
                .FirstOrDefault();

            LogVerbose("Selecting {0} as the best target framework for current settings", result ?? "(null)");
            return result;
        }

        /// <summary>
        /// Uninstalls all of the currently installed packages.
        /// </summary>
        internal static void UninstallAll()
        {
            foreach (NugetPackage package in installedPackages.Values.ToList())
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
            fileHelper.DeleteDirectory(packageInstallDirectory);

            string metaFile = Path.Combine(NugetConfigFile.RepositoryPath, string.Format("{0}.{1}.meta", package.Id, package.Version));
            fileHelper.DeleteFile(metaFile);

            string toolsInstallDirectory = Path.Combine(Application.dataPath, string.Format("../Packages/{0}.{1}", package.Id, package.Version));
            fileHelper.DeleteDirectory(toolsInstallDirectory);

            installedPackages.Remove(package.Id);

            if (refreshAssets)
            {
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// Updates a package by uninstalling the currently installed version and installing the "new" version.
        /// </summary>
        /// <param name="currentVersion">The current package to uninstall.</param>
        /// <param name="newVersion">The package to install.</param>
        /// <param name="refreshAssets">True to refresh the assets inside Unity.  False to ignore them (for now).  Defaults to true.</param>
        public static bool Update(NugetPackageIdentifier currentVersion, NugetPackage newVersion, bool refreshAssets = true)
        {
            LogVerbose("Updating {0} {1} to {2}", currentVersion.Id, currentVersion.Version, newVersion.Version);
            Uninstall(currentVersion, false);
            return InstallIdentifier(newVersion, refreshAssets);
        }

        /// <summary>
        /// Installs all of the given updates, and uninstalls the corresponding package that is already installed.
        /// </summary>
        /// <param name="updates">The list of all updates to install.</param>
        /// <param name="packagesToUpdate">The list of all packages currently installed.</param>
        public static void UpdateAll(IEnumerable<NugetPackage> updates, IEnumerable<NugetPackage> packagesToUpdate)
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
        public static IEnumerable<NugetPackage> InstalledPackages { get { return installedPackages.Values; } }

        /// <summary>
        /// Updates the dictionary of packages that are actually installed in the project based on the files that are currently installed.
        /// </summary>
        public static void UpdateInstalledPackages()
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
                    NugetPackage package = NugetPackage.FromNupkgFile(nupkgFile, downloadHelper);
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
                    NugetPackage package = NugetPackage.FromNuspec(NuspecFile.Load(nuspecFile), downloadHelper);
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
        public static List<NugetPackage> Search(
            string searchTerm = "", 
            bool includeAllVersions = false, 
            bool includePrerelease = false, 
            int numberToGet = 15, 
            int numberToSkip = 0
        )
        {
            List<NugetPackage> packages = new List<NugetPackage>();

            // Loop through all active sources and combine them into a single list
            foreach (NugetPackageSource source in packageSources.Where(s => s.IsEnabled))
            {
                IEnumerable<NugetPackage> newPackages = source.Search(
                    searchTerm, 
                    includeAllVersions, 
                    includePrerelease, 
                    numberToGet, 
                    numberToSkip
                );
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
        public static List<NugetPackage> GetUpdates(IEnumerable<NugetPackage> packagesToUpdate, bool includePrerelease = false, bool includeAllVersions = false, string targetFrameworks = "", string versionContraints = "")
        {
            List<NugetPackage> packages = new List<NugetPackage>();

            // Loop through all active sources and combine them into a single list
            foreach (NugetPackageSource source in packageSources.Where(s => s.IsEnabled))
            {
                IEnumerable<NugetPackage> newPackages = source.GetUpdates(packagesToUpdate, includePrerelease, includeAllVersions, targetFrameworks, versionContraints);
                packages.AddRange(newPackages);
                packages = packages.Distinct().ToList();
            }

            return packages;
        }

        /// <summary>
        /// Gets a NugetPackage from the NuGet server with the exact ID and Version given.
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
                    package = NugetPackage.FromNupkgFile(cachedPackagePath, downloadHelper);
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
            foreach (NugetPackageSource source in packageSources.Where(s => s.IsEnabled))
            {
                NugetPackage foundPackage = source.GetSpecificPackage(packageId);
                if (foundPackage == null)
                {
                    continue;
                }

                if (foundPackage.Version == packageId.Version)
                {
                    LogVerbose("{0} {1} was found in {2}", foundPackage.Id, foundPackage.Version, source.Name);
                    return foundPackage;
                }

                LogVerbose("{0} {1} was found in {2}, but wanted {3}", foundPackage.Id, foundPackage.Version, source.Name, packageId.Version);
                if (package == null)
                {
                    // if another package hasn't been found yet, use the current found one
                    package = foundPackage;
                }
                // another package has been found previously, but neither match identically
                else if (foundPackage > package)
                {
                    // use the new package if it's closer to the desired version
                    package = foundPackage;
                }
            }
            if (package != null)
            {
                LogVerbose("{0} {1} not found, using {2}", packageId.Id, packageId.Version, package.Version);
            }
            else
            {
                LogVerbose("Failed to find {0} {1}", packageId.Id, packageId.Version);
            }

            return package;
        }

        /// <summary>
        /// Installs the package given by the identifer.  It fetches the appropriate full package from the installed packages, package cache, or package sources and installs it.
        /// </summary>
        /// <param name="package">The identifer of the package to install.</param>
        /// <param name="refreshAssets">True to refresh the Unity asset database.  False to ignore the changes (temporarily).</param>
        internal static bool InstallIdentifier(NugetPackageIdentifier package, bool refreshAssets = true)
        {
            if (IsAlreadyImportedInEngine(package))
            {
                LogVerbose("Package {0} is already imported in engine, skipping install.", package);
                return true;
            }

            NugetPackage foundPackage = GetSpecificPackage(package);

            if (foundPackage != null)
            {
                return Install(foundPackage, refreshAssets);
            }
            else
            {
                Debug.LogErrorFormat("Could not find {0} {1} or greater.", package.Id, package.Version);
                return false;
            }
        }

        /// <summary>
        /// Outputs the given message to the log only if verbose mode is active.  Otherwise it does nothing.
        /// </summary>
        /// <param name="format">The formatted message string.</param>
        /// <param name="args">The arguments for the formattted message string.</param>
        public static void LogVerbose(string format, params object[] args)
        {
            if (NugetConfigFile == null || NugetConfigFile.Verbose)
            {
#if UNITY_5_4_OR_NEWER
                StackTraceLogType stackTraceLogType = Application.GetStackTraceLogType(LogType.Log);
                Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
#else
                var stackTraceLogType = Application.stackTraceLogType;
                Application.stackTraceLogType = StackTraceLogType.None;
#endif
                Debug.LogFormat(format, args);

#if UNITY_5_4_OR_NEWER
                Application.SetStackTraceLogType(LogType.Log, stackTraceLogType);
#else
                Application.stackTraceLogType = stackTraceLogType;
#endif
            }
        }

        /// <summary>
        /// Installs the given package.
        /// </summary>
        /// <param name="package">The package to install.</param>
        /// <param name="refreshAssets">True to refresh the Unity asset database.  False to ignore the changes (temporarily).</param>
        public static bool Install(NugetPackage package, bool refreshAssets = true)
        {
            if (IsAlreadyImportedInEngine(package))
            {
                LogVerbose("Package {0} is already imported in engine, skipping install.", package);
                return true;
            }

            NugetPackage installedPackage = null;
            if (installedPackages.TryGetValue(package.Id, out installedPackage))
            {
                if (installedPackage < package)
                {
                    LogVerbose("{0} {1} is installed, but need {2} or greater. Updating to {3}", installedPackage.Id, installedPackage.Version, package.Version, package.Version);
                    return Update(installedPackage, package, false);
                }
                else if (installedPackage > package)
                {
                    LogVerbose("{0} {1} is installed. {2} or greater is needed, so using installed version.", installedPackage.Id, installedPackage.Version, package.Version);
                }
                else
                {
                    LogVerbose("Already installed: {0} {1}", package.Id, package.Version);
                }
                return true;
            }

            bool installSuccess = false;
            try
            {
                LogVerbose("Installing: {0} {1}", package.Id, package.Version);

                // look to see if the package (any version) is already installed


                if (refreshAssets)
                {
                    EditorUtility.DisplayProgressBar(string.Format("Installing {0} {1}", package.Id, package.Version), "Installing Dependencies", 0.1f);
                }

                // install all dependencies for target framework
                NugetFrameworkGroup frameworkGroup = GetBestDependencyFrameworkGroupForCurrentSettings(package);

                LogVerbose("Installing dependencies for TargetFramework: {0}", frameworkGroup.TargetFramework);
                foreach (var dependency in frameworkGroup.Dependencies)
                {
                    LogVerbose("Installing Dependency: {0} {1}", dependency.Id, dependency.Version);
                    bool installed = InstallIdentifier(dependency);
                    if (!installed)
                    {
                        throw new Exception(string.Format("Failed to install dependency: {0} {1}.", dependency.Id, dependency.Version));
                    }
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
                        File.Copy(Path.Combine(package.PackageSource.ExpandedPath, string.Format("./{0}.{1}.nupkg", package.Id, package.Version)), cachedPackagePath, true);
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
                        {
                            EditorUtility.DisplayProgressBar(string.Format("Installing {0} {1}", package.Id, package.Version), "Downloading Package", 0.3f);
                        }

                        Stream objStream = downloadHelper.RequestUrl(package.DownloadUrl, package.PackageSource.UserName, package.PackageSource.ExpandedPassword, timeOut: null);
                        using (Stream file = File.Create(cachedPackagePath))
                        {
                            downloadHelper.CopyStream(objStream, file);
                        }
                    }
                }

                if (refreshAssets)
                {
                    EditorUtility.DisplayProgressBar(string.Format("Installing {0} {1}", package.Id, package.Version), "Extracting Package", 0.6f);
                }

                if (File.Exists(cachedPackagePath))
                {
                    string baseDirectory = Path.Combine(NugetConfigFile.RepositoryPath, string.Format("{0}.{1}", package.Id, package.Version));

                    // unzip the package
                    using (ZipArchive zip = ZipFile.OpenRead(cachedPackagePath))
                    {
                        foreach (ZipArchiveEntry entry in zip.Entries)
                        {
                            string filePath = Path.Combine(baseDirectory, entry.FullName);
                            string directory = Path.GetDirectoryName(filePath);
                            Directory.CreateDirectory(directory);
                            if (Directory.Exists(filePath)) continue;

                            entry.ExtractToFile(filePath, overwrite: true);

                            if (NugetConfigFile.ReadOnlyPackageFiles)
                            {
                                FileInfo extractedFile = new FileInfo(filePath);
                                extractedFile.Attributes |= FileAttributes.ReadOnly;
                            }
                        }
                    }

                    // copy the .nupkg inside the Unity project
                    File.Copy(cachedPackagePath, Path.Combine(baseDirectory, string.Format("{0}.{1}.nupkg", package.Id, package.Version)), true);
                }
                else
                {
                    Debug.LogErrorFormat("File not found: {0}", cachedPackagePath);
                }

                if (refreshAssets)
                {
                    EditorUtility.DisplayProgressBar(string.Format("Installing {0} {1}", package.Id, package.Version), "Cleaning Package", 0.9f);
                }

                // clean
                Clean(package);

                // update the installed packages list
                installedPackages.Add(package.Id, package);
                installSuccess = true;
            }
            catch (Exception e)
            {
                WarnIfDotNetAuthenticationIssue(e);
                Debug.LogErrorFormat("Unable to install package {0} {1}\n{2}", package.Id, package.Version, e.ToString());
                installSuccess = false;
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
            return installSuccess;
        }

        private static void WarnIfDotNetAuthenticationIssue(Exception e)
        {
#if !NET_4_6
            WebException webException = e as WebException;
            HttpWebResponse webResponse = webException != null ? webException.Response as HttpWebResponse : null;
            if (webResponse != null && webResponse.StatusCode == HttpStatusCode.BadRequest && webException.Message.Contains("Authentication information is not given in the correct format"))
            {
                // This error occurs when downloading a package with authentication using .NET 3.5, but seems to be fixed by the new .NET 4.6 runtime.
                // Inform users when this occurs.
                Debug.LogError("Authentication failed. This can occur due to a known issue in .NET 3.5. This can be fixed by changing Scripting Runtime to Experimental (.NET 4.6 Equivalent) in Player Settings.");
            }
#endif
        }

        private struct AuthenticatedFeed
        {
            public string AccountUrlPattern;
            public string ProviderUrlTemplate;

            public string GetAccount(string url)
            {
                Match match = Regex.Match(url, AccountUrlPattern, RegexOptions.IgnoreCase);
                if (!match.Success) { return null; }

                return match.Groups["account"].Value;
            }

            public string GetProviderUrl(string account)
            {
                return ProviderUrlTemplate.Replace("{account}", account);
            }
        }

        // TODO: Move to ScriptableObjet
        private static List<NugetHelper.AuthenticatedFeed> knownAuthenticatedFeeds = new List<NugetHelper.AuthenticatedFeed>()
        {
            new NugetHelper.AuthenticatedFeed()
            {
                AccountUrlPattern = @"^https:\/\/(?<account>[a-zA-z0-9]+).pkgs.visualstudio.com",
                ProviderUrlTemplate = "https://{account}.pkgs.visualstudio.com/_apis/public/nuget/client/CredentialProviderBundle.zip"
            },
            new NugetHelper.AuthenticatedFeed()
            {
                AccountUrlPattern = @"^https:\/\/pkgs.dev.azure.com\/(?<account>[a-zA-z0-9]+)\/",
                ProviderUrlTemplate = "https://pkgs.dev.azure.com/{account}/_apis/public/nuget/client/CredentialProviderBundle.zip"
            }
        };

        /// <summary>
        /// Restores all packages defined in packages.config.
        /// </summary>
        public static void Restore()
        {
            UpdateInstalledPackages();

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                float progressStep = 1.0f / PackagesConfigFile.Packages.Count;
                float currentProgress = 0;

                // copy the list since the InstallIdentifier operation below changes the actual installed packages list
                List<NugetPackageIdentifier> packagesToInstall = new List<NugetPackageIdentifier>(PackagesConfigFile.Packages);

                LogVerbose("Restoring {0} packages.", packagesToInstall.Count);

                foreach (NugetPackageIdentifier package in packagesToInstall)
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

                CheckForUnnecessaryPackages();
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

        internal static void CheckForUnnecessaryPackages()
        {
            if (!Directory.Exists(NugetConfigFile.RepositoryPath))
            {
                return;
            }

            string[] directories = Directory.GetDirectories(NugetConfigFile.RepositoryPath, "*", SearchOption.TopDirectoryOnly);
            foreach (string folder in directories)
            {
                string pkgPath = Path.Combine(folder, $"{Path.GetFileName(folder)}.nupkg");
                NugetPackage package = NugetPackage.FromNupkgFile(pkgPath, downloadHelper);

                bool installed = false;
                foreach (NugetPackageIdentifier packageId in PackagesConfigFile.Packages)
                {
                    if (packageId.CompareTo(package) == 0)
                    {
                        installed = true;
                        break;
                    }
                }
                if (!installed)
                {
                    LogVerbose("---DELETE unnecessary package {0}", folder);

                    fileHelper.DeleteDirectory(folder);
                    fileHelper.DeleteFile(folder + ".meta");
                }
            }

        }

        /// <summary>
        /// Checks if a given package is installed.
        /// </summary>
        /// <param name="package">The package to check if is installed.</param>
        /// <returns>True if the given package is installed.  False if it is not.</returns>
        internal static bool IsInstalled(NugetPackageIdentifier package)
        {
            if (IsAlreadyImportedInEngine(package))
            {
                return true;
            }

            bool isInstalled = false;
            NugetPackage installedPackage = null;

            if (installedPackages.TryGetValue(package.Id, out installedPackage))
            {
                isInstalled = package.CompareVersion(installedPackage.Version) == 0;
            }

            return isInstalled;
        }

        private static void DownloadCredentialProviders(Uri feedUri)
        {
            foreach (NugetHelper.AuthenticatedFeed feed in NugetHelper.knownAuthenticatedFeeds)
            {
                string account = feed.GetAccount(feedUri.ToString());
                if (string.IsNullOrEmpty(account)) { continue; }

                string providerUrl = feed.GetProviderUrl(account);

                HttpWebRequest credentialProviderRequest = (HttpWebRequest)WebRequest.Create(providerUrl);

                try
                {
                    Stream credentialProviderDownloadStream = credentialProviderRequest.GetResponse().GetResponseStream();

                    string tempFileName = Path.GetTempFileName();
                    LogVerbose("Writing {0} to {1}", providerUrl, tempFileName);

                    using (FileStream file = File.Create(tempFileName))
                    {
                        downloadHelper.CopyStream(credentialProviderDownloadStream, file);
                    }

                    string providerDestination = Environment.GetEnvironmentVariable("NUGET_CREDENTIALPROVIDERS_PATH");
                    if (string.IsNullOrEmpty(providerDestination))
                    {
                        providerDestination = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nuget/CredentialProviders");
                    }

                    // Unzip the bundle and extract any credential provider exes
                    using (ZipArchive zip = ZipFile.OpenRead(tempFileName))
                    {
                        foreach (ZipArchiveEntry entry in zip.Entries)
                        {
                            if (Regex.IsMatch(entry.FullName, @"^credentialprovider.+\.exe$", RegexOptions.IgnoreCase))
                            {
                                LogVerbose("Extracting {0} to {1}", entry.FullName, providerDestination);
                                string filePath = Path.Combine(providerDestination, entry.FullName);
                                string directory = Path.GetDirectoryName(filePath);
                                Directory.CreateDirectory(directory);

                                entry.ExtractToFile(filePath, overwrite: true);
                            }
                        }
                    }

                    // Delete the bundle
                    File.Delete(tempFileName);
                }
                catch (Exception e)
                {
                    Debug.LogErrorFormat("Failed to download credential provider from {0}: {1}", credentialProviderRequest.Address, e.Message);
                }
            }

        }

        /// <summary>
        /// Helper function to aquire a token to access VSTS hosted nuget feeds by using the CredentialProvider.VSS.exe
        /// tool. Downloading it from the VSTS instance if needed.
        /// See here for more info on nuget Credential Providers:
        /// https://docs.microsoft.com/en-us/nuget/reference/extensibility/nuget-exe-credential-providers
        /// </summary>
        /// <param name="feedUri">The hostname where the VSTS instance is hosted (such as microsoft.pkgs.visualsudio.com.</param>
        /// <returns>The password in the form of a token, or null if the password could not be aquired</returns>
        public static CredentialProviderResponse? GetCredentialFromProvider(Uri feedUri)
        {
            CredentialProviderResponse? response;
            if (!cachedCredentialsByFeedUri.TryGetValue(feedUri, out response))
            {
                response = GetCredentialFromProvider_Uncached(feedUri, true);
                cachedCredentialsByFeedUri[feedUri] = response;
            }
            return response;
        }

        /// <summary>
        /// Given the URI of a nuget method, returns the URI of the feed itself without the method and query parameters.
        /// </summary>
        /// <param name="methodUri">URI of nuget method.</param>
        /// <returns>URI of the feed without the method and query parameters.</returns>
        public static Uri GetTruncatedFeedUri(Uri methodUri)
        {
            string truncatedUriString = methodUri.GetLeftPart(UriPartial.Path);

            // Pull off the function if there is one
            if (truncatedUriString.EndsWith(")"))
            {
                int lastSeparatorIndex = truncatedUriString.LastIndexOf('/');
                if (lastSeparatorIndex != -1)
                {
                    truncatedUriString = truncatedUriString.Substring(0, lastSeparatorIndex);
                }
            }
            Uri truncatedUri = new Uri(truncatedUriString);
            return truncatedUri;
        }

        /// <summary>
        /// Clears static credentials previously cached by GetCredentialFromProvider.
        /// </summary>
        public static void ClearCachedCredentials()
        {
            cachedCredentialsByFeedUri.Clear();
        }

        /// <summary>
        /// Internal function called by GetCredentialFromProvider to implement retrieving credentials. For performance reasons,
        /// most functions should call GetCredentialFromProvider in order to take advantage of cached credentials.
        /// </summary>
        private static CredentialProviderResponse? GetCredentialFromProvider_Uncached(Uri feedUri, bool downloadIfMissing)
        {
            LogVerbose("Getting credential for {0}", feedUri);

            // Build the list of possible locations to find the credential provider. In order it should be local app data, paths set on the
            // environment varaible, and lastly look at the root of the pacakges save location.
            List<string> possibleCredentialProviderPaths = new List<string>
            {
                Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nuget"), "CredentialProviders")
            };

            string environmentCredentialProviderPaths = Environment.GetEnvironmentVariable("NUGET_CREDENTIALPROVIDERS_PATH");
            if (!string.IsNullOrEmpty(environmentCredentialProviderPaths))
            {
                possibleCredentialProviderPaths.AddRange(
                    environmentCredentialProviderPaths.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries) ?? Enumerable.Empty<string>());
            }

            // Try to find any nuget.exe in the package tools installation location
            string toolsPackagesFolder = Path.Combine(Application.dataPath, "../Packages");
            possibleCredentialProviderPaths.Add(toolsPackagesFolder);

            // Search through all possible paths to find the credential provider.
            List<string> providerPaths = new List<string>();
            foreach (string possiblePath in possibleCredentialProviderPaths)
            {
                if (Directory.Exists(possiblePath))
                {
                    providerPaths.AddRange(Directory.GetFiles(possiblePath, "credentialprovider*.exe", SearchOption.AllDirectories));
                }
            }

            foreach (string providerPath in providerPaths.Distinct())
            {
                // Launch the credential provider executable and get the json encoded response from the std output
                Process process = new Process();
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.FileName = providerPath;
                process.StartInfo.Arguments = string.Format("-uri \"{0}\"", feedUri.ToString());

                // http://stackoverflow.com/questions/16803748/how-to-decode-cmd-output-correctly
                // Default = 65533, ASCII = ?, Unicode = nothing works at all, UTF-8 = 65533, UTF-7 = 242 = WORKS!, UTF-32 = nothing works at all
                process.StartInfo.StandardOutputEncoding = Encoding.GetEncoding(850);
                process.Start();
                process.WaitForExit();

                string output = process.StandardOutput.ReadToEnd();
                string errors = process.StandardError.ReadToEnd();

                switch ((CredentialProviderExitCode)process.ExitCode)
                {
                    case CredentialProviderExitCode.ProviderNotApplicable: break; // Not the right provider
                    case CredentialProviderExitCode.Failure: // Right provider, failure to get creds
                        {
                            Debug.LogErrorFormat("Failed to get credentials from {0}!\n\tOutput\n\t{1}\n\tErrors\n\t{2}", providerPath, output, errors);
                            return null;
                        }
                    case CredentialProviderExitCode.Success:
                        {
                            return JsonUtility.FromJson<CredentialProviderResponse>(output);
                        }
                    default:
                        {
                            Debug.LogWarningFormat("Unrecognized exit code {0} from {1} {2}", process.ExitCode, providerPath, process.StartInfo.Arguments);
                            break;
                        }
                }
            }

            if (downloadIfMissing)
            {
                DownloadCredentialProviders(feedUri);
                return GetCredentialFromProvider_Uncached(feedUri, false);
            }

            return null;
        }
    }
}
