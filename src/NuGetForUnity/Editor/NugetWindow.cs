using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace NugetForUnity
{
    /// <summary>
    ///     Represents the NuGet Package Manager Window in the Unity Editor.
    /// </summary>
    public class NugetWindow : EditorWindow
    {
        private const string UpmPackageName = "com.github-glitchenzo.nugetforunity";

        private const string UpmPackageGitUrl = "https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity";

        private const string GitHubReleasesPageUrl = "https://github.com/GlitchEnzo/NuGetForUnity/releases";

        private const string GitHubReleasesApiUrl = "https://api.github.com/repos/GlitchEnzo/NuGetForUnity/releases?per_page=10";

        private static GUIStyle cachedHeaderStyle;

        private static GUIStyle cachedBackgroundStyle;

        private static GUIStyle cachedFoldoutStyle;

        private static GUIStyle cachedContrastStyle;

        private readonly Dictionary<string, bool> foldouts = new Dictionary<string, bool>();

        /// <summary>
        ///     The number of packages to get from the request to the server.
        /// </summary>
        private readonly int numberToGet = 15;

        /// <summary>
        ///     Used to keep track of which packages the user has opened the clone window on.
        /// </summary>
        private readonly HashSet<NugetPackage> openCloneWindows = new HashSet<NugetPackage>();

        /// <summary>
        ///     Used to keep track of which packages are selected for uninstalling or updating.
        /// </summary>
        private readonly HashSet<NugetPackage> selectedPackages = new HashSet<NugetPackage>();

        /// <summary>
        ///     The titles of the tabs in the window.
        /// </summary>
        private readonly string[] tabTitles = { "Online", "Installed", "Updates" };

        /// <summary>
        ///     The list of NugetPackages available to install.
        /// </summary>
        [SerializeField]
        private List<NugetPackage> availablePackages = new List<NugetPackage>();

        /// <summary>
        ///     The currently selected tab in the window.
        /// </summary>
        private int currentTab;

        /// <summary>
        ///     The default icon to display for packages.
        /// </summary>
        [SerializeField]
        private Texture2D defaultIcon;

        private List<NugetPackage> filteredInstalledPackages;

        /// <summary>
        ///     True when the NugetWindow has initialized. This is used to skip time-consuming reloading operations when the assembly is reloaded.
        /// </summary>
        [SerializeField]
        private bool hasRefreshed;

        /// <summary>
        ///     The search term to search the installed packages for.
        /// </summary>
        private string installedSearchTerm = "Search";

        private string lastInstalledSearchTerm;

        /// <summary>
        ///     The number of packages to skip when requesting a list of packages from the server.  This is used to get a new group of packages.
        /// </summary>
        [SerializeField]
        private int numberToSkip;

        /// <summary>
        ///     The search term to search the online packages for.
        /// </summary>
        private string onlineSearchTerm = "Search";

        /// <summary>
        ///     The current position of the scroll bar in the GUI.
        /// </summary>
        private Vector2 scrollPosition;

        /// <summary>
        ///     True to show all old package versions.  False to only show the latest version.
        /// </summary>
        private bool showAllOnlineVersions;

        /// <summary>
        ///     True to show all old package versions.  False to only show the latest version.
        /// </summary>
        private bool showAllUpdateVersions;

        private bool showImplicitlyInstalled;

        /// <summary>
        ///     True to show beta and alpha package versions.  False to only show stable versions.
        /// </summary>
        private bool showOnlinePrerelease;

        /// <summary>
        ///     True to show beta and alpha package versions.  False to only show stable versions.
        /// </summary>
        private bool showPrereleaseUpdates;

        /// <summary>
        ///     The list of package updates available, based on the already installed packages.
        /// </summary>
        [SerializeField]
        private List<NugetPackage> updatePackages = new List<NugetPackage>();

        /// <summary>
        ///     The search term to search the update packages for.
        /// </summary>
        private string updatesSearchTerm = "Search";

        /// <summary>
        ///     The filtered list of package updates available.
        /// </summary>
        private List<NugetPackage> FilteredUpdatePackages
        {
            get
            {
                if (string.IsNullOrWhiteSpace(updatesSearchTerm) || updatesSearchTerm == "Search")
                {
                    return updatePackages;
                }

                return updatePackages.Where(
                        package => package.Id.IndexOf(updatesSearchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                                   package.Title.IndexOf(updatesSearchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0)
                    .ToList();
            }
        }

        private List<NugetPackage> FilteredInstalledPackages
        {
            get
            {
                if (filteredInstalledPackages != null && lastInstalledSearchTerm == installedSearchTerm)
                {
                    return filteredInstalledPackages;
                }

                lastInstalledSearchTerm = installedSearchTerm;
                if (string.IsNullOrWhiteSpace(installedSearchTerm) || installedSearchTerm == "Search")
                {
                    filteredInstalledPackages = NugetHelper.InstalledPackages.ToList();
                }
                else
                {
                    filteredInstalledPackages = NugetHelper.InstalledPackages.Where(
                            package => package.Id.IndexOf(installedSearchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                                       package.Title.IndexOf(installedSearchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0)
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

        /// <summary>
        ///     Opens the NuGet Package Manager Window.
        /// </summary>
        [MenuItem("NuGet/Manage NuGet Packages", false, 0)]
        protected static void DisplayNugetWindow()
        {
            GetWindow<NugetWindow>();
        }

        /// <summary>
        ///     Restores all packages defined in packages.config
        /// </summary>
        [MenuItem("NuGet/Restore Packages", false, 1)]
        protected static void RestorePackages()
        {
            NugetHelper.Restore();
            foreach (var nugetWindow in Resources.FindObjectsOfTypeAll<NugetWindow>())
            {
                nugetWindow.ClearViewCache();
            }
        }

        /// <summary>
        ///     Displays the version number of NuGetForUnity.
        /// </summary>
        [MenuItem("NuGet/Version " + NugetPreferences.NuGetForUnityVersion, false, 10)]
        protected static void DisplayVersion()
        {
            // open the preferences window
            SettingsService.OpenUserPreferences("Preferences/NuGet For Unity");
        }

        /// <summary>
        ///     Checks/launches the Releases page to update NuGetForUnity with a new version.
        /// </summary>
        [MenuItem("NuGet/Check for Updates...", false, 10)]
        protected static void CheckForUpdates()
        {
            var request = UnityWebRequest.Get(GitHubReleasesApiUrl);
            var operation = request.SendWebRequest();
            NugetHelper.LogVerbose("HTTP GET {0}", GitHubReleasesApiUrl);
            EditorUtility.DisplayProgressBar("Checking updates", null, 0.0f);

            operation.completed += asyncOperation =>
            {
                try
                {
                    string latestVersion = null;
                    string latestVersionDownloadUrl = null;
                    string response = null;
                    if (string.IsNullOrEmpty(request.error))
                    {
                        response = request.downloadHandler.text;
                    }

                    if (response != null)
                    {
                        latestVersion = GetLatestVersonFromReleasesApi(response, out latestVersionDownloadUrl);
                    }

                    EditorUtility.ClearProgressBar();

                    if (latestVersion == null)
                    {
                        EditorUtility.DisplayDialog(
                            "Unable to Determine Updates",
                            string.Format("Couldn't find release information at {0}. Error: {1}", GitHubReleasesApiUrl, request.error),
                            "OK");
                        return;
                    }

                    var current = new NugetPackageIdentifier("NuGetForUnity", NugetPreferences.NuGetForUnityVersion);
                    var latest = new NugetPackageIdentifier("NuGetForUnity", latestVersion);
                    if (current >= latest)
                    {
                        EditorUtility.DisplayDialog(
                            "No Updates Available",
                            string.Format("Your version of NuGetForUnity is up to date.\nVersion {0}.", NugetPreferences.NuGetForUnityVersion),
                            "OK");
                        return;
                    }

                    // New version is available. Give user options for installing it.
                    switch (EditorUtility.DisplayDialogComplex(
                                "Update Available",
                                string.Format("Current Version: {0}\nLatest Version: {1}", NugetPreferences.NuGetForUnityVersion, latestVersion),
                                "Install Latest",
                                "Open Releases Page",
                                "Cancel"))
                    {
                        case 0:
                            new NuGetForUnityUpdateInstaller(latestVersionDownloadUrl);
                            break;
                        case 1:
                            Application.OpenURL(GitHubReleasesPageUrl);
                            break;
                        case 2:
                            break;
                    }
                }
                finally
                {
                    request.Dispose();
                }
            };
        }

        private static string GetLatestVersonFromReleasesApi(string response, out string unitypackageDownloadUrl)
        {
            // JsonUtility doesn't support top level arrays so we wrap it inside a object.
            var releases = JsonUtility.FromJson<GitHubReleaseApiRequestList>(string.Concat("{ \"list\": ", response, " }"));
            foreach (var release in releases.list)
            {
                // skip beta versions e.g. v2.0.0-preview
                if (release.tag_name.Contains('-'))
                {
                    continue;
                }

                unitypackageDownloadUrl = release.assets
                    .FirstOrDefault(asset => asset.name.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
                    ?.browser_download_url;
                return release.tag_name.TrimStart('v');
            }

            unitypackageDownloadUrl = null;
            return null;
        }

        /// <summary>
        ///     Called when enabling the window.
        /// </summary>
        private void OnEnable()
        {
            Refresh(false);
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
                    NugetHelper.ClearCachedCredentials();
                }

                // reload the NuGet.config file, in case it was changed after Unity opened, but before the manager window opened (now)
                NugetHelper.LoadNugetConfigFile();

                // if we are entering playmode, don't do anything
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    return;
                }

                NugetHelper.LogVerbose(hasRefreshed ? "NugetWindow reloading config" : "NugetWindow reloading config and updating packages");

                // set the window title
                titleContent = new GUIContent("NuGet");

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

                NugetHelper.LogVerbose("NugetWindow reloading took {0} ms", stopwatch.ElapsedMilliseconds);
            }
        }

        /// <summary>
        ///     Updates the list of available packages by running a search with the server using the currently set parameters (# to get, # to skip, etc).
        /// </summary>
        private void UpdateOnlinePackages()
        {
            availablePackages = NugetHelper.Search(
                onlineSearchTerm != "Search" ? onlineSearchTerm : string.Empty,
                showAllOnlineVersions,
                showOnlinePrerelease,
                numberToGet,
                numberToSkip);
        }

        private void UpdateInstalledPackages()
        {
            NugetHelper.UpdateInstalledPackages();
            ClearViewCache();
        }

        /// <summary>
        ///     Updates the list of update packages.
        /// </summary>
        private void UpdateUpdatePackages()
        {
            // get any available updates for the installed packages
            updatePackages = NugetHelper.GetUpdates(NugetHelper.InstalledPackages, showPrereleaseUpdates, showAllUpdateVersions);
        }

        /// <summary>
        ///     From here: http://forum.unity3d.com/threads/changing-the-background-color-for-beginhorizontal.66015/
        /// </summary>
        /// <param name="color">The color to fill the texture with.</param>
        /// <returns>The generated texture.</returns>
        private static Texture2D CreateSingleColorTexture(Color color)
        {
            const int width = 16;
            const int height = 16;
            var pix = new Color32[width * height];
            Color32 color32 = color;
            for (var index = 0; index < pix.Length; index++)
            {
                pix[index] = color32;
            }

            var result = new Texture2D(width, height);
            result.SetPixels32(pix);
            result.Apply();

            return result;
        }

        /// <summary>
        ///     Automatically called by Unity to draw the GUI.
        /// </summary>
        protected void OnGUI()
        {
            var selectedTab = GUILayout.Toolbar(currentTab, tabTitles);

            if (selectedTab != currentTab)
            {
                OnTabChanged();
            }

            currentTab = selectedTab;

            switch (currentTab)
            {
                case 0:
                    DrawOnline();
                    break;
                case 1:
                    DrawInstalled();
                    break;
                case 2:
                    DrawUpdates();
                    break;
            }
        }

        private void OnTabChanged()
        {
            selectedPackages.Clear();
            openCloneWindows.Clear();
            ResetScrollPosition();
        }

        private void ResetScrollPosition()
        {
            scrollPosition.y = 0f;
        }

        /// <summary>
        ///     Creates a GUI style with a contrasting background color based upon if the Unity Editor is the free (light) skin or the Pro (dark) skin.
        /// </summary>
        /// <returns>A GUI style with the appropriate background color set.</returns>
        private static GUIStyle GetContrastStyle()
        {
            if (cachedContrastStyle != null)
            {
                return cachedContrastStyle;
            }

            cachedContrastStyle = new GUIStyle();
            var backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.6f, 0.6f, 0.6f);
            cachedContrastStyle.normal.background = CreateSingleColorTexture(backgroundColor);

            return cachedContrastStyle;
        }

        /// <summary>
        ///     Creates a GUI style with a background color the same as the editor's current background color.
        /// </summary>
        /// <returns>A GUI style with the appropriate background color set.</returns>
        private static GUIStyle GetBackgroundStyle()
        {
            if (cachedBackgroundStyle != null)
            {
                return cachedBackgroundStyle;
            }

            cachedBackgroundStyle = new GUIStyle();
            var backgroundColor = EditorGUIUtility.isProSkin ? new Color32(56, 56, 56, 255) : new Color32(194, 194, 194, 255);
            cachedBackgroundStyle.normal.background = CreateSingleColorTexture(backgroundColor);

            return cachedBackgroundStyle;
        }

        private static GUIStyle GetHeaderStyle()
        {
            if (cachedHeaderStyle != null)
            {
                return cachedHeaderStyle;
            }

            cachedHeaderStyle = new GUIStyle();
            var backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.1f, 0.1f, 0.1f) : new Color(0.4f, 0.4f, 0.4f);
            cachedHeaderStyle.alignment = TextAnchor.MiddleLeft;
            cachedHeaderStyle.normal.background = CreateSingleColorTexture(backgroundColor);
            cachedHeaderStyle.normal.textColor = Color.white;

            return cachedHeaderStyle;
        }

        private static GUIStyle GetFoldoutStyle()
        {
            if (cachedFoldoutStyle != null)
            {
                return cachedFoldoutStyle;
            }

            cachedFoldoutStyle = new GUIStyle(EditorStyles.foldout)
            {
                focused = { textColor = Color.white },
                onFocused = { textColor = Color.white },
                active = { textColor = Color.white },
                onActive = { textColor = Color.white },
                alignment = TextAnchor.MiddleLeft,
            };

            return cachedFoldoutStyle;
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
            if (filteredUpdatePackages != null && filteredUpdatePackages.Count > 0)
            {
                DrawPackages(filteredUpdatePackages, true);
            }
            else
            {
                EditorStyles.label.fontStyle = FontStyle.Bold;
                EditorStyles.label.fontSize = 14;
                EditorGUILayout.LabelField("There are no updates available!", GUILayout.Height(20));
                EditorStyles.label.fontSize = 10;
                EditorStyles.label.fontStyle = FontStyle.Normal;
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
                var headerStyle = GetHeaderStyle();

                EditorGUILayout.LabelField("Installed packages", headerStyle, GUILayout.Height(20));
                DrawPackages(installedPackages.TakeWhile(package => package.IsManuallyInstalled), true);

                var rectangle = EditorGUILayout.GetControlRect(true, 20f, headerStyle);
                EditorGUI.LabelField(rectangle, "", headerStyle);

                showImplicitlyInstalled = EditorGUI.Foldout(
                    rectangle,
                    showImplicitlyInstalled,
                    "Implicitly installed packages",
                    true,
                    GetFoldoutStyle());
                if (showImplicitlyInstalled)
                {
                    DrawPackages(installedPackages.SkipWhile(package => package.IsManuallyInstalled), true);
                }
            }
            else
            {
                EditorStyles.label.fontStyle = FontStyle.Bold;
                EditorStyles.label.fontSize = 14;
                EditorGUILayout.LabelField("There are no packages installed!", GUILayout.Height(20));
                EditorStyles.label.fontSize = 10;
                EditorStyles.label.fontStyle = FontStyle.Normal;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
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

            if (availablePackages != null)
            {
                DrawPackages(availablePackages);
            }

            var showMoreStyle = GetHeaderStyle();
            EditorGUILayout.BeginVertical(showMoreStyle);

            // allow the user to display more results
            if (GUILayout.Button("Show More", GUILayout.Width(120)))
            {
                numberToSkip += numberToGet;
                availablePackages.AddRange(
                    NugetHelper.Search(
                        onlineSearchTerm != "Search" ? onlineSearchTerm : string.Empty,
                        showAllOnlineVersions,
                        showOnlinePrerelease,
                        numberToGet,
                        numberToSkip));
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawPackages(IEnumerable<NugetPackage> packages, bool canBeSelected = false)
        {
            var backgroundStyle = GetBackgroundStyle();
            var contrastStyle = GetContrastStyle();

            foreach (var package in packages)
            {
                EditorGUILayout.BeginVertical(backgroundStyle);
                DrawPackage(package, backgroundStyle, contrastStyle, canBeSelected);
                EditorGUILayout.EndVertical();

                // swap styles
                (backgroundStyle, contrastStyle) = (contrastStyle, backgroundStyle);
            }
        }

        /// <summary>
        ///     Draws the header which allows filtering the online list of packages.
        /// </summary>
        private void DrawOnlineHeader()
        {
            var headerStyle = GetHeaderStyle();

            EditorGUILayout.BeginVertical(headerStyle);
            {
                EditorGUILayout.BeginHorizontal();
                {
                    var showAllVersionsTemp = EditorGUILayout.Toggle("Show All Versions", showAllOnlineVersions);
                    if (showAllVersionsTemp != showAllOnlineVersions)
                    {
                        showAllOnlineVersions = showAllVersionsTemp;
                        UpdateOnlinePackages();
                    }

                    DrawMandatoryButtons();
                }
                EditorGUILayout.EndHorizontal();

                var showPrereleaseTemp = EditorGUILayout.Toggle("Show Prerelease", showOnlinePrerelease);
                if (showPrereleaseTemp != showOnlinePrerelease)
                {
                    showOnlinePrerelease = showPrereleaseTemp;
                    UpdateOnlinePackages();
                }

                var enterPressed = Event.current.Equals(Event.KeyboardEvent("return"));

                EditorGUILayout.BeginHorizontal();
                {
                    var oldFontSize = GUI.skin.textField.fontSize;
                    GUI.skin.textField.fontSize = 25;
                    onlineSearchTerm = EditorGUILayout.TextField(onlineSearchTerm, GUILayout.Height(30));

                    if (GUILayout.Button("Search", GUILayout.Width(100), GUILayout.Height(28)))
                    {
                        // the search button emulates the Enter key
                        enterPressed = true;
                    }

                    GUI.skin.textField.fontSize = oldFontSize;
                }
                EditorGUILayout.EndHorizontal();

                // search only if the enter key is pressed
                if (enterPressed)
                {
                    // reset the number to skip
                    numberToSkip = 0;
                    UpdateOnlinePackages();
                }
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        ///     Draws the header which allows filtering the installed list of packages.
        /// </summary>
        private void DrawInstalledHeader()
        {
            var headerStyle = GetHeaderStyle();

            EditorGUILayout.BeginVertical(headerStyle);
            {
                EditorGUILayout.BeginHorizontal();
                {
                    GUILayout.FlexibleSpace();

                    if (NugetHelper.InstalledPackages.Any())
                    {
                        if (GUILayout.Button("Uninstall All", GUILayout.Width(100)))
                        {
                            NugetHelper.UninstallAll(NugetHelper.InstalledPackages.ToList());
                            UpdateInstalledPackages();
                            UpdateUpdatePackages();
                        }
                    }

                    if (NugetHelper.InstalledPackages.Any(selectedPackages.Contains))
                    {
                        if (GUILayout.Button("Uninstall Selected", GUILayout.Width(120)))
                        {
                            NugetHelper.UninstallAll(NugetHelper.InstalledPackages.Where(selectedPackages.Contains).ToList());
                            UpdateInstalledPackages();
                            UpdateUpdatePackages();
                        }
                    }

                    DrawMandatoryButtons();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                {
                    var oldFontSize = GUI.skin.textField.fontSize;
                    GUI.skin.textField.fontSize = 25;
                    installedSearchTerm = EditorGUILayout.TextField(installedSearchTerm, GUILayout.Height(30));

                    GUI.skin.textField.fontSize = oldFontSize;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        ///     Draws the header for the Updates tab.
        /// </summary>
        private void DrawUpdatesHeader()
        {
            var headerStyle = GetHeaderStyle();

            EditorGUILayout.BeginVertical(headerStyle);
            {
                EditorGUILayout.BeginHorizontal();
                {
                    var showAllVersionsTemp = EditorGUILayout.Toggle("Show All Versions", showAllUpdateVersions);
                    if (showAllVersionsTemp != showAllUpdateVersions)
                    {
                        showAllUpdateVersions = showAllVersionsTemp;
                        UpdateUpdatePackages();
                    }

                    if (updatePackages.Count > 0)
                    {
                        if (GUILayout.Button("Update All", GUILayout.Width(100)))
                        {
                            NugetHelper.UpdateAll(updatePackages, NugetHelper.InstalledPackages);
                            UpdateInstalledPackages();
                            UpdateUpdatePackages();
                        }

                        if (updatePackages.Any(selectedPackages.Contains))
                        {
                            if (GUILayout.Button("Update Selected", GUILayout.Width(120)))
                            {
                                NugetHelper.UpdateAll(updatePackages.Where(selectedPackages.Contains), NugetHelper.InstalledPackages);
                                UpdateInstalledPackages();
                                UpdateUpdatePackages();
                            }
                        }
                    }

                    DrawMandatoryButtons();
                }
                EditorGUILayout.EndHorizontal();

                var showPrereleaseTemp = EditorGUILayout.Toggle("Show Prerelease", showPrereleaseUpdates);
                if (showPrereleaseTemp != showPrereleaseUpdates)
                {
                    showPrereleaseUpdates = showPrereleaseTemp;
                    UpdateUpdatePackages();
                }

                EditorGUILayout.BeginHorizontal();
                {
                    var oldFontSize = GUI.skin.textField.fontSize;
                    GUI.skin.textField.fontSize = 25;
                    updatesSearchTerm = EditorGUILayout.TextField(updatesSearchTerm, GUILayout.Height(30));

                    GUI.skin.textField.fontSize = oldFontSize;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        ///     Draws the "Refresh" and "Preferences" buttons in the upper right corner, which are visible in every tab.
        /// </summary>
        private void DrawMandatoryButtons()
        {
            if (GUILayout.Button("Refresh", GUILayout.Width(60)))
            {
                Refresh(true);
            }

            if (GUILayout.Button("Preferences", GUILayout.Width(80)))
            {
                SettingsService.OpenUserPreferences("Preferences/NuGet For Unity");
                GetWindow<NugetWindow>().Close();
            }
        }

        /// <summary>
        ///     Draws the given <see cref="NugetPackage" />.
        /// </summary>
        /// <param name="package">The <see cref="NugetPackage" /> to draw.</param>
        private void DrawPackage(NugetPackage package, GUIStyle packageStyle, GUIStyle contrastStyle, bool canBeSelected = false)
        {
            var installedPackages = NugetHelper.InstalledPackages;
            var installed = installedPackages.FirstOrDefault(p => p.Id == package.Id);

            EditorGUILayout.BeginHorizontal();
            {
                // The Unity GUI system (in the Editor) is terrible.  This probably requires some explanation.
                // Every time you use a Horizontal block, Unity appears to divide the space evenly.
                // (i.e. 2 components have half of the window width, 3 components have a third of the window width, etc)
                // GUILayoutUtility.GetRect is SUPPOSED to return a rect with the given height and width, but in the GUI layout.  It doesn't.
                // We have to use GUILayoutUtility to get SOME rect properties, but then manually calculate others.
                EditorGUILayout.BeginHorizontal();
                {
                    const int iconSize = 32;
                    var paddingX = Math.Max(EditorStyles.label.padding.horizontal, 3);
                    var rect = GUILayoutUtility.GetRect(0, iconSize);
                    rect.y += Math.Max(EditorStyles.label.padding.vertical, 3);
                    if (canBeSelected)
                    {
                        const int toggleSize = 18;
                        rect.x += toggleSize;
                        var isSelected = selectedPackages.Contains(package);
                        var shouldBeSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Height(iconSize));
                        if (shouldBeSelected != isSelected)
                        {
                            if (shouldBeSelected)
                            {
                                selectedPackages.Add(package);
                            }
                            else
                            {
                                selectedPackages.Remove(package);
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

                    EditorStyles.label.fontStyle = FontStyle.Bold;
                    EditorStyles.label.fontSize = 16;

                    var idSize = EditorStyles.label.CalcSize(new GUIContent(package.Id));
                    GUI.Label(rect, package.Id, EditorStyles.label);
                    rect.x += Mathf.Min(idSize.x, rect.width) + paddingX;

                    EditorStyles.label.fontSize = 10;
                    EditorStyles.label.fontStyle = FontStyle.Normal;
                    rect.y += EditorStyles.label.fontSize / 2f;

                    if (!string.IsNullOrEmpty(package.Authors))
                    {
                        var authorLabel = $"by {package.Authors}";
                        var size = EditorStyles.label.CalcSize(new GUIContent(authorLabel));
                        GUI.Label(rect, authorLabel, EditorStyles.label);
                        rect.x += size.x + paddingX;
                    }

                    if (package.DownloadCount > 0)
                    {
                        var downloadLabel = $"{package.DownloadCount:#,#} downloads";
                        GUI.Label(rect, downloadLabel, EditorStyles.label);
                    }
                }

                GUILayout.FlexibleSpace();
                if (installed != null && installed.Version != package.Version)
                {
                    GUILayout.Label($"Current Version {installed.Version}");
                }

                GUILayout.Label($"Version {package.Version}");

                if (installed != null)
                {
                    if (!installed.IsManuallyInstalled && package.PackageSource == null)
                    {
                        if (GUILayout.Button("Add as explicit"))
                        {
                            NugetHelper.SetManuallyInstalledFlag(installed);
                            ClearViewCache();
                        }
                    }

                    if (installed < package)
                    {
                        // An older version is installed
                        if (GUILayout.Button("Update"))
                        {
                            NugetHelper.Update(installed, package);
                            UpdateInstalledPackages();
                            UpdateUpdatePackages();
                        }
                    }
                    else if (installed > package)
                    {
                        // A newer version is installed
                        if (GUILayout.Button("Downgrade"))
                        {
                            NugetHelper.Update(installed, package);
                            UpdateInstalledPackages();
                            UpdateUpdatePackages();
                        }
                    }

                    if (GUILayout.Button("Uninstall"))
                    {
                        NugetHelper.Uninstall(installed);
                        UpdateInstalledPackages();
                        UpdateUpdatePackages();
                    }
                }
                else
                {
                    var alreadyInstalled = NugetHelper.IsAlreadyImportedInEngine(package, false);
                    using (new EditorGUI.DisabledScope(alreadyInstalled))
                    {
                        if (GUILayout.Button(new GUIContent("Install", null, alreadyInstalled ? "Already imported by Unity" : null)))
                        {
                            package.IsManuallyInstalled = true;
                            NugetHelper.InstallIdentifier(package);
                            UpdateInstalledPackages();
                            UpdateUpdatePackages();
                        }
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.BeginVertical();
                {
                    // Show the package details
                    EditorStyles.label.wordWrap = true;
                    EditorStyles.label.fontStyle = FontStyle.Normal;

                    var summary = package.Summary;
                    if (string.IsNullOrEmpty(summary))
                    {
                        summary = package.Description;
                    }

                    if (!package.Title.Equals(package.Id, StringComparison.InvariantCultureIgnoreCase))
                    {
                        summary = string.Format("{0} - {1}", package.Title, summary);
                    }

                    if (summary.Length >= 240)
                    {
                        summary = string.Format("{0}...", summary.Substring(0, 237));
                    }

                    EditorGUILayout.LabelField(summary);

                    bool detailsFoldout;
                    var detailsFoldoutId = string.Format("{0}.{1}", package.Id, "Details");
                    if (!foldouts.TryGetValue(detailsFoldoutId, out detailsFoldout))
                    {
                        foldouts[detailsFoldoutId] = detailsFoldout;
                    }

                    detailsFoldout = EditorGUILayout.Foldout(detailsFoldout, "Details");
                    foldouts[detailsFoldoutId] = detailsFoldout;

                    if (detailsFoldout)
                    {
                        EditorGUI.indentLevel++;
                        if (!string.IsNullOrEmpty(package.Description))
                        {
                            EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
                            EditorGUILayout.LabelField(package.Description);
                        }

                        if (!string.IsNullOrEmpty(package.ReleaseNotes))
                        {
                            EditorGUILayout.LabelField("Release Notes", EditorStyles.boldLabel);
                            EditorGUILayout.LabelField(package.ReleaseNotes);
                        }

                        // Show project URL link
                        if (!string.IsNullOrEmpty(package.ProjectUrl))
                        {
                            EditorGUILayout.LabelField("Project Url", EditorStyles.boldLabel);
                            GUILayoutLink(package.ProjectUrl);
                            GUILayout.Space(4f);
                        }

                        // Show the dependencies
                        if (package.Dependencies.Count > 0)
                        {
                            EditorStyles.label.wordWrap = true;
                            EditorStyles.label.fontStyle = FontStyle.Italic;
                            var builder = new StringBuilder();

                            var frameworkGroup = NugetHelper.GetBestDependencyFrameworkGroupForCurrentSettings(package);
                            foreach (var dependency in frameworkGroup.Dependencies)
                            {
                                builder.Append(string.Format(" {0} {1};", dependency.Id, dependency.Version));
                            }

                            EditorGUILayout.Space();
                            EditorGUILayout.LabelField(string.Format("Depends on:{0}", builder));
                            EditorStyles.label.fontStyle = FontStyle.Normal;
                        }

                        // Create the style for putting a box around the 'Clone' button
                        var cloneButtonBoxStyle = new GUIStyle("box");
                        cloneButtonBoxStyle.stretchWidth = false;
                        cloneButtonBoxStyle.margin.top = 0;
                        cloneButtonBoxStyle.margin.bottom = 0;
                        cloneButtonBoxStyle.padding.bottom = 4;

                        var normalButtonBoxStyle = new GUIStyle(cloneButtonBoxStyle);
                        normalButtonBoxStyle.normal.background = packageStyle.normal.background;

                        var showCloneWindow = openCloneWindows.Contains(package);
                        cloneButtonBoxStyle.normal.background = showCloneWindow ? contrastStyle.normal.background : packageStyle.normal.background;

                        // Create a similar style for the 'Clone' window
                        var cloneWindowStyle = new GUIStyle(cloneButtonBoxStyle);
                        cloneWindowStyle.padding = new RectOffset(6, 6, 2, 6);

                        // Show button bar
                        EditorGUILayout.BeginHorizontal();
                        {
                            if (package.RepositoryType == RepositoryType.Git || package.RepositoryType == RepositoryType.TfsGit)
                            {
                                if (!string.IsNullOrEmpty(package.RepositoryUrl))
                                {
                                    EditorGUILayout.BeginHorizontal(cloneButtonBoxStyle);
                                    {
                                        var cloneButtonStyle = new GUIStyle(GUI.skin.button);
                                        cloneButtonStyle.normal = showCloneWindow ? cloneButtonStyle.active : cloneButtonStyle.normal;
                                        if (GUILayout.Button("Clone", cloneButtonStyle, GUILayout.ExpandWidth(false)))
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
                                    EditorGUILayout.EndHorizontal();
                                }
                            }

                            if (!string.IsNullOrEmpty(package.LicenseUrl) && package.LicenseUrl != "http://your_license_url_here")
                            {
                                // Create a box around the license button to keep it aligned with Clone button
                                EditorGUILayout.BeginHorizontal(normalButtonBoxStyle);

                                // Show the license button
                                if (GUILayout.Button("View License", GUILayout.ExpandWidth(false)))
                                {
                                    Application.OpenURL(package.LicenseUrl);
                                }

                                EditorGUILayout.EndHorizontal();
                            }
                        }
                        EditorGUILayout.EndHorizontal();

                        if (showCloneWindow)
                        {
                            EditorGUILayout.BeginVertical(cloneWindowStyle);
                            {
                                // Clone latest label
                                EditorGUILayout.BeginHorizontal();
                                GUILayout.Space(20f);
                                EditorGUILayout.LabelField("clone latest");
                                EditorGUILayout.EndHorizontal();

                                // Clone latest row
                                EditorGUILayout.BeginHorizontal();
                                {
                                    if (GUILayout.Button("Copy", GUILayout.ExpandWidth(false)))
                                    {
                                        GUI.FocusControl(package.Id + package.Version + "repoUrl");
                                        GUIUtility.systemCopyBuffer = package.RepositoryUrl;
                                    }

                                    GUI.SetNextControlName(package.Id + package.Version + "repoUrl");
                                    EditorGUILayout.TextField(package.RepositoryUrl);
                                }
                                EditorGUILayout.EndHorizontal();

                                // Clone @ commit label
                                GUILayout.Space(4f);
                                EditorGUILayout.BeginHorizontal();
                                GUILayout.Space(20f);
                                EditorGUILayout.LabelField("clone @ commit");
                                EditorGUILayout.EndHorizontal();

                                // Clone @ commit row
                                EditorGUILayout.BeginHorizontal();
                                {
                                    // Create the three commands a user will need to run to get the repo @ the commit. Intentionally leave off the last newline for better UI appearance
                                    var commands = string.Format(
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

                                    EditorGUILayout.BeginVertical();
                                    GUI.SetNextControlName(package.Id + package.Version + "commands");
                                    EditorGUILayout.TextArea(commands);
                                    EditorGUILayout.EndVertical();
                                }
                                EditorGUILayout.EndHorizontal();
                            }
                            EditorGUILayout.EndVertical();
                        }

                        EditorGUI.indentLevel--;
                    }

                    EditorGUILayout.Separator();
                    EditorGUILayout.Separator();
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();
        }

        public static void GUILayoutLink(string url)
        {
            var hyperLinkStyle = new GUIStyle(GUI.skin.label);
            hyperLinkStyle.stretchWidth = false;
            hyperLinkStyle.richText = true;

            var colorFormatString = "<color=#add8e6ff>{0}</color>";

            var underline = new string('_', url.Length);

            var formattedUrl = string.Format(colorFormatString, url);
            var formattedUnderline = string.Format(colorFormatString, underline);
            var urlRect = GUILayoutUtility.GetRect(new GUIContent(url), hyperLinkStyle);

            // Update rect for indentation
            {
                var indentedUrlRect = EditorGUI.IndentedRect(urlRect);
                var delta = indentedUrlRect.x - urlRect.x;
                indentedUrlRect.width += delta;
                urlRect = indentedUrlRect;
            }

            GUI.Label(urlRect, formattedUrl, hyperLinkStyle);
            GUI.Label(urlRect, formattedUnderline, hyperLinkStyle);

            EditorGUIUtility.AddCursorRect(urlRect, MouseCursor.Link);
            if (urlRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.MouseUp)
                {
                    Application.OpenURL(url);
                }
            }
        }

        /// <summary>
        ///     Checks if NuGetForUnity is installed using UPM.
        ///     If installed using UPM we update the package.
        ///     If not we open the browser to download the .unitypackage.
        /// </summary>
        private sealed class NuGetForUnityUpdateInstaller
        {
            private readonly string latestVersionDownloadUrl;

            private readonly ListRequest upmPackageListRequest;

            private AddRequest upmPackageAddRequest;

            public NuGetForUnityUpdateInstaller(string latestVersionDownloadUrl)
            {
                this.latestVersionDownloadUrl = latestVersionDownloadUrl;
                EditorUtility.DisplayProgressBar("NuGetForUnity update ...", null, 0.0f);

                // get list of installed packages
                upmPackageListRequest = Client.List(true);
                EditorApplication.update += HandleListRequest;
            }

            private void HandleListRequest()
            {
                if (!upmPackageListRequest.IsCompleted)
                {
                    return;
                }

                try
                {
                    if (upmPackageListRequest.Status == StatusCode.Success &&
                        upmPackageListRequest.Result.Any(package => package.name == UpmPackageName))
                    {
                        // NuGetForUnity is installed as UPM package so we can just re-add it inside the package-manager to get update (see https://docs.unity3d.com/Manual/upm-git.html)
                        EditorUtility.DisplayProgressBar("NuGetForUnity update ...", "Installing with UPM", 0.1f);
                        upmPackageAddRequest = Client.Add(UpmPackageGitUrl);
                        EditorApplication.update += HandleAddRequest;
                    }
                    else
                    {
                        EditorUtility.ClearProgressBar();
                        Application.OpenURL(latestVersionDownloadUrl);
                    }
                }
                finally
                {
                    EditorApplication.update -= HandleListRequest;
                }
            }

            private void HandleAddRequest()
            {
                if (!upmPackageAddRequest.IsCompleted)
                {
                    return;
                }

                EditorUtility.ClearProgressBar();
                EditorApplication.update -= HandleAddRequest;
            }
        }

#pragma warning disable 0649

        [Serializable]
        private sealed class GitHubReleaseApiRequestList
        {
            public List<GitHubReleaseApiRequest> list;
        }

        [Serializable]
        private sealed class GitHubReleaseApiRequest
        {
            public List<GitHubAsset> assets;

            public string tag_name;
        }

        [Serializable]
        private sealed class GitHubAsset
        {
            public string browser_download_url;

            public string name;
        }

#pragma warning restore 0649
    }
}
