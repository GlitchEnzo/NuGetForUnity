using UnityEditorInternal;

namespace NugetForUnity
{
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Handles the displaying, editing, and saving of the preferences for NuGet For Unity.
    /// </summary>
    public class NugetPreferences
    {
        /// <summary>
        /// Nested class to hold the preference key string values.
        /// </summary>
        private static class PrefKeys
        {
            public const string UseCache = "NugetUseCache";
        }

        /// <summary>
        /// Gets a value indicating whether NuGet is using the local cache or not.
        /// </summary>
        public static bool UseCache { get; private set; }

        /// <summary>
        /// Static contructor used to load the preferences.
        /// </summary>
        static NugetPreferences()
        {
            UseCache = EditorPrefs.GetBool(PrefKeys.UseCache, true);
        }

        /// <summary>
        /// Draws the preferences GUI inside the Unity preferences window in the Editor.
        /// </summary>
        [PreferenceItem("NuGet For Unity")]
        public static void PreferencesGUI()
        {
            // Draw the GUI
            UseCache = EditorGUILayout.Toggle("Install From the Cache", UseCache);

            // Save the preferences
            if (GUI.changed)
            {
                EditorPrefs.SetBool(PrefKeys.UseCache, UseCache);
            }

            NugetHelper.NugetConfigFile.Verbose = EditorGUILayout.Toggle("Use Verbose Logging", NugetHelper.NugetConfigFile.Verbose);

            EditorGUILayout.LabelField("Package Sources:");

            foreach (var source in NugetHelper.NugetConfigFile.PackageSources)
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
            }

            if (GUILayout.Button(string.Format("Save")))
            {
                NugetHelper.NugetConfigFile.Save(NugetHelper.NugetConfigFilePath);
            }
        }
    }
}