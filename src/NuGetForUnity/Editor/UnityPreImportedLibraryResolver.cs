using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;

namespace NugetForUnity
{
    /// <summary>
    ///     Helper to resolve what libraries are already known / imported by unity.
    /// </summary>
    internal static class UnityPreImportedLibraryResolver
    {
        private static HashSet<string> alreadyImportedLibs;

        /// <summary>
        ///     Gets all libraries that are already imported by unity so we shouldn't / don't need to install them as NuGet packages.
        /// </summary>
        /// <returns>A set of all names of libraries that are already imported by unity.</returns>
        internal static HashSet<string> GetAlreadyImportedLibs()
        {
            if (alreadyImportedLibs != null)
            {
                return alreadyImportedLibs;
            }

            // Find all the assemblies already installed by NuGetForUnity
            var alreadyInstalledDllFileNames = new HashSet<string>();

            if (NugetHelper.NugetConfigFile != null && Directory.Exists(NugetHelper.NugetConfigFile.RepositoryPath))
            {
                alreadyInstalledDllFileNames = new HashSet<string>(
                    Directory.EnumerateFiles(NugetHelper.NugetConfigFile.RepositoryPath, "*.dll", SearchOption.AllDirectories)
                        .Select(Path.GetFileNameWithoutExtension));
            }

            // Search the all project assemblies that are not from a package or a Unity assembly.
            // We only use player assemblies as we don't need to collect UnityEditor assemblies, we don't support installing NuGet packages with reference to UnityEditor.
#if UNITY_2019_3_OR_NEWER
            const AssembliesType assemblieType = AssembliesType.PlayerWithoutTestAssemblies;
#else
            const AssembliesType assemblieType = AssembliesType.Player;
#endif
            var projectAssemblies = CompilationPipeline.GetAssemblies(assemblieType)
                .Where(
                    playerAssembly => playerAssembly.sourceFiles.Length == 0 ||
                                      playerAssembly.sourceFiles.Any(
                                          sourceFilePath => sourceFilePath.StartsWith("Assets/") || sourceFilePath.StartsWith("Assets\\")));

            // Collect all referenced assemblies but exclude all assemblies installed by NuGetForUnity.
            var porojectReferences = projectAssemblies.SelectMany(playerAssembly => playerAssembly.allReferences);
            alreadyImportedLibs = new HashSet<string>(
                porojectReferences.Select(compiledAssemblyReference => Path.GetFileNameWithoutExtension(compiledAssemblyReference))
                    .Where(assemblyName => !alreadyInstalledDllFileNames.Contains(assemblyName)));

            if (PlayerSettings.GetApiCompatibilityLevel(EditorUserBuildSettings.selectedBuildTargetGroup) == ApiCompatibilityLevel.NET_Standard_2_0)
            {
                // mark NuGet packages that contain the .net standard references as already imported
                alreadyImportedLibs.Add("NETStandard.Library");
                alreadyImportedLibs.Add("Microsoft.NETCore.Platforms");
            }

            // the compiler / language is available by default
            alreadyImportedLibs.Add("Microsoft.CSharp");

            NugetHelper.LogVerbose("Already imported libs: {0}", string.Join(", ", alreadyImportedLibs));

            return alreadyImportedLibs;
        }
    }
}
