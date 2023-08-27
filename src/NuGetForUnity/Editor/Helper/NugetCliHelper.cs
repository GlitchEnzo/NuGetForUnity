using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using NugetForUnity.Configuration;
using NugetForUnity.Models;
using Debug = UnityEngine.Debug;

namespace NugetForUnity.Helper
{
    /// <summary>
    ///     Helper class for calling the nuget.exe command line tool.
    /// </summary>
    internal static class NugetCliHelper
    {
        /// <summary>
        ///     The amount of time, in milliseconds, before the nuget.exe process times out and is killed.
        /// </summary>
        private const int TimeOut = 60000;

        /// <summary>
        ///     Calls "nuget.exe pack" to create a .nupkg file based on the given .nuspec file.
        /// </summary>
        /// <param name="nuspecFilePath">The full file-path to the .nuspec file to use.</param>
        public static void Pack(string nuspecFilePath)
        {
            Directory.CreateDirectory(PackageCacheManager.CacheOutputDirectory);

            // Use -NoDefaultExcludes to allow files and folders that start with a . to be packed into the package
            // This is done because if you want a file/folder in a Unity project, but you want Unity to ignore it, it must start with a .
            // This is especially useful for .cs and .js files that you don't want Unity to compile as game scripts
            var arguments = $"pack \"{nuspecFilePath}\" -OutputDirectory \"{PackageCacheManager.CacheOutputDirectory}\" -NoDefaultExcludes";

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
            var packagePath = nuspec.GetLocalPackageFilePath(PackageCacheManager.CacheOutputDirectory);
            if (!File.Exists(packagePath))
            {
                NugetLogger.LogVerbose("Attempting to Pack.");
                Pack(nuspecFilePath);

                if (!File.Exists(packagePath))
                {
                    Debug.LogErrorFormat("NuGet package not found: {0}", packagePath);
                    return;
                }
            }

            var arguments = $"push \"{packagePath}\" {apiKey} -configfile \"{ConfigurationManager.NugetConfigFilePath}\"";

            RunNugetProcess(arguments);
        }

        /// <summary>
        ///     Runs nuget.exe using the given arguments.
        /// </summary>
        /// <param name="arguments">The arguments to run nuget.exe with.</param>
        /// <param name="logOutput">True to output debug information to the Unity console.  Defaults to true.</param>
        private static void RunNugetProcess(string arguments, bool logOutput = true)
        {
            // Try to find any nuget.exe in the package tools installation location
            var toolsPackagesFolder = Path.Combine(UnityPathHelper.AbsoluteProjectPath, "Packages");

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
                NugetPackageInstaller.InstallIdentifier(new NugetPackageIdentifier("NuGet.CommandLine", null));
                files = Directory.GetFiles(toolsPackagesFolder, "nuget.exe", SearchOption.AllDirectories);
                if (files.Length < 1)
                {
                    Debug.LogErrorFormat("nuget.exe still not found. Quitting...");
                    return;
                }
            }

            NugetLogger.LogVerbose("Running: {0} \nArgs: {1}", files[0], arguments);

            string fileName;
            string commandLine;
            if (Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                // ATTENTION: you must install mono running on your mac, we use this mono to run `nuget.exe`
                fileName = "/Library/Frameworks/Mono.framework/Versions/Current/Commands/mono";
                commandLine = " " + files[0] + " " + arguments;
                NugetLogger.LogVerbose("command: " + commandLine);
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
                              }) ??
                          throw new InvalidOperationException("Failed to start process.");

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
            if (logOutput && !string.IsNullOrEmpty(output))
            {
                Debug.Log(output);
            }
        }
    }
}
