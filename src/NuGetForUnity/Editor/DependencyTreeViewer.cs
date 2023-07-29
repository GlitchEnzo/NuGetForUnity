using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NugetForUnity
{
    /// <summary>
    ///     A viewer for all of the packages and their dependencies currently installed in the project.
    /// </summary>
    public class DependencyTreeViewer : EditorWindow
    {
        private readonly Dictionary<NugetPackage, bool> expanded = new Dictionary<NugetPackage, bool>();

        /// <summary>
        ///     The list of packages that depend on the specified package.
        /// </summary>
        private readonly List<NugetPackage> parentPackages = new List<NugetPackage>();

        /// <summary>
        ///     The titles of the tabs in the window.
        /// </summary>
        private readonly string[] tabTitles = { "Dependency Tree", "Who Depends on Me?" };

        /// <summary>
        ///     The currently selected tab in the window.
        /// </summary>
        private int currentTab;

        /// <summary>
        ///     The array of currently installed package IDs.
        /// </summary>
        private string[] installedPackageIds;

        /// <summary>
        ///     The list of currently installed packages.
        /// </summary>
        private List<NugetPackage> installedPackages;

        private List<NugetPackage> roots;

        private Vector2 scrollPosition;

        private int selectedPackageIndex = -1;

        /// <summary>
        ///     Opens the NuGet Package Manager Window.
        /// </summary>
        [MenuItem("NuGet/Show Dependency Tree", false, 5)]
        protected static void DisplayDependencyTree()
        {
            GetWindow<DependencyTreeViewer>();
        }

        /// <summary>
        ///     Called when enabling the window.
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

                NugetHelper.UpdateInstalledPackages();
                installedPackages = NugetHelper.InstalledPackages.ToList();
                var installedPackageNames = new List<string>();

                foreach (var package in installedPackages)
                {
                    if (!expanded.ContainsKey(package))
                    {
                        expanded.Add(package, false);
                    }

                    installedPackageNames.Add(package.Id);
                }

                installedPackageIds = installedPackageNames.ToArray();

                BuildTree();
            }
            catch (Exception e)
            {
                Debug.LogErrorFormat("{0}", e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void BuildTree()
        {
            roots = NugetHelper.GetInstalledRootPackages();
        }

        /// <summary>
        ///     Automatically called by Unity to draw the GUI.
        /// </summary>
        protected void OnGUI()
        {
            currentTab = GUILayout.Toolbar(currentTab, tabTitles);

            switch (currentTab)
            {
                case 0:
                    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                    foreach (var package in roots)
                    {
                        DrawPackage(package);
                    }

                    EditorGUILayout.EndScrollView();
                    break;
                case 1:
                    EditorStyles.label.fontStyle = FontStyle.Bold;
                    EditorStyles.label.fontSize = 14;
                    EditorGUILayout.LabelField("Select Dependency:", GUILayout.Height(20));
                    EditorStyles.label.fontStyle = FontStyle.Normal;
                    EditorStyles.label.fontSize = 10;
                    EditorGUI.indentLevel++;
                    var newIndex = EditorGUILayout.Popup(selectedPackageIndex, installedPackageIds);
                    EditorGUI.indentLevel--;

                    if (newIndex != selectedPackageIndex)
                    {
                        selectedPackageIndex = newIndex;

                        parentPackages.Clear();
                        var selectedPackage = installedPackages[selectedPackageIndex];
                        foreach (var package in installedPackages)
                        {
                            var frameworkGroup = NugetHelper.GetBestDependencyFrameworkGroupForCurrentSettings(package);
                            foreach (var dependency in frameworkGroup.Dependencies)
                            {
                                if (dependency.Id == selectedPackage.Id)
                                {
                                    parentPackages.Add(package);
                                }
                            }
                        }
                    }

                    EditorGUILayout.Space();
                    EditorStyles.label.fontStyle = FontStyle.Bold;
                    EditorStyles.label.fontSize = 14;
                    EditorGUILayout.LabelField("Packages That Depend on Above:", GUILayout.Height(20));
                    EditorStyles.label.fontStyle = FontStyle.Normal;
                    EditorStyles.label.fontSize = 10;

                    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                    EditorGUI.indentLevel++;
                    if (parentPackages.Count <= 0)
                    {
                        EditorGUILayout.LabelField("NONE");
                    }
                    else
                    {
                        foreach (var parent in parentPackages)
                        {
                            DrawPackage(parent);
                        }
                    }

                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndScrollView();
                    break;
            }
        }

        private void DrawDepencency(NugetPackageIdentifier dependency)
        {
            var fullDependency = installedPackages.Find(p => p.Id == dependency.Id);
            if (fullDependency != null)
            {
                DrawPackage(fullDependency);
            }
            else if (!NugetHelper.IsInstalled(dependency))
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

                    var frameworkGroup = NugetHelper.GetBestDependencyFrameworkGroupForCurrentSettings(package);
                    foreach (var dependency in frameworkGroup.Dependencies)
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
