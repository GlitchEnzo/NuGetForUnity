#pragma warning disable SA1512,SA1124 // Single-line comments should not be followed by blank line

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using NugetForUnity.Configuration;
using NugetForUnity.Helper;
using NugetForUnity.PackageSource;
using NugetForUnity.PluginSupport;
using UnityEditor;
using UnityEngine;

#region No ReShaper

// ReSharper disable All
// needed because 'JetBrains.Annotations.NotNull' and 'System.Diagnostics.CodeAnalysis.NotNull' collide if this file is compiled with a never version of Unity / C#
using SuppressMessageAttribute = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;

// ReSharper restore All

#endregion

#pragma warning restore SA1512,SA1124 // Single-line comments should not be followed by blank line
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
        public const string NuGetForUnityVersion = "4.0.2";

        private readonly GUIContent deleteX = new GUIContent("\u2716");

        private readonly GUIContent downArrow = new GUIContent("\u25bc");

        private readonly List<NugetPlugin> plugins;

        private readonly GUIContent upArrow = new GUIContent("\u25b2");

        /// <summary>
        ///     The current position of the scroll bar in the GUI for the list of plugins.
        /// </summary>
        private Vector2 pluginsScrollPosition;

        private GUIStyle redToggleStyle;

        /// <summary>
        ///     Indicates if the warning for packages.config file path should be shown in case it is outside of Assets folder.
        /// </summary>
        private bool shouldShowPackagesConfigPathWarning;

        /// <summary>
        ///     The current position of the scroll bar in the GUI for the list of sources.
        /// </summary>
        private Vector2 sourcesScrollPosition;

        /// <summary>
        ///     Initializes a new instance of the <see cref="NugetPreferences" /> class.
        ///     Path of packages.config file is checked here as well in case it was manually changed.
        /// </summary>
        private NugetPreferences()
            : base("Preferences/NuGet For Unity", SettingsScope.User)
        {
            shouldShowPackagesConfigPathWarning = !UnityPathHelper.IsPathInAssets(ConfigurationManager.NugetConfigFile.PackagesConfigDirectoryPath);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var enabledPlugins = new HashSet<NugetForUnityPluginId>(ConfigurationManager.NugetConfigFile.EnabledPlugins);
            plugins = assemblies.Where(assembly => assembly.FullName.IndexOf("NugetForUnityPlugin", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(assembly => new NugetPlugin(assembly, enabledPlugins.Remove(new NugetForUnityPluginId(assembly))))
                .ToList();

            // If there are any enabled plugins that are not found in the project add them to the list as invalid plugins so user can disable them.
            plugins.AddRange(enabledPlugins.Select(pluginId => new NugetPlugin(pluginId.Name, pluginId.Path)));
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
        public override void OnGUI([CanBeNull] string searchContext)
        {
            if (redToggleStyle == null)
            {
                redToggleStyle = new GUIStyle(GUI.skin.toggle) { onNormal = { textColor = Color.red }, onHover = { textColor = Color.red } };
            }

            var preferencesChangedThisFrame = false;
            var sourcePathChangedThisFrame = false;

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

            var slimRestore = EditorGUILayout.Toggle(
                new GUIContent(
                    "Slim Restore",
                    "Slim restore is a faster way of installing/restoring packages after opening the Unity project. " +
                    "To achieve this, the package installation step skips installing dependencies not listed inside the package.config, " +
                    "also it skips checking against Unity pre-imported libraries. If you have a complex project setup with multiple target " +
                    "platforms that include different packages, you might need to disable this feature. " +
                    "Manually restoring via menu option will ignore this setting."),
                ConfigurationManager.NugetConfigFile.SlimRestore);
            if (slimRestore != ConfigurationManager.NugetConfigFile.SlimRestore)
            {
                preferencesChangedThisFrame = true;
                ConfigurationManager.NugetConfigFile.SlimRestore = slimRestore;
            }

            using (new EditorGUILayout.HorizontalScope())
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
                        shouldShowPackagesConfigPathWarning = !UnityPathHelper.IsPathInAssets(newPath);

                        PackagesConfigFile.Move(newPath);
                        preferencesChangedThisFrame = true;
                    }
                }
            }

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

            EditorGUILayout.LabelField("Package Sources:");

            using (var scrollView = new EditorGUILayout.ScrollViewScope(sourcesScrollPosition))
            {
                sourcesScrollPosition = scrollView.scrollPosition;

                INugetPackageSource sourceToMoveUp = null;
                INugetPackageSource sourceToMoveDown = null;
                INugetPackageSource sourceToRemove = null;

                foreach (var source in ConfigurationManager.NugetConfigFile.PackageSources)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(10);
                        var isEnabled = EditorGUILayout.Toggle(source.IsEnabled, GUILayout.Width(20));
                        if (isEnabled != source.IsEnabled)
                        {
                            preferencesChangedThisFrame = true;
                            source.IsEnabled = isEnabled;
                        }

                        var name = EditorGUILayout.TextField(source.Name, GUILayout.Width(140));
                        if (name != source.Name)
                        {
                            preferencesChangedThisFrame = true;
                            source.Name = name;
                        }

                        var savedPath = EditorGUILayout.TextField(source.SavedPath).Trim();
                        if (savedPath != source.SavedPath)
                        {
                            preferencesChangedThisFrame = true;
                            sourcePathChangedThisFrame = true;
                            source.SavedPath = savedPath;
                        }

                        if (GUILayout.Button(upArrow, GUILayout.Width(24)))
                        {
                            sourceToMoveUp = source;
                        }

                        if (GUILayout.Button(downArrow, GUILayout.Width(24)))
                        {
                            sourceToMoveDown = source;
                        }

                        if (GUILayout.Button(deleteX, GUILayout.Width(24)))
                        {
                            sourceToRemove = source;
                        }
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(29);
                        EditorGUIUtility.labelWidth = 75;
                        using (new EditorGUILayout.VerticalScope())
                        {
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
                        }
                    }
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
                    ConfigurationManager.NugetConfigFile.PackageSources.Add(new NugetPackageSourceLocal("New Source", "source_path"));
                    preferencesChangedThisFrame = true;
                }
            }

            if (plugins.Count > 0)
            {
                EditorGUILayout.LabelField("Plugins:");

                using (var scrollView = new EditorGUILayout.ScrollViewScope(pluginsScrollPosition))
                {
                    pluginsScrollPosition = scrollView.scrollPosition;
                    NugetPlugin pluginToRemove = null;
                    foreach (var plugin in plugins)
                    {
                        var valid = plugin.Assembly != null;
                        var style = valid ? GUI.skin.toggle : redToggleStyle;
                        var enabled = GUILayout.Toggle(plugin.Enabled, plugin.Name, style);
                        if (enabled == plugin.Enabled)
                        {
                            continue;
                        }

                        plugin.Enabled = enabled;
                        preferencesChangedThisFrame = true;
                        if (enabled)
                        {
                            ConfigurationManager.NugetConfigFile.EnabledPlugins.Add(new NugetForUnityPluginId(plugin.Name, plugin.Path));
                        }
                        else
                        {
                            ConfigurationManager.NugetConfigFile.EnabledPlugins.Remove(new NugetForUnityPluginId(plugin.Name, plugin.Path));
                            if (!valid)
                            {
                                pluginToRemove = plugin;
                            }
                        }

                        if (valid)
                        {
                            PluginRegistry.Reinitialize();
                        }
                    }

                    if (pluginToRemove != null)
                    {
                        plugins.Remove(pluginToRemove);
                    }
                }
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Reset To Default"))
            {
                NugetConfigFile.CreateDefaultFile(ConfigurationManager.NugetConfigFilePath);
                ConfigurationManager.LoadNugetConfigFile();
                preferencesChangedThisFrame = true;
            }

            if (!preferencesChangedThisFrame)
            {
                return;
            }

            ConfigurationManager.NugetConfigFile.Save(ConfigurationManager.NugetConfigFilePath);

            if (sourcePathChangedThisFrame)
            {
                // we need to force reload as the changed 'url' can lead to a package source type change.
                // e.g. the 'url' can be changed to a V3 nuget API url so we need to create a V3 package source.
                ConfigurationManager.LoadNugetConfigFile();
            }
        }

        private sealed class NugetPlugin
        {
            public NugetPlugin([NotNull] Assembly assembly, bool enabled)
            {
                Assembly = assembly;
                Enabled = enabled;
                Name = Assembly.GetName().Name;
                Path = Assembly.Location;
            }

            public NugetPlugin(string name, string path)
            {
                Assembly = null;
                Enabled = true;
                Name = name;
                Path = path;
            }

            [CanBeNull]
            public Assembly Assembly { get; }

            public string Name { get; }

            public string Path { get; }

            public bool Enabled { get; set; }
        }
    }
}
