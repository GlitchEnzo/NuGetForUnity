#pragma warning disable SA1512,SA1124 // Single-line comments should not be followed by blank line

using System;
using System.Collections.Generic;
using System.IO;
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
        public const string NuGetForUnityVersion = "4.1.1";

        private const float LabelPading = 5;

        private readonly GUIContent deleteX = new GUIContent("\u2716");

        private readonly GUIContent downArrow = new GUIContent("\u25bc");

        private readonly Dictionary<string, bool> packageSourceAdvancedSettingsExpanded = new Dictionary<string, bool>();

        private readonly List<NugetPlugin> plugins;

        private readonly GUIContent upArrow = new GUIContent("\u25b2");

        private float? biggestAdvancedSettingsPackageSourceSectionLabelSize;

        private float? biggestPackageSourceSectionLabelSize;

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
            shouldShowPackagesConfigPathWarning = ConfigurationManager.NugetConfigFile.InstallLocation == PackageInstallLocation.CustomWithinAssets &&
                                                  !UnityPathHelper.IsPathInAssets(ConfigurationManager.NugetConfigFile.PackagesConfigDirectoryPath);
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
            redToggleStyle = redToggleStyle ??
                             new GUIStyle(GUI.skin.toggle) { onNormal = { textColor = Color.red }, onHover = { textColor = Color.red } };

            var preferencesChangedThisFrame = false;
            var sourcePathChangedThisFrame = false;
            var needsAssetRefresh = false;

            var biggestLabelSize = EditorStyles.label.CalcSize(new GUIContent("Prefer .NET Standard dependencies over .NET Framework")).x;
            EditorGUIUtility.labelWidth = biggestLabelSize;
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

            var newInstallLocation = (PackageInstallLocation)EditorGUILayout.EnumPopup(
                "Placement:",
                ConfigurationManager.NugetConfigFile.InstallLocation);
            if (newInstallLocation != ConfigurationManager.NugetConfigFile.InstallLocation)
            {
                var oldRepoPath = ConfigurationManager.NugetConfigFile.RepositoryPath;
                InstalledPackagesManager.UpdateInstalledPackages(); // Make sure it is initialized before we move files around
                ConfigurationManager.MoveConfig(newInstallLocation);
                var newRepoPath = ConfigurationManager.NugetConfigFile.RepositoryPath;
                PackageContentManager.MoveInstalledPackages(oldRepoPath, newRepoPath);

                if (newInstallLocation == PackageInstallLocation.CustomWithinAssets &&
                    Directory.Exists(UnityPathHelper.AbsoluteUnityPackagesNugetPath))
                {
                    Directory.Delete(UnityPathHelper.AbsoluteUnityPackagesNugetPath, true);
                }

                preferencesChangedThisFrame = true;
                needsAssetRefresh = true;
            }

            if (newInstallLocation == PackageInstallLocation.CustomWithinAssets)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var repositoryPath = ConfigurationManager.NugetConfigFile.RepositoryPath;

                    GUILayout.Label(
                        new GUIContent("Packages Install path:", $"Absolute path: {repositoryPath}"),
                        GUILayout.Width(EditorGUIUtility.labelWidth));
                    GUILayout.Label(ConfigurationManager.NugetConfigFile.ConfiguredRepositoryPath);

                    if (GUILayout.Button("Browse", GUILayout.Width(100)))
                    {
                        var newPath = EditorUtility.OpenFolderPanel("Select folder where packages will be installed", repositoryPath, string.Empty);
                        if (!string.IsNullOrEmpty(newPath))
                        {
                            var newRelativePath = PathHelper.GetRelativePath(Application.dataPath, newPath);

                            // We need to make sure saved path is using forward slashes so it works on all systems
                            newRelativePath = newRelativePath.Replace('\\', '/');

                            if (newPath != repositoryPath && UnityPathHelper.IsValidInstallPath(newRelativePath))
                            {
                                PackageContentManager.MoveInstalledPackages(repositoryPath, newPath);
                                ConfigurationManager.NugetConfigFile.ConfiguredRepositoryPath = newRelativePath;
                                UnityPathHelper.EnsurePackageInstallDirectoryIsSetup();
                                preferencesChangedThisFrame = true;
                                needsAssetRefresh = true;
                            }
                            else if (newPath != repositoryPath)
                            {
                                Debug.LogError(
                                    $"Packages install path {newPath} {newRelativePath} is not valid. It must be somewhere under Assets or Packages folder.");
                            }
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    var packagesConfigPath = ConfigurationManager.NugetConfigFile.PackagesConfigDirectoryPath;

                    GUILayout.Label(
                        new GUIContent("Packages Config path:", $"Absolute path: {packagesConfigPath}"),
                        GUILayout.Width(EditorGUIUtility.labelWidth));
                    GUILayout.Label(ConfigurationManager.NugetConfigFile.RelativePackagesConfigDirectoryPath);

                    if (GUILayout.Button("Browse", GUILayout.Width(100)))
                    {
                        var newPath = EditorUtility.OpenFolderPanel("Select Folder", packagesConfigPath, string.Empty);

                        if (!string.IsNullOrEmpty(newPath) && newPath != packagesConfigPath)
                        {
                            // if the path root is different or it is not under Assets folder, we want to show a warning message
                            shouldShowPackagesConfigPathWarning = !UnityPathHelper.IsPathInAssets(newPath);

                            PackagesConfigFile.Move(newPath);
                            preferencesChangedThisFrame = true;
                            needsAssetRefresh = true;
                        }
                    }
                }

                if (shouldShowPackagesConfigPathWarning)
                {
                    EditorGUILayout.HelpBox(
                        "The packages.config is placed outside of Assets folder, this disables the functionality of automatically restoring packages if the file is changed on the disk.",
                        MessageType.Warning);
                }
            }

            var preferNetStandardOverNetFramework = EditorGUILayout.Toggle(
                new GUIContent(
                    "Prefer .NET Standard dependencies over .NET Framework",
                    "If a nuget package contains DLL's for .NET Framework an .NET Standard the .NET Standard DLL's are preferred."),
                ConfigurationManager.NugetConfigFile.PreferNetStandardOverNetFramework);
            if (preferNetStandardOverNetFramework != ConfigurationManager.NugetConfigFile.PreferNetStandardOverNetFramework)
            {
                preferencesChangedThisFrame = true;
                ConfigurationManager.NugetConfigFile.PreferNetStandardOverNetFramework = preferNetStandardOverNetFramework;
            }

            if (TargetFrameworkResolver.CurrentBuildTargetApiCompatibilityLevel.Value == ApiCompatibilityLevel.NET_Standard_2_0)
            {
                EditorGUILayout.HelpBox(
                    "The prefer .NET Standard setting has no effect as you have set the API compatibility level to .NET Standard so .NET Standard will always be preferred, as it is the only supported.",
                    MessageType.Info);
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

                var isFirstPackageSource = true;
                foreach (var source in ConfigurationManager.NugetConfigFile.PackageSources)
                {
                    if (!isFirstPackageSource)
                    {
                        EditorGUILayout.Separator();
                    }

                    isFirstPackageSource = false;
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
                        using (new EditorGUILayout.VerticalScope())
                        {
                            if (source is NugetPackageSourceV3 sourceV3)
                            {
                                packageSourceAdvancedSettingsExpanded.TryGetValue(sourceV3.SavedPath, out var advancedSettingsExpanded);
                                advancedSettingsExpanded = EditorGUILayout.Foldout(
                                    advancedSettingsExpanded,
                                    "Advanced Settings (maybe needed if using a custom non 'nuget.org' package source)",
                                    true,
                                    new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold });
                                packageSourceAdvancedSettingsExpanded[sourceV3.SavedPath] = advancedSettingsExpanded;
                                if (advancedSettingsExpanded)
                                {
                                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                                    {
                                        biggestAdvancedSettingsPackageSourceSectionLabelSize = biggestAdvancedSettingsPackageSourceSectionLabelSize ??
                                                                                               EditorStyles.label.CalcSize(
                                                                                                       new GUIContent(
                                                                                                           "Package download URL template overwrite"))
                                                                                                   .x;
                                        EditorGUIUtility.labelWidth = biggestAdvancedSettingsPackageSourceSectionLabelSize.Value + LabelPading;

                                        var supportsPackageIdSearchFilter = EditorGUILayout.Toggle(
                                            new GUIContent(
                                                "Supports 'packageId' query search filter",
                                                "If the API supports querying a package by its id using the syntax 'packageid:{packageId}'. " +
                                                "This syntax is used to easily search for package updates. " +
                                                "If it is disabled package updates are searched by loading the information of all packages one by one. " +
                                                "Currently we know that this setting need to be disabled when using a registry hosted by 'GitHub' or 'Azure Artifacts'."),
                                            sourceV3.SupportsPackageIdSearchFilter);
                                        if (supportsPackageIdSearchFilter != sourceV3.SupportsPackageIdSearchFilter)
                                        {
                                            preferencesChangedThisFrame = true;
                                            sourceV3.SupportsPackageIdSearchFilter = supportsPackageIdSearchFilter;
                                        }

                                        if (sourceV3.SupportsPackageIdSearchFilter)
                                        {
                                            var updateSearchBatchSize = EditorGUILayout.IntField(
                                                new GUIContent(
                                                    "Update search batch size",
                                                    "The size of each batch in witch avaiLabel updates are fetched. " +
                                                    "To prevent the search query string to exceed the URI length limit we fetch the updates in groups. " +
                                                    "Currently we know that 'Artifactory' requires the setting to be set to 1. Defaults to 20."),
                                                sourceV3.UpdateSearchBatchSize);
                                            if (updateSearchBatchSize != sourceV3.UpdateSearchBatchSize)
                                            {
                                                preferencesChangedThisFrame = true;
                                                sourceV3.UpdateSearchBatchSize = updateSearchBatchSize;
                                            }
                                        }

                                        var packageDownloadUrlTemplateOverwrite = EditorGUILayout.TextField(
                                            new GUIContent(
                                                "Package download URL template overwrite",
                                                "Overwrite for the URL used to download packages '.nupkg' files. " +
                                                "Normally this is not required. Currently we know that 'Artifactory' requires the setting. " +
                                                "The template should contain the placeholder '{0}' for the package id and '{1}' for the package version. " +
                                                "E.g. for nuget.org the template package download URL would be 'https://api.nuget.org/v3-flatcontainer/{0}/{1}/{0}.{1}.nupkg'"),
                                            sourceV3.PackageDownloadUrlTemplateOverwrite);
                                        packageDownloadUrlTemplateOverwrite = packageDownloadUrlTemplateOverwrite?.Trim();
                                        if (string.IsNullOrEmpty(packageDownloadUrlTemplateOverwrite))
                                        {
                                            packageDownloadUrlTemplateOverwrite = null;
                                        }

                                        if (packageDownloadUrlTemplateOverwrite != sourceV3.PackageDownloadUrlTemplateOverwrite)
                                        {
                                            preferencesChangedThisFrame = true;
                                            sourceV3.PackageDownloadUrlTemplateOverwrite = packageDownloadUrlTemplateOverwrite;
                                        }
                                    }
                                }
                            }

                            biggestPackageSourceSectionLabelSize = biggestPackageSourceSectionLabelSize ??
                                                                   EditorStyles.label.CalcSize(new GUIContent("Force use API v3")).x;
                            EditorGUIUtility.labelWidth = biggestPackageSourceSectionLabelSize.Value + LabelPading;

                            var currentForceUseApiV3 = source.SavedProtocolVersion?.Equals("3", StringComparison.Ordinal) == true;
                            if ((source is NugetPackageSourceV3 && currentForceUseApiV3) || source is NugetPackageSourceV2)
                            {
                                var forceUseApiV3 = EditorGUILayout.Toggle(
                                    new GUIContent(
                                        "Force use API v3",
                                        "Normally the API version is autodetect. " +
                                        "It defaults to version \"2\" when not pointing to a package source URL ending in .json (e.g. https://api.nuget.org/v3/index.json). " +
                                        "So if a package source uses API v3 but the URL doesn't end in .json you need to enable this setting (e.g. needed when using 'Aritfactory')."),
                                    currentForceUseApiV3);
                                if (forceUseApiV3 != currentForceUseApiV3)
                                {
                                    preferencesChangedThisFrame = true;
                                    source.SavedProtocolVersion = forceUseApiV3 ? "3" : null;
                                    sourcePathChangedThisFrame = true; // we need to fully reload to be able to switch from V2 to V3
                                }
                            }

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

            if (needsAssetRefresh)
            {
                // AssetDatabase.Refresh(); doesn't work when we move the files from Assets to Packages so we use this instead:
                EditorApplication.ExecuteMenuItem("Assets/Refresh");
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
