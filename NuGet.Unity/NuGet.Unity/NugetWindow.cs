namespace NugetForUnity
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Networking;

    /// <summary>
    /// Represents the NuGet Package Manager Window in the Unity Editor.
    /// </summary>
    public class NugetWindow : EditorWindow
    {
        /// <summary>
        /// True when the NugetWindow has initialized. This is used to skip time-consuming reloading operations when the assembly is reloaded.
        /// </summary>
        [SerializeField]
        private bool hasRefreshed = false;

        /// <summary>
        /// The current position of the scroll bar in the GUI.
        /// </summary>
        private Vector2 scrollPosition;

        /// <summary>
        /// The list of NugetPackages available to install.
        /// </summary>
        [SerializeField]
        private List<NugetPackage> availablePackages = new List<NugetPackage>();

        /// <summary>
        /// The list of package updates available, based on the already installed packages.
        /// </summary>
        [SerializeField]
        private List<NugetPackage> updatePackages = new List<NugetPackage>();

        /// <summary>
        /// The filtered list of package updates available.
        /// </summary>
        private List<NugetPackage> filteredUpdatePackages = new List<NugetPackage>();

        /// <summary>
        /// True to show all old package versions.  False to only show the latest version.
        /// </summary>
        private bool showAllOnlineVersions;

        /// <summary>
        /// True to show beta and alpha package versions.  False to only show stable versions.
        /// </summary>
        private bool showOnlinePrerelease;

        /// <summary>
        /// True to show all old package versions.  False to only show the latest version.
        /// </summary>
        private bool showAllUpdateVersions;

        /// <summary>
        /// True to show beta and alpha package versions.  False to only show stable versions.
        /// </summary>
        private bool showPrereleaseUpdates;

        /// <summary>
        /// The width to use for the install/uninstall/update/downgrade button
        /// </summary>
        private readonly GUILayoutOption installButtonWidth = GUILayout.Width(180);

        /// <summary>
        /// The height to use for the install/uninstall/update/downgrade button
        /// </summary>
        private readonly GUILayoutOption installButtonHeight = GUILayout.Height(27);

        /// <summary>
        /// The search term to search the online packages for.
        /// </summary>
        private string onlineSearchTerm = "Search";

        /// <summary>
        /// The search term to search the installed packages for.
        /// </summary>
        private string installedSearchTerm = "Search";

        /// <summary>
        /// The search term in progress while it is being typed into the search box.
        /// We wait until the Enter key or Search button is pressed before searching in order
        /// to match the way that the Online and Updates searches work.
        /// </summary>
        private string installedSearchTermEditBox = "Search";

        /// <summary>
        /// The search term to search the update packages for.
        /// </summary>
        private string updatesSearchTerm = "Search";

        /// <summary>
        /// The number of packages to get from the request to the server.
        /// </summary>
        private int numberToGet = 15;

        /// <summary>
        /// The number of packages to skip when requesting a list of packages from the server.  This is used to get a new group of packages.
        /// </summary>
        [SerializeField]
        private int numberToSkip;

        /// <summary>
        /// The currently selected tab in the window.
        /// </summary>
        private int currentTab;

        /// <summary>
        /// The currently selected feed.
        /// </summary>
        private int currentFeedIndex;

        private GUIStyle cachedContrastStyle;

        private GUIStyle cachedBackgroundStyle;

        private GUIStyle cachedProjectUrlStyle;

        private GUIStyle cachedShowMoreStyle;

        private GUIStyle cachedHeaderStyle;

        /// <summary>
        /// The selected feed.
        /// </summary>
        private NugetPackageSource currentFeed;

        /// <summary>
        /// The titles of the tabs in the window.
        /// </summary>
        private readonly string[] tabTitles = { "Online", "Installed", "Updates" };

        /// <summary>
        /// The default icon to display for packages.
        /// </summary>
        [SerializeField]
        private Texture2D defaultIcon;

        /// <summary>
        /// Used to keep track of which packages the user has opened the clone window on.
        /// </summary>
        private HashSet<NugetPackage> openCloneWindows = new HashSet<NugetPackage>();
        private NugetPackage selectedPackage;
        private GUIStyle cachedDescriptionStyle;

        private IEnumerable<NugetPackage> FilteredInstalledPackages
        {
            get
            {
                if (installedSearchTerm == "Search")
                    return NugetHelper.InstalledPackages;

                return NugetHelper.InstalledPackages.Where(x => x.Id.ToLower().Contains(installedSearchTerm) || x.Title.ToLower().Contains(installedSearchTerm)).ToList();
            }
        }

        /// <summary>
        /// Opens the NuGet Package Manager Window.
        /// </summary>
        [MenuItem("Window/NuGet/Manage NuGet Packages", false, 0)]
        protected static void DisplayNugetWindow()
        {
            GetWindow<NugetWindow>();
        }

        /// <summary>
        /// Restores all packages defined in packages.config
        /// </summary>
        [MenuItem("Window/NuGet/Restore Packages", false, 1)]
        protected static void RestorePackages()
        {
            NugetHelper.Restore();
        }

        /// <summary>
        /// Finds all packages that include source code. We generate the
        /// mdb with 'pdb2mdb' mono utility.
        /// </summary>
        [MenuItem("Window/NuGet/Generate Mdbs", false, 2)]
        protected static void GenerateMdb()
        {
            NugetHelper.GenerateMdbsForInstalledPackages();
        }

        [MenuItem("Window/NuGet/Rebase Mdbs", false, 2)]
        protected static void RebaseMdbs()
        {
            NugetHelper.RebaseForInstalledPackages();
        }

        /// <summary>
        /// Displays the version number of NuGetForUnity.
        /// </summary>
        [MenuItem("Window/NuGet/Version " + NugetPreferences.NuGetForUnityVersion, false, 10)]
        protected static void DisplayVersion()
        {
            var assembly = System.Reflection.Assembly.GetAssembly(typeof(EditorWindow));
            var preferencesWindow = assembly.GetType("UnityEditor.PreferencesWindow");
            var preferencesWindowSection = assembly.GetType("UnityEditor.PreferencesWindow+Section"); // access nested class via + instead of .         

            // open the preferences window
            EditorWindow preferencesEditorWindow = EditorWindow.GetWindowWithRect(preferencesWindow, new Rect(100f, 100f, 500f, 400f), true, "Unity Preferences");
            //preferencesEditorWindow.m_Parent.window.m_DontSaveToLayout = true; //<-- Unity's implementation also does this

            // Get the flag to see if custom sections have already been added
            var m_RefreshCustomPreferences = preferencesWindow.GetField("m_RefreshCustomPreferences", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bool refesh = (bool)m_RefreshCustomPreferences.GetValue(preferencesEditorWindow);

            if (refesh)
            {
                // Invoke the AddCustomSections to load all user-specified preferences sections.  This normally isn't done until OnGUI, but we need to call it now to set the proper index
                var addCustomSections = preferencesWindow.GetMethod("AddCustomSections", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                addCustomSections.Invoke(preferencesEditorWindow, null);

                // Unity is dumb and doesn't set the flag for having loaded the custom sections INSIDE the AddCustomSections method!  So we must call it manually.
                m_RefreshCustomPreferences.SetValue(preferencesEditorWindow, false);
            }

            // get the List<PreferencesWindow.Section> m_Sections.Count
            var m_Sections = preferencesWindow.GetField("m_Sections", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            object list = m_Sections.GetValue(preferencesEditorWindow);
            var sectionList = typeof(List<>).MakeGenericType(new Type[] { preferencesWindowSection });
            var getCount = sectionList.GetProperty("Count").GetGetMethod(true);
            int count = (int)getCount.Invoke(list, null);
            //Debug.LogFormat("Count = {0}", count);

            // Figure out the index of the NuGet for Unity preferences
            var getItem = sectionList.GetMethod("get_Item");
            int nugetIndex = 0;
            for (int i = 0; i < count; i++)
            {
                var section = getItem.Invoke(list, new object[] { i });
                GUIContent content = (GUIContent)section.GetType().GetField("content", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).GetValue(section);
                if (content != null && content.text == "NuGet For Unity")
                {
                    nugetIndex = i;
                    break;
                }
            }
            //Debug.LogFormat("NuGet index = {0}", nugetIndex);

            // set the selected section index
            var selectedSectionIndex = preferencesWindow.GetProperty("selectedSectionIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var selectedSectionIndexSetter = selectedSectionIndex.GetSetMethod(true);
            selectedSectionIndexSetter.Invoke(preferencesEditorWindow, new object[] { nugetIndex });
            //var selectedSectionIndexGetter = selectedSectionIndex.GetGetMethod(true);
            //object index = selectedSectionIndexGetter.Invoke(preferencesEditorWindow, null);
            //Debug.LogFormat("Selected Index = {0}", index);
        }

        /// <summary>
        /// Checks/launches the Releases page to update NuGetForUnity with a new version.
        /// </summary>
        [MenuItem("Window/NuGet/Check for Updates...", false, 10)]
        protected static void CheckForUpdates()
        {
            const string url = "https://github.com/GlitchEnzo/NuGetForUnity/releases";
#if UNITY_2017_1_OR_NEWER // UnityWebRequest is not available in Unity 5.2, which is the currently the earliest version supported by NuGetForUnity.
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.Send();
#else
            using (WWW request = new WWW(url))
            {
#endif

                NugetHelper.LogVerbose("HTTP GET {0}", url);
                while (!request.isDone)
                {
                    EditorUtility.DisplayProgressBar("Checking updates", null, 0.0f);
                }
                EditorUtility.ClearProgressBar();

                string latestVersion = null;
                string latestVersionDownloadUrl = null;

                string response = null;
#if UNITY_2017_1_OR_NEWER
                if (!request.isNetworkError && !request.isHttpError)
                {
                    response = request.downloadHandler.text;
                }
#else
                if (request.error == null)
                {
                    response = request.text;
                }
#endif

                if (response != null)
                {
                    latestVersion = GetLatestVersonFromReleasesHtml(response, out latestVersionDownloadUrl);
                }

                if (latestVersion == null)
                {
                    EditorUtility.DisplayDialog(
                            "Unable to Determine Updates",
                            string.Format("Couldn't find release information at {0}.", url),
                            "OK");
                    return;
                }

                NugetPackageIdentifier current = new NugetPackageIdentifier("NuGetForUnity", NugetPreferences.NuGetForUnityVersion);
                NugetPackageIdentifier latest = new NugetPackageIdentifier("NuGetForUnity", latestVersion);
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
                    case 0: Application.OpenURL(latestVersionDownloadUrl); break;
                    case 1: Application.OpenURL(url); break;
                    case 2: break;
                }
            }
        }

        private static string GetLatestVersonFromReleasesHtml(string response, out string url)
        {
            Regex hrefRegex = new Regex(@"<a href=""(?<url>.*NuGetForUnity\.(?<version>\d+\.\d+\.\d+)\.unitypackage)""");
            Match match = hrefRegex.Match(response);
            if (!match.Success)
            {
                url = null;
                return null;
            }
            url = "https://github.com/" + match.Groups["url"].Value;
            return match.Groups["version"].Value;
        }

        /// <summary>
        /// Called when enabling the window.
        /// </summary>
        private void OnEnable()
        {
            Refresh(false);
        }

        private void Refresh(bool forceFullRefresh)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
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
                UnityEngine.Debug.LogErrorFormat("{0}", e.ToString());
            }
            finally
            {
                EditorUtility.ClearProgressBar();

                NugetHelper.LogVerbose("NugetWindow reloading took {0} ms", stopwatch.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// Updates the list of available packages by running a search with the server using the currently set parameters (# to get, # to skip, etc).
        /// </summary>
        private void UpdateOnlinePackages()
        {
            availablePackages = NugetHelper.Search(onlineSearchTerm != "Search" ? onlineSearchTerm : string.Empty, showAllOnlineVersions, showOnlinePrerelease, numberToGet, numberToSkip, currentFeed);
        }

        /// <summary>
        /// Updates the list of update packages.
        /// </summary>
        private void UpdateUpdatePackages()
        {
            // get any available updates for the installed packages
            updatePackages = NugetHelper.GetUpdates(NugetHelper.InstalledPackages, showPrereleaseUpdates, showAllUpdateVersions, string.Empty, string.Empty, currentFeed);
            filteredUpdatePackages = updatePackages;

            if (updatesSearchTerm != "Search")
            {
                filteredUpdatePackages = updatePackages.Where(x => x.Id.ToLower().Contains(updatesSearchTerm) || x.Title.ToLower().Contains(updatesSearchTerm)).ToList();
            }
        }

        /// <summary>
        /// From here: http://forum.unity3d.com/threads/changing-the-background-color-for-beginhorizontal.66015/
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="col"></param>
        /// <returns></returns>
        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];

            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();

            return result;
        }

        /// <summary>
        /// Automatically called by Unity to draw the GUI.
        /// </summary>
        protected void OnGUI()
        {
            DrawHeader();

            GUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.ExpandWidth(true));
            int selectedTab = GUILayout.Toolbar(currentTab, tabTitles, EditorStyles.toolbarButton);
            GUILayout.EndHorizontal();

            DrawBodyContent();

            if (selectedTab != currentTab)
                OnTabChanged();

            currentTab = selectedTab;
        }

        private void DrawHeader()
        {
            switch (currentTab)
            {
                case 0:
                    DrawOnlineHeader();
                    break;
                case 1:
                    DrawInstalledHeader();
                    break;
                case 2:
                    DrawUpdatesHeader();
                    break;
            }
        }

        private void DrawBodyContent()
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(position.width * .7f));
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
            GUILayout.EndVertical();

            // Selected package...
            GUILayout.BeginVertical(EditorStyles.helpBox);

            if(selectedPackage != null)
            {
                DrawPackageDetail(selectedPackage);
            }

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        private void OnTabChanged()
        {
            openCloneWindows.Clear();
        }

        /// <summary>
        /// Creates a GUI style with a contrasting background color based upon if the Unity Editor is the free (light) skin or the Pro (dark) skin.
        /// </summary>
        /// <returns>A GUI style with the appropriate background color set.</returns>
        private GUIStyle GetContrastStyle()
        {
            if (cachedContrastStyle == null)
            {
                cachedContrastStyle = new GUIStyle();
                Color backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.6f, 0.6f, 0.6f);
                cachedContrastStyle.normal.background = MakeTex(16, 16, backgroundColor);
            }

            return cachedContrastStyle;
        }

        /// <summary>
        /// Creates a GUI style with a background color the same as the editor's current background color.
        /// </summary>
        /// <returns>A GUI style with the appropriate background color set.</returns>
        private GUIStyle GetBackgroundStyle()
        {
            if (cachedBackgroundStyle == null)
            {
                cachedBackgroundStyle = new GUIStyle();
                Color32 backgroundColor = EditorGUIUtility.isProSkin ? new Color32(56, 56, 56, 255) : new Color32(194, 194, 194, 255);
                cachedBackgroundStyle.normal.background = MakeTex(16, 16, backgroundColor);
            }

            return cachedBackgroundStyle;
        }

        private GUIStyle GetDescriptionStyle()
        {
            if (cachedDescriptionStyle == null)
            {
                cachedDescriptionStyle = new GUIStyle(EditorStyles.miniLabel);
                cachedDescriptionStyle.wordWrap = true;
            }

            return cachedDescriptionStyle;
        }

        private GUIStyle GetProjectUrlStyle()
        {
            if(cachedProjectUrlStyle == null)
            {
                cachedProjectUrlStyle = new GUIStyle(GUI.skin.label);
                cachedProjectUrlStyle.richText = true;
                cachedProjectUrlStyle.wordWrap = true;
            }

            return cachedProjectUrlStyle;
        }

        /// <summary>
        /// Draws the list of installed packages that have updates available.
        /// </summary>
        private void DrawUpdates()
        {
            // display all of the installed packages
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.BeginVertical();

            GUIStyle style = GetContrastStyle();

            if (filteredUpdatePackages != null && filteredUpdatePackages.Count > 0)
            {
                DrawPackages(filteredUpdatePackages);
            }
            else
            {
                GUILayout.Label("There are no updates available!", EditorStyles.boldLabel, GUILayout.Height(20));
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Draws the list of installed packages.
        /// </summary>
        private void DrawInstalled()
        {
            // display all of the installed packages
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.BeginVertical();

            List<NugetPackage> filteredInstalledPackages = FilteredInstalledPackages.ToList();
            if (filteredInstalledPackages != null && filteredInstalledPackages.Count > 0)
            {
                DrawPackages(filteredInstalledPackages);
            }
            else
            {
                GUILayout.Label("There are no packages installed!", EditorStyles.boldLabel, GUILayout.Height(20));
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Draws the current list of available online packages.
        /// </summary>
        private void DrawOnline()
        {
            // display all of the packages
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, false, false);
            EditorGUILayout.BeginVertical();

            if (availablePackages != null)
            {
                DrawPackages(availablePackages);
            }

            EditorGUILayout.BeginVertical(GetShowMoreStyle());
            // allow the user to dislay more results
            if (GUILayout.Button("Show More", GUILayout.Width(120)))
            {
                numberToSkip += numberToGet;
                availablePackages.AddRange(NugetHelper.Search(onlineSearchTerm != "Search" ? onlineSearchTerm : string.Empty, showAllOnlineVersions, showOnlinePrerelease, numberToGet, numberToSkip, currentFeed));
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private GUIStyle GetShowMoreStyle()
        {
            if (cachedShowMoreStyle == null)
            {
                cachedShowMoreStyle = new GUIStyle();

                if (Application.HasProLicense())
                {
                    cachedShowMoreStyle.normal.background = MakeTex(20, 20, new Color(0.05f, 0.05f, 0.05f));
                }
                else
                {
                    cachedShowMoreStyle.normal.background = MakeTex(20, 20, new Color(0.4f, 0.4f, 0.4f));
                }
            }

            return cachedShowMoreStyle;
        }

        private void DrawPackages(List<NugetPackage> packages)
        {
            for (int i = 0; i < packages.Count; i++)
            {
                var style = i % 2 == 0 ? GetContrastStyle() : GetBackgroundStyle();
                EditorGUILayout.BeginVertical(style);
                DrawPackage(packages[i], style);
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawPackageSourcePopup()
        {
            var names = from packageSource in NugetHelper.NugetConfigFile.PackageSources
                        select packageSource.Name;

            var selectedIndex = EditorGUILayout.Popup(currentFeedIndex, names.ToArray(), EditorStyles.toolbarPopup);

            if (selectedIndex != currentFeedIndex)
            {
                currentFeedIndex = selectedIndex;
                currentFeed = NugetHelper.NugetConfigFile.PackageSources[currentFeedIndex];
                UpdateOnlinePackages();
            }
        }

        /// <summary>
        /// Draws the header which allows filtering the online list of packages.
        /// </summary>
        private void DrawOnlineHeader()
        {
            bool enterPressed = Event.current.Equals(Event.KeyboardEvent("return"));

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.ExpandWidth(true));
            {
                int oldFontSize = GUI.skin.textField.fontSize;
                GUI.skin.textField.fontSize = 25;

                DrawPackageSourcePopup();

                onlineSearchTerm = DrawSearchBar(onlineSearchTerm);

                if (GUILayout.Button("Search", EditorStyles.toolbarButton, GUILayout.Width(100), GUILayout.Height(28)))
                {
                    // the search button emulates the Enter key
                    enterPressed = true;
                }

                GUILayout.FlexibleSpace();

                bool showAllVersionsTemp = GUILayout.Toggle(showAllOnlineVersions, "Show All Versions", EditorStyles.toolbarButton);
                if (showAllVersionsTemp != showAllOnlineVersions)
                {
                    showAllOnlineVersions = showAllVersionsTemp;
                    UpdateOnlinePackages();
                }

                bool showPrereleaseTemp = GUILayout.Toggle(showOnlinePrerelease, "Show Prerelease", EditorStyles.toolbarButton);
                if (showPrereleaseTemp != showOnlinePrerelease)
                {
                    showOnlinePrerelease = showPrereleaseTemp;
                    UpdateOnlinePackages();
                }

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    Refresh(true);
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

        /// <summary>
        /// Draws the header which allows filtering the installed list of packages.
        /// </summary>
        private void DrawInstalledHeader()
        {
            bool enterPressed = Event.current.Equals(Event.KeyboardEvent("return"));

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.ExpandWidth(true));
            {
                int oldFontSize = GUI.skin.textField.fontSize;
                GUI.skin.textField.fontSize = 25;
                installedSearchTermEditBox = DrawSearchBar(installedSearchTermEditBox);

                if (GUILayout.Button("Search", EditorStyles.toolbarButton, GUILayout.Width(100), GUILayout.Height(28)))
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

        /// <summary>
        /// Draws the header for the Updates tab.
        /// </summary>
        private void DrawUpdatesHeader()
        {
            bool enterPressed = Event.current.Equals(Event.KeyboardEvent("return"));

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.ExpandWidth(true));
            {
                int oldFontSize = GUI.skin.textField.fontSize;
                GUI.skin.textField.fontSize = 25;
                updatesSearchTerm = DrawSearchBar(updatesSearchTerm);

                if (GUILayout.Button("Search", EditorStyles.toolbarButton, GUILayout.Width(100), GUILayout.Height(28)))
                {
                    // the search button emulates the Enter key
                    enterPressed = true;
                }

                GUILayout.FlexibleSpace();

                bool showAllVersionsTemp = GUILayout.Toggle(showAllUpdateVersions, "Show All Versions", EditorStyles.toolbarButton);
                if (showAllVersionsTemp != showAllUpdateVersions)
                {
                    showAllUpdateVersions = showAllVersionsTemp;
                    UpdateUpdatePackages();
                }

                bool showPrereleaseTemp = GUILayout.Toggle(showPrereleaseUpdates, "Show Prerelease", EditorStyles.toolbarButton);
                if (showPrereleaseTemp != showPrereleaseUpdates)
                {
                    showPrereleaseUpdates = showPrereleaseTemp;
                    UpdateUpdatePackages();
                }

                if (GUILayout.Button("Install All Updates", EditorStyles.toolbarButton, GUILayout.Width(150)))
                {
                    NugetHelper.UpdateAll(updatePackages, NugetHelper.InstalledPackages);
                    NugetHelper.UpdateInstalledPackages();
                    UpdateUpdatePackages();
                }

                GUI.skin.textField.fontSize = oldFontSize;
            }
            EditorGUILayout.EndHorizontal();

            // search only if the enter key is pressed
            if (enterPressed)
            {
                if (updatesSearchTerm != "Search")
                {
                    filteredUpdatePackages = updatePackages.Where(x => x.Id.ToLower().Contains(updatesSearchTerm) || x.Title.ToLower().Contains(updatesSearchTerm)).ToList();
                }
            }
        }

        private string DrawSearchBar(string currentSearchValue)
        {
            GUILayout.BeginHorizontal(GUILayout.MinWidth(342));
            var searchValue = EditorGUILayout.TextField(currentSearchValue, GUI.skin.FindStyle("ToolbarSeachTextField"));
            if (GUILayout.Button(string.Empty, GUI.skin.FindStyle("ToolbarSeachCancelButton")))
            {
                searchValue = string.Empty;
            }
            GUILayout.EndHorizontal();

            return searchValue;
        }

        /// <summary>
        /// Draws the given <see cref="NugetPackage"/>.
        /// </summary>
        /// <param name="package">The <see cref="NugetPackage"/> to draw.</param>
        private void DrawPackage(NugetPackage package, GUIStyle packageStyle)
        {
            GUILayout.BeginVertical();

            DrawPackageNameAndIcon(package);
            DrawPackageDescription(package);
            DrawProjectUrl(package);

            GUILayout.EndVertical();

            var packageRect = GUILayoutUtility.GetLastRect();

            if (Event.current.type == EventType.MouseDown && packageRect.Contains(Event.current.mousePosition))
            {
                SelectPackageForDetail(package);
                GUIUtility.ExitGUI();

                Repaint();
            }

            DrawCurrentInstalledVersion(package);
        }

        private static void DrawCloneWindow(NugetPackage package)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
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
                    string commands = string.Format("git clone {0} {1} --no-checkout{2}cd {1}{2}git checkout {3}", package.RepositoryUrl, package.Id, Environment.NewLine, package.RepositoryCommit);

                    if (GUILayout.Button("Copy", GUILayout.ExpandWidth(false)))
                    {
                        GUI.FocusControl(package.Id + package.Version + "commands");

                        // Add a newline so the last command will execute when pasted to the CL
                        GUIUtility.systemCopyBuffer = (commands + Environment.NewLine);
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

        private void DrawCurrentInstalledVersion(NugetPackage package)
        {
            NugetPackage installed = GetInstalledPacakge(package);

            if (installed != null)
            {
                GUILayout.Label(string.Format("currently [{0}]  ", installed.Version), EditorStyles.miniLabel, installButtonWidth);
            }
        }

        private void DrawCloneButton(NugetPackage package)
        {
            if (package.RepositoryType == RepositoryType.Git || package.RepositoryType == RepositoryType.TfsGit)
            {
                if (!string.IsNullOrEmpty(package.RepositoryUrl))
                {
                    bool showCloneWindow = openCloneWindows.Contains(package);
  
                    if (GUILayout.Button("Clone", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
                    {
                        showCloneWindow = !showCloneWindow;

                        if (showCloneWindow)
                        {
                            openCloneWindows.Add(package);
                        }
                        else
                        {
                            openCloneWindows.Remove(package);
                        }
                    }

                    if(showCloneWindow)
                    {
                        DrawCloneWindow(package);
                    }
                }
            }
        }

        private static void DrawLicenseButton(NugetPackage package)
        {
            if (!string.IsNullOrEmpty(package.LicenseUrl) && package.LicenseUrl != "http://your_license_url_here")
            {
                // Show the license button
                if (GUILayout.Button("View License", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
                {
                    Application.OpenURL(package.LicenseUrl);
                }
            }
        }

        private static NugetPackage GetInstalledPacakge(NugetPackage package)
        {
            IEnumerable<NugetPackage> installedPackages = NugetHelper.InstalledPackages;
            var installed = installedPackages.FirstOrDefault(p => p.Id == package.Id);
            return installed;
        }

        private void DrawPackageDetail(NugetPackage package)
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbarButton);
            DrawInstallUninstallPackageButtons(package);
            DrawLicenseButton(package);
            DrawCloneButton(package);
            GUILayout.EndHorizontal();

            DrawPackageNameAndIcon(package);
            DrawPackageDescription(package);
            DrawReleaseNotes(package);
            DrawPackageDependencies(package);
            DrawProjectUrl(package);
        }

        private void SelectPackageForDetail(NugetPackage package)
        {
            selectedPackage = package;
        }

        private void DrawPackageNameAndIcon(NugetPackage package)
        {
            GUILayout.BeginHorizontal(GUILayout.ExpandHeight(false));
            GUILayout.Space(34);
            GUILayout.Label(string.Format("{1} [{0}]", package.Version, package.Title), EditorStyles.boldLabel, GUILayout.Height(24));
            GUILayout.EndHorizontal();

            var lastRect = GUILayoutUtility.GetLastRect();

            const int iconSize = 24;
            const int leftPadding = 5;

            lastRect.x += leftPadding;
            lastRect.y += 3;
            lastRect.width = iconSize;
            lastRect.height = iconSize;

            if (package.Icon != null)
            {
                GUI.DrawTexture(lastRect, package.Icon, ScaleMode.StretchToFill);
            }
            else
            {
                GUI.DrawTexture(lastRect, defaultIcon, ScaleMode.StretchToFill);
            }

        }

        private void DrawPackageDescription(NugetPackage package)
        {
            EditorStyles.miniBoldLabel.fontStyle = FontStyle.Bold;
            GUILayout.Label("Description:", EditorStyles.miniBoldLabel);

            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(string.Format("{0}", package.Description), GetDescriptionStyle());
            GUILayout.EndVertical();
        }

        private static void DrawPackageDependencies(NugetPackage package)
        {
            if (package.Dependencies.Count > 0)
            {
                GUILayout.Label("Dependencies:", EditorStyles.miniBoldLabel);

                StringBuilder builder = new StringBuilder();
                foreach (var dependency in package.Dependencies)
                {
                    builder.Append(string.Format(" {0} {1};", dependency.Id, dependency.Version));
                }

                GUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label(string.Format("Depends on:{0}", builder.ToString()), EditorStyles.miniLabel);
                GUILayout.EndVertical();
            }
        }

        private void DrawProjectUrl(NugetPackage package)
        {
            if (!string.IsNullOrEmpty(package.ProjectUrl))
            {
                GUILayoutLink(package.ProjectUrl);
            }
        }

        private static void DrawReleaseNotes(NugetPackage package)
        {
            if (!string.IsNullOrEmpty(package.ReleaseNotes))
            {
                GUILayout.Label(string.Format("Release Notes:"), EditorStyles.miniBoldLabel);
                GUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label(string.Format("{0}", package.ReleaseNotes), EditorStyles.miniLabel);
                GUILayout.EndVertical();
            }
        }

        private void DrawInstallUninstallPackageButtons(NugetPackage package)
        {
            IEnumerable<NugetPackage> installedPackages = NugetHelper.InstalledPackages;
            var installed = installedPackages.FirstOrDefault(p => p.Id == package.Id);

            if (installedPackages.Contains(package))
            {
                // This specific version is installed
                if (GUILayout.Button("Uninstall", EditorStyles.toolbarButton, installButtonWidth, installButtonHeight))
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
                        if (GUILayout.Button(string.Format("Update to [{0}]", package.Version), EditorStyles.toolbarButton, installButtonWidth, installButtonHeight))
                        {
                            NugetHelper.Update(installed, package);
                            NugetHelper.UpdateInstalledPackages();
                            UpdateUpdatePackages();
                        }
                    }
                    else if (installed > package)
                    {
                        // A newer version is installed
                        if (GUILayout.Button(string.Format("Downgrade to [{0}]", package.Version), EditorStyles.toolbarButton, installButtonWidth, installButtonHeight))
                        {
                            NugetHelper.Update(installed, package);
                            NugetHelper.UpdateInstalledPackages();
                            UpdateUpdatePackages();
                        }
                    }
                }
                else
                {
                    if (GUILayout.Button("Install", EditorStyles.toolbarButton, installButtonWidth, installButtonHeight))
                    {
                        NugetHelper.InstallIdentifier(package);
                        AssetDatabase.Refresh();
                        NugetHelper.UpdateInstalledPackages();
                        UpdateUpdatePackages();
                    }
                }
            }
        }

        public void GUILayoutLink(string url)
        {
            string colorFormatString = "<color=blue>{0}</color>";

            string underline = new string('_', url.Length);

            string formattedUrl = string.Format(colorFormatString, url);
            string formattedUnderline = string.Format(colorFormatString, underline);
            var urlRect = GUILayoutUtility.GetRect(new GUIContent(url), GetProjectUrlStyle());
            GUI.Label(urlRect, formattedUrl, GetProjectUrlStyle());

            EditorGUIUtility.AddCursorRect(urlRect, MouseCursor.Link);
            if (urlRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.MouseUp)
                    Application.OpenURL(url);
            }
        }
    }
}