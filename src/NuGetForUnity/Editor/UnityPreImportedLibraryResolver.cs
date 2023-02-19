using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

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
            if (alreadyImportedLibs == null)
            {
                // Find all the dll's already installed by NuGetForUnity
                var alreadyInstalledDllFileNames = new HashSet<string>();

                if (NugetHelper.NugetConfigFile != null && Directory.Exists(NugetHelper.NugetConfigFile.RepositoryPath))
                {
                    alreadyInstalledDllFileNames = new HashSet<string>(
                        Directory.EnumerateFiles(NugetHelper.NugetConfigFile.RepositoryPath, "*.dll", SearchOption.AllDirectories)
                            .Select(Path.GetFileNameWithoutExtension));
                }

                // Get all assemblies loaded into Unity and filter out those installed by NuGetForUnity
                alreadyImportedLibs = new HashSet<string>(
                    AppDomain.CurrentDomain.GetAssemblies()
                        .Select(assembly => Path.GetFileNameWithoutExtension(assembly.ManifestModule.Name))
                        .Where(p => !alreadyInstalledDllFileNames.Contains(p)));

                if (PlayerSettings.GetApiCompatibilityLevel(EditorUserBuildSettings.selectedBuildTargetGroup) ==
                    ApiCompatibilityLevel.NET_Standard_2_0)
                {
                    alreadyImportedLibs.Add("NETStandard.Library");
                    alreadyImportedLibs.Add("Microsoft.NETCore.Platforms");
                }

                NugetHelper.LogVerbose("Already imported libs: {0}", string.Join(", ", alreadyImportedLibs));
            }

            return alreadyImportedLibs;
        }
    }
}
