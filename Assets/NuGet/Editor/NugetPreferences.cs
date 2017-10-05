namespace NugetForUnity
{
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Handles the displaying, editing, and saving of the preferences for NuGet For Unity.
    /// </summary>
    public static class NugetPreferences
    {
        /// <summary>
        /// The current version of NuGet for Unity.
        /// </summary>
        public const string NuGetForUnityVersion = "0.0.11";

        /// <summary>
        /// The current position of the scroll bar in the GUI.
        /// </summary>
        private static Vector2 scrollPosition;

        /// <summary>
        /// Draws the preferences GUI inside the Unity preferences window in the Editor.
        /// </summary>
        [PreferenceItem("NuGet For Unity")]
        public static void PreferencesGUI()
        {
            EditorGUILayout.LabelField(string.Format("Version: {0}", NuGetForUnityVersion));

            if (NugetHelper.NugetConfigFile == null)
            {
                NugetHelper.LoadNugetConfigFile();
            }

            NugetHelper.NugetConfigFile.InstallFromCache = EditorGUILayout.Toggle("Install From the Cache", NugetHelper.NugetConfigFile.InstallFromCache);

            NugetHelper.NugetConfigFile.Verbose = EditorGUILayout.Toggle("Use Verbose Logging", NugetHelper.NugetConfigFile.Verbose);

            EditorGUILayout.LabelField("Package Sources:");

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            NugetPackageSource sourceToMoveUp = null;
            NugetPackageSource sourceToMoveDown = null;
            NugetPackageSource sourceToRemove = null;

            foreach (var source in NugetHelper.NugetConfigFile.PackageSources)
            {
                EditorGUILayout.BeginVertical();
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        EditorGUILayout.BeginVertical(GUILayout.Width(20));
                        {
                            GUILayout.Space(10);
                            source.IsEnabled = EditorGUILayout.Toggle(source.IsEnabled, GUILayout.Width(20));
                        }
                        EditorGUILayout.EndVertical();

                        EditorGUILayout.BeginVertical();
                        {
                            source.Name = EditorGUILayout.TextField(source.Name);
                            source.Path = EditorGUILayout.TextField(source.Path);
                        }
                        EditorGUILayout.EndVertical();
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    {
                        GUILayout.Space(29);
                        EditorGUIUtility.labelWidth = 60;
                        source.HasPassword = EditorGUILayout.Toggle("password", source.HasPassword);
                        if (source.HasPassword)
                        {
                            EditorGUIUtility.labelWidth = 0;
                            source.Password = EditorGUILayout.PasswordField(source.Password);
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    {
                        if (GUILayout.Button(string.Format("Move Up")))
                        {
                            sourceToMoveUp = source;
                        }

                        if (GUILayout.Button(string.Format("Move Down")))
                        {
                            sourceToMoveDown = source;
                        }

                        if (GUILayout.Button(string.Format("Remove")))
                        {
                            sourceToRemove = source;
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }

            if (sourceToMoveUp != null)
            {
                int index = NugetHelper.NugetConfigFile.PackageSources.IndexOf(sourceToMoveUp);
                if (index > 0)
                {
                    NugetHelper.NugetConfigFile.PackageSources[index] = NugetHelper.NugetConfigFile.PackageSources[index - 1];
                    NugetHelper.NugetConfigFile.PackageSources[index - 1] = sourceToMoveUp;
                }
            }

            if (sourceToMoveDown != null)
            {
                int index = NugetHelper.NugetConfigFile.PackageSources.IndexOf(sourceToMoveDown);
                if (index < NugetHelper.NugetConfigFile.PackageSources.Count - 1)
                {
                    NugetHelper.NugetConfigFile.PackageSources[index] = NugetHelper.NugetConfigFile.PackageSources[index + 1];
                    NugetHelper.NugetConfigFile.PackageSources[index + 1] = sourceToMoveDown;
                }
            }

            if (sourceToRemove != null)
            {
                NugetHelper.NugetConfigFile.PackageSources.Remove(sourceToRemove);
            }

            if (GUILayout.Button(string.Format("Add New Source")))
            {
                NugetHelper.NugetConfigFile.PackageSources.Add(new NugetPackageSource("New Source", "source_path"));
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button(string.Format("Save")))
            {
                NugetHelper.NugetConfigFile.Save(NugetHelper.NugetConfigFilePath);
                NugetHelper.LoadNugetConfigFile();
            }
        }
    }
}