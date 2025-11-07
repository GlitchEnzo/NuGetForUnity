#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NugetForUnity;
using NugetForUnity.Configuration;
using NugetForUnity.Helper;
using UnityEngine;

namespace NuGetForUnity.Cli
{
    /// <summary>
    ///     Simple command line interface to restore NuGet packages for Unity projects.
    /// </summary>
    public static class Program
    {
        private static readonly string[] HelpOptions = { "-?", "-h", "--help" };

        private static string DefaultProjectPath => Directory.GetCurrentDirectory();

        /// <summary>
        ///     Starting point of the application.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>The application exit code.</returns>
        public static int Main(string[] args)
        {
            var availableArguments = args.ToList();

            if (args.Length < 1 || !args[0].Equals("restore", StringComparison.OrdinalIgnoreCase) || args.Any(IsHelpOption))
            {
                return PrintUsage();
            }

            // remove restore
            availableArguments.RemoveAt(0);

            string projectPath;
            if (availableArguments.Count != 0)
            {
                projectPath = availableArguments[0].Replace("'", string.Empty).Replace("\"", string.Empty);
                availableArguments.RemoveAt(0);
            }
            else
            {
                projectPath = DefaultProjectPath;
            }

            if (availableArguments.Count > 0)
            {
                foreach (var unknownArgument in availableArguments)
                {
                    Console.Error.WriteLine($"Unrecognized command or argument '{unknownArgument}'");
                }

                return PrintUsage();
            }

            Application.SetUnityProjectPath(projectPath);

            // need to disable dependency installation as UnityPreImportedLibraryResolver.GetAlreadyImportedLibs is not working outside Unity.
            PackageRestorer.Restore(true);
            FixRoslynAnalyzerImportSettings();
            return Debug.HasError ? 1 : 0;
        }

        /// <summary>
        ///     In Unity the errors that RoslynAnalyzer DLL's can't be imported because it has multiple files with the same name are blocking
        ///     even before our AssetPostprocessor can change the import settings so Unity knows that the DLL is a RoslynAnalyzer.
        ///     To bypass this error we generate the .dll.meta files with the RoslynAnalyzer label and the disable for platform configuration.
        ///     A alternative could be to delete the duplicate .resources.dll files when restoring,
        ///     but this would require to decide which user language to keep.
        /// </summary>
        private static void FixRoslynAnalyzerImportSettings()
        {
            if (!Directory.Exists(ConfigurationManager.NugetConfigFile.RepositoryPath))
            {
                return;
            }

            UTF8Encoding? utf8NoBom = null;
            foreach (var packageDirectoryPath in Directory.EnumerateDirectories(ConfigurationManager.NugetConfigFile.RepositoryPath))
            {
                var analyzersDirectoryPath = Path.Combine(packageDirectoryPath, "analyzers");
                if (!Directory.Exists(analyzersDirectoryPath))
                {
                    continue;
                }

                foreach (var analyzerDllPath in Directory.EnumerateFiles(analyzersDirectoryPath, "*.dll", SearchOption.AllDirectories))
                {
                    var analyzerDllMetaPath = $"{analyzerDllPath}.meta";
                    if (File.Exists(analyzerDllMetaPath))
                    {
                        continue;
                    }

                    var isSupportedRoslynAnalyzer = AnalyzerHelper.ShouldEnableRoslynAnalyzer(analyzerDllPath);
                    var labelsForAsset = isSupportedRoslynAnalyzer ?
                        """
                        labels:
                        - RoslynAnalyzer
                        """ :
                        "labels: []";
                    utf8NoBom ??= new UTF8Encoding(false);
                    File.WriteAllText(
                        analyzerDllMetaPath,
                        $"""
                         fileFormatVersion: 2
                         guid: {Guid.NewGuid():N}
                         {labelsForAsset}
                         PluginImporter:
                           serializedVersion: 2
                           platformData:
                           - first:
                               : Any
                             second:
                               enabled: 0
                           - first:
                               Any:
                             second:
                               enabled: 0
                         """,
                        utf8NoBom);
                }
            }
        }

        private static bool IsHelpOption(string argument)
        {
            return HelpOptions.Any(helpOption => argument.Equals(helpOption, StringComparison.OrdinalIgnoreCase));
        }

        private static int PrintUsage()
        {
            var description = typeof(Program).Assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? string.Empty;

            // create single line string from multi line string
            description = string.Join(
                ' ',
                description.Split(new[] { "\n", "\r\n" }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
            Console.WriteLine(
                $"""
                 Description:
                     {description}

                 Usage:
                     nugetforunity restore <PROJECT_PATH> [options]

                 Arguments:
                     <PROJECT_PATH>  The path to the Unity project, should be the root path where e.g. the 'ProjectSettings' folder is located. If not specified, the command will use the current directory. [default: {DefaultProjectPath}]

                 Options:
                     -?, -h, --help  Show command line help.
                 """);
            return 1;
        }
    }
}
