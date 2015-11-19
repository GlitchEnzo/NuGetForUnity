using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
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
    private static readonly string NugetConfigFilePath = Path.Combine(NugetPath, "./NuGet.config");

    /// <summary>
    /// The path to the nuget.exe file.
    /// </summary>
    private static readonly string NugetExeFilePath = Path.Combine(NugetPath, "./nuget.exe");

    /// <summary>
    /// The path to the packages.config file.
    /// </summary>
    private static readonly string PackagesConfigFilePath = Path.Combine(Application.dataPath, "./packages.config");

    /// <summary>
    /// The path where to put created (packed) .nupkg files.
    /// </summary>
    private static readonly string PackOutputDirectory = Path.Combine(Application.dataPath, "../nupkgs");

    /// <summary>
    /// The amount of time, in milliseconds, before the nuget.exe process times out and is killed.
    /// </summary>
    private const int TimeOut = 60000;

    /// <summary>
    /// Static constructor used by Unity to restore packages defined in packages.config.
    /// </summary>
    static NugetHelper()
    {
        // restore packages silently since this would be output EVERY time the project is loaded or a code-file changes
        Restore(false);
    }

    /// <summary>
    /// Runs nuget.exe using the given arguments.
    /// </summary>
    /// <param name="arguments">The arguments to run nuget.exe with.</param>
    /// <param name="logOuput">True to output debug information to the Unity console.  Defaults to true.</param>
    /// <returns>The string of text that was output from nuget.exe following its execution.</returns>
    private static string RunNugetProcess(string arguments, bool logOuput = true)
    {
        ////Debug.Log("Args = " + arguments);

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

        return output;
    }

    /// <summary>
    /// Calls "nuget.exe list" and returns of a list of NugetPackages that were returned as a result.
    /// </summary>
    /// <param name="search">The search term to use to filter the list results.  Defaults to string.Empty.</param>
    /// <param name="showAllVersions">True to include old versions of packages.  False to only display the latest.  Defaults to false.</param>
    /// <param name="showPrerelease">True to show prerelease packages (alpha, beta, etc).  False to only display stable versions.  Defaults to false.</param>
    /// <returns></returns>
    public static List<NugetPackage> List(string search = "", bool showAllVersions = false, bool showPrerelease = false)
    {
        string arguments = string.Format("list {0} -verbosity detailed -configfile \"{1}\"", search, NugetConfigFilePath);
        if (showAllVersions)
            arguments += " -AllVersions";
        if (showPrerelease)
            arguments += " -Prerelease";

        string output = RunNugetProcess(arguments, false);

        List<NugetPackage> packages = new List<NugetPackage>();

        // parse the command line output to build a list of available packages
        // In the form:
        // angular-webstorage
        //  0.14.0
        //  WebStorage Service for AngularJS
        //  License url: http://opensource.org/licenses/MIT
        string[] lines = output.Split('\n');
        //Debug.Log(lines.Count() + " lines");
        for (int i = 0; i < lines.Length - 4; i++)
        {
            // using NuGet.Server or NuGet Gallery causes the HTTP GET command used to be output to the console
            if (lines[i].StartsWith("GET "))
                continue;

            NugetPackage package = new NugetPackage();
            package.ID = lines[i++].Trim();
            package.Version = lines[i++].Trim();
            package.Description = lines[i++];

            while (!lines[i].Contains("License"))
                package.Description += lines[i++];

            package.Description = package.Description.Trim();
            package.LicenseURL = lines[i++].Replace(" License url: ", String.Empty);

            packages.Add(package);
            //Debug.LogFormat("Created {0}", package.ID);
        } 

        return packages;
    }

    /// <summary>
    /// Calls "nuget.exe restore" to restore all packages defined in packages.config.
    /// </summary>
    /// <param name="logOutput">True to output debug info to the Unity console.  False to restore silently.  Defaults to true.</param>
    public static void Restore(bool logOutput = true)
    {
        string arguments = string.Format("restore \"{0}\" -configfile \"{1}\"", PackagesConfigFilePath, NugetConfigFilePath);

        RunNugetProcess(arguments, logOutput);

        // http://stackoverflow.com/questions/12930868/nuget-exe-install-not-updating-packages-config-or-csproj
        // http://stackoverflow.com/questions/17187725/using-nuget-exe-commandline-to-install-dependency
        //var packages = LoadInstalledPackages();
        //foreach (var package in packages)
        //{
        //    Install(package);
        //}

        Clean();

        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Calls "nuget.exe install" to install the given package.
    /// </summary>
    /// <param name="package">The NugetPackage to install.  Only the ID and Version are used for the installation.</param>
    public static void Install(NugetPackage package)
    {
        string arguments = string.Format("install {0} -Version {1} -configfile \"{2}\"", package.ID, package.Version, NugetConfigFilePath);

        string output = RunNugetProcess(arguments);

        // Check the output for any installed dependencies
        // https://msdn.microsoft.com/en-us/library/bs2twtah(v=vs.110).aspx
        // Example: "Attempting to resolve dependency 'StyleCop.MSBuild (ò 4.7.49.0)'.
        //           Installing 'StyleCop.MSBuild 4.7.49.1'."
        //string pattern = @"Attempting to resolve dependency '(?<package>.+)'";
        string pattern = @"Attempting to resolve dependency.+\nInstalling '(?<package>.+)'";
        Regex dependencyRegex = new Regex(pattern, RegexOptions.Multiline);

        var matches = dependencyRegex.Matches(output);
        foreach (Match match in matches)
        {
            //Debug.Log(match.ToString());
            //Debug.Log(match.Groups["package"].Value);
            string[] split = match.Groups["package"].Value.Split(' ');

            NugetPackage dependencyPackage = new NugetPackage();
            dependencyPackage.ID = split[0].Trim();

            //char compare = split[1][0];
            //Debug.Log((int)compare);
            //char greaterThanEqual = '\xF2';
            //if (compare == greaterThanEqual)
            //{
            //    Debug.Log("Greater than or equals...");
            //}

            //dependencyPackage.Version = split[1].Substring(2, split[1].Length - 3);
            dependencyPackage.Version = split[1];
            //Debug.Log(id + ":" + version);

            AddInstalledPackage(dependencyPackage);

            Clean(dependencyPackage);
        }

        // Update the packages.config file
        AddInstalledPackage(package);

        Clean(package);

        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Clean all currently installed packages.
    /// </summary>
    private static void Clean()
    {
        var installedPackages = LoadInstalledPackages();

        foreach (var package in installedPackages)
        {
            Clean(package);
        }
    }

    /// <summary>
    /// Cleans up a package after it has been installed.
    /// Since we are in Unity, we can make certain assumptions on which files will NOT be used, so we can delete them.
    /// </summary>
    /// <param name="package">The NugetPackage to clean.</param>
    private static void Clean(NugetPackage package)
    {
        // TODO: Get the install directory from the NuGet.config file
        string packageInstallDirectory = Application.dataPath + "/Packages";
        packageInstallDirectory += "/" + package.ID + "." + package.Version;

        ////Debug.Log("Cleaning " + packageInstallDirectory);

        string metaFile = packageInstallDirectory + "/" + package.ID + ".nuspec.meta";
        if (File.Exists(metaFile))
        {
            File.Delete(metaFile);
        }

        // Unity has no use for the tools or build directories
        DeleteDirectory(packageInstallDirectory + "/tools");
        DeleteDirectory(packageInstallDirectory + "/build");

        // Unity can only use .NET 3.5 or lower, so delete everything else
        if (Directory.Exists(packageInstallDirectory + "/lib"))
        {
            string[] libDirectories = Directory.GetDirectories(packageInstallDirectory + "/lib");
            foreach (var directory in libDirectories)
            {
                if (directory.Contains("net40") || directory.Contains("net45") || directory.Contains("netcore45"))
                {
                    DeleteDirectory(directory);
                }
                else if (directory.Contains("net20"))
                {
                    // if .NET 2.0 exists, keep it, unless there is also a .NET 3.5 (highest allowed in Unity) version as well
                    if (Directory.Exists(packageInstallDirectory + "/lib/net35"))
                    {
                        DeleteDirectory(directory);
                    }
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
    public static void Push(NuspecFile nuspec, string nuspecFilePath)
    {
        string packagePath = Path.Combine(PackOutputDirectory, string.Format("{0}.{1}.nupkg", nuspec.Id, nuspec.Version));
        if (!File.Exists(packagePath))
        {
            ////Debug.Log("Attempting to Pack.");
            Pack(nuspecFilePath);

            if (!File.Exists(packagePath))
            {
                Debug.LogErrorFormat("NuGet package not found: {0}", packagePath);
                return;
            }
        }

        string arguments = string.Format("push \"{0}\" -configfile \"{1}\"", packagePath, NugetConfigFilePath);

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

        var directoryInfo = new DirectoryInfo(directoryPath) { Attributes = FileAttributes.Normal };
        foreach (var childInfo in directoryInfo.GetFileSystemInfos())
        {
            DeleteDirectory(childInfo.FullName);
        }

        directoryInfo.Attributes = FileAttributes.Normal;
        directoryInfo.Delete(true);
    }

    /// <summary>
    /// "Uninstalls" the given package by simply deleting its folder.
    /// </summary>
    /// <param name="package">The NugetPackage to uninstall.</param>
    /// <param name="refreshAssets">True to force Unity to refesh its Assets folder.  False to temporarily ignore the change.  Defaults to true.</param>
    public static void Uninstall(NugetPackage package, bool refreshAssets = true)
    {
        // TODO: Get the install directory from the NuGet.config file
        string packageInstallDirectory = Application.dataPath + "/Packages";
        packageInstallDirectory += "/" + package.ID + "." + package.Version;
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
    public static void Update(NugetPackage currentVersion, NugetPackage newVersion)
    {
        Uninstall(currentVersion, false);
        Install(newVersion);
    }

    /// <summary>
    /// Loads a list of all currently installed packages by reading the packages.config file.
    /// </summary>
    /// <returns>A list of installed NugetPackages.</returns>
    public static List<NugetPackage> LoadInstalledPackages()
    {
        List<NugetPackage> packages = new List<NugetPackage>();

        XDocument packagesFile = XDocument.Load(PackagesConfigFilePath);
        foreach (var packageElement in packagesFile.Root.Elements())
        {
            NugetPackage package = new NugetPackage();
            package.ID = packageElement.Attribute("id").Value;
            package.Version = packageElement.Attribute("version").Value;
            packages.Add(package);
        }

        return packages;
    }

    /// <summary>
    /// Adds a package to the packages.config file.
    /// </summary>
    /// <param name="package">The NugetPackage to add to the packages.config file.</param>
    private static void AddInstalledPackage(NugetPackage package)
    {
        List<NugetPackage> packages = LoadInstalledPackages();

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
    private static void RemoveInstalledPackage(NugetPackage package)
    {
        List<NugetPackage> packages = LoadInstalledPackages();
        packages.Remove(package);
        SaveInstalledPackages(packages);
    }

    /// <summary>
    /// Saves the packages.config file and populates it with given installed NugetPackages.
    /// </summary>
    /// <param name="packages">The list of currently installed NugetPackages to write to the packages.config file.</param>
    private static void SaveInstalledPackages(List<NugetPackage> packages)
    {
        XDocument packagesFile = new XDocument();
        packagesFile.Add(new XElement("packages"));
        foreach (var package in packages)
        {
            XElement packageElement = new XElement("package");
            packageElement.Add(new XAttribute("id", package.ID));
            packageElement.Add(new XAttribute("version", package.Version));
            packagesFile.Root.Add(packageElement);
        }

        packagesFile.Save(PackagesConfigFilePath);
    }
}
