using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using NugetForUnity.Configuration;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace NugetForUnity
{
    /// <summary>
    ///     Helper to resolve what libraries are already known / imported by unity.
    /// </summary>
    internal static class UnityPreImportedLibraryResolver
    {
        [CanBeNull]
        private static HashSet<string> alreadyImportedLibs;

        [CanBeNull]
        private static HashSet<string> alreadyImportedEditorOnlyLibraries;

        /// <summary>
        ///     Gets all libraries that are already imported by Unity Editor so we shouldn't / don't need to install them as NuGet packages.
        /// </summary>
        /// <returns>The list of packages.</returns>
        [NotNull]
        internal static HashSet<string> GetAlreadyImportedEditorOnlyLibraries()
        {
            if (alreadyImportedEditorOnlyLibraries == null)
            {
                GetAlreadyImportedLibs();
            }

            Debug.Assert(alreadyImportedEditorOnlyLibraries != null, nameof(alreadyImportedEditorOnlyLibraries) + " != null");
            return alreadyImportedEditorOnlyLibraries;
        }

        /// <summary>
        ///     Check if a package is already imported in the Unity project e.g. is a part of Unity.
        /// </summary>
        /// <param name="packageId">The package identifier witch is checked.</param>
        /// <param name="log">Whether to log a message with the result of the check.</param>
        /// <returns>If it is included in Unity.</returns>
        internal static bool IsAlreadyImportedInEngine([NotNull] string packageId, bool log = true)
        {
            var alreadyImported = GetAlreadyImportedLibs();
            var isAlreadyImported = alreadyImported.Contains(packageId);
            if (log)
            {
                NugetLogger.LogVerbose("Is package '{0}' already imported? {1}", packageId, isAlreadyImported);
            }

            return isAlreadyImported;
        }

        /// <summary>
        ///     Gets all libraries that are already imported by unity so we shouldn't / don't need to install them as NuGet packages.
        /// </summary>
        /// <returns>A set of all names of libraries that are already imported by unity.</returns>
        [NotNull]
        private static HashSet<string> GetAlreadyImportedLibs()
        {
            if (alreadyImportedLibs != null)
            {
                return alreadyImportedLibs;
            }

            // Find all the assemblies already installed by NuGetForUnity
            var alreadyInstalledDllFileNames = new HashSet<string>();
            var repositoryPath = ConfigurationManager.NugetConfigFile.RepositoryPath;
            if (Directory.Exists(repositoryPath))
            {
                alreadyInstalledDllFileNames = new HashSet<string>(
                    Directory.EnumerateFiles(repositoryPath, "*.dll", SearchOption.AllDirectories).Select(Path.GetFileNameWithoutExtension));
            }

            // Search the all project assemblies that are not editor only.
            // We only use player assemblies as we don't need to collect UnityEditor assemblies, we don't support installing NuGet packages with reference to UnityEditor.
#if UNITY_2019_3_OR_NEWER
            const AssembliesType assemblyType = AssembliesType.PlayerWithoutTestAssemblies;
#else
            const AssembliesType assemblyType = AssembliesType.Player;
#endif
            var projectAssemblies = CompilationPipeline.GetAssemblies(assemblyType)
                .Where(playerAssembly => playerAssembly.flags != AssemblyFlags.EditorAssembly);

            // Collect all referenced assemblies but exclude all assemblies installed by NuGetForUnity.
            var projectReferences = projectAssemblies.SelectMany(playerAssembly => playerAssembly.allReferences);
            alreadyImportedLibs = new HashSet<string>(
                projectReferences.Select(Path.GetFileNameWithoutExtension)
                    .Where(assemblyName => !alreadyInstalledDllFileNames.Contains(assemblyName)));

            if (TargetFrameworkResolver.CurrentBuildTargetApiCompatibilityLevel.Value == ApiCompatibilityLevel.NET_Standard_2_0)
            {
                // mark NuGet packages that contain the .net standard references as already imported
                alreadyImportedLibs.Add("NETStandard.Library");
                alreadyImportedLibs.Add("Microsoft.NETCore.Platforms");
            }

            // the compiler / language is available by default
            alreadyImportedLibs.Add("Microsoft.CSharp");

            var editorOnlyAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor)
                .Where(assembly => assembly.flags == AssemblyFlags.EditorAssembly)
                .ToList();
            alreadyImportedEditorOnlyLibraries = new HashSet<string>();

            // com.unity.visualscripting uses .net 4.8 so it implicitly has System.CodeDom
            if (!alreadyImportedLibs.Contains("System.CodeDom") &&
                editorOnlyAssemblies.Any(editorOnlyAssembly => editorOnlyAssembly.name == "Unity.VisualScripting.Shared.Editor"))
            {
                alreadyImportedEditorOnlyLibraries.Add("System.CodeDom");
            }

            NugetLogger.LogVerbose("Already imported libs: {0}", string.Join(", ", alreadyImportedLibs));
            NugetLogger.LogVerbose("Already imported editor only libraries: {0}", string.Join(", ", alreadyImportedEditorOnlyLibraries));

            return alreadyImportedLibs;
        }
    }
}
