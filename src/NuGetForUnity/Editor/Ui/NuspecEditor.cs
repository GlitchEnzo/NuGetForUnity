using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using NugetForUnity.Helper;
using NugetForUnity.Models;
using UnityEditor;
using UnityEngine;

namespace NugetForUnity.Ui
{
    /// <summary>
    ///     Represents a custom editor inside the Unity editor that allows easy editing of a .nuspec file.
    /// </summary>
    public class NuspecEditor : EditorWindow
    {
        /// <summary>
        ///     The API key used to verify an acceptable package being pushed to the server.
        /// </summary>
        private string apiKey = string.Empty;

        /// <summary>
        ///     True if the dependencies list is expanded in the GUI.  False if it is collapsed.
        /// </summary>
        private bool dependenciesExpanded = true;

        /// <summary>
        ///     The full file-path to the .nuspec file that is being edited.
        /// </summary>
        [CanBeNull]
        private string filepath;

        /// <summary>
        ///     The NuspecFile that was loaded from the .nuspec file.
        /// </summary>
        [CanBeNull]
        private NuspecFile nuspec;

        /// <summary>
        ///     Creates a nuspec file at the given path and opens its editor.
        /// </summary>
        /// <param name="filePath">Absolute path to the folder within project where nuspec should be created.</param>
        public static void CreateNuspecFile(string filePath)
        {
            if (!string.IsNullOrEmpty(Path.GetExtension(filePath)))
            {
                // if it was a file that was selected, use containing directory
                filePath = Path.GetDirectoryName(filePath);
            }

            Debug.Assert(filePath != null, "filepath != null");
            var packageName = Path.GetFileName(filePath);
            filePath = Path.Combine(filePath, $"{packageName}.nuspec");

            Debug.LogFormat("Creating: {0}", filePath);

            var file = NuspecFile.CreateDefault(packageName);
            file.Save(filePath);

            AssetDatabase.Refresh();

            // select the newly created .nuspec file
            var relativeNuspecFilePath = filePath.Substring(UnityPathHelper.AbsoluteProjectPath.Length + 1);
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(relativeNuspecFilePath);

            // automatically display the editor with the newly created .nuspec file
            DisplayNuspecEditor();
        }

        /// <summary>
        ///     Creates a new MyPackage.nuspec file.
        /// </summary>
        [MenuItem("Assets/NuGet/Create Nuspec File", false, 2000)]
        protected static void CreateNuspecFile()
        {
            var filePath = Application.dataPath;

            if (Selection.activeObject != null && Selection.activeObject != Selection.activeGameObject)
            {
                var selectedFile = AssetDatabase.GetAssetPath(Selection.activeObject);
                filePath = Path.Combine(UnityPathHelper.AbsoluteProjectPath, selectedFile);
            }

            CreateNuspecFile(filePath);
        }

        /// <summary>
        ///     Opens the .nuspec file editor.
        /// </summary>
        [MenuItem("Assets/NuGet/Open Nuspec Editor", false, 2000)]
        protected static void DisplayNuspecEditor()
        {
            var nuspecEditor = GetWindow<NuspecEditor>();
            nuspecEditor.Reload();
        }

        /// <summary>
        ///     Validates the opening of the .nuspec file editor.
        /// </summary>
        /// <returns>True if the editor can be opened, false otherwise.</returns>
        [MenuItem("Assets/NuGet/Open Nuspec Editor", true, 2000)]
        protected static bool DisplayNuspecEditorValidation()
        {
            var isNuspec = false;

            var defaultAsset = Selection.activeObject as DefaultAsset;
            if (defaultAsset != null)
            {
                var filePath = AssetDatabase.GetAssetPath(defaultAsset);
                filePath = Path.Combine(UnityPathHelper.AbsoluteProjectPath, filePath);

                isNuspec = string.Equals(Path.GetExtension(filePath), ".nuspec", StringComparison.OrdinalIgnoreCase);
            }

            return isNuspec;
        }

        /// <summary>
        ///     Use the Unity GUI to draw the controls.
        /// </summary>
        protected void OnGUI()
        {
            if (nuspec == null)
            {
                Reload();
            }

            if (nuspec == null)
            {
                titleContent = new GUIContent("[NO NUSPEC]");
                EditorGUILayout.LabelField("There is no .nuspec file selected.");
            }
            else
            {
                if (filepath == null)
                {
                    titleContent = new GUIContent("[NO PATH]");
                    EditorGUILayout.LabelField("There is no directory selected.");
                    return;
                }

                EditorGUIUtility.labelWidth = 100;
                nuspec.Id = EditorGUILayout.TextField(new GUIContent("ID", "The id of the package."), nuspec.Id);
                nuspec.Title = EditorGUILayout.TextField(new GUIContent("Title", "The name of the package."), nuspec.Title);
                nuspec.Version = EditorGUILayout.TextField(new GUIContent("Version", "The semantic version of the package."), nuspec.Version);
                nuspec.Authors = EditorGUILayout.TextField(new GUIContent("Authors", "The authors of the package."), nuspec.Authors);
                nuspec.Owners = EditorGUILayout.TextField(new GUIContent("Owners", "The owners of the package."), nuspec.Owners);
                nuspec.LicenseUrl = EditorGUILayout.TextField(
                    new GUIContent("License URL", "The URL for the license of the package."),
                    nuspec.LicenseUrl);
                nuspec.ProjectUrl = EditorGUILayout.TextField(new GUIContent("Project URL", "The URL of the package project."), nuspec.ProjectUrl);
                nuspec.IconUrl = EditorGUILayout.TextField(new GUIContent("Icon URL", "The URL for the icon of the package."), nuspec.IconUrl);
                nuspec.RequireLicenseAcceptance = EditorGUILayout.Toggle(
                    new GUIContent("Require License Acceptance", "Does the package license need to be accepted before use?"),
                    nuspec.RequireLicenseAcceptance);
                nuspec.Description = EditorGUILayout.TextField(new GUIContent("Description", "The description of the package."), nuspec.Description);
                nuspec.Summary = EditorGUILayout.TextField(new GUIContent("Summary", "The brief description of the package."), nuspec.Summary);
                nuspec.ReleaseNotes = EditorGUILayout.TextField(
                    new GUIContent("Release Notes", "The release notes for this specific version of the package."),
                    nuspec.ReleaseNotes);
                nuspec.Copyright = EditorGUILayout.TextField(new GUIContent("Copyright", "The copyright details for the package."), nuspec.Copyright);
                nuspec.Tags = EditorGUILayout.TextField(
                    new GUIContent(
                        "Tags",
                        "The space-delimited list of tags and keywords that describe the package and aid discoverability of packages through search and filtering."),
                    nuspec.Tags);

                dependenciesExpanded = EditorGUILayout.Foldout(
                    dependenciesExpanded,
                    new GUIContent("Dependencies", "The list of NuGet packages that this packages depends on."));

                if (dependenciesExpanded)
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        GUILayout.Space(50);

                        // automatically fill in the dependencies based upon the "root" packages currently installed in the project
                        if (GUILayout.Button(
                                new GUIContent(
                                    "Automatically Fill Dependencies",
                                    "Populates the list of dependencies with the \"root\" NuGet packages currently installed in the project.")))
                        {
                            var installedPackages = InstalledPackagesManager.InstalledPackages.ToList();

                            // default all packages to being roots
                            var roots = new List<INugetPackageIdentifier>(installedPackages);

                            // remove a package as a root if another package is dependent on it
                            foreach (var package in installedPackages)
                            {
                                var frameworkDependencies = package.CurrentFrameworkDependencies;
                                foreach (var dependency in frameworkDependencies)
                                {
                                    roots.RemoveAll(p => p.Id == dependency.Id);
                                }
                            }

                            // remove all existing dependencies from the .nuspec
                            nuspec.Dependencies.Clear();

                            nuspec.Dependencies.Add(new NugetFrameworkGroup());
                            nuspec.Dependencies[0].Dependencies = roots;
                        }
                    }

                    EditorGUILayout.EndHorizontal();

                    // display the dependencies
                    NugetPackageIdentifier toDelete = null;
                    var nuspecFrameworkGroup = TargetFrameworkResolver.GetBestDependencyFrameworkGroupForCurrentSettings(nuspec, null);
                    foreach (var dependency in nuspecFrameworkGroup.Dependencies.Cast<NugetPackageIdentifier>())
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(75);
                        var prevLabelWidth = EditorGUIUtility.labelWidth;
                        EditorGUIUtility.labelWidth = 50;
                        dependency.Id = EditorGUILayout.TextField(new GUIContent("ID", "The ID of the dependency package."), dependency.Id);
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(75);
                        dependency.Version = EditorGUILayout.TextField(
                            new GUIContent("Version", "The version number of the dependency package. (specify ranges with =><)"),
                            dependency.Version);
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.BeginHorizontal();
                        {
                            GUILayout.Space(75);

                            if (GUILayout.Button("Remove " + dependency.Id))
                            {
                                toDelete = dependency;
                            }
                        }

                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.Separator();

                        EditorGUIUtility.labelWidth = prevLabelWidth;
                    }

                    if (toDelete != null)
                    {
                        nuspecFrameworkGroup.Dependencies.Remove(toDelete);
                    }

                    EditorGUILayout.BeginHorizontal();
                    {
                        GUILayout.Space(50);

                        if (GUILayout.Button("Add Dependency"))
                        {
                            nuspecFrameworkGroup.Dependencies.Add(new NugetPackageIdentifier());
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Separator();

                if (GUILayout.Button($"Save {Path.GetFileName(filepath)}"))
                {
                    nuspec.Save(filepath);
                }

                EditorGUILayout.Separator();

                if (GUILayout.Button($"Pack {Path.GetFileNameWithoutExtension(filepath)}.nupkg"))
                {
                    NugetCliHelper.Pack(filepath);
                }

                EditorGUILayout.Separator();

                apiKey = EditorGUILayout.TextField(new GUIContent("API Key", "The API key to use when pushing the package to the server"), apiKey);

                if (GUILayout.Button("Push to Server"))
                {
                    NugetCliHelper.Push(nuspec, filepath, apiKey);
                }
            }
        }

        /// <summary>
        ///     Reload the currently selected asset as a .nuspec file.
        /// </summary>
        private void Reload()
        {
            var defaultAsset = Selection.activeObject as DefaultAsset;
            if (defaultAsset == null)
            {
                return;
            }

            var assetFilepath = AssetDatabase.GetAssetPath(defaultAsset);
            assetFilepath = Path.Combine(UnityPathHelper.AbsoluteProjectPath, assetFilepath);

            var isNuspec = Path.GetExtension(assetFilepath) == ".nuspec";
            var alreadyLoaded = nuspec != null && filepath == assetFilepath;

            if (!isNuspec || alreadyLoaded)
            {
                return;
            }

            filepath = assetFilepath;
            nuspec = NuspecFile.Load(filepath);
            titleContent = new GUIContent(Path.GetFileNameWithoutExtension(filepath));

            // force a repaint
            Repaint();
        }

        /// <summary>
        ///     Called when enabling the window.
        /// </summary>
        private void OnFocus()
        {
            Reload();
        }

        /// <summary>
        ///     Reloads the .nuspec file when the selection changes.
        /// </summary>
        private void OnSelectionChange()
        {
            Reload();
        }
    }
}
