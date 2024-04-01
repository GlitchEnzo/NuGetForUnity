using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NugetForUnity.Configuration;
using NugetForUnity.Helper;
using NugetForUnity.Models;
using NugetForUnity.PluginAPI;
using NugetForUnity.PluginSupport;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace NugetForUnity.Ui
{
    /// <summary>
    ///     Represents the NuGet Package Manager Window in the Unity Editor.
    /// </summary>
    public class NugetWindow : EditorWindow, ISerializationCallbackReceiver
    {
        private readonly StringBuilder cachedStringBuilder = new StringBuilder();

        private readonly Dictionary<string, bool> foldouts = new Dictionary<string, bool>();

        /// <summary>
        ///     The number of packages to get from the request to the server.
        /// </summary>
        private readonly int numberToGet = 15;

        /// <summary>
        ///     Used to keep track of which packages the user has opened the clone window on.
        /// </summary>
        private readonly HashSet<INugetPackage> openCloneWindows = new HashSet<INugetPackage>(new NugetPackageIdEqualityComparer());

        /// <summary>
        ///     Used to keep track of which packages are selected for downgrading.
        /// </summary>
        private readonly HashSet<INugetPackage> selectedPackageDowngrades = new HashSet<INugetPackage>(new NugetPackageIdEqualityComparer());

        /// <summary>
        ///     Used to keep track of which packages are selected for installing.
        /// </summary>
        private readonly HashSet<INugetPackage> selectedPackageInstalls = new HashSet<INugetPackage>(new NugetPackageIdEqualityComparer());

        /// <summary>
        ///     Used to keep track of which packages are selected for uninstalling.
        /// </summary>
        private readonly HashSet<INugetPackage> selectedPackageUninstalls = new HashSet<INugetPackage>(new NugetPackageIdEqualityComparer());

        /// <summary>
        ///     Used to keep track of which packages are selected for updating.
        /// </summary>
        private readonly HashSet<INugetPackage> selectedPackageUpdates = new HashSet<INugetPackage>(new NugetPackageIdEqualityComparer());

        private readonly GUIContent showDowngradesContent = new GUIContent("Show Downgrades");

        private readonly GUIContent showPrereleaseContent = new GUIContent("Show Prerelease");

        /// <summary>
        ///     The titles of the tabs in the window.
        /// </summary>
        private readonly GUIContent[] tabTitles = { new GUIContent("Online"), new GUIContent("Installed"), new GUIContent("Updates") };

        /// <summary>
        ///     For each package this contains the currently selected version / the state of the version drop-down.
        /// </summary>
        private readonly Dictionary<string, VersionDropdownData> versionDropdownDataPerPackage = new Dictionary<string, VersionDropdownData>();

        /// <summary>
        ///     The list of NugetPackages available to install.
        /// </summary>
        private List<INugetPackage> availablePackages = new List<INugetPackage>();

        /// <summary>
        ///     The currently selected tab in the window.
        /// </summary>
        private NugetWindowTab currentTab;

        /// <summary>
        ///     The default icon to display for packages.
        /// </summary>
        [CanBeNull]
        [SerializeField]
        private Texture2D defaultIcon;

        [CanBeNull]
        private List<INugetPackage> filteredInstalledPackages;

        /// <summary>
        ///     True when the NugetWindow has initialized. This is used to skip time-consuming reloading operations when the assembly is reloaded.
        /// </summary>
        [SerializeField]
        private bool hasRefreshed;

        /// <summary>
        ///     The search term to search the installed packages for.
        /// </summary>
        private string installedSearchTerm;

        [CanBeNull]
        private string lastInstalledSearchTerm;

        /// <summary>
        ///     The number of packages to skip when requesting a list of packages from the server.  This is used to get a new group of packages.
        /// </summary>
        [SerializeField]
        private int numberToSkip;

        /// <summary>
        ///     The search term to search the online packages for.
        /// </summary>
        private string onlineSearchTerm;

        /// <summary>
        ///     The current position of the scroll bar in the GUI.
        /// </summary>
        private Vector2 scrollPosition;

        [CanBeNull]
        [SerializeField]
        private List<SerializableNugetPackage> serializableAvailablePackages;

        [CanBeNull]
        [SerializeField]
        private List<SerializableNugetPackage> serializableToInstallPackages;

        [CanBeNull]
        [SerializeField]
        private List<SerializableNugetPackage> serializableUpdatePackages;

        /// <summary>
        ///     True to show downgrades of package version instead of updates.
        /// </summary>
        private bool showDowngrades;

        private bool showImplicitlyInstalled;

        private bool showInstalled = true;

        private bool showOnlinePackages = true;

        /// <summary>
        ///     True to show beta and alpha package versions.  False to only show stable versions.
        /// </summary>
        private bool showOnlinePrerelease;

        /// <summary>
        ///     True if packages selected for install should be displayed on Online tab, false if availablePackages should be displayed.
        /// </summary>
        private bool showPackagesToInstall = true;

        /// <summary>
        ///     True to show beta and alpha package versions.  False to only show stable versions.
        /// </summary>
        private bool showPrereleaseUpdates;

        /// <summary>
        ///     The list of package updates available, based on the already installed packages.
        /// </summary>
        private List<INugetPackage> updatePackages = new List<INugetPackage>();

        /// <summary>
        ///     The search term to search the update packages for.
        /// </summary>
        private string updatesSearchTerm;

        /// <summary>
        ///     Gets the filtered list of package updates available.
        /// </summary>
        private List<INugetPackage> FilteredUpdatePackages
        {
            get
            {
                IEnumerable<INugetPackage> result;
                if (string.IsNullOrWhiteSpace(updatesSearchTerm))
                {
                    result = updatePackages;
                }
                else
                {
                    result = updatePackages.Where(
                        package => package.Id.IndexOf(updatesSearchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                                   package.Title?.IndexOf(updatesSearchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0);
                }

                var installedPackages = InstalledPackagesManager.InstalledPackages;

                // filter not updatable / not downgradable packages
                return result.Where(
                        package =>
                        {
                            var installed = installedPackages.FirstOrDefault(p => p.Id.Equals(package.Id, StringComparison.OrdinalIgnoreCase));

                            if (installed == null || package.Versions.Count == 0)
                            {
                                // normally shouldn't happen but for now include it in the result
                                return true;
                            }

                            // we do not want to show packages that have no updates if we're showing updates
                            // similarly, we do not show packages that are on the lowest possible version if we're showing downgrades
                            return (showDowngrades && installed.PackageVersion > package.Versions[package.Versions.Count - 1]) ||
                                   (!showDowngrades && installed.PackageVersion < package.Versions[0]);
                        })
                    .ToList();
            }
        }

        private List<INugetPackage> FilteredInstalledPackages
        {
            get
            {
                if (filteredInstalledPackages != null && lastInstalledSearchTerm == installedSearchTerm)
                {
                    return filteredInstalledPackages;
                }

                lastInstalledSearchTerm = installedSearchTerm;
                if (string.IsNullOrWhiteSpace(installedSearchTerm))
                {
                    filteredInstalledPackages = InstalledPackagesManager.InstalledPackages.ToList();
                }
                else
                {
                    filteredInstalledPackages = InstalledPackagesManager.InstalledPackages.Where(
                            package => package.Id.IndexOf(installedSearchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                                       package.Title?.IndexOf(installedSearchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0)
                        .ToList();
                }

                filteredInstalledPackages.Sort(
                    (p1, p2) =>
                    {
                        var cmp = p2.IsManuallyInstalled.CompareTo(p1.IsManuallyInstalled);
                        return cmp != 0 ? cmp : string.Compare(p1.Id, p2.Id, StringComparison.Ordinal);
                    });

                return filteredInstalledPackages;
            }
        }

        private HashSet<INugetPackage> SelectedPackages
        {
            get
            {
                switch (currentTab)
                {
                    case NugetWindowTab.UpdatesTab:
                        return showDowngrades ? selectedPackageDowngrades : selectedPackageUpdates;
                    case NugetWindowTab.InstalledTab:
                        return selectedPackageUninstalls;
                    case NugetWindowTab.OnlineTab:
                        return selectedPackageInstalls;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        /// <inheritdoc />
        public void OnBeforeSerialize()
        {
            serializableAvailablePackages = availablePackages.ConvertAll(package => new SerializableNugetPackage(package));
            serializableToInstallPackages = selectedPackageInstalls.Select(package => new SerializableNugetPackage(package)).ToList();
            serializableUpdatePackages = updatePackages.ConvertAll(package => new SerializableNugetPackage(package));
        }

        /// <inheritdoc />
        public void OnAfterDeserialize()
        {
            if (serializableAvailablePackages != null)
            {
                availablePackages = serializableAvailablePackages.ConvertAll(package => package.Interfaced);
                serializableAvailablePackages = null;
            }

            if (serializableToInstallPackages != null)
            {
                selectedPackageInstalls.Clear();
                selectedPackageInstalls.UnionWith(serializableToInstallPackages.Select(package => package.Interfaced));
                serializableToInstallPackages = null;
            }

            if (serializableUpdatePackages != null)
            {
                updatePackages = serializableUpdatePackages.ConvertAll(package => package.Interfaced);
                serializableUpdatePackages = null;
            }
        }

        /// <summary>
        ///     Opens the NuGet Package Manager Window.
        /// </summary>
        [MenuItem("NuGet/Manage NuGet Packages", false, 0)]
        protected static void DisplayNugetWindow()
        {
            GetWindow<NugetWindow>();
        }

        /// <summary>
        ///     Restores all packages defined in packages.config.
        /// </summary>
        [MenuItem("NuGet/Restore Packages", false, 1)]
        protected static void RestorePackages()
        {
            PackageRestorer.Restore(false);
            foreach (var nugetWindow in Resources.FindObjectsOfTypeAll<NugetWindow>())
            {
                nugetWindow.ClearViewCache();
            }
        }

        /// <summary>
        ///     Opens the preferences window.
        /// </summary>
        [MenuItem("NuGet/Preferences", false, 9)]
        protected static void DisplayPreferences()
        {
            SettingsService.OpenUserPreferences("Preferences/NuGet For Unity");
        }

        /// <summary>
        ///     Automatically called by Unity to draw the GUI.
        /// </summary>
        protected void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var selectedTab = (NugetWindowTab)GUILayout.Toolbar(
                (int)currentTab,
                tabTitles,
                null,
                GUI.ToolbarButtonSize.FitToContents,
                GUILayout.Height(25f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (selectedTab != currentTab)
            {
                OnTabChanged();
            }

            currentTab = selectedTab;

            switch (currentTab)
            {
                case NugetWindowTab.OnlineTab:
                    DrawOnline();
                    break;
                case NugetWindowTab.InstalledTab:
                    DrawInstalled();
                    break;
                case NugetWindowTab.UpdatesTab:
                    DrawUpdates();
                    break;
            }
        }

        private static void GUILayoutLink(string url)
        {
            var rect = EditorGUILayout.GetControlRect();
            rect.yMin -= 2f;
            rect.xMin += 30f;

            if (GUI.Button(rect, url, Styles.LinkLabelStyle))
            {
                Application.OpenURL(url);
            }
        }

        private static void DrawNoDataAvailableInfo(string message)
        {
            EditorGUILayout.LabelField(message, Styles.BoldLabelStyle, GUILayout.Height(20));
        }

        /// <summary>
        ///     Called when enabling the window.
        /// </summary>
        private void OnEnable()
        {
            name = "NuGetForUnity";
            titleContent = new GUIContent("NuGet For Unity");
            Refresh(false);
            UnityPathHelper.EnsurePackageInstallDirectoryIsSetup();
        }

        private void ClearViewCache()
        {
            filteredInstalledPackages = null;
        }

        private void Refresh(bool forceFullRefresh)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                if (forceFullRefresh)
                {
                    CredentialProviderHelper.ClearCachedCredentials();
                }

                // reload the NuGet.config file, in case it was changed after Unity opened, but before the manager window opened (now)
                ConfigurationManager.LoadNugetConfigFile();

                // if we are entering playmode, don't do anything
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    return;
                }

                NugetLogger.LogVerbose(hasRefreshed ? "NugetWindow reloading config" : "NugetWindow reloading config and updating packages");

                if (!hasRefreshed || forceFullRefresh)
                {
                    // reset the number to skip
                    numberToSkip = 0;

                    // TODO: Do we even need to load ALL of the data, or can we just get the Online tab packages?
                    EditorUtility.DisplayProgressBar("Opening NuGet", "Fetching packages from server...", 0.3f);
                    UpdateOnlinePackages();

                    EditorUtility.DisplayProgressBar("Opening NuGet", "Getting installed packages...", 0.6f);
                    UpdateInstalledPackages();

                    EditorUtility.DisplayProgressBar("Opening NuGet", "Getting available updates...", 0.9f);
                    UpdateUpdatePackages();

                    versionDropdownDataPerPackage.Clear();

                    // load the default icon from the Resources folder
                    defaultIcon = (Texture2D)Resources.Load("defaultIcon", typeof(Texture2D));
                }

                hasRefreshed = true;
            }
            catch (Exception e)
            {
                Debug.LogErrorFormat("Error while refreshing NuGet packages list: {0}", e);
            }
            finally
            {
                ClearViewCache();
                EditorUtility.ClearProgressBar();

                NugetLogger.LogVerbose("NugetWindow reloading took {0} ms", stopwatch.ElapsedMilliseconds);
            }
        }

        /// <summary>
        ///     Updates the list of available packages by running a search with the server using the currently set parameters (# to get, # to skip, etc).
        /// </summary>
        private void UpdateOnlinePackages()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // we just block the main thread
            availablePackages = Task.Run(() => ConfigurationManager.SearchAsync(onlineSearchTerm, showOnlinePrerelease, numberToGet, numberToSkip))
                .GetAwaiter()
                .GetResult();
            NugetLogger.LogVerbose(
                "Searching '{0}' in all active package sources returned: {1} packages after {2} ms",
                onlineSearchTerm,
                availablePackages.Count,
                stopwatch.ElapsedMilliseconds);

            // We need to make sure previously selected packages remain in the list
            foreach (var selectedPackage in selectedPackageInstalls.Where(selectedPackage => !availablePackages.Contains(selectedPackage)))
            {
                availablePackages.Add(selectedPackage);
            }
        }

        private void UpdateInstalledPackages()
        {
            InstalledPackagesManager.UpdateInstalledPackages();
            ClearViewCache();
            selectedPackageInstalls.ExceptWith(InstalledPackagesManager.InstalledPackages);
        }

        /// <summary>
        ///     Updates the list of update packages.
        /// </summary>
        private void UpdateUpdatePackages()
        {
            // get any available updates for the installed packages
            updatePackages = ConfigurationManager.GetUpdates(InstalledPackagesManager.InstalledPackages, showPrereleaseUpdates);
        }

        private void OnTabChanged()
        {
            openCloneWindows.Clear();
            foreach (var dropdownData in versionDropdownDataPerPackage.Values)
            {
                // reset to latest package version else the update tab will show the same version as the installed tab
                dropdownData.SelectedIndex = 0;
            }

            ResetScrollPosition();
        }

        private void ResetScrollPosition()
        {
            scrollPosition.y = 0f;
        }

        /// <summary>
        ///     Draws the list of installed packages that have updates available.
        /// </summary>
        private void DrawUpdates()
        {
            DrawUpdatesHeader();

            // display all of the installed packages
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.BeginVertical();

            var filteredUpdatePackages = FilteredUpdatePackages;
            if (filteredUpdatePackages.Count > 0)
            {
                DrawPackagesSplittedByManuallyInstalled(filteredUpdatePackages);
            }
            else
            {
                DrawNoDataAvailableInfo("There are no updates available!");
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        ///     Draws the list of installed packages.
        /// </summary>
        private void DrawInstalled()
        {
            DrawInstalledHeader();

            // display all of the installed packages
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.BeginVertical();

            var installedPackages = FilteredInstalledPackages;
            if (installedPackages.Count > 0)
            {
                DrawPackagesSplittedByManuallyInstalled(installedPackages);
            }
            else
            {
                DrawNoDataAvailableInfo("There are no packages installed!");
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawPackagesSplittedByManuallyInstalled(List<INugetPackage> packages)
        {
            var foldoutRect = EditorGUILayout.GetControlRect(true, 20f).AddY(-1f);
            EditorGUI.DrawRect(foldoutRect.Expand(2f), Styles.FoldoutHeaderColor);
            EditorGUI.DrawRect(foldoutRect.ExpandX(2f).AddY(-2f).SetHeight(1f), Styles.LineColor);
            EditorGUI.DrawRect(foldoutRect.ExpandX(2f).AddY(foldoutRect.height + 1f).SetHeight(1f), Styles.LineColor);
            showInstalled = EditorGUI.Foldout(foldoutRect, showInstalled, "Installed packages", true);
            if (showInstalled)
            {
                if (packages.Exists(package => package.IsManuallyInstalled))
                {
                    DrawPackages(packages.Where(package => package.IsManuallyInstalled), true);
                }
                else
                {
                    DrawNoDataAvailableInfo("There are no explicitly installed packages.");
                }
            }

            foldoutRect = EditorGUILayout.GetControlRect(true, 20f);
            EditorGUI.DrawRect(foldoutRect.Expand(2f), Styles.FoldoutHeaderColor);
            EditorGUI.DrawRect(foldoutRect.ExpandX(2f).AddY(-2f).SetHeight(1f), Styles.LineColor);
            EditorGUI.DrawRect(foldoutRect.ExpandX(2f).AddY(foldoutRect.height + 1f).SetHeight(1f), Styles.LineColor);
            showImplicitlyInstalled = EditorGUI.Foldout(foldoutRect, showImplicitlyInstalled, "Implicitly installed packages", true);
            if (showImplicitlyInstalled)
            {
                DrawPackages(packages.Where(package => !package.IsManuallyInstalled), true);
            }
        }

        /// <summary>
        ///     Draws the current list of available online packages.
        /// </summary>
        private void DrawOnline()
        {
            DrawOnlineHeader();

            // display all of the packages
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.BeginVertical();

            if (selectedPackageInstalls.Count > 0)
            {
                DrawSelectedForInstallationHeader();
                if (showPackagesToInstall)
                {
                    DrawPackages(availablePackages.Where(p => selectedPackageInstalls.Contains(p)), true);
                }
            }

            // If user deselected all the packages revert to showing available packages
            if (selectedPackageInstalls.Count == 0)
            {
                showOnlinePackages = true;
            }

            if (selectedPackageInstalls.Count > 0)
            {
                var foldoutRect = EditorGUILayout.GetControlRect(false, 22f);
                EditorGUI.DrawRect(foldoutRect.Expand(2f), Styles.FoldoutHeaderColor);
                EditorGUI.DrawRect(foldoutRect.ExpandX(2f).AddY(-2f).SetHeight(1f), Styles.LineColor);
                EditorGUI.DrawRect(foldoutRect.ExpandX(2f).AddY(foldoutRect.height + 1f).SetHeight(1f), Styles.LineColor);
                showOnlinePackages = EditorGUI.Foldout(foldoutRect, showOnlinePackages, "Online packages", true);
            }

            if (showOnlinePackages)
            {
                DrawPackages(availablePackages.Where(p => !selectedPackageInstalls.Contains(p)), true);
            }

            GUILayout.Space(3f);

            // allow the user to display more results
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (showOnlinePackages && GUILayout.Button("Show More", GUILayout.Width(120)))
                {
                    numberToSkip += numberToGet;
                    availablePackages.AddRange(
                        Task.Run(() => ConfigurationManager.SearchAsync(onlineSearchTerm, showOnlinePrerelease, numberToGet, numberToSkip))
                            .GetAwaiter()
                            .GetResult());
                }

                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(4f);

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawSelectedForInstallationHeader()
        {
            var foldoutRect = EditorGUILayout.GetControlRect(false, 22f).AddY(-1f);

            EditorGUI.DrawRect(foldoutRect.Expand(2f), Styles.FoldoutHeaderColor);
            EditorGUI.DrawRect(foldoutRect.ExpandX(2f).AddY(-2f).SetHeight(1f), Styles.LineColor);
            EditorGUI.DrawRect(foldoutRect.ExpandX(2f).AddY(foldoutRect.height + 1f).SetHeight(1f), Styles.LineColor);
            foldoutRect.width -= 150f;
            showPackagesToInstall = EditorGUI.Foldout(
                foldoutRect,
                showPackagesToInstall,
                $"Selected for installation: {selectedPackageInstalls.Count}",
                true);

            foldoutRect.x += foldoutRect.width;
            foldoutRect.width = 148f;
            foldoutRect.y += 2f;
            foldoutRect.height -= 4f;

            if (GUI.Button(foldoutRect, "Install All Selected"))
            {
                foreach (var package in selectedPackageInstalls)
                {
                    package.IsManuallyInstalled = true;
                    NugetPackageInstaller.InstallIdentifier(package, false);
                }

                selectedPackageInstalls.Clear();

                AssetDatabase.Refresh();
                UpdateInstalledPackages();
                UpdateUpdatePackages();
            }
        }

        private void DrawPackages(IEnumerable<INugetPackage> packages, bool canBeSelected = false)
        {
            var backgroundStyle = Styles.BackgroundStyle;

            foreach (var package in packages)
            {
                using (new EditorGUILayout.VerticalScope(backgroundStyle))
                {
                    DrawPackage(package, backgroundStyle, canBeSelected);
                }
            }
        }

        /// <summary>
        ///     Draws the header which allows filtering the online list of packages.
        /// </summary>
        private void DrawOnlineHeader()
        {
            using (new EditorGUILayout.VerticalScope(Styles.BackgroundStyle))
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    var showPrereleaseTemp = GUILayout.Toggle(
                        showOnlinePrerelease,
                        showPrereleaseContent,
                        EditorStyles.toolbarButton,
                        GUILayout.Width(120f));
                    if (showPrereleaseTemp != showOnlinePrerelease)
                    {
                        showOnlinePrerelease = showPrereleaseTemp;
                        UpdateOnlinePackages();
                    }

                    GUILayout.FlexibleSpace();
                    DrawSelectFromClipboardButton();
                    DrawMandatoryButtons();
                }

                using (new EditorGUILayout.HorizontalScope(Styles.ToolbarStyle))
                {
                    var enterPressed = Event.current.Equals(Event.KeyboardEvent("return"));

                    // draw search field
                    onlineSearchTerm = EditorGUILayout.TextField(onlineSearchTerm, Styles.SearchFieldStyle, GUILayout.Height(20));

                    if (GUILayout.Button("Search", GUILayout.Width(100), GUILayout.Height(20)))
                    {
                        // the search button emulates the Enter key
                        enterPressed = true;
                    }

                    // search only if the enter key is pressed
                    if (enterPressed)
                    {
                        // reset the number to skip
                        numberToSkip = 0;
                        UpdateOnlinePackages();
                    }
                }
            }
        }

        private void DrawSelectFromClipboardButton()
        {
            if (GUILayout.Button("Select all from clipboard", EditorStyles.toolbarButton, GUILayout.Width(170f)))
            {
                var packageIds = GUIUtility.systemCopyBuffer.Split('\n', ',').Select(p => p.Trim()).ToList();
                try
                {
                    for (var i = 0; i < packageIds.Count; i++)
                    {
                        var packageId = packageIds[i];
                        if (InstalledPackagesManager.IsInstalled(packageId, true))
                        {
                            continue;
                        }

                        var alreadyAvailablePackage = availablePackages.Find(package => package.Id == packageId);
                        if (alreadyAvailablePackage != null)
                        {
                            selectedPackageInstalls.Add(alreadyAvailablePackage);
                            continue;
                        }

                        EditorUtility.DisplayProgressBar("Searching", "Searching for packages", (float)i / packageIds.Count);
                        var packages = Task.Run(() => ConfigurationManager.SearchAsync(packageId, showOnlinePrerelease, numberToGet, numberToSkip))
                            .GetAwaiter()
                            .GetResult();
                        if (packages.Count > 0)
                        {
                            selectedPackageInstalls.Add(packages[0]);
                            availablePackages.Add(packages[0]);
                        }
                    }
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }
        }

        /// <summary>
        ///     Draws the header which allows filtering the installed list of packages.
        /// </summary>
        private void DrawInstalledHeader()
        {
            using (new EditorGUILayout.VerticalScope(Styles.HeaderStyle))
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    GUILayout.FlexibleSpace();

                    if (InstalledPackagesManager.InstalledPackages.Any())
                    {
                        if (GUILayout.Button("Uninstall All", EditorStyles.toolbarButton, GUILayout.Width(100)))
                        {
                            NugetPackageUninstaller.UninstallAll();
                            UpdateInstalledPackages();
                            UpdateUpdatePackages();
                        }
                    }

                    if (selectedPackageUninstalls.Count > 0)
                    {
                        if (GUILayout.Button("Uninstall Selected", EditorStyles.toolbarButton, GUILayout.Width(120)))
                        {
                            NugetPackageUninstaller.UninstallAll(selectedPackageUninstalls.ToList());
                            UpdateInstalledPackages();
                            UpdateUpdatePackages();
                        }
                    }

                    DrawMandatoryButtons();
                }

                using (new EditorGUILayout.HorizontalScope(Styles.ToolbarStyle))
                {
                    installedSearchTerm = EditorGUILayout.TextField(installedSearchTerm, Styles.SearchFieldStyle, GUILayout.Height(20));
                }
            }
        }

        /// <summary>
        ///     Draws the header for the Updates tab.
        /// </summary>
        private void DrawUpdatesHeader()
        {
            using (new EditorGUILayout.VerticalScope(Styles.HeaderStyle))
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    var showPrereleaseTemp = GUILayout.Toggle(
                        showPrereleaseUpdates,
                        showPrereleaseContent,
                        EditorStyles.toolbarButton,
                        GUILayout.Width(120f));
                    if (showPrereleaseTemp != showPrereleaseUpdates)
                    {
                        showPrereleaseUpdates = showPrereleaseTemp;
                        UpdateUpdatePackages();
                    }

                    var showDowngradesTemp = GUILayout.Toggle(
                        showDowngrades,
                        showDowngradesContent,
                        EditorStyles.toolbarButton,
                        GUILayout.Width(120f));
                    if (showDowngradesTemp != showDowngrades)
                    {
                        versionDropdownDataPerPackage.Clear();
                        showDowngrades = showDowngradesTemp;
                    }

                    GUILayout.FlexibleSpace();

                    if (updatePackages.Count > 0)
                    {
                        if (!showDowngrades && GUILayout.Button("Update All", EditorStyles.toolbarButton, GUILayout.Width(100)))
                        {
                            NugetPackageUpdater.UpdateAll(updatePackages, InstalledPackagesManager.InstalledPackages);
                            UpdateInstalledPackages();
                            UpdateUpdatePackages();
                        }

                        var workingSelections = SelectedPackages;
                        if (workingSelections.Count > 0)
                        {
                            if (GUILayout.Button(
                                    showDowngrades ? "Downgrade Selected" : "Update Selected",
                                    EditorStyles.toolbarButton,
                                    GUILayout.Width(120)))
                            {
                                NugetPackageUpdater.UpdateAll(workingSelections, InstalledPackagesManager.InstalledPackages);
                                UpdateInstalledPackages();
                                UpdateUpdatePackages();
                            }
                        }
                    }

                    DrawMandatoryButtons();
                }

                using (new EditorGUILayout.HorizontalScope(Styles.ToolbarStyle))
                {
                    updatesSearchTerm = EditorGUILayout.TextField(updatesSearchTerm, Styles.SearchFieldStyle, GUILayout.Height(20));
                }
            }
        }

        /// <summary>
        ///     Draws the "Refresh" and "Preferences" buttons in the upper right corner, which are visible in every tab.
        /// </summary>
        private void DrawMandatoryButtons()
        {
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                Refresh(true);
            }

            if (GUILayout.Button("Preferences", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                SettingsService.OpenUserPreferences("Preferences/NuGet For Unity");
                GetWindow<NugetWindow>().Close();
            }
        }

        /// <summary>
        ///     Draws the given <see cref="INugetPackage" />.
        /// </summary>
        /// <param name="package">The <see cref="INugetPackage" /> to draw.</param>
        /// <param name="backgroundStyle">The normal style of the package section.</param>
        /// <param name="canBeSelected">If a check-box should be shown.</param>
        private void DrawPackage(INugetPackage package, GUIStyle backgroundStyle, bool canBeSelected = false)
        {
            var installedPackages = InstalledPackagesManager.InstalledPackages;
            var installed = installedPackages.FirstOrDefault(p => p.Id.Equals(package.Id, StringComparison.OrdinalIgnoreCase));
            var isAlreadyImportedInEngine = UnityPreImportedLibraryResolver.IsAlreadyImportedInEngine(package.Id, false);

            GUILayout.Space(7f);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(7f);

                // The Unity GUI system (in the Editor) is terrible.  This probably requires some explanation.
                // Every time you use a Horizontal block, Unity appears to divide the space evenly.
                // (i.e. 2 components have half of the window width, 3 components have a third of the window width, etc)
                // GUILayoutUtility.GetRect is SUPPOSED to return a rect with the given height and width, but in the GUI layout.  It doesn't.
                // We have to use GUILayoutUtility to get SOME rect properties, but then manually calculate others.
                using (new EditorGUILayout.HorizontalScope())
                {
                    const int iconSize = 32;
                    var paddingX = 5f;
                    var rect = GUILayoutUtility.GetRect(0, iconSize);
                    rect.y += Math.Max(EditorStyles.label.padding.vertical, 3);
                    if (canBeSelected)
                    {
                        const int toggleSize = 18;
                        rect.x += toggleSize;
                        var workingSelections = SelectedPackages;
                        var isSelected = workingSelections.Contains(package);
                        var alreadyInstalled = installed != null || isAlreadyImportedInEngine;
                        using (new EditorGUI.DisabledScope(currentTab == NugetWindowTab.OnlineTab && alreadyInstalled))
                        {
                            var shouldBeSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Height(iconSize));
                            if (shouldBeSelected != isSelected)
                            {
                                if (shouldBeSelected)
                                {
                                    workingSelections.Add(package);
                                }
                                else
                                {
                                    workingSelections.Remove(package);
                                }
                            }
                        }
                    }

                    rect.x += paddingX;
                    rect.width = iconSize;
                    rect.height = iconSize;

                    var icon = defaultIcon;
                    if (package.IconTask != null && package.IconTask.IsCompleted && package.IconTask.Result != null)
                    {
                        // as this is called every frame we don't need to wait for the task we can just use the image if it is available
                        icon = package.IconTask.Result;
                    }
                    else if (installed != null && installed.IconTask != null && installed.IconTask.IsCompleted && installed.IconTask.Result != null)
                    {
                        // fallback to the icon of the already installed package (somehow there are cases where the update package has no icon URL)
                        // as this is called every frame we don't need to wait for the task we can just use the image if it is available
                        icon = installed.IconTask.Result;
                    }

                    if (icon != null)
                    {
                        GUI.DrawTexture(rect, icon, ScaleMode.StretchToFill);
                        rect.x += iconSize + paddingX;
                    }

                    // text is allowed to get the half of the available space rest is for buttons and version label
                    rect.width = (position.width - rect.x) / 2;

                    var labelStyle = Styles.PackageNameLabelStyle;
                    var idSize = labelStyle.CalcSize(new GUIContent(package.Id));
                    GUI.Label(rect, package.Id, labelStyle);
                    rect.x += Mathf.Min(idSize.x, rect.width) + paddingX;
                }

                GUILayout.FlexibleSpace();

                // Show the installed version of the package if it exists
                if (installed != null)
                {
                    GUILayout.Label($"Installed Version: {installed.PackageVersion.FullVersion}");
                }

                // Show the version selection dropdown only on Updates tab OR on Online tab if the package is not installed and not already in Unity
                if (currentTab == NugetWindowTab.UpdatesTab ||
                    (currentTab == NugetWindowTab.OnlineTab && installed == null && !isAlreadyImportedInEngine))
                {
                    if (package.Versions.Count <= 1)
                    {
                        GUILayout.Label($"Version {package.PackageVersion.FullVersion}");
                    }
                    else
                    {
                        if (!versionDropdownDataPerPackage.TryGetValue(package.Id, out var versionDropdownData))
                        {
                            EditorStyles.popup.CalcMinMaxWidth(
                                new GUIContent(
                                    package.Versions.Select(version => version.FullVersion).OrderByDescending(version => version.Length).First()),
                                out _,
                                out var maxWidth);

                            var sortedVersions = package.Versions;
                            if (installed != null)
                            {
                                sortedVersions = showDowngrades ?
                                    package.Versions.FindAll(version => version < installed.PackageVersion) :
                                    package.Versions.FindAll(version => version > installed.PackageVersion);
                            }

                            versionDropdownData = new VersionDropdownData { SortedVersions = sortedVersions, CalculatedMaxWith = maxWidth + 5 };

                            versionDropdownData.DropdownOptions = versionDropdownData.SortedVersions.Select(version => version.FullVersion).ToArray();

                            // Show the highest available update/downgrade first
                            versionDropdownData.SelectedIndex = 0;
                            versionDropdownDataPerPackage.Add(package.Id, versionDropdownData);
                        }

                        if (versionDropdownData.SortedVersions.Count > 0)
                        {
                            GUILayout.Label("Version");
                            versionDropdownData.SelectedIndex = EditorGUILayout.Popup(
                                versionDropdownData.SelectedIndex,
                                versionDropdownData.DropdownOptions,
                                GUILayout.Width(versionDropdownData.CalculatedMaxWith));
                            package.PackageVersion = versionDropdownData.SortedVersions[versionDropdownData.SelectedIndex];
                        }
                    }
                }

                var existsInUnity = installed == null && isAlreadyImportedInEngine;

                PluginRegistry.Instance.DrawButtons(package, installed, existsInUnity);

                if (installed != null)
                {
                    if (currentTab == NugetWindowTab.InstalledTab && !installed.IsManuallyInstalled)
                    {
                        if (GUILayout.Button("Add as explicit"))
                        {
                            InstalledPackagesManager.SetManuallyInstalledFlag(installed);
                            ClearViewCache();
                        }
                    }

                    if (currentTab == NugetWindowTab.UpdatesTab)
                    {
                        if (showDowngrades)
                        {
                            // Showing only downgrades
                            if (GUILayout.Button("Downgrade"))
                            {
                                NugetPackageUpdater.Update(installed, package);
                                UpdateInstalledPackages();
                                UpdateUpdatePackages();
                            }
                        }
                        else
                        {
                            // Showing only updates
                            if (GUILayout.Button("Update"))
                            {
                                NugetPackageUpdater.Update(installed, package);
                                UpdateInstalledPackages();
                                UpdateUpdatePackages();
                            }
                        }
                    }

                    if (currentTab == NugetWindowTab.InstalledTab && GUILayout.Button("Uninstall"))
                    {
                        NugetPackageUninstaller.Uninstall(installed, PackageUninstallReason.IndividualUninstall);
                        UpdateInstalledPackages();
                        UpdateUpdatePackages();
                    }
                }
                else
                {
                    using (new EditorGUI.DisabledScope(existsInUnity))
                    {
                        if (GUILayout.Button(new GUIContent("Install", null, existsInUnity ? "Already imported by Unity" : null)))
                        {
                            package.IsManuallyInstalled = true;
                            NugetPackageInstaller.InstallIdentifier(package);
                            UpdateInstalledPackages();
                            UpdateUpdatePackages();
                        }
                    }
                }
            }

            // authors
            {
                var rect = EditorGUILayout.GetControlRect().AddX(10f);
                var labelStyle = Styles.AuthorsLabelStyle;

                rect.y += labelStyle.fontSize / 2f;

                if (package.Authors.Count > 0)
                {
                    var authorLabel = $"by {string.Join(", ", package.Authors)}";
                    var size = labelStyle.CalcSize(new GUIContent(authorLabel));
                    GUI.Label(rect, authorLabel, labelStyle);
                    rect.x += size.x;
                }

                if (package.TotalDownloads > 0)
                {
                    var downloadLabel = $"{package.TotalDownloads:#,#} downloads";
                    GUI.Label(rect, downloadLabel, labelStyle);
                }
            }

            GUILayout.Space(7f);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    // Show the package details
                    var labelStyle = Styles.DescriptionLabelStyle;

                    var summary = package.Summary;
                    if (string.IsNullOrEmpty(summary))
                    {
                        summary = package.Description;
                    }

                    if (!string.IsNullOrEmpty(package.Title) && !package.Title.Equals(package.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        summary = $"{package.Title} - {summary}";
                    }

                    if (summary != null && summary.Length >= 240)
                    {
                        summary = $"{summary.Substring(0, 237)}...";
                    }

                    var summaryRect = EditorGUILayout.GetControlRect(
                            true,
                            labelStyle.CalcHeight(new GUIContent(summary), EditorGUIUtility.currentViewWidth - 20f) + 5f)
                        .AddX(10f);
                    EditorGUI.LabelField(summaryRect, summary, labelStyle);

                    var detailsFoldoutId = $"{package.Id}.Details";
                    if (!foldouts.TryGetValue(detailsFoldoutId, out var detailsFoldout))
                    {
                        foldouts[detailsFoldoutId] = detailsFoldout;
                    }

                    GUILayout.Space(2f);

                    var detailsFoldoutRect = EditorGUILayout.GetControlRect().AddX(10f);
                    detailsFoldout = EditorGUI.Foldout(detailsFoldoutRect, detailsFoldout, "Details", true);
                    foldouts[detailsFoldoutId] = detailsFoldout;

                    if (detailsFoldout)
                    {
                        EditorGUI.indentLevel += 2;
                        if (!string.IsNullOrEmpty(package.Description))
                        {
                            EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
                            var descriptionContent = new GUIContent(package.Description);
                            var descriptionRect = EditorGUILayout.GetControlRect(
                                true,
                                labelStyle.CalcHeight(descriptionContent, EditorGUIUtility.currentViewWidth - 20f) + 12f);
                            EditorGUI.LabelField(descriptionRect, descriptionContent, labelStyle);
                            GUILayout.Space(4f);
                        }

                        if (!string.IsNullOrEmpty(package.ReleaseNotes))
                        {
                            EditorGUILayout.LabelField("Release Notes", EditorStyles.boldLabel);
                            EditorGUILayout.LabelField(package.ReleaseNotes);
                            GUILayout.Space(4f);
                        }

                        // Show project URL link
                        if (!string.IsNullOrEmpty(package.ProjectUrl))
                        {
                            EditorGUILayout.LabelField("Project URL", EditorStyles.boldLabel);
                            GUILayoutLink(package.ProjectUrl);
                            GUILayout.Space(4f);
                        }

                        // Show the dependencies
                        if (package.GetDependenciesAsync().IsCompleted)
                        {
                            var frameworkDependencies = package.CurrentFrameworkDependencies;
                            if (frameworkDependencies.Count > 0)
                            {
                                EditorGUILayout.LabelField("Dependencies", EditorStyles.boldLabel);
                                labelStyle.fontStyle = FontStyle.Italic;
                                cachedStringBuilder.Clear();

                                foreach (var dependency in frameworkDependencies)
                                {
                                    cachedStringBuilder.AppendLine($"{dependency.Id} {dependency.Version}");
                                }

                                EditorGUILayout.LabelField(cachedStringBuilder.ToString(), labelStyle);
                                labelStyle.fontStyle = FontStyle.Normal;
                            }
                        }
                        else
                        {
                            EditorGUILayout.LabelField("Loading dependencies...");
                        }

                        // Create the style for putting a box around the 'Clone' button
                        var cloneButtonBoxStyle =
                            new GUIStyle("box") { stretchWidth = false, margin = { top = 0, bottom = 0 }, padding = { bottom = 4 } };

                        var normalButtonBoxStyle = new GUIStyle(cloneButtonBoxStyle) { normal = { background = backgroundStyle.normal.background } };

                        var showCloneWindow = openCloneWindows.Contains(package);
                        cloneButtonBoxStyle.normal.background = backgroundStyle.normal.background;

                        // Create a similar style for the 'Clone' window
                        var cloneWindowStyle = new GUIStyle(cloneButtonBoxStyle) { padding = new RectOffset(6, 6, 2, 6) };

                        if (package.RepositoryType == RepositoryType.Git || package.RepositoryType == RepositoryType.TfsGit)
                        {
                            if (!string.IsNullOrEmpty(package.RepositoryUrl))
                            {
                                var buttonRect = EditorGUILayout.GetControlRect();
                                buttonRect.width = 116f;
                                buttonRect.xMin += 31f;

                                // Show the clone button
                                if (GUI.Button(buttonRect, "Clone"))
                                {
                                    showCloneWindow = !showCloneWindow;
                                }

                                if (showCloneWindow)
                                {
                                    openCloneWindows.Add(package);
                                }
                                else
                                {
                                    openCloneWindows.Remove(package);
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(package.LicenseUrl) && package.LicenseUrl != "http://your_license_url_here")
                        {
                            var buttonRect = EditorGUILayout.GetControlRect();
                            buttonRect.width = 116f;
                            buttonRect.xMin += 31f;

                            // Show the license button
                            if (GUI.Button(buttonRect, "View License"))
                            {
                                Application.OpenURL(package.LicenseUrl);
                            }
                        }

                        if (showCloneWindow)
                        {
                            var oldIndent = EditorGUI.indentLevel;
                            EditorGUI.indentLevel = 0;

                            using (new EditorGUILayout.HorizontalScope())
                            {
                                GUILayout.Space(23f);
                                using (new EditorGUILayout.VerticalScope(cloneWindowStyle))
                                {
                                    // Clone latest label
                                    EditorGUILayout.LabelField("clone latest");

                                    // Clone latest row
                                    using (new EditorGUILayout.HorizontalScope())
                                    {
                                        if (GUILayout.Button("Copy", GUILayout.ExpandWidth(false)))
                                        {
                                            GUI.FocusControl(package.Id + package.Version + "repoUrl");
                                            GUIUtility.systemCopyBuffer = package.RepositoryUrl;
                                        }

                                        GUI.SetNextControlName(package.Id + package.Version + "repoUrl");
                                        EditorGUILayout.TextField(package.RepositoryUrl);
                                    }

                                    // Clone @ commit label
                                    GUILayout.Space(4f);
                                    EditorGUILayout.LabelField("clone @ commit");

                                    // Clone @ commit row
                                    using (new EditorGUILayout.HorizontalScope())
                                    {
                                        // Create the three commands a user will need to run to get the repo @ the commit. Intentionally leave off the last newline for better UI appearance
                                        var commands = string.Format(
                                            CultureInfo.InvariantCulture,
                                            "git clone {0} {1} --no-checkout{2}cd {1}{2}git checkout {3}",
                                            package.RepositoryUrl,
                                            package.Id,
                                            Environment.NewLine,
                                            package.RepositoryCommit);

                                        if (GUILayout.Button("Copy", GUILayout.ExpandWidth(false)))
                                        {
                                            GUI.FocusControl(package.Id + package.Version + "commands");

                                            // Add a newline so the last command will execute when pasted to the CL
                                            GUIUtility.systemCopyBuffer = commands + Environment.NewLine;
                                        }

                                        using (new EditorGUILayout.VerticalScope())
                                        {
                                            GUI.SetNextControlName(package.Id + package.Version + "commands");
                                            EditorGUILayout.TextArea(commands);
                                        }
                                    }
                                }
                            }

                            EditorGUI.indentLevel = oldIndent;
                        }

                        EditorGUI.indentLevel -= 2;
                    }

                    EditorGUILayout.Space();
                }
            }

            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1f), Styles.LineColor);
        }

        private sealed class VersionDropdownData
        {
            public int SelectedIndex { get; set; }

            public List<NugetPackageVersion> SortedVersions { get; set; }

            public float CalculatedMaxWith { get; set; }

            public string[] DropdownOptions { get; set; }
        }
    }
}
