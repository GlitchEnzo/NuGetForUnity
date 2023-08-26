using System.Diagnostics.CodeAnalysis;
using NugetForUnity.Configuration;
using NugetForUnity.Helper;
using NugetForUnity.PackageSource;
using UnityEditor;
using UnityEngine;

namespace NugetForUnity.Ui
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
        private Vector2 scrollPosition;

        /// <summary>
        ///     Indicates if the warning for packages.config file path should be shown in case it is outside of Assets folder.
        /// </summary>
        private bool shouldShowPackagesConfigPathWarning;

        /// <summary>
        ///     Initializes a new instance of the <see cref="NugetPreferences" /> class.
        ///     Path of packages.config file is checked here as well in case it was manually changed.
        /// </summary>
        private NugetPreferences()
            : base("Preferences/NuGet For Unity", SettingsScope.User)
        {
            shouldShowPackagesConfigPathWarning = UnityPathHelper.IsPathInAssets(ConfigurationManager.NugetConfigFile.PackagesConfigDirectoryPath);
        }

        /// <summary>
        ///     Creates a instance of the NuGet for Unity settings provider.
        /// </summary>
        /// <returns>The instance of the settings provider.</returns>
        [SettingsProvider]
        [SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "Used by unity.")]
        public static SettingsProvider Create()
        {
            return new NugetPreferences();
        }

        /// <summary>
        ///     Draws the preferences GUI inside the Unity preferences window in the Editor.
        /// </summary>
        /// <param name="searchContext">The search context for the preferences.</param>
        public override void OnGUI(string searchContext)
        {
            var preferencesChangedThisFrame = false;

            EditorGUILayout.LabelField($"Version: {NuGetForUnityVersion}");

            var installFromCache = EditorGUILayout.Toggle("Install From the Cache", ConfigurationManager.NugetConfigFile.InstallFromCache);
            if (installFromCache != ConfigurationManager.NugetConfigFile.InstallFromCache)
            {
                preferencesChangedThisFrame = true;
                ConfigurationManager.NugetConfigFile.InstallFromCache = installFromCache;
            }

            var readOnlyPackageFiles = EditorGUILayout.Toggle("Read-Only Package Files", ConfigurationManager.NugetConfigFile.ReadOnlyPackageFiles);
            if (readOnlyPackageFiles != ConfigurationManager.NugetConfigFile.ReadOnlyPackageFiles)
            {
                preferencesChangedThisFrame = true;
                ConfigurationManager.NugetConfigFile.ReadOnlyPackageFiles = readOnlyPackageFiles;
            }

            var verbose = EditorGUILayout.Toggle("Use Verbose Logging", ConfigurationManager.NugetConfigFile.Verbose);
            if (verbose != ConfigurationManager.NugetConfigFile.Verbose)
            {
                preferencesChangedThisFrame = true;
                ConfigurationManager.NugetConfigFile.Verbose = verbose;
            }

            EditorGUILayout.BeginHorizontal();
            {
                var packagesConfigPath = ConfigurationManager.NugetConfigFile.PackagesConfigDirectoryPath;
                EditorGUILayout.LabelField(
                    new GUIContent(
                        $"Packages Config path: {ConfigurationManager.NugetConfigFile.RelativePackagesConfigDirectoryPath}",
                        $"Absolute path: {packagesConfigPath}"));
                if (GUILayout.Button("Browse"))
                {
                    var newPath = EditorUtility.OpenFolderPanel("Select Folder", packagesConfigPath, string.Empty);

                    if (!string.IsNullOrEmpty(newPath) && newPath != packagesConfigPath)
                    {
                        // if the path root is different or it is not under Assets folder, we want to show a warning message
                        shouldShowPackagesConfigPathWarning = UnityPathHelper.IsPathInAssets(newPath);

                        PackagesConfigFile.Move(newPath);
                        preferencesChangedThisFrame = true;
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
            if (shouldShowPackagesConfigPathWarning)
            {
                EditorGUILayout.HelpBox(
                    "The packages.config is placed outside of Assets folder, this disables the functionality of automatically restoring packages if the file is changed on the disk.",
                    MessageType.Warning);
            }

            var requestTimeout = EditorGUILayout.IntField(
                new GUIContent(
                    "Request Timeout in seconds",
                    "Timeout used for web requests to the package source. A value of -1 can be used to disable timeout."),
                ConfigurationManager.NugetConfigFile.RequestTimeoutSeconds);
            if (requestTimeout != ConfigurationManager.NugetConfigFile.RequestTimeoutSeconds)
            {
                preferencesChangedThisFrame = true;
                ConfigurationManager.NugetConfigFile.RequestTimeoutSeconds = requestTimeout;
            }

            var lockPackagesOnRestore = EditorGUILayout.Toggle(
                new GUIContent(
                    "Lock Packages on Restore",
                    "When lock packages on restore is enabled only packages explicitly listed inside the 'packages.config' file will be installed. No dependencies are installed. This feature should only be used when having issues that unneeded or broken packages are installed."),
                ConfigurationManager.NugetConfigFile.LockPackagesOnRestore);
            if (lockPackagesOnRestore != ConfigurationManager.NugetConfigFile.LockPackagesOnRestore)
            {
                preferencesChangedThisFrame = true;
                ConfigurationManager.NugetConfigFile.LockPackagesOnRestore = lockPackagesOnRestore;
            }

            EditorGUILayout.LabelField("Package Sources:");

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            INugetPackageSource sourceToMoveUp = null;
            INugetPackageSource sourceToMoveDown = null;
            INugetPackageSource sourceToRemove = null;

            foreach (var source in ConfigurationManager.NugetConfigFile.PackageSources)
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
                var index = ConfigurationManager.NugetConfigFile.PackageSources.IndexOf(sourceToMoveUp);
                if (index > 0)
                {
                    ConfigurationManager.NugetConfigFile.PackageSources[index] = ConfigurationManager.NugetConfigFile.PackageSources[index - 1];
                    ConfigurationManager.NugetConfigFile.PackageSources[index - 1] = sourceToMoveUp;
                }

                preferencesChangedThisFrame = true;
            }

            if (sourceToMoveDown != null)
            {
                var index = ConfigurationManager.NugetConfigFile.PackageSources.IndexOf(sourceToMoveDown);
                if (index < ConfigurationManager.NugetConfigFile.PackageSources.Count - 1)
                {
                    ConfigurationManager.NugetConfigFile.PackageSources[index] = ConfigurationManager.NugetConfigFile.PackageSources[index + 1];
                    ConfigurationManager.NugetConfigFile.PackageSources[index + 1] = sourceToMoveDown;
                }

                preferencesChangedThisFrame = true;
            }

            if (sourceToRemove != null)
            {
                ConfigurationManager.NugetConfigFile.PackageSources.Remove(sourceToRemove);
                preferencesChangedThisFrame = true;
            }

            if (GUILayout.Button("Add New Source"))
            {
                ConfigurationManager.NugetConfigFile.PackageSources.Add(new NugetPackageSourceV2("New Source", "source_path"));
                preferencesChangedThisFrame = true;
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Reset To Default"))
            {
                NugetConfigFile.CreateDefaultFile(ConfigurationManager.NugetConfigFilePath);
                ConfigurationManager.LoadNugetConfigFile();
                preferencesChangedThisFrame = true;
            }

            if (preferencesChangedThisFrame)
            {
                ConfigurationManager.NugetConfigFile.Save(ConfigurationManager.NugetConfigFilePath);
            }
        }
    }
}
