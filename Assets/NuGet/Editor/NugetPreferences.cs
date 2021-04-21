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
        public const string NuGetForUnityVersion = "3.0.2";

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
            bool preferencesChangedThisFrame = false;

            EditorGUILayout.LabelField(string.Format("Version: {0}", NuGetForUnityVersion));

            if (NugetHelper.NugetConfigFile == null)
            {
                NugetHelper.LoadNugetConfigFile();
            }

            bool installFromCache = EditorGUILayout.Toggle("Install From the Cache", NugetHelper.NugetConfigFile.InstallFromCache);
            if (installFromCache != NugetHelper.NugetConfigFile.InstallFromCache)
            {
                preferencesChangedThisFrame = true;
                NugetHelper.NugetConfigFile.InstallFromCache = installFromCache;
            }

            bool readOnlyPackageFiles = EditorGUILayout.Toggle("Read-Only Package Files", NugetHelper.NugetConfigFile.ReadOnlyPackageFiles);
            if (readOnlyPackageFiles != NugetHelper.NugetConfigFile.ReadOnlyPackageFiles)
            {
                preferencesChangedThisFrame = true;
                NugetHelper.NugetConfigFile.ReadOnlyPackageFiles = readOnlyPackageFiles;
            }

            bool verbose = EditorGUILayout.Toggle("Use Verbose Logging", NugetHelper.NugetConfigFile.Verbose);
            if (verbose != NugetHelper.NugetConfigFile.Verbose)
            {
                preferencesChangedThisFrame = true;
                NugetHelper.NugetConfigFile.Verbose = verbose;
            }

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
                            bool isEnabled = EditorGUILayout.Toggle(source.IsEnabled, GUILayout.Width(20));
                            if (isEnabled != source.IsEnabled)
                            {
                                preferencesChangedThisFrame = true;
                                source.IsEnabled = isEnabled;
                            }
                        }
                        EditorGUILayout.EndVertical();

                        EditorGUILayout.BeginVertical();
                        {
                            string name = EditorGUILayout.TextField(source.Name);
                            if (name != source.Name)
                            {
                                preferencesChangedThisFrame = true;
                                source.Name = name;
                            }

                            string savedPath = EditorGUILayout.TextField(source.SavedPath).Trim();
                            if (savedPath != source.SavedPath)
                            {
                                preferencesChangedThisFrame = true;
                                source.SavedPath = savedPath;
                            }
                        }
                        EditorGUILayout.EndVertical();
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    {
                        GUILayout.Space(29);
                        EditorGUIUtility.labelWidth = 75;
                        EditorGUILayout.BeginVertical();

                        bool hasPassword = EditorGUILayout.Toggle("Credentials", source.HasPassword);
                        if (hasPassword != source.HasPassword)
                        {
                            preferencesChangedThisFrame = true;
                            source.HasPassword = hasPassword;
                        }

                        if (source.HasPassword)
                        {
                            string userName = EditorGUILayout.TextField("User Name", source.UserName);
                            if (userName != source.UserName)
                            {
                                preferencesChangedThisFrame = true;
                                source.UserName = userName;
                            }

                            string savedPassword = EditorGUILayout.PasswordField("Password", source.SavedPassword);
                            if (savedPassword != source.SavedPassword)
                            {
                                preferencesChangedThisFrame = true;
                                source.SavedPassword = savedPassword;
                            }
                        }
                        else
                        {
                            source.UserName = null;
                        }
                        EditorGUIUtility.labelWidth = 0;
                        EditorGUILayout.EndVertical();
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
                preferencesChangedThisFrame = true;
            }

            if (sourceToMoveDown != null)
            {
                int index = NugetHelper.NugetConfigFile.PackageSources.IndexOf(sourceToMoveDown);
                if (index < NugetHelper.NugetConfigFile.PackageSources.Count - 1)
                {
                    NugetHelper.NugetConfigFile.PackageSources[index] = NugetHelper.NugetConfigFile.PackageSources[index + 1];
                    NugetHelper.NugetConfigFile.PackageSources[index + 1] = sourceToMoveDown;
                }
                preferencesChangedThisFrame = true;
            }

            if (sourceToRemove != null)
            {
                NugetHelper.NugetConfigFile.PackageSources.Remove(sourceToRemove);
                preferencesChangedThisFrame = true;
            }

            if (GUILayout.Button(string.Format("Add New Source")))
            {
                NugetHelper.NugetConfigFile.PackageSources.Add(new NugetPackageSource("New Source", "source_path"));
                preferencesChangedThisFrame = true;
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button(string.Format("Reset To Default")))
            {
                NugetConfigFile.CreateDefaultFile(NugetHelper.NugetConfigFilePath);
                NugetHelper.LoadNugetConfigFile();
                preferencesChangedThisFrame = true;
            }

            if (preferencesChangedThisFrame)
            {
                NugetHelper.NugetConfigFile.Save(NugetHelper.NugetConfigFilePath);
            }
        }
    }
}
