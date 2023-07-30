using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

[assembly: InternalsVisibleTo("NuGetForUnity.Editor.Tests")]

namespace NugetForUnity
{
    /// <summary>
    ///     A set of helper methods that act as a wrapper around nuget.exe
    ///     TIP: It's incredibly useful to associate .nupkg files as compressed folder in Windows (View like .zip files).  To do this:
    ///     1) Open a command prompt as admin (Press Windows key. Type "cmd".  Right click on the icon and choose "Run as Administrator"
    ///     2) Enter this command: cmd /c assoc .nupkg=CompressedFolder.
    /// </summary>
    public static class NugetHelper
    {
        /// <summary>
        ///     The amount of time, in milliseconds, before the nuget.exe process times out and is killed.
        /// </summary>
        private const int TimeOut = 60000;

        /// <summary>
        ///     The path to the nuget.config file.
        /// </summary>
        /// <remarks>
        ///     <see cref="NugetConfigFile" />.
        /// </remarks>
        public static readonly string NugetConfigFilePath = Path.GetFullPath(Path.Combine(Application.dataPath, NugetConfigFile.FileName));

        /// <summary>
        ///     The path to the packages.config file.
        /// </summary>
        /// <remarks>
        ///     <see cref="PackagesConfigFile" />.
        /// </remarks>
        internal static readonly string PackagesConfigFilePath = Path.GetFullPath(Path.Combine(Application.dataPath, PackagesConfigFile.FileName));

        /// <summary>
        ///     Gets the absolute path to the Unity-Project root directory.
        /// </summary>
        internal static readonly string AbsoluteProjectPath = Path.GetFullPath(Path.GetDirectoryName(Application.dataPath));

        /// <summary>
        ///     The path where to put created (packed) and downloaded (not installed yet) .nupkg files.
        /// </summary>
        public static readonly string PackOutputDirectory = Path.Combine(
            Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)),
            Path.Combine("NuGet", "Cache"));

        /// <summary>
        ///     Backing field for the packages.config file.
        /// </summary>
        private static PackagesConfigFile packagesConfigFile;

        /// <summary>
        ///     The <see cref="INuGetPackageSource" /> to use.
        /// </summary>
        private static INuGetPackageSource activePackageSource;

        /// <summary>
        ///     The dictionary of currently installed <see cref="NugetPackage" />s keyed off of their ID string.
        /// </summary>
        private static Dictionary<string, INuGetPackage> installedPackages;

        /// <summary>
        ///     Static constructor called only once.
        /// </summary>
        static NugetHelper()
        {
            // create the nupkgs directory, if it doesn't exist
            Directory.CreateDirectory(PackOutputDirectory);
        }

        /// <summary>
        ///     The loaded NuGet.config file that holds the settings for NuGet.
        /// </summary>
        public static NugetConfigFile NugetConfigFile { get; private set; }

        /// <summary>
        ///     Gets the loaded packages.config file that hold the dependencies for the project.
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
        ///     Gets the packages that are actually installed in the project.
        /// </summary>
        public static IEnumerable<INuGetPackage> InstalledPackages => InstalledPackagesDictionary.Values;

        /// <summary>
        ///     Gets the dictionary of packages that are actually installed in the project, keyed off of the ID.
        /// </summary>
        private static Dictionary<string, INuGetPackage> InstalledPackagesDictionary
        {
            get
            {
                if (installedPackages == null)
                {
                    UpdateInstalledPackages();
                }

                return installedPackages;
            }
        }

        /// <summary>
        ///     Invalidates the currently loaded 'packages.config' so it is reloaded when it is accessed the next time.
        /// </summary>
        internal static void ReloadPackagesConfig()
        {
            packagesConfigFile = null;
        }

        /// <summary>
        ///     Loads the NuGet.config file.
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
            }

            // parse any command line arguments
            var packageSourcesFromCommandLine = new List<INuGetPackageSource>();
            var readingSources = false;
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
                        var source = NuGetPackageSourceCreator.CreatePackageSource($"CMD_LINE_SRC_{packageSourcesFromCommandLine.Count}", arg, null);
                        LogVerbose("Adding command line package source {0} at {1}", source.Name, arg);
                        packageSourcesFromCommandLine.Add(source);
                    }
                }

                if (arg.Equals("-Source", StringComparison.OrdinalIgnoreCase))
                {
                    // if the source is being forced, don't install packages from the cache
                    NugetConfigFile.InstallFromCache = false;
                    readingSources = true;
                }
            }

            if (packageSourcesFromCommandLine.Count == 1)
            {
                activePackageSource = packageSourcesFromCommandLine[0];
            }
            else if (packageSourcesFromCommandLine.Count > 1)
            {
                activePackageSource = new CombinedNuGetPackageSource(packageSourcesFromCommandLine);
            }
            else
            {
                // if there are not command line overrides, use the NuGet.config package sources
                activePackageSource = NugetConfigFile.ActivePackageSource;
            }
        }

        /// <summary>
        ///     Runs nuget.exe using the given arguments.
        /// </summary>
        /// <param name="arguments">The arguments to run nuget.exe with.</param>
        /// <param name="logOuput">True to output debug information to the Unity console.  Defaults to true.</param>
        /// <returns>The string of text that was output from nuget.exe following its execution.</returns>
        private static void RunNugetProcess(string arguments, bool logOuput = true)
        {
            // Try to find any nuget.exe in the package tools installation location
            var toolsPackagesFolder = Path.Combine(AbsoluteProjectPath, "Packages");

            // create the folder to prevent an exception when getting the files
            Directory.CreateDirectory(toolsPackagesFolder);

            var files = Directory.GetFiles(toolsPackagesFolder, "nuget.exe", SearchOption.AllDirectories);
            if (files.Length > 1)
            {
                Debug.LogWarningFormat("More than one nuget.exe found. Using first one.");
            }
            else if (files.Length < 1)
            {
                Debug.LogWarningFormat("No nuget.exe found! Attempting to install the NuGet.CommandLine package.");
                InstallIdentifier(new NugetPackageIdentifier("NuGet.CommandLine", "2.8.6"));
                files = Directory.GetFiles(toolsPackagesFolder, "nuget.exe", SearchOption.AllDirectories);
                if (files.Length < 1)
                {
                    Debug.LogErrorFormat("nuget.exe still not found. Quiting...");
                    return;
                }
            }

            LogVerbose("Running: {0} \nArgs: {1}", files[0], arguments);

            string fileName;
            string commandLine;
            if (Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                // ATTENTION: you must install mono running on your mac, we use this mono to run `nuget.exe`
                fileName = "/Library/Frameworks/Mono.framework/Versions/Current/Commands/mono";
                commandLine = " " + files[0] + " " + arguments;
                LogVerbose("command: " + commandLine);
            }
            else
            {
                fileName = files[0];
                commandLine = arguments;
            }

            var process = Process.Start(
                new ProcessStartInfo(fileName, commandLine)
                {
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,

                    // WorkingDirectory = Path.GettargetFramework(files[0]),

                    // http://stackoverflow.com/questions/16803748/how-to-decode-cmd-output-correctly
                    // Default = 65533, ASCII = ?, Unicode = nothing works at all, UTF-8 = 65533, UTF-7 = 242 = WORKS!, UTF-32 = nothing works at all
                    StandardOutputEncoding = Encoding.GetEncoding(850),
                });

            if (!process.WaitForExit(TimeOut))
            {
                Debug.LogWarning("NuGet took too long to finish.  Killing operation.");
                process.Kill();
            }

            var error = process.StandardError.ReadToEnd();
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError(error);
            }

            var output = process.StandardOutput.ReadToEnd();
            if (logOuput && !string.IsNullOrEmpty(output))
            {
                Debug.Log(output);
            }
        }

        /// <summary>
        ///     Replace all %20 encodings with a normal space.
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

            var subdirectories = Directory.GetDirectories(directoryPath);
            foreach (var subDir in subdirectories)
            {
                FixSpaces(subDir);
            }

            var files = Directory.GetFiles(directoryPath);
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
        ///     Cleans up a package after it has been installed.
        ///     Since we are in Unity, we can make certain assumptions on which files will NOT be used, so we can delete them.
        /// </summary>
        /// <param name="package">The NugetPackage to clean.</param>
        private static void CleanInstallationDirectory(INuGetPackageIdentifier package)
        {
            var packageInstallDirectory = Path.Combine(NugetConfigFile.RepositoryPath, string.Format("{0}.{1}", package.Id, package.Version));

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

            // For now, delete src.  We may use it later...
            DeleteDirectory(packageInstallDirectory + "/src");

            // Since we don't automatically fix up the runtime dll platforms, remove them until we improve support
            // for this newer feature of nuget packages.
            DeleteDirectory(Path.Combine(packageInstallDirectory, "runtimes"));

            // Delete documentation folders since they sometimes have HTML docs with JavaScript, which Unity tried to parse as "UnityScript"
            DeleteDirectory(packageInstallDirectory + "/docs");

            // Delete ref folder, as it is just used for compile-time reference and does not contain implementations.
            // Leaving it results in "assembly loading" and "multiple pre-compiled assemblies with same name" errors
            DeleteDirectory(packageInstallDirectory + "/ref");

            var packageLibsDirectory = Path.Combine(packageInstallDirectory, "lib");
            if (Directory.Exists(packageLibsDirectory))
            {
                // go through the library folders in descending order (highest to lowest version)
                var libDirectories = new DirectoryInfo(packageLibsDirectory).GetDirectories();

                var bestLibDirectory = TargetFrameworkResolver.TryGetBestTargetFramework(libDirectories, directory => directory.Name);
                if (bestLibDirectory == null)
                {
                    Debug.LogWarningFormat("Couldn't find a library folder with a supported target-framework for the package {0}", package);
                }
                else
                {
                    LogVerbose(
                        "Selecting directory '{0}' with the best target framework {1} for current settings",
                        bestLibDirectory,
                        bestLibDirectory.Name);
                }

                // delete all of the libraries except for the selected one
                foreach (var directory in libDirectories)
                {
                    // we use reference equality as the TargetFrameworkResolver returns the input reference.
                    if (directory != bestLibDirectory)
                    {
                        DeleteDirectory(directory.FullName);
                    }
                }

                if (bestLibDirectory != null)
                {
                    // some older packages e.g. Microsoft.CodeAnalysis.Common 2.10.0 have multiple localization resource files
                    // e.g. Microsoft.CodeAnalysis.resources.dll each inside a folder with the language name as a folder name e.g. zh-Hant or fr
                    // unity doesn't support importing multiple assemblies with the same file name.
                    // for now we just delete all folders so the language neutral version is used and Unity is happy.
                    var languageSupFolders = bestLibDirectory.GetDirectories();
                    if (languageSupFolders.All(languageSupFolder => languageSupFolder.Name.Split('-').FirstOrDefault()?.Length == 2))
                    {
                        foreach (var languageSupFolder in languageSupFolders)
                        {
                            languageSupFolder.Delete(true);
                        }
                    }
                }
            }

            if (Directory.Exists(packageInstallDirectory + "/tools"))
            {
                // Move the tools folder outside of the Unity Assets folder
                var toolsInstallDirectory = Path.Combine(AbsoluteProjectPath, "Packages", $"{package.Id}.{package.Version}", "tools");

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
                var files = Directory.GetFiles(packageInstallDirectory + "/output");
                foreach (var file in files)
                {
                    var newFilePath = Directory.GetCurrentDirectory() + "/" + Path.GetFileName(file);
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
                var pluginsDirectory = Application.dataPath + "/Plugins/";

                DirectoryCopy(packageInstallDirectory + "/unityplugin", pluginsDirectory);

                LogVerbose("Deleting {0}", packageInstallDirectory + "/unityplugin");

                DeleteDirectory(packageInstallDirectory + "/unityplugin");
            }

            // if there are Unity StreamingAssets, copy them to the Unity StreamingAssets folder (Assets/StreamingAssets)
            if (Directory.Exists(packageInstallDirectory + "/StreamingAssets"))
            {
                var streamingAssetsDirectory = Application.dataPath + "/StreamingAssets/";

                if (!Directory.Exists(streamingAssetsDirectory))
                {
                    Directory.CreateDirectory(streamingAssetsDirectory);
                }

                // move the files
                var files = Directory.GetFiles(packageInstallDirectory + "/StreamingAssets");
                foreach (var file in files)
                {
                    var newFilePath = streamingAssetsDirectory + Path.GetFileName(file);

                    try
                    {
                        LogVerbose("Moving {0} to {1}", file, newFilePath);
                        DeleteFile(newFilePath);
                        File.Move(file, newFilePath);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarningFormat("{0} couldn't be moved. \n{1}", newFilePath, e);
                    }
                }

                // move the directories
                var directories = Directory.GetDirectories(packageInstallDirectory + "/StreamingAssets");
                foreach (var directory in directories)
                {
                    var newDirectoryPath = streamingAssetsDirectory + new DirectoryInfo(directory).Name;

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
                        Debug.LogWarningFormat("{0} couldn't be moved. \n{1}", newDirectoryPath, e);
                    }
                }

                // delete the package's StreamingAssets folder and .meta file
                LogVerbose("Deleting {0}", packageInstallDirectory + "/StreamingAssets");
                DeleteDirectory(packageInstallDirectory + "/StreamingAssets");
                DeleteFile(packageInstallDirectory + "/StreamingAssets.meta");
            }
        }

        /// <summary>
        ///     Check if a package is already imported in the Unity project e.g. is a part of Unity.
        /// </summary>
        /// <param name="package">The package of witch the identifier is checked.</param>
        /// <param name="log">Whether to log a message with the result of the check.</param>
        /// <returns>If it is included in Unity.</returns>
        internal static bool IsAlreadyImportedInEngine(INuGetPackageIdentifier package, bool log = true)
        {
            var alreadyImportedLibs = UnityPreImportedLibraryResolver.GetAlreadyImportedLibs();
            var isAlreadyImported = alreadyImportedLibs.Contains(package.Id);
            if (log)
            {
                LogVerbose("Is package '{0}' already imported? {1}", package.Id, isAlreadyImported);
            }

            return isAlreadyImported;
        }

        /// <summary>
        ///     Select the best target-framework group of a NuGet package.
        /// </summary>
        /// <param name="packageDependencies">The available frameworks.</param>
        /// <returns>The selected target framework group or null if non is matching.</returns>
        internal static NugetFrameworkGroup GetNullableBestDependencyFrameworkGroupForCurrentSettings(List<NugetFrameworkGroup> packageDependencies)
        {
            var bestTargetFramework = TargetFrameworkResolver.TryGetBestTargetFramework(
                packageDependencies,
                frameworkGroup => frameworkGroup.TargetFramework);
            LogVerbose("Selecting {0} as the best target framework for current settings", bestTargetFramework?.TargetFramework ?? "(null)");
            return bestTargetFramework;
        }

        /// <summary>
        ///     Select the best target-framework group of a NuGet package.
        /// </summary>
        /// <param name="packageDependencies">The available frameworks.</param>
        /// <returns>The selected target framework group or a empty group if non is matching.</returns>
        internal static NugetFrameworkGroup GetBestDependencyFrameworkGroupForCurrentSettings(List<NugetFrameworkGroup> packageDependencies)
        {
            return GetNullableBestDependencyFrameworkGroupForCurrentSettings(packageDependencies) ?? new NugetFrameworkGroup();
        }

        /// <summary>
        ///     Select the best target-framework group of a NuGet package.
        /// </summary>
        /// <param name="nuspec">The package of witch the dependencies are selected.</param>
        /// <returns>The selected target framework group or a empty group if non is matching.</returns>
        internal static NugetFrameworkGroup GetBestDependencyFrameworkGroupForCurrentSettings(NuspecFile nuspec)
        {
            return GetBestDependencyFrameworkGroupForCurrentSettings(nuspec.Dependencies);
        }

        /// <summary>
        ///     Select the best target-framework group of a NuGet package.
        /// </summary>
        /// <param name="package">The package of witch the dependencies are selected.</param>
        /// <returns>The selected target framework group or a empty group if non is matching.</returns>
        internal static NugetFrameworkGroup GetBestDependencyFrameworkGroupForCurrentSettings(INuGetPackage package)
        {
            return GetBestDependencyFrameworkGroupForCurrentSettings(package.Dependencies);
        }

        /// <summary>
        ///     Select the best target-framework name.
        /// </summary>
        /// <param name="targetFrameworks">The available frameworks.</param>
        /// <returns>The selected target framework or null if non is matching.</returns>
        internal static string TryGetBestTargetFrameworkForCurrentSettings(IEnumerable<string> targetFrameworks)
        {
            var result = TargetFrameworkResolver.TryGetBestTargetFramework(targetFrameworks.ToList());
            LogVerbose("Selecting {0} as the best target framework for current settings", result ?? "(null)");
            return result;
        }

        /// <summary>
        ///     Calls "nuget.exe pack" to create a .nupkg file based on the given .nuspec file.
        /// </summary>
        /// <param name="nuspecFilePath">The full file-path to the .nuspec file to use.</param>
        public static void Pack(string nuspecFilePath)
        {
            Directory.CreateDirectory(PackOutputDirectory);

            // Use -NoDefaultExcludes to allow files and folders that start with a . to be packed into the package
            // This is done because if you want a file/folder in a Unity project, but you want Unity to ignore it, it must start with a .
            // This is especially useful for .cs and .js files that you don't want Unity to compile as game scripts
            var arguments = string.Format("pack \"{0}\" -OutputDirectory \"{1}\" -NoDefaultExcludes", nuspecFilePath, PackOutputDirectory);

            RunNugetProcess(arguments);
        }

        /// <summary>
        ///     Calls "nuget.exe push" to push a .nupkf file to the server location defined in the NuGet.config file.
        ///     Note: This differs slightly from NuGet's Push command by automatically calling Pack if the .nupkg doesn't already exist.
        /// </summary>
        /// <param name="nuspec">The NuspecFile which defines the package to push.  Only the ID and Version are used.</param>
        /// <param name="nuspecFilePath">The full file-path to the .nuspec file to use.  This is required by NuGet's Push command.</param>
        /// ///
        /// <param name="apiKey">The API key to use when pushing a package to the server.  This is optional.</param>
        public static void Push(NuspecFile nuspec, string nuspecFilePath, string apiKey = "")
        {
            var packagePath = nuspec.GetLocalPackageFilePath(PackOutputDirectory);
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

            var arguments = string.Format("push \"{0}\" {1} -configfile \"{2}\"", packagePath, apiKey, NugetConfigFilePath);

            RunNugetProcess(arguments);
        }

        /// <summary>
        ///     Recursively copies all files and sub-directories from one directory to another.
        /// </summary>
        /// <param name="sourceDirectoryPath">The filepath to the folder to copy from.</param>
        /// <param name="destDirectoryPath">The filepath to the folder to copy to.</param>
        private static void DirectoryCopy(string sourceDirectoryPath, string destDirectoryPath)
        {
            var dir = new DirectoryInfo(sourceDirectoryPath);
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException("Source directory does not exist or could not be found: " + sourceDirectoryPath);
            }

            // if the destination directory doesn't exist, create it
            if (!Directory.Exists(destDirectoryPath))
            {
                LogVerbose("Creating new directory: {0}", destDirectoryPath);
                Directory.CreateDirectory(destDirectoryPath);
            }

            // get the files in the directory and copy them to the new location
            var files = dir.GetFiles();
            foreach (var file in files)
            {
                var newFilePath = Path.Combine(destDirectoryPath, file.Name);

                try
                {
                    LogVerbose("Moving {0} to {1}", file.ToString(), newFilePath);
                    file.CopyTo(newFilePath, true);
                }
                catch (Exception e)
                {
                    Debug.LogWarningFormat(
                        "{0} couldn't be moved to {1}. It may be a native plugin already locked by Unity. Please trying closing Unity and manually moving it. \n{2}",
                        file,
                        newFilePath,
                        e);
                }
            }

            // copy sub-directories and their contents to new location
            var dirs = dir.GetDirectories();
            foreach (var subdir in dirs)
            {
                var temppath = Path.Combine(destDirectoryPath, subdir.Name);
                DirectoryCopy(subdir.FullName, temppath);
            }
        }

        /// <summary>
        ///     Recursively deletes the folder at the given path.
        ///     NOTE: Directory.Delete() doesn't delete Read-Only files, whereas this does.
        /// </summary>
        /// <param name="directoryPath">The path of the folder to delete.</param>
        private static void DeleteDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                return;
            }

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
        ///     Deletes a file at the given file-path.
        /// </summary>
        /// <param name="filePath">The file-path to the file to delete.</param>
        private static void DeleteFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.SetAttributes(filePath, FileAttributes.Normal);
                File.Delete(filePath);
            }
        }

        /// <summary>
        ///     Deletes all files in the given directory or in any sub-directory, with the given extension.
        /// </summary>
        /// <param name="directoryPath">The path to the directory to delete all files of the given extension from.</param>
        /// <param name="extension">The extension of the files to delete, in the form "*.ext".</param>
        private static void DeleteAllFiles(string directoryPath, string extension)
        {
            var files = Directory.GetFiles(directoryPath, extension, SearchOption.AllDirectories);
            foreach (var file in files)
            {
                DeleteFile(file);
            }
        }

        /// <summary>
        ///     Uninstall's all of the currently installed packages.
        /// </summary>
        internal static void UninstallAll(List<INuGetPackage> packagesToUninstall)
        {
            foreach (var package in packagesToUninstall)
            {
                Uninstall(package, false);
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        ///     "Uninstall's" the given package by simply deleting its folder.
        /// </summary>
        /// <param name="package">The NugetPackage to uninstall.</param>
        /// <param name="refreshAssets">True to force Unity to refresh its Assets folder.  False to temporarily ignore the change.  Defaults to true.</param>
        public static void Uninstall(INuGetPackageIdentifier package, bool refreshAssets = true)
        {
            LogVerbose("Uninstalling: {0} {1}", package.Id, package.Version);

            var foundPackage = package as NugetPackage ?? package as NuGetPackageV3 ?? GetSpecificPackage(package);

            // update the package.config file
            if (PackagesConfigFile.RemovePackage(foundPackage))
            {
                PackagesConfigFile.Save(PackagesConfigFilePath);
            }

            var packageInstallDirectory = Path.Combine(NugetConfigFile.RepositoryPath, $"{foundPackage.Id}.{foundPackage.Version}");
            DeleteDirectory(packageInstallDirectory);

            var metaFile = Path.Combine(NugetConfigFile.RepositoryPath, $"{foundPackage.Id}.{foundPackage.Version}.meta");
            DeleteFile(metaFile);

            var toolsInstallDirectory = Path.Combine(AbsoluteProjectPath, "Packages", $"{foundPackage.Id}.{foundPackage.Version}");
            DeleteDirectory(toolsInstallDirectory);

            if (installedPackages != null && installedPackages.Count > 0)
            {
                installedPackages.Remove(foundPackage.Id);

                // uninstall all non manually installed dependencies that are not a dependency of another installed package
                var frameworkGroup = GetBestDependencyFrameworkGroupForCurrentSettings(foundPackage);
                foreach (var dependency in frameworkGroup.Dependencies)
                {
                    var packageConfiguration = PackagesConfigFile.Packages.Find(pkg => pkg.Id == dependency.Id);
                    if (packageConfiguration == null || packageConfiguration.IsManuallyInstalled)
                    {
                        continue;
                    }

                    var hasMoreParents = installedPackages.Values.Select(GetBestDependencyFrameworkGroupForCurrentSettings)
                        .Any(frameworkGrp => frameworkGrp.Dependencies.Any(dep => dep.Id == dependency.Id));

                    if (!hasMoreParents)
                    {
                        Uninstall(dependency, false);
                    }
                }
            }

            if (refreshAssets)
            {
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        ///     Updates a package by uninstalling the currently installed version and installing the "new" version.
        /// </summary>
        /// <param name="currentVersion">The current package to uninstall.</param>
        /// <param name="newVersion">The package to install.</param>
        /// <param name="refreshAssets">True to refresh the assets inside Unity.  False to ignore them (for now).  Defaults to true.</param>
        public static bool Update(INuGetPackageIdentifier currentVersion, INuGetPackage newVersion, bool refreshAssets = true)
        {
            LogVerbose("Updating {0} {1} to {2}", currentVersion.Id, currentVersion.Version, newVersion.Version);
            Uninstall(currentVersion, false);
            newVersion.IsManuallyInstalled = newVersion.IsManuallyInstalled || currentVersion.IsManuallyInstalled;
            return InstallIdentifier(newVersion, refreshAssets, true);
        }

        /// <summary>
        ///     Installs all of the given updates, and uninstalls the corresponding package that is already installed.
        /// </summary>
        /// <param name="updates">The list of all updates to install.</param>
        /// <param name="packagesToUpdate">The list of all packages currently installed.</param>
        public static void UpdateAll(IEnumerable<INuGetPackage> updates, IEnumerable<INuGetPackage> packagesToUpdate)
        {
            var progressStep = 1.0f / updates.Count();
            float currentProgress = 0;

            foreach (var update in updates)
            {
                EditorUtility.DisplayProgressBar(
                    string.Format("Updating to {0} {1}", update.Id, update.Version),
                    "Installing All Updates",
                    currentProgress);

                var installedPackage = packagesToUpdate.FirstOrDefault(p => p.Id == update.Id);
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

        public static void SetManuallyInstalledFlag(INuGetPackageIdentifier package)
        {
            PackagesConfigFile.SetManuallyInstalledFlag(package);
            PackagesConfigFile.Save(PackagesConfigFilePath);
        }

        /// <summary>
        ///     Updates the dictionary of packages that are actually installed in the project based on the files that are currently installed.
        /// </summary>
        public static void UpdateInstalledPackages()
        {
            LoadNugetConfigFile();

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            if (installedPackages == null)
            {
                installedPackages = new Dictionary<string, INuGetPackage>();
            }
            else
            {
                installedPackages.Clear();
            }

            // loops through the packages that are actually installed in the project
            if (Directory.Exists(NugetConfigFile.RepositoryPath))
            {
                var manuallyInstalledPackagesNumber = 0;

                void AddPackageToInstalled(NugetPackage package)
                {
                    if (!installedPackages.ContainsKey(package.Id))
                    {
                        package.IsManuallyInstalled = PackagesConfigFile.Packages.Find(pkg => pkg.Id == package.Id)?.IsManuallyInstalled ?? false;
                        if (package.IsManuallyInstalled)
                        {
                            manuallyInstalledPackagesNumber++;
                        }

                        installedPackages.Add(package.Id, package);
                    }
                    else
                    {
                        Debug.LogErrorFormat("Package is already in installed list: {0}", package.Id);
                    }
                }

                // a package that was installed via NuGet will have the .nupkg it came from inside the folder
                var nupkgFiles = Directory.GetFiles(NugetConfigFile.RepositoryPath, "*.nupkg", SearchOption.AllDirectories);
                foreach (var nupkgFile in nupkgFiles)
                {
                    var package = NugetPackage.FromNupkgFile(
                        nupkgFile,
                        new LocalNuGetPackageSource("Nupkg file from Project", Path.GetDirectoryName(nupkgFile)));
                    AddPackageToInstalled(package);
                }

                // if the source code & assets for a package are pulled directly into the project (ex: via a symlink/junction) it should have a .nuspec defining the package
                var nuspecFiles = Directory.GetFiles(NugetConfigFile.RepositoryPath, "*.nuspec", SearchOption.AllDirectories);
                foreach (var nuspecFile in nuspecFiles)
                {
                    var package = NugetPackage.FromNuspec(
                        NuspecFile.Load(nuspecFile),
                        new LocalNuGetPackageSource("Nuspec file from Project", Path.GetDirectoryName(nuspecFile)));
                    AddPackageToInstalled(package);
                }

                if (manuallyInstalledPackagesNumber == 0)
                {
                    // set root packages as manually installed if none are marked as such
                    foreach (var rootPackage in GetInstalledRootPackages())
                    {
                        PackagesConfigFile.SetManuallyInstalledFlag(rootPackage);
                    }

                    PackagesConfigFile.Save(PackagesConfigFilePath);
                }
            }

            LogVerbose("Getting installed packages took {0} ms", stopwatch.ElapsedMilliseconds);
        }

        internal static List<INuGetPackage> GetInstalledRootPackages()
        {
            // default all packages to being roots
            var roots = new List<INuGetPackage>(installedPackages.Values);

            // remove a package as a root if another package is dependent on it
            foreach (var package in installedPackages.Values)
            {
                var frameworkGroup = GetBestDependencyFrameworkGroupForCurrentSettings(package);
                foreach (var dependency in frameworkGroup.Dependencies)
                {
                    roots.RemoveAll(p => p.Id == dependency.Id);
                }
            }

            return roots;
        }

        /// <summary>
        ///     Gets a list of NuGetPackages via the HTTP Search() function defined by NuGet.Server and NuGet Gallery.
        ///     This allows searching for partial IDs or even the empty string (the default) to list ALL packages.
        ///     NOTE: See the functions and parameters defined here: https://www.nuget.org/api/v2/$metadata.
        /// </summary>
        /// <param name="searchTerm">The search term to use to filter packages. Defaults to the empty string.</param>
        /// <param name="includeAllVersions">True to include older versions that are not the latest version.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="numberToGet">The number of packages to fetch.</param>
        /// <param name="numberToSkip">The number of packages to skip before fetching.</param>
        /// <returns>The list of available packages.</returns>
        public static Task<List<INuGetPackage>> Search(
            string searchTerm = "",
            bool includeAllVersions = false,
            bool includePrerelease = false,
            int numberToGet = 15,
            int numberToSkip = 0,
            CancellationToken cancellationToken = default)
        {
            return activePackageSource.Search(searchTerm, includeAllVersions, includePrerelease, numberToGet, numberToSkip, cancellationToken);
        }

        /// <summary>
        ///     Queries the server with the given list of installed packages to get any updates that are available.
        /// </summary>
        /// <param name="packagesToUpdate">The list of currently installed packages.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="includeAllVersions">True to include older versions that are not the latest version.</param>
        /// <param name="targetFrameworks">The specific frameworks to target?.</param>
        /// <param name="versionConstraints">The version constraints?.</param>
        /// <returns>A list of all updates available.</returns>
        public static List<INuGetPackage> GetUpdates(
            IEnumerable<INuGetPackage> packagesToUpdate,
            bool includePrerelease = false,
            bool includeAllVersions = false,
            string targetFrameworks = "",
            string versionConstraints = "")
        {
            return activePackageSource.GetUpdates(packagesToUpdate, includePrerelease, includeAllVersions, targetFrameworks, versionConstraints);
        }

        /// <summary>
        ///     Gets a NugetPackage from the NuGet server with the exact ID and Version given.
        /// </summary>
        /// <param name="packageId">The <see cref="INuGetPackageIdentifier" /> containing the ID and Version of the package to get.</param>
        /// <returns>The retrieved package, if there is one.  Null if no matching package was found.</returns>
        private static INuGetPackage GetSpecificPackage(INuGetPackageIdentifier packageId)
        {
            // First look to see if the package is already installed
            var package = GetInstalledPackage(packageId);

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
        ///     Tries to find an already installed package that matches (or is in the range of) the given package ID.
        /// </summary>
        /// <param name="packageId">The <see cref="INuGetPackageIdentifier" /> of the <see cref="NugetPackage" /> to find.</param>
        /// <returns>The best <see cref="NugetPackage" /> match, if there is one, otherwise null.</returns>
        private static INuGetPackage GetInstalledPackage(INuGetPackageIdentifier packageId)
        {
            if (InstalledPackagesDictionary.TryGetValue(packageId.Id, out var installedPackage))
            {
                if (packageId.Version != installedPackage.Version)
                {
                    if (packageId.InRange(installedPackage))
                    {
                        var configPackage = PackagesConfigFile.Packages.Find(p => p.Id == packageId.Id);
                        if (configPackage != null && configPackage.CompareVersion(installedPackage) < 0)
                        {
                            LogVerbose(
                                "Requested {0} {1}. {2} is already installed, but config demands lower version.",
                                packageId.Id,
                                packageId.Version,
                                installedPackage.Version);
                            installedPackage = null;
                        }
                        else
                        {
                            LogVerbose(
                                "Requested {0} {1}, but {2} is already installed, so using that.",
                                packageId.Id,
                                packageId.Version,
                                installedPackage.Version);
                        }
                    }
                    else
                    {
                        LogVerbose(
                            "Requested {0} {1}. {2} is already installed, but it is out of range.",
                            packageId.Id,
                            packageId.Version,
                            installedPackage.Version);
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
        ///     Tries to find an already cached package that matches the given package ID.
        /// </summary>
        /// <param name="packageId">The <see cref="NugetPackageIdentifier" /> of the <see cref="NugetPackage" /> to find.</param>
        /// <returns>The best <see cref="NugetPackage" /> match, if there is one, otherwise null.</returns>
        private static NugetPackage GetCachedPackage(INuGetPackageIdentifier packageId)
        {
            NugetPackage package = null;

            if (NugetConfigFile.InstallFromCache && !packageId.HasVersionRange)
            {
                var cachedPackagePath = Path.Combine(PackOutputDirectory, packageId.PackageFileName);

                if (File.Exists(cachedPackagePath))
                {
                    LogVerbose("Found exact package in the cache: {0}", cachedPackagePath);
                    package = NugetPackage.FromNupkgFile(
                        cachedPackagePath,
                        new LocalNuGetPackageSource("Nupkg file from cache", Path.GetDirectoryName(cachedPackagePath)));
                }
            }

            return package;
        }

        /// <summary>
        ///     Tries to find an "online" (in the package sources - which could be local) package that matches (or is in the range of) the given package ID.
        /// </summary>
        /// <param name="packageId">The <see cref="INuGetPackageIdentifier" /> of the <see cref="INuGetPackage" /> to find.</param>
        /// <returns>The best <see cref="INuGetPackage" /> match, if there is one, otherwise null.</returns>
        private static INuGetPackage GetOnlinePackage(INuGetPackageIdentifier packageId)
        {
            var package = activePackageSource.GetSpecificPackage(packageId);

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
        ///     Installs the package given by the identifier.  It fetches the appropriate full package from the installed packages, package cache, or package
        ///     sources and installs it.
        /// </summary>
        /// <param name="package">The identifier of the package to install.</param>
        /// <param name="refreshAssets">True to refresh the Unity asset database.  False to ignore the changes (temporarily).</param>
        /// <param name="isUpdate">True to indicate we're calling method as result of Update and don't want to go through IsAlreadyImportedInEngine</param>
        /// <param name="installDependencies">True to also install all dependencies of the <paramref name="package" />.</param>
        internal static bool InstallIdentifier(
            INuGetPackageIdentifier package,
            bool refreshAssets = true,
            bool isUpdate = false,
            bool installDependencies = true)
        {
            if (!isUpdate && IsAlreadyImportedInEngine(package, false))
            {
                LogVerbose("Package {0} is already imported in engine, skipping install.", package);
                return true;
            }

            var foundPackage = GetSpecificPackage(package);

            if (foundPackage == null)
            {
                Debug.LogErrorFormat("Could not find {0} {1} or greater.", package.Id, package.Version);
                return false;
            }

            foundPackage.IsManuallyInstalled = package.IsManuallyInstalled;
            return Install(foundPackage, refreshAssets, isUpdate, installDependencies);
        }

        /// <summary>
        ///     Outputs the given message to the log only if verbose mode is active.  Otherwise it does nothing.
        /// </summary>
        /// <param name="format">The formatted message string.</param>
        /// <param name="args">The arguments for the formatted message string.</param>
        public static void LogVerbose(string format, params object[] args)
        {
            if (NugetConfigFile != null && !NugetConfigFile.Verbose)
            {
                return;
            }

#if UNITY_2019_1_OR_NEWER
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, format, args);
#else

            // application state changes need to run on main thread
            var isMainThread = !Thread.CurrentThread.IsThreadPoolThread;
            StackTraceLogType stackTraceLogType = default;
            if (isMainThread)
            {
                stackTraceLogType = Application.GetStackTraceLogType(LogType.Log);
                Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            }

            Debug.LogFormat(format, args);
            if (isMainThread)
            {
                Application.SetStackTraceLogType(LogType.Log, stackTraceLogType);
            }
#endif
        }

        /// <summary>
        ///     Installs the given package.
        /// </summary>
        /// <param name="package">The package to install.</param>
        /// <param name="refreshAssets">True to refresh the Unity asset database.  False to ignore the changes (temporarily).</param>
        /// <param name="isUpdate">True to indicate we're calling method as result of Update and don't want to go through IsAlreadyImportedInEngine.</param>
        /// <param name="installDependencies">True to also install all dependencies of the <paramref name="package" />.</param>
        public static bool Install(INuGetPackage package, bool refreshAssets = true, bool isUpdate = false, bool installDependencies = true)
        {
            if (!isUpdate && IsAlreadyImportedInEngine(package, false))
            {
                LogVerbose("Package {0} is already imported in engine, skipping install.", package);
                return true;
            }

            if (InstalledPackagesDictionary.TryGetValue(package.Id, out var installedPackage))
            {
                var comparisonResult = installedPackage.CompareTo(package);
                if (comparisonResult < 0)
                {
                    LogVerbose(
                        "{0} {1} is installed, but need {2} or greater. Updating to {3}",
                        installedPackage.Id,
                        installedPackage.Version,
                        package.Version,
                        package.Version);
                    return Update(installedPackage, package, false);
                }

                if (comparisonResult > 0)
                {
                    var configPackage = PackagesConfigFile.Packages.Find(identifier => identifier.Id == package.Id);
                    if (configPackage != null && configPackage.CompareTo(installedPackage) < 0)
                    {
                        LogVerbose(
                            "{0} {1} is installed but config needs {2} so downgrading.",
                            installedPackage.Id,
                            installedPackage.Version,
                            package.Version);
                        return Update(installedPackage, package, false);
                    }

                    LogVerbose(
                        "{0} {1} is installed. {2} or greater is needed, so using installed version.",
                        installedPackage.Id,
                        installedPackage.Version,
                        package.Version);
                }
                else
                {
                    LogVerbose("Already installed: {0} {1}", package.Id, package.Version);
                }

                return true;
            }

            try
            {
                LogVerbose("Installing: {0} {1}", package.Id, package.Version);

                // look to see if the package (any version) is already installed

                if (refreshAssets)
                {
                    EditorUtility.DisplayProgressBar(
                        string.Format("Installing {0} {1}", package.Id, package.Version),
                        "Installing Dependencies",
                        0.1f);
                }

                if (installDependencies)
                {
                    var dependencies = package.Dependencies;

                    // install all dependencies for target framework
                    var frameworkGroup = GetNullableBestDependencyFrameworkGroupForCurrentSettings(dependencies);

                    if (frameworkGroup == null && dependencies.Count != 0)
                    {
                        Debug.LogWarningFormat(
                            "Can't find a matching dependency group for the NuGet Package {0} {1} that has a TargetFramework supported by the current Unity Scripting Backend. The NuGet Package supports the following TargetFramework's: {2}",
                            package.Id,
                            package.Version,
                            string.Join(", ", dependencies.Select(dependency => dependency.TargetFramework)));
                    }
                    else if (frameworkGroup != null)
                    {
                        LogVerbose("Installing dependencies for TargetFramework: {0}", frameworkGroup.TargetFramework);
                        foreach (var dependency in frameworkGroup.Dependencies)
                        {
                            LogVerbose("Installing Dependency: {0} {1}", dependency.Id, dependency.Version);
                            var installed = InstallIdentifier(dependency, refreshAssets, installDependencies);
                            if (!installed)
                            {
                                throw new Exception(string.Format("Failed to install dependency: {0} {1}.", dependency.Id, dependency.Version));
                            }
                        }
                    }
                }

                // update packages.config
                PackagesConfigFile.AddPackage(package);
                PackagesConfigFile.Save(PackagesConfigFilePath);

                var cachedPackagePath = Path.Combine(PackOutputDirectory, package.PackageFileName);
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
                        package.DownloadNupkgToFile(cachedPackagePath);
                    }
                    else
                    {
                        LogVerbose("Downloading package {0} {1}", package.Id, package.Version);

                        if (refreshAssets)
                        {
                            EditorUtility.DisplayProgressBar(
                                string.Format("Installing {0} {1}", package.Id, package.Version),
                                "Downloading Package",
                                0.3f);
                        }

                        package.DownloadNupkgToFile(cachedPackagePath);
                    }
                }

                if (refreshAssets)
                {
                    EditorUtility.DisplayProgressBar(string.Format("Installing {0} {1}", package.Id, package.Version), "Extracting Package", 0.6f);
                }

                if (File.Exists(cachedPackagePath))
                {
                    var baseDirectory = Path.Combine(NugetConfigFile.RepositoryPath, string.Format("{0}.{1}", package.Id, package.Version));

                    // unzip the package
                    using (var zip = ZipFile.OpenRead(cachedPackagePath))
                    {
                        foreach (var entry in zip.Entries)
                        {
                            var filePath = Path.Combine(baseDirectory, entry.FullName);

                            var directory = Path.GetDirectoryName(filePath);
                            Directory.CreateDirectory(directory);
                            if (Directory.Exists(filePath))
                            {
                                Debug.LogWarning($"The path {filePath} refers to an existing directory. Overwriting it may lead to data loss.");
                                continue;
                            }

                            entry.ExtractToFile(filePath, true);

                            if (NugetConfigFile.ReadOnlyPackageFiles)
                            {
                                var extractedFile = new FileInfo(filePath);
                                extractedFile.Attributes |= FileAttributes.ReadOnly;
                            }
                        }
                    }

                    // copy the .nupkg inside the Unity project
                    File.Copy(cachedPackagePath, Path.Combine(baseDirectory, package.PackageFileName), true);
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
                CleanInstallationDirectory(package);

                // update the installed packages list
                InstalledPackagesDictionary.Add(package.Id, package);
                return true;
            }
            catch (Exception e)
            {
                WarnIfDotNetAuthenticationIssue(e);
                Debug.LogErrorFormat("Unable to install package {0} {1}\n{2}", package.Id, package.Version, e);
                return false;
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

        private static void WarnIfDotNetAuthenticationIssue(Exception e)
        {
            var webException = e as WebException;
            var webResponse = webException != null ? webException.Response as HttpWebResponse : null;
            if (webResponse != null &&
                webResponse.StatusCode == HttpStatusCode.BadRequest &&
                webException.Message.Contains("Authentication information is not given in the correct format"))
            {
                // This error occurs when downloading a package with authentication using .NET 3.5, but seems to be fixed by the new .NET 4.6 runtime.
                // Inform users when this occurs.
                Debug.LogError(
                    "Authentication failed. This can occur due to a known issue in .NET 3.5. This can be fixed by changing Scripting Runtime to Experimental (.NET 4.6 Equivalent) in Player Settings.");
            }
        }

        /// <summary>
        ///     Get the specified URL from the web. Throws exceptions if the request fails.
        /// </summary>
        /// <param name="url">URL that will be loaded.</param>
        /// <param name="userName">UserName that will be passed in the Authorization header or the request. If null, authorization is omitted.</param>
        /// <param name="password">Password that will be passed in the Authorization header or the request. If null, authorization is omitted.</param>
        /// <param name="timeOut">Timeout in milliseconds or null to use the default timeout values of HttpWebRequest.</param>
        /// <returns>Stream containing the result.</returns>
        public static Stream RequestUrl(string url, string userName, string password, int? timeOut)
        {
            // Mono doesn't have a Certificate Authority, so we have to provide all validation manually. Currently just accept anything.
            // See here: http://stackoverflow.com/questions/4926676/mono-webrequest-fails-with-https
            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, policyErrors) => true;

#pragma warning disable SYSLIB0014 // Type or member is obsolete
            var getRequest = (HttpWebRequest)WebRequest.Create(url);
#pragma warning restore SYSLIB0014 // Type or member is obsolete
            getRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.None;
            getRequest.Timeout = timeOut ?? NugetConfigFile.RequestTimeoutSeconds * 1000;

            if (string.IsNullOrEmpty(password))
            {
                var creds = CredentialProviderHelper.GetCredentialFromProvider(getRequest.RequestUri);
                if (creds.HasValue)
                {
                    userName = creds.Value.UserName;
                    password = creds.Value.Password;
                }
            }

            if (password != null)
            {
                // Send password as described by https://docs.microsoft.com/en-us/vsts/integrate/get-started/rest/basics.
                // This works with Visual Studio Team Services, but hasn't been tested with other authentication schemes so there may be additional work needed if there
                // are different kinds of authentication.
                getRequest.Headers.Add(
                    "Authorization",
                    "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", userName, password))));
            }

            LogVerbose("HTTP GET {0}", url);
            var objStream = getRequest.GetResponse().GetResponseStream();
            return objStream;
        }

        /// <summary>
        ///     Restores all packages defined in packages.config.
        /// </summary>
        /// <param name="installDependencies">True to also install all dependencies of the packages listed in the <see cref="PackagesConfigFile" />.</param>
        public static void Restore(bool installDependencies = true)
        {
            UpdateInstalledPackages();

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var somethingChanged = false;
            try
            {
                var packagesToInstall = PackagesConfigFile.Packages.FindAll(package => !IsInstalled(package));
                if (packagesToInstall.Count > 0)
                {
                    var progressStep = 1.0f / packagesToInstall.Count;
                    float currentProgress = 0;

                    LogVerbose("Restoring {0} packages.", packagesToInstall.Count);

                    foreach (var package in packagesToInstall)
                    {
                        if (package != null)
                        {
                            EditorUtility.DisplayProgressBar(
                                "Restoring NuGet Packages",
                                string.Format("Restoring {0} {1}", package.Id, package.Version),
                                currentProgress);
                            LogVerbose("---Restoring {0} {1}", package.Id, package.Version);
                            InstallIdentifier(package, installDependencies: installDependencies);
                            somethingChanged = true;
                        }

                        currentProgress += progressStep;
                    }
                }
                else
                {
                    LogVerbose("No packages need restoring.");
                }

                somethingChanged = somethingChanged || CheckForUnnecessaryPackages();
            }
            catch (Exception e)
            {
                Debug.LogErrorFormat("{0}", e);
            }
            finally
            {
                LogVerbose("Restoring packages took {0} ms", stopwatch.ElapsedMilliseconds);

                if (somethingChanged)
                {
                    AssetDatabase.Refresh();
                }

                EditorUtility.ClearProgressBar();
            }
        }

        internal static bool CheckForUnnecessaryPackages()
        {
            if (!Directory.Exists(NugetConfigFile.RepositoryPath))
            {
                return false;
            }

            var directories = Directory.GetDirectories(NugetConfigFile.RepositoryPath, "*", SearchOption.TopDirectoryOnly);
            var somethingDeleted = false;
            foreach (var folder in directories)
            {
                var folderName = Path.GetFileName(folder);
                if (folderName.Equals(".svn", StringComparison.OrdinalIgnoreCase))
                {
                    // ignore folder required by SVN tool
                    continue;
                }

                var pkgPath = Path.Combine(folder, $"{folderName}.nupkg");
                if (!File.Exists(pkgPath))
                {
                    // ignore folder not containing a nuget-package
                    continue;
                }

                var package = NugetPackage.FromNupkgFile(
                    pkgPath,
                    new LocalNuGetPackageSource("Nupkg file already installed", Path.GetDirectoryName(pkgPath)));

                var installed = false;
                foreach (var packageId in PackagesConfigFile.Packages)
                {
                    if (packageId.CompareTo(package) == 0)
                    {
                        installed = true;
                        break;
                    }
                }

                if (!installed)
                {
                    somethingDeleted = true;
                    LogVerbose("---DELETE unnecessary package {0}", folder);

                    DeleteDirectory(folder);
                    DeleteFile(folder + ".meta");
                }
            }

            if (somethingDeleted)
            {
                UpdateInstalledPackages();
            }

            return somethingDeleted;
        }

        /// <summary>
        ///     Checks if a given package is installed.
        /// </summary>
        /// <param name="package">The package to check if is installed.</param>
        /// <returns>True if the given package is installed.  False if it is not.</returns>
        internal static bool IsInstalled(INuGetPackageIdentifier package)
        {
            if (IsAlreadyImportedInEngine(package))
            {
                return true;
            }

            var isInstalled = false;
            if (InstalledPackagesDictionary.TryGetValue(package.Id, out var installedPackage))
            {
                isInstalled = package.Equals(installedPackage);
            }

            return isInstalled;
        }
    }
}
