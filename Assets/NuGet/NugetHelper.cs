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
    private static readonly string NugetPath = Application.dataPath + "/NuGet";

    private const string ConfigFilePath = "NuGet.config";

    private const string PackagesFilePath = "..//packages.config";

    private static readonly string PackOutputDirectory = Path.Combine(Application.dataPath, "../nupkgs");

    static NugetHelper()
    {
        // restore packages silently since this would be output EVERY time the project is loaded or a code-file changes
        Restore(false);
    }

    private static string RunNugetProcess(string arguments, bool logOuput = true)
    {
        Process process = new Process();
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.FileName = NugetPath + "//nuget.exe";
        process.StartInfo.Arguments = arguments;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.WorkingDirectory = NugetPath;
        process.StartInfo.StandardOutputEncoding = Encoding.UTF7; // Default = 65533, ASCII = ?, Unicode = nothing works at all, UTF-8 = 65533, UTF-7 = 242 = WORKS!
        process.Start();

        if (!process.WaitForExit(5000))
        {
            Debug.LogWarning("NuGet took too long to finish.  Killing operation.");
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

    public static List<NugetPackage> List(string search = "", bool showAllVersions = false, bool showPrerelease = false)
    {
        string arguments = string.Format("list {0} -verbosity detailed -configfile {1}", search, ConfigFilePath);
        if (showAllVersions)
            arguments += " -AllVersions";
        if (showPrerelease)
            arguments += " -Prerelease";

        string output = RunNugetProcess(arguments, false);

        List<NugetPackage> packages = new List<NugetPackage>();

        // parse the command line output to build a list of available packages
        string[] lines = output.Split('\n');
        //Debug.Log(lines.Count() + " lines");
        for (int i = 0; i < lines.Length - 4; i++)
        {
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

    public static void Restore(bool logOutput = true)
    {
        string arguments = string.Format("restore {0} -configfile {1}", PackagesFilePath, ConfigFilePath);

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

    public static void Install(NugetPackage package)
    {
        string arguments = string.Format("install {0} -Version {1} -configfile {2}", package.ID, package.Version, ConfigFilePath);

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

    public static void Pack(string nuspecFilePath)
    {
        if (!Directory.Exists(PackOutputDirectory))
        {
            Directory.CreateDirectory(PackOutputDirectory);
        }

        string arguments = string.Format("pack {0} -OutputDirectory {1}", nuspecFilePath, PackOutputDirectory);

        RunNugetProcess(arguments);
    }

    public static void Push(NuspecFile nuspec, string nuspecFilePath)
    {
        string packagePath = Path.Combine(PackOutputDirectory, string.Format("{0}.{1}.nupkg", nuspec.Id, nuspec.Version));
        if (!File.Exists(packagePath))
        {
            //Debug.LogErrorFormat("NuGet package not found: {0}", packagePath);
            //Debug.Log("Attempting to Pack.");
            Pack(nuspecFilePath);

            if (!File.Exists(packagePath))
            {
                Debug.LogErrorFormat("NuGet package not found: {0}", packagePath);
                return;
            }
        }

        string arguments = string.Format("push {0} -configfile {1}", packagePath, ConfigFilePath);

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

    public static void Update(NugetPackage currentVersion, NugetPackage newVersion)
    {
        Uninstall(currentVersion, false);
        Install(newVersion);
    }

    public static List<NugetPackage> LoadInstalledPackages()
    {
        List<NugetPackage> packages = new List<NugetPackage>();
        
        string packagesFilePath = Path.Combine(NugetPath, PackagesFilePath);

        XDocument packagesFile = XDocument.Load(packagesFilePath);
        foreach (var packageElement in packagesFile.Root.Elements())
        {
            NugetPackage package = new NugetPackage();
            package.ID = packageElement.Attribute("id").Value;
            package.Version = packageElement.Attribute("version").Value;
            packages.Add(package);
        }

        return packages;
    }

    private static void AddInstalledPackage(NugetPackage package)
    {
        List<NugetPackage> packages = LoadInstalledPackages();

        if (!packages.Contains(package))
        {
            packages.Add(package);
            SaveInstalledPackages(packages);
        }
    }

    private static void RemoveInstalledPackage(NugetPackage package)
    {
        List<NugetPackage> packages = LoadInstalledPackages();
        packages.Remove(package);
        SaveInstalledPackages(packages);
    }

    private static void SaveInstalledPackages(List<NugetPackage> packages)
    {
        string packagesFilePath = Path.Combine(NugetPath, PackagesFilePath);
        XDocument packagesFile = new XDocument();
        packagesFile.Add(new XElement("packages"));
        foreach (var package in packages)
        {
            XElement packageElement = new XElement("package");
            packageElement.Add(new XAttribute("id", package.ID));
            packageElement.Add(new XAttribute("version", package.Version));
            packagesFile.Root.Add(packageElement);
        }

        packagesFile.Save(packagesFilePath);
    }
}
