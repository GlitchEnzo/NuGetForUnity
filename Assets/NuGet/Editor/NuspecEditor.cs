namespace NugetForUnity
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Represents a custom editor inside the Unity editor that allows easy editting of a .nuspec file.
    /// </summary>
    [CustomEditor(typeof(DefaultAsset))]
    public class NuspecEditor : Editor
    {
        /// <summary>
        /// True if the selected file is a .nuspec file.
        /// </summary>
        private bool isNuspec;

        /// <summary>
        /// The full filepath to the .nuspec file that is being edited.
        /// </summary>
        private string filepath;

        /// <summary>
        /// The NuspecFile that was loaded from the .nuspec file.
        /// </summary>
        private NuspecFile nuspec;

        /// <summary>
        /// True if the dependencies list is expanded in the GUI.  False if it is collapsed.
        /// </summary>
        private bool dependenciesExpanded = true;

        /// <summary>
        /// The API key used to verify an acceptable package being pushed to the server.
        /// </summary>
        private string apiKey = string.Empty;

        /// <summary>
        /// Automatically called by Unity when the Inspector is first opened (when a .nuspec file is clicked on in the Project view).
        /// </summary>
        public void OnEnable()
        {
            filepath = AssetDatabase.GetAssetPath(target);
            string dataPath = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);
            filepath = Path.Combine(dataPath, filepath);

            isNuspec = Path.GetExtension(filepath) == ".nuspec";
            if (isNuspec)
            {
                nuspec = NuspecFile.Load(filepath);
            }
        }

        /// <summary>
        /// Use the Header GUI to draw the controls since the Inspector GUI method call is disabled by Unity.
        /// </summary>
        protected override void OnHeaderGUI()
        {
            // draw the normal header
            base.OnHeaderGUI();

            if (isNuspec)
            {
                nuspec.Id = EditorGUILayout.TextField(new GUIContent("ID", "The name of the package."), nuspec.Id);
                nuspec.Version = EditorGUILayout.TextField(new GUIContent("Version", "The semantic version of the package."), nuspec.Version);
                nuspec.Authors = EditorGUILayout.TextField(new GUIContent("Authors", "The authors of the package."), nuspec.Authors);
                nuspec.Owners = EditorGUILayout.TextField(new GUIContent("Owners", "The owners of the package."), nuspec.Owners);
                nuspec.LicenseUrl = EditorGUILayout.TextField(new GUIContent("License URL", "The URL for the license of the package."), nuspec.LicenseUrl);
                nuspec.ProjectUrl = EditorGUILayout.TextField(new GUIContent("Project URL", "The URL of the package project."), nuspec.ProjectUrl);
                nuspec.IconUrl = EditorGUILayout.TextField(new GUIContent("Icon URL", "The URL for the icon of the package."), nuspec.IconUrl);
                nuspec.RequireLicenseAcceptance = EditorGUILayout.Toggle(new GUIContent("Require License Acceptance", "Does the package license need to be accepted before use?"), nuspec.RequireLicenseAcceptance);
                nuspec.Description = EditorGUILayout.TextField(new GUIContent("Description", "The description of the package."), nuspec.Description);
                nuspec.ReleaseNotes = EditorGUILayout.TextField(new GUIContent("Release Notes", "The release notes for this specific version of the package."), nuspec.ReleaseNotes);
                nuspec.Copyright = EditorGUILayout.TextField(new GUIContent("Copyright", "The copyright of the package."), nuspec.Copyright);
                nuspec.Tags = EditorGUILayout.TextField(new GUIContent("Tags", "The tags of the package."), nuspec.Tags);

                dependenciesExpanded = EditorGUILayout.Foldout(dependenciesExpanded, "Dependencies");

                if (dependenciesExpanded)
                {
                    EditorGUI.indentLevel++;

                    // automatically fill in the dependencies based upon the "root" packages currently installed in the project
                    if (GUILayout.Button("Automatically Fill Dependencies"))
                    {
                        List<NugetPackage> installedPackages = NugetHelper.GetInstalledPackages();

                        // default all packages to being roots
                        List<NugetPackage> roots = new List<NugetPackage>(installedPackages);

                        // remove a package as a root if another package is dependent on it
                        foreach (NugetPackage package in installedPackages)
                        {
                            foreach (NugetPackageIdentifier dependency in package.Dependencies)
                            {
                                roots.RemoveAll(p => p.Id == dependency.Id);
                            }
                        }

                        // remove all existing dependencies from the .nuspec
                        nuspec.Dependencies.Clear();

                        nuspec.Dependencies = roots.Cast<NugetPackageIdentifier>().ToList();
                    }

                    // display the dependencies
                    NugetPackageIdentifier toDelete = null;
                    foreach (var dependency in nuspec.Dependencies)
                    {
                        dependency.Id = EditorGUILayout.TextField(new GUIContent("ID", "The ID of the dependency package."), dependency.Id);

                        //int oldSeletedIndex = IndexOf(ref existingComponents, dependency.Id);
                        //int newSelectIndex = EditorGUILayout.Popup("Name", oldSeletedIndex, existingComponents);
                        //if (oldSeletedIndex != newSelectIndex)
                        //{
                        //    dependency.Name = existingComponents[newSelectIndex];
                        //}

                        dependency.Version = EditorGUILayout.TextField(new GUIContent("Version", "The version number of the dependency package. (specify ranges with =><)"), dependency.Version);

                        if (GUILayout.Button("Remove " + dependency.Id))
                        {
                            toDelete = dependency;
                        }

                        EditorGUILayout.Separator();
                    }

                    if (toDelete != null)
                    {
                        nuspec.Dependencies.Remove(toDelete);
                    }

                    if (GUILayout.Button("Add Dependency"))
                    {
                        nuspec.Dependencies.Add(new NugetPackageIdentifier());
                    }

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Separator();

                if (GUILayout.Button(string.Format("Save {0}", Path.GetFileName(filepath))))
                {
                    nuspec.Save(filepath);
                }

                EditorGUILayout.Separator();

                if (GUILayout.Button(string.Format("Pack {0}.nupkg", Path.GetFileNameWithoutExtension(filepath))))
                {
                    NugetHelper.Pack(filepath);
                }

                EditorGUILayout.Separator();

                apiKey = EditorGUILayout.TextField(new GUIContent("API Key", "The API key to use when pushing the package to the server"), apiKey);

                if (GUILayout.Button(string.Format("Push to Server")))
                {
                    NugetHelper.Push(nuspec, filepath, apiKey);
                }
            }
        }

        /// <summary>
        /// Allow the Inspector GUI to behave as normal if it's NOT a .nuspec file.
        /// </summary>
        public override void OnInspectorGUI()
        {
            if (isNuspec)
            {
                // do nothing
            }
        }
    }
}