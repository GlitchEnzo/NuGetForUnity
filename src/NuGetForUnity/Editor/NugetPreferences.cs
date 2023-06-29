using UnityEditor;
using UnityEngine;

namespace NugetForUnity
{
    /// <summary>
    ///     Handles the displaying, editing, and saving of the preferences for NuGet For Unity.
    /// </summary>
    public class NugetPreferences : SettingsProvider
    {
        /// <summary>
        ///     The current version of NuGet for Unity.
        /// </summary>
        public const string NuGetForUnityVersion = "3.1.3";

        /// <summary>
        ///     The current position of the scroll bar in the GUI.
        /// </summary>
        private static Vector2 scrollPosition;

        /// <summary>
        ///     Instantiate the settings provider.
        /// </summary>
        public NugetPreferences()
            : base("Preferences/NuGet For Unity", SettingsScope.User)
        {
        }

        /// <summary>
        ///     Creates a instance of the NuGet for Unity settings provider.
        /// </summary>
        /// <returns>The instance of the settings provider.</returns>
        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new NugetPreferences();
        }

        /// <summary>
        ///     Draws the preferences GUI inside the Unity preferences window in the Editor.
        /// </summary>
        public override void OnGUI(string searchContext)
        {
            var preferencesChangedThisFrame = false;

            EditorGUILayout.LabelField(string.Format("Version: {0}", NuGetForUnityVersion));

            if (NugetHelper.NugetConfigFile == null)
            {
                NugetHelper.LoadNugetConfigFile();
            }

            var installFromCache = EditorGUILayout.Toggle("Install From the Cache", NugetHelper.NugetConfigFile.InstallFromCache);
            if (installFromCache != NugetHelper.NugetConfigFile.InstallFromCache)
            {
                preferencesChangedThisFrame = true;
                NugetHelper.NugetConfigFile.InstallFromCache = installFromCache;
            }

            var readOnlyPackageFiles = EditorGUILayout.Toggle("Read-Only Package Files", NugetHelper.NugetConfigFile.ReadOnlyPackageFiles);
            if (readOnlyPackageFiles != NugetHelper.NugetConfigFile.ReadOnlyPackageFiles)
            {
                preferencesChangedThisFrame = true;
                NugetHelper.NugetConfigFile.ReadOnlyPackageFiles = readOnlyPackageFiles;
            }

            var verbose = EditorGUILayout.Toggle("Use Verbose Logging", NugetHelper.NugetConfigFile.Verbose);
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
                            var isEnabled = EditorGUILayout.Toggle(source.IsEnabled, GUILayout.Width(20));
                            if (isEnabled != source.IsEnabled)
                            {
                                preferencesChangedThisFrame = true;
                                source.IsEnabled = isEnabled;
                            }
                        }
                        EditorGUILayout.EndVertical();

                        EditorGUILayout.BeginVertical();
                        {
                            var name = EditorGUILayout.TextField(source.Name);
                            if (name != source.Name)
                            {
                                preferencesChangedThisFrame = true;
                                source.Name = name;
                            }

                            var savedPath = EditorGUILayout.TextField(source.SavedPath).Trim();
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

                        var hasPassword = EditorGUILayout.Toggle("Credentials", source.HasPassword);
                        if (hasPassword != source.HasPassword)
                        {
                            preferencesChangedThisFrame = true;
                            source.HasPassword = hasPassword;
                        }

                        if (source.HasPassword)
                        {
                            var userName = EditorGUILayout.TextField("User Name", source.UserName);
                            if (userName != source.UserName)
                            {
                                preferencesChangedThisFrame = true;
                                source.UserName = userName;
                            }

                            var savedPassword = EditorGUILayout.PasswordField("Password", source.SavedPassword);
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
                        if (GUILayout.Button("Move Up"))
                        {
                            sourceToMoveUp = source;
                        }

                        if (GUILayout.Button("Move Down"))
                        {
                            sourceToMoveDown = source;
                        }

                        if (GUILayout.Button("Remove"))
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
                var index = NugetHelper.NugetConfigFile.PackageSources.IndexOf(sourceToMoveUp);
                if (index > 0)
                {
                    NugetHelper.NugetConfigFile.PackageSources[index] = NugetHelper.NugetConfigFile.PackageSources[index - 1];
                    NugetHelper.NugetConfigFile.PackageSources[index - 1] = sourceToMoveUp;
                }

                preferencesChangedThisFrame = true;
            }

            if (sourceToMoveDown != null)
            {
                var index = NugetHelper.NugetConfigFile.PackageSources.IndexOf(sourceToMoveDown);
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

            if (GUILayout.Button("Add New Source"))
            {
                NugetHelper.NugetConfigFile.PackageSources.Add(new NugetPackageSource("New Source", "source_path"));
                preferencesChangedThisFrame = true;
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Reset To Default"))
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
