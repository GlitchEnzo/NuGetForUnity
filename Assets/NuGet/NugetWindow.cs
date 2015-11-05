namespace NugetForUnity
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Represents the NuGet Package Manager Window in the Unity Editor.
    /// </summary>
    public class NugetWindow : EditorWindow
    {
        /// <summary>
        /// The current position of the scroll bar in the GUI.
        /// </summary>
        private Vector2 scrollPosition;

        /// <summary>
        /// The list of NugetPackages available to install.
        /// </summary>
        private List<NugetPackage> packages;

        /// <summary>
        /// Opens the NuGet Package Manager Window.
        /// </summary>
        [MenuItem("NuGet/Manage NuGet Packages")]
        protected static void DisplayNugetWindow()
        {
            GetWindow<NugetWindow>();
        }

        /// <summary>
        /// Called when enabling the window.
        /// </summary>
        private void OnEnable()
        {
            titleContent = new GUIContent("NuGet");
            packages = NugetHelper.List();
        }

        /// <summary>
        /// Draws the GUI.
        /// </summary>
        protected void OnGUI()
        {
            if (GUILayout.Button("List all packages"))
            {
                packages = NugetHelper.List();
            }

            if (GUILayout.Button("Restore packages"))
            {
                NugetHelper.Restore();
                AssetDatabase.Refresh();
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.BeginVertical();

            if (packages != null)
            {
                foreach (var package in packages)
                {
                    if (GUILayout.Button("Install", GUILayout.Width(130)))
                    {
                        NugetHelper.Install(package.ID);
                        AssetDatabase.Refresh();
                    }

                    EditorGUILayout.LabelField(string.Format("{1} [{0}]", package.Version, package.ID));
                    EditorStyles.label.wordWrap = true;
                    EditorGUILayout.LabelField(string.Format("{0}", package.Description));
                    //EditorGUILayout.LabelField(string.Format("{0}", package.LicenseURL));

                    if (GUILayout.Button("View License", GUILayout.Width(130)))
                    {
                        Application.OpenURL(package.LicenseURL);
                    }

                    GUILayout.Space(40);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }
    }
}