using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using JetBrains.Annotations;
using NugetForUnity.Configuration;
using NugetForUnity.Models;
using UnityEngine;
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

        private static string dotNetExecutablePath;

        /// <summary>
        ///     Calls "nuget.exe pack" to create a .nupkg file based on the given .nuspec file.
        /// </summary>
        /// <param name="nuspecFilePath">The full file-path to the .nuspec file to use.</param>
        public static void Pack([NotNull] string nuspecFilePath)
        {
            Directory.CreateDirectory(PackageCacheManager.CacheOutputDirectory);

            // Use -NoDefaultExcludes to allow files and folders that start with a . to be packed into the package
            // This is done because if you want a file/folder in a Unity project, but you want Unity to ignore it, it must start with a .
            // This is especially useful for .cs and .js files that you don't want Unity to compile as game scripts
            var arguments = $"pack \"{nuspecFilePath}\" -OutputDirectory \"{PackageCacheManager.CacheOutputDirectory}\" -NoDefaultExcludes";

            RunNugetProcess(arguments);
        }

        /// <summary>
        ///     Calls "nuget.exe push" to push a .nupkg file to the server location defined in the NuGet.config file.
        ///     Note: This differs slightly from NuGet's Push command by automatically calling Pack if the .nupkg doesn't already exist.
        /// </summary>
        /// <param name="nuspec">The NuspecFile which defines the package to push.  Only the ID and Version are used.</param>
        /// <param name="nuspecFilePath">The full file-path to the .nuspec file to use.  This is required by NuGet's Push command.</param>
        /// ///
        /// <param name="apiKey">The API key to use when pushing a package to the server.  This is optional.</param>
        public static void Push([NotNull] NuspecFile nuspec, [NotNull] string nuspecFilePath, [NotNull] string apiKey = "")
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
        ///     Creates a <see cref="ProcessStartInfo" /> configured to call a .Net '.exe' on the current operating system. This function ensures the correct
        ///     output encoding is used. On Unix systems (Linux / MacOS) it also uses the 'dotnet' / 'mono' executable to start the
        ///     <paramref name="executableFilePath" />.
        /// </summary>
        /// <param name="executableFilePath">The absolute file path to the .Net '.exe' file that should be started.</param>
        /// <param name="arguments">The arguments that should be passed to the executable <paramref name="executableFilePath" />.</param>
        /// <returns>The start info with the configuration.</returns>
        internal static ProcessStartInfo CreateStartInfoForDotNetExecutable(string executableFilePath, string arguments)
        {
            var outputEncoding = Encoding.UTF8;
            string executableToStart;
            string executableArguments;
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                // Windows -> just call the .exe but use a special encoding
                try
                {
                    // http://stackoverflow.com/questions/16803748/how-to-decode-cmd-output-correctly
                    // Default = 65533, ASCII = ?, Unicode = nothing works at all, UTF-8 = 65533, UTF-7 = 242 = WORKS!, UTF-32 = nothing works at all
                    outputEncoding = Encoding.GetEncoding(850);
                }
                catch (Exception e) when (e is ArgumentException || e is NotSupportedException)
                {
                    Debug.LogWarningFormat("Failed to get console output encoding 850 -> using UTF8 instead. Error: {0}", e);
                }

                executableToStart = executableFilePath;
                executableArguments = arguments;
            }
            else
            {
                // Linux or MacOS -> we need 'mono' / 'dotnet' to run the executable
                if (dotNetExecutablePath == null)
                {
                    dotNetExecutablePath = GetDotnetExecutablePath(outputEncoding);
                }

                executableToStart = dotNetExecutablePath;
                executableArguments = $" {executableFilePath} {arguments}";
                NugetLogger.LogVerbose("Changed command to call executable '{0}' with arguments '{1}'.", executableToStart, executableArguments);
            }

            return new ProcessStartInfo(executableToStart, executableArguments)
            {
                StandardOutputEncoding = outputEncoding, StandardErrorEncoding = outputEncoding,
            };
        }

        /// <summary>
        ///     Runs nuget.exe using the given arguments.
        /// </summary>
        /// <param name="arguments">The arguments to run nuget.exe with.</param>
        /// <param name="logOutput">True to output debug information to the Unity console.  Defaults to true.</param>
        private static void RunNugetProcess([NotNull] string arguments, bool logOutput = true)
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
                Debug.LogWarningFormat("No nuget.exe found! Attempting to install the latest version of the NuGet.CommandLine package.");
                NugetPackageInstaller.InstallIdentifier(new NugetPackageIdentifier("NuGet.CommandLine", null));
                files = Directory.GetFiles(toolsPackagesFolder, "nuget.exe", SearchOption.AllDirectories);
                if (files.Length < 1)
                {
                    Debug.LogErrorFormat("nuget.exe still not found. Quitting...");
                    return;
                }
            }

            NugetLogger.LogVerbose("Running: {0} \nArgs: {1}", files[0], arguments);

            var startInfo = CreateStartInfoForDotNetExecutable(files[0], arguments);
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            using (var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start process."))
            {
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

        private static string GetDotnetExecutablePath(Encoding outputEncoding)
        {
            // search for full path to 'dotnet' or 'mono' executable
            foreach (var commandLineToolName in new[] { "dotnet", "mono" })
            {
                try
                {
                    using (var process = Process.Start(
                                             new ProcessStartInfo("which", commandLineToolName)
                                             {
                                                 RedirectStandardOutput = true, StandardOutputEncoding = outputEncoding, UseShellExecute = false,
                                             }) ??
                                         throw new InvalidOperationException("Failed to start process."))
                    {
                        process.WaitForExit();

                        // which either returns nothing (not found) or the full path to the executable.
                        var commandPath = process.StandardOutput.ReadLine();
                        if (!string.IsNullOrEmpty(commandPath))
                        {
                            return commandPath;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarningFormat(
                        "Failed to get path of command {0} using 'which' command -> using '{0}' as a command path instead. Error: {1}",
                        commandLineToolName,
                        e);
                    return commandLineToolName;
                }
            }

            Debug.LogWarning(
                "Can't find 'dotnet' or 'mono' executable. Is it installed on your system? It is needed to run dotnet .exe files. Try to use 'dotnet' as a command even if 'which dotnet' didn't find it.");
            return "dotnet";
        }
    }
}
