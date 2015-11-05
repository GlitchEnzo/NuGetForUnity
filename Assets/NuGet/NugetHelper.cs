using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// A set of helper methods that act as a wrapper around nuget.exe
/// </summary>
public class NugetHelper 
{
    private static readonly string NugetPath = Application.dataPath + "//NuGet";

    private const string ConfigFilePath = "NuGet.config";

    private const string PackagesFilePath = "..//packages.config";

    public static List<NugetPackage> List(string search = "", bool showAllVersions = false, bool showPrerelease = false)
    {
        Process process = new Process();
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.FileName = NugetPath + "//nuget.exe";
        process.StartInfo.Arguments = String.Format("list {0} -verbosity detailed -configfile {1}", search, ConfigFilePath);
        if (showAllVersions)
            process.StartInfo.Arguments += " -AllVersions";
        if (showPrerelease)
            process.StartInfo.Arguments += " -Prerelease";
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.WorkingDirectory = NugetPath;
        process.Start();
        process.WaitForExit();

        List<NugetPackage> packages = new List<NugetPackage>();

        string error = process.StandardError.ReadToEnd();
        if (error != String.Empty)
        {
            Debug.LogError(error);
        }
        else
        {
            string output = process.StandardOutput.ReadToEnd();
            //Debug.Log(output);

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
        }

        return packages;
    }

    public static void Restore()
    {
        Process process = new Process();
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.FileName = NugetPath + "//nuget.exe";
        process.StartInfo.Arguments = String.Format("restore {0} -configfile {1}", PackagesFilePath, ConfigFilePath);
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.WorkingDirectory = NugetPath;
        process.Start();
        process.WaitForExit();

        string error = process.StandardError.ReadToEnd();
        if (error != String.Empty)
        {
            Debug.LogError(error);
        }
        else
        {
            string output = process.StandardOutput.ReadToEnd();
            if (!string.IsNullOrEmpty(output))
                Debug.Log(output);
        }

        AssetDatabase.Refresh();
    }

    public static void Install(NugetPackage package)
    {
        Process process = new Process();
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.FileName = NugetPath + "//nuget.exe";
        process.StartInfo.Arguments = String.Format("install {0} -Version {1} -configfile {2}", package.ID, package.Version, ConfigFilePath);
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.WorkingDirectory = NugetPath;
        process.Start();
        process.WaitForExit();

        string error = process.StandardError.ReadToEnd();
        if (error != String.Empty)
        {
            Debug.LogError(error);
        }
        else
        {
            string output = process.StandardOutput.ReadToEnd();
            Debug.Log(output);

            // Update the packages.config file
            AddInstalledPackage(package);
        }

        AssetDatabase.Refresh();
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

    public static void Uninstall(NugetPackage package)
    {
        // TODO: Get the install directory from the NuGet.config file
        string packageInstallDirectory = Application.dataPath + "//Packages";
        packageInstallDirectory += "//" + package.ID + "." + package.Version;
        DeleteDirectory(packageInstallDirectory);

        RemoveInstalledPackage(package);

        AssetDatabase.Refresh();
    }

    public static void Update(NugetPackage currentVersion, NugetPackage newVersion)
    {
        Uninstall(currentVersion);
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
            //Debug.LogFormat("Installed: {0}", package.ID);
            packages.Add(package);
        }

        return packages;
    }

    private static void AddInstalledPackage(NugetPackage package)
    {
        List<NugetPackage> packages = LoadInstalledPackages();
        packages.Add(package);
        SaveInstalledPackages(packages);
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
