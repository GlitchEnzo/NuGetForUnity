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

        /// <summary>
        ///     The filtered list of package updates available.
        /// </summary>
        private List<NugetPackage> filteredUpdatePackages = new List<NugetPackage>();

        /// <summary>
        ///     True when the NugetWindow has initialized. This is used to skip time-consuming reloading operations when the assembly is reloaded.
        /// </summary>
        [SerializeField]
        private bool hasRefreshed;

        /// <summary>
        ///     The search term to search the installed packages for.
        /// </summary>
        private string installedSearchTerm = "Search";

        /// <summary>
        ///     The search term in progress while it is being typed into the search box.
        ///     We wait until the Enter key or Search button is pressed before searching in order
        ///     to match the way that the Online and Updates searches work.
        /// </summary>
        private string installedSearchTermEditBox = "Search";

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

        private IEnumerable<NugetPackage> FilteredInstalledPackages
        {
            get
            {
                if (installedSearchTerm == "Search")
                {
                    return NugetHelper.InstalledPackages;
                }

                return NugetHelper.InstalledPackages
                    .Where(x => x.Id.ToLower().Contains(installedSearchTerm) || x.Title.ToLower().Contains(installedSearchTerm))
                    .ToList();
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
                    NugetHelper.UpdateInstalledPackages();

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

        /// <summary>
        ///     Updates the list of update packages.
        /// </summary>
        private void UpdateUpdatePackages()
        {
            // get any available updates for the installed packages
            updatePackages = NugetHelper.GetUpdates(NugetHelper.InstalledPackages, showPrereleaseUpdates, showAllUpdateVersions);
            filteredUpdatePackages = updatePackages;

            if (updatesSearchTerm != "Search")
            {
                filteredUpdatePackages = updatePackages
                    .Where(x => x.Id.ToLower().Contains(updatesSearchTerm) || x.Title.ToLower().Contains(updatesSearchTerm))
                    .ToList();
            }
        }

        /// <summary>
        ///     From here: http://forum.unity3d.com/threads/changing-the-background-color-for-beginhorizontal.66015/
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="col"></param>
        /// <returns></returns>
        private static Texture2D MakeTex(int width, int height, Color col)
        {
            var pix = new Color[width * height];

            for (var i = 0; i < pix.Length; i++)
            {
                pix[i] = col;
            }

            var result = new Texture2D(width, height);
            result.SetPixels(pix);
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
            openCloneWindows.Clear();
        }

        /// <summary>
        ///     Creates a GUI style with a contrasting background color based upon if the Unity Editor is the free (light) skin or the Pro (dark) skin.
        /// </summary>
        /// <returns>A GUI style with the appropriate background color set.</returns>
        private static GUIStyle GetContrastStyle()
        {
            var style = new GUIStyle();
            var backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.6f, 0.6f, 0.6f);
            style.normal.background = MakeTex(16, 16, backgroundColor);
            return style;
        }

        /// <summary>
        ///     Creates a GUI style with a background color the same as the editor's current background color.
        /// </summary>
        /// <returns>A GUI style with the appropriate background color set.</returns>
        private static GUIStyle GetBackgroundStyle()
        {
            var style = new GUIStyle();
            var backgroundColor = EditorGUIUtility.isProSkin ? new Color32(56, 56, 56, 255) : new Color32(194, 194, 194, 255);
            style.normal.background = MakeTex(16, 16, backgroundColor);
            return style;
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

            if (filteredUpdatePackages != null && filteredUpdatePackages.Count > 0)
            {
                DrawPackages(filteredUpdatePackages);
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

            var filteredInstalledPackages = FilteredInstalledPackages.ToList();
            if (filteredInstalledPackages != null && filteredInstalledPackages.Count > 0)
            {
                DrawPackages(filteredInstalledPackages);
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

            var showMoreStyle = new GUIStyle();
            if (Application.HasProLicense())
            {
                showMoreStyle.normal.background = MakeTex(20, 20, new Color(0.05f, 0.05f, 0.05f));
            }
            else
            {
                showMoreStyle.normal.background = MakeTex(20, 20, new Color(0.4f, 0.4f, 0.4f));
            }

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

        private void DrawPackages(List<NugetPackage> packages)
        {
            var backgroundStyle = GetBackgroundStyle();
            var contrastStyle = GetContrastStyle();

            for (var i = 0; i < packages.Count; i++)
            {
                EditorGUILayout.BeginVertical(backgroundStyle);
                DrawPackage(packages[i], backgroundStyle, contrastStyle);
                EditorGUILayout.EndVertical();

                // swap styles
                var tempStyle = backgroundStyle;
                backgroundStyle = contrastStyle;
                contrastStyle = tempStyle;
            }
        }

        /// <summary>
        ///     Draws the header which allows filtering the online list of packages.
        /// </summary>
        private void DrawOnlineHeader()
        {
            var headerStyle = new GUIStyle();
            if (Application.HasProLicense())
            {
                headerStyle.normal.background = MakeTex(20, 20, new Color(0.05f, 0.05f, 0.05f));
            }
            else
            {
                headerStyle.normal.background = MakeTex(20, 20, new Color(0.4f, 0.4f, 0.4f));
            }

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
            var headerStyle = new GUIStyle();
            if (Application.HasProLicense())
            {
                headerStyle.normal.background = MakeTex(20, 20, new Color(0.05f, 0.05f, 0.05f));
            }
            else
            {
                headerStyle.normal.background = MakeTex(20, 20, new Color(0.4f, 0.4f, 0.4f));
            }

            EditorGUILayout.BeginVertical(headerStyle);
            {
                var enterPressed = Event.current.Equals(Event.KeyboardEvent("return"));

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Preferences", GUILayout.Width(80)))
                    {
                        SettingsService.OpenUserPreferences("Preferences/NuGet For Unity");
                        GetWindow<NugetWindow>().Close();
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                {
                    var oldFontSize = GUI.skin.textField.fontSize;
                    GUI.skin.textField.fontSize = 25;
                    installedSearchTermEditBox = EditorGUILayout.TextField(installedSearchTermEditBox, GUILayout.Height(30));

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
                    installedSearchTerm = installedSearchTermEditBox;
                }
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        ///     Draws the header for the Updates tab.
        /// </summary>
        private void DrawUpdatesHeader()
        {
            var headerStyle = new GUIStyle();
            if (Application.HasProLicense())
            {
                headerStyle.normal.background = MakeTex(20, 20, new Color(0.05f, 0.05f, 0.05f));
            }
            else
            {
                headerStyle.normal.background = MakeTex(20, 20, new Color(0.4f, 0.4f, 0.4f));
            }

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

                    if (GUILayout.Button("Install All Updates", GUILayout.Width(150)))
                    {
                        NugetHelper.UpdateAll(updatePackages, NugetHelper.InstalledPackages);
                        NugetHelper.UpdateInstalledPackages();
                        UpdateUpdatePackages();
                    }

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
                EditorGUILayout.EndHorizontal();

                var showPrereleaseTemp = EditorGUILayout.Toggle("Show Prerelease", showPrereleaseUpdates);
                if (showPrereleaseTemp != showPrereleaseUpdates)
                {
                    showPrereleaseUpdates = showPrereleaseTemp;
                    UpdateUpdatePackages();
                }

                var enterPressed = Event.current.Equals(Event.KeyboardEvent("return"));

                EditorGUILayout.BeginHorizontal();
                {
                    var oldFontSize = GUI.skin.textField.fontSize;
                    GUI.skin.textField.fontSize = 25;
                    updatesSearchTerm = EditorGUILayout.TextField(updatesSearchTerm, GUILayout.Height(30));

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
                    if (updatesSearchTerm != "Search")
                    {
                        filteredUpdatePackages = updatePackages.Where(
                                x => x.Id.ToLower().Contains(updatesSearchTerm) || x.Title.ToLower().Contains(updatesSearchTerm))
                            .ToList();
                    }
                }
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        ///     Draws the given <see cref="NugetPackage" />.
        /// </summary>
        /// <param name="package">The <see cref="NugetPackage" /> to draw.</param>
        private void DrawPackage(NugetPackage package, GUIStyle packageStyle, GUIStyle contrastStyle)
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
                    var padding = EditorStyles.label.padding.vertical;
                    var rect = GUILayoutUtility.GetRect(iconSize, iconSize);

                    // only use GetRect's Y position.  It doesn't correctly set the width, height or X position.
                    rect.x = padding;
                    rect.y += padding;
                    rect.width = iconSize;
                    rect.height = iconSize;

                    var icon = defaultIcon;
                    if (package.IconTask != null && package.IconTask.IsCompleted && package.IconTask.Result != null)
                    {
                        // as this is called every frame we don't need to wait for the task we can just use the image if it is available
                        icon = package.IconTask.Result;
                    }

                    if (icon != null)
                    {
                        GUI.DrawTexture(rect, icon, ScaleMode.StretchToFill);
                    }

                    rect.x = iconSize + 2 * padding;
                    rect.width = position.width / 2 - (iconSize + padding);
                    rect.y -= padding; // This will leave the text aligned with the top of the image

                    EditorStyles.label.fontStyle = FontStyle.Bold;
                    EditorStyles.label.fontSize = 16;

                    var idSize = EditorStyles.label.CalcSize(new GUIContent(package.Id));
                    rect.y += iconSize / 2 - idSize.y / 2 + padding;
                    GUI.Label(rect, package.Id, EditorStyles.label);
                    rect.x += idSize.x;

                    EditorStyles.label.fontSize = 10;
                    EditorStyles.label.fontStyle = FontStyle.Normal;

                    var versionSize = EditorStyles.label.CalcSize(new GUIContent(package.Version));
                    rect.y += idSize.y - versionSize.y - padding / 2;

                    if (!string.IsNullOrEmpty(package.Authors))
                    {
                        var authorLabel = string.Format("by {0}", package.Authors);
                        var size = EditorStyles.label.CalcSize(new GUIContent(authorLabel));
                        GUI.Label(rect, authorLabel, EditorStyles.label);
                        rect.x += size.x;
                    }

                    if (package.DownloadCount > 0)
                    {
                        var downloadLabel = string.Format("{0} downloads", package.DownloadCount.ToString("#,#"));
                        var size = EditorStyles.label.CalcSize(new GUIContent(downloadLabel));
                        GUI.Label(rect, downloadLabel, EditorStyles.label);
                        rect.x += size.x;
                    }
                }

                GUILayout.FlexibleSpace();
                if (installed != null && installed.Version != package.Version)
                {
                    GUILayout.Label(string.Format("Current Version {0}", installed.Version));
                }

                GUILayout.Label(string.Format("Version {0}", package.Version));

                if (installedPackages.Contains(package))
                {
                    // This specific version is installed
                    if (GUILayout.Button("Uninstall"))
                    {
                        // TODO: Perhaps use a "mark as dirty" system instead of updating all of the data all the time?
                        NugetHelper.Uninstall(package);
                        NugetHelper.UpdateInstalledPackages();
                        UpdateUpdatePackages();
                    }
                }
                else
                {
                    if (installed != null)
                    {
                        if (installed < package)
                        {
                            // An older version is installed
                            if (GUILayout.Button("Update"))
                            {
                                NugetHelper.Update(installed, package);
                                NugetHelper.UpdateInstalledPackages();
                                UpdateUpdatePackages();
                            }
                        }
                        else if (installed > package)
                        {
                            // A newer version is installed
                            if (GUILayout.Button("Downgrade"))
                            {
                                NugetHelper.Update(installed, package);
                                NugetHelper.UpdateInstalledPackages();
                                UpdateUpdatePackages();
                            }
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Install"))
                        {
                            NugetHelper.InstallIdentifier(package);
                            NugetHelper.UpdateInstalledPackages();
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
