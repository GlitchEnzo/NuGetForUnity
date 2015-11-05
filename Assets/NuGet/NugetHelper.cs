using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Debug = UnityEngine.Debug;

/// <summary>
/// A set of helper methods that act as a wrapper around nuget.exe
/// </summary>
public class NugetHelper 
{
    private static readonly string NugetPath = UnityEngine.Application.dataPath + "//NuGet";

    private const string ConfigFilePath = "NuGet.config";

    private const string PackagesFilePath = "..//packages.config";

    public static List<NugetPackage> List(string search = "")
    {
        Process process = new Process();
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.FileName = NugetPath + "//nuget.exe";
        process.StartInfo.Arguments = string.Format("list {0} -verbosity detailed -configfile {1}", search, ConfigFilePath);
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.WorkingDirectory = NugetPath;
        process.Start();
        process.WaitForExit();

        List<NugetPackage> packages = new List<NugetPackage>();

        string error = process.StandardError.ReadToEnd();
        if (error != string.Empty)
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
                package.LicenseURL = lines[i++].Replace(" License url: ", string.Empty);

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
        process.StartInfo.Arguments = string.Format("restore {0} -configfile {1}", PackagesFilePath, ConfigFilePath);
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.WorkingDirectory = NugetPath;
        process.Start();
        process.WaitForExit();

        string error = process.StandardError.ReadToEnd();
        if (error != string.Empty)
        {
            Debug.LogError(error);
        }
        else
        {
            string output = process.StandardOutput.ReadToEnd();
            Debug.Log(output);
        }
    }

    public static void Install(string packageIdOrConfig)
    {
        Process process = new Process();
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.FileName = NugetPath + "//nuget.exe";
        process.StartInfo.Arguments = string.Format("install {0} -configfile {1}", packageIdOrConfig, ConfigFilePath);
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.WorkingDirectory = NugetPath;
        process.Start();
        process.WaitForExit();

        string error = process.StandardError.ReadToEnd();
        if (error != string.Empty)
        {
            Debug.LogError(error);
        }
        else
        {
            string output = process.StandardOutput.ReadToEnd();
            Debug.Log(output);

            // TODO: Update the packages.config file
        }
    }
}
