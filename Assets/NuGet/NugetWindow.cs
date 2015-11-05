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
        /// The list of NugetPackages already installed.
        /// </summary>
        private List<NugetPackage> installedPackages;

        /// <summary>
        /// Opens the NuGet Package Manager Window.
        /// </summary>
        [MenuItem("NuGet/Manage NuGet Packages")]
        protected static void DisplayNugetWindow()
        {
            GetWindow<NugetWindow>();
        }

        /// <summary>
        /// Restores all packages defined in packages.config
        /// </summary>
        [MenuItem("NuGet/Restore Packages")]
        protected static void RestorePackages()
        {
            NugetHelper.Restore();
        }

        /// <summary>
        /// Called when enabling the window.
        /// </summary>
        private void OnEnable()
        {
            titleContent = new GUIContent("NuGet");
            packages = NugetHelper.List();
            installedPackages = NugetHelper.LoadInstalledPackages();
        }

        /// <summary>
        /// From here: http://forum.unity3d.com/threads/changing-the-background-color-for-beginhorizontal.66015/
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="col"></param>
        /// <returns></returns>
        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];

            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();

            return result;
        }

        /// <summary>
        /// Draws the GUI.
        /// </summary>
        protected void OnGUI()
        {
            //if (GUILayout.Button("Restore packages"))
            //{
            //    NugetHelper.Restore();
            //}

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.BeginVertical();

            GUIStyle style = new GUIStyle();
            style.normal.background = MakeTex(20, 20, new Color(0.3f, 0.3f, 0.3f));

            if (packages != null)
            {
                for (int i = 0; i < packages.Count; i++)
                {
                    if (i%2 == 0)
                        EditorGUILayout.BeginVertical();
                    else
                        EditorGUILayout.BeginVertical(style);

                    EditorGUILayout.BeginHorizontal();
                    {
                        EditorStyles.label.fontStyle = FontStyle.Bold;
                        EditorGUILayout.LabelField(string.Format("{1} [{0}]", packages[i].Version, packages[i].ID));

                        //if (GUILayout.Button("Update", GUILayout.Width(130)))
                        //{

                        //}

                        if (installedPackages.Contains(packages[i]))
                        {
                            if (GUILayout.Button("Uninstall", GUILayout.Width(130)))
                            {
                                installedPackages.Remove(packages[i]);
                                NugetHelper.Uninstall(packages[i]);
                            }
                        }
                        else
                        {
                            if (GUILayout.Button("Install", GUILayout.Width(130)))
                            {
                                installedPackages.Add(packages[i]);
                                NugetHelper.Install(packages[i]);
                            }
                        }
                        
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorStyles.label.wordWrap = true;
                    EditorStyles.label.fontStyle = FontStyle.Normal;
                    EditorGUILayout.LabelField(string.Format("{0}", packages[i].Description));

                    if (GUILayout.Button("View License", GUILayout.Width(120)))
                    {
                        Application.OpenURL(packages[i].LicenseURL);
                    }

                    //GUILayout.Space(40);

                    EditorGUILayout.EndVertical();
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }
    }
}