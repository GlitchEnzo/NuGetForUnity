namespace NugetForUnity
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// A viewer for all of the packages and their dependencies currently installed in the project.
    /// </summary>
    public class DependencyTreeViewer : EditorWindow
    {
        /// <summary>
        /// Opens the NuGet Package Manager Window.
        /// </summary>
        [MenuItem("NuGet/Show Dependency Tree")]
        protected static void DisplayDependencyTree()
        {
            GetWindow<DependencyTreeViewer>();
        }

        /// <summary>
        /// The list of currently installed packages.
        /// </summary>
        private List<NugetPackage> installedPackages;

        private Dictionary<NugetPackage, bool> expanded = new Dictionary<NugetPackage, bool>();

        private List<NugetPackage> roots;

        /// <summary>
        /// Called when enabling the window.
        /// </summary>
        private void OnEnable()
        {
            try
            {
                // reload the NuGet.config file, in case it was changed after Unity opened, but before the manager window opened (now)
                NugetHelper.LoadNugetConfigFile();

                // set the window title
                titleContent = new GUIContent("Dependencies");

                EditorUtility.DisplayProgressBar("Building Dependency Tree", "Reading installed packages...", 0.5f);

                installedPackages = NugetHelper.GetInstalledPackages();

                foreach (NugetPackage package in installedPackages)
                {
                    expanded.Add(package, true);
                }

                BuildTree();
            }
            catch (System.Exception e)
            {
                Debug.LogErrorFormat("{0}", e.ToString());
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void BuildTree()
        {
            // default all packages to being roots
            roots = new List<NugetPackage>(installedPackages);

            // remove a package as a root if another package is dependent on it
            foreach (NugetPackage package in installedPackages)
            {
                foreach (NugetPackageIdentifier dependency in package.Dependencies)
                {
                    roots.RemoveAll(p => p.Id == dependency.Id);
                }
            }
        }

        /// <summary>
        /// Automatically called by Unity to draw the GUI.
        /// </summary>
        protected void OnGUI()
        {
            foreach (NugetPackage package in roots)
            {
                DrawPackage(package);
            }
        }

        private void DrawDepencency(NugetPackageIdentifier dependency)
        {
            NugetPackage fullDependency = installedPackages.Find(p => p.Id == dependency.Id);
            if (fullDependency != null)
            {
                DrawPackage(fullDependency);
            }
            else
            {
                Debug.LogErrorFormat("{0} {1} is not installed!", dependency.Id, dependency.Version);
            }
        }

        private void DrawPackage(NugetPackage package)
        {
            if (package.Dependencies != null && package.Dependencies.Count > 0)
            {
                expanded[package] = EditorGUILayout.Foldout(expanded[package], string.Format("{0} {1}", package.Id, package.Version));

                if (expanded[package])
                {
                    EditorGUI.indentLevel++;
                    foreach (NugetPackageIdentifier dependency in package.Dependencies)
                    {
                        DrawDepencency(dependency);
                    }
                    EditorGUI.indentLevel--;
                }
            }
            else
            {
                EditorGUILayout.LabelField(string.Format("{0} {1}", package.Id, package.Version));
            }
        }
    }
}
