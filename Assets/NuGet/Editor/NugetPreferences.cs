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
        /// True if the preferences are loaded from the registry.  False if not.
        /// </summary>
        private static bool prefsLoaded;

        /// <summary>
        /// Gets a value indicating whether NuGet is using the local cache or not.
        /// </summary>
        public static bool UseCache { get; private set; }

        /// <summary>
        /// Draws the preferences GUI inside the Unity preferences window in the Editor.
        /// </summary>
        [PreferenceItem("NuGet For Unity")]
        public static void PreferencesGUI()
        {
            // Load the preferences
            if (!prefsLoaded)
            {
                UseCache = EditorPrefs.GetBool(PrefKeys.UseCache, true);
                prefsLoaded = true;
            }

            // Draw the GUI
            UseCache = EditorGUILayout.Toggle("Use Cache", UseCache);

            // Save the preferences
            if (GUI.changed)
            {
                EditorPrefs.SetBool(PrefKeys.UseCache, UseCache);
            }
        }
    }
}