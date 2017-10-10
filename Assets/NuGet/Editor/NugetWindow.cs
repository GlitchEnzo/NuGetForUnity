namespace NugetForUnity
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Represents the NuGet Package Manager Window in the Unity Editor.
    /// </summary>
    public class NugetWindow : EditorWindow
    {
        /// <summary>
        /// The current position of the scroll bar in the GUI.
        /// </summary>
        private Vector2 scrollPosition;

        /// <summary>
        /// The list of NugetPackages available to install.
        /// </summary>
        private List<NugetPackage> availablePackages = new List<NugetPackage>();

        /// <summary>
        /// The list of NugetPackages already installed.
        /// </summary>
        private List<NugetPackage> installedPackages = new List<NugetPackage>();

        /// <summary>
        /// The filtered list of NugetPackages already installed.
        /// </summary>
        private List<NugetPackage> filteredInstalledPackages = new List<NugetPackage>();

        /// <summary>
        /// The list of package updates available, based on the already installed packages.
        /// </summary>
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
        private readonly GUILayoutOption installButtonWidth = GUILayout.Width(160);

        /// <summary>
        /// The search term to search the online packages for.
        /// </summary>
        private string onlineSearchTerm = "Search";

        /// <summary>
        /// The search term to search the installed packages for.
        /// </summary>
        private string installedSearchTerm = "Search";

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
        private int numberToSkip;

        /// <summary>
        /// The currently selected tab in the window.
        /// </summary>
        private int currentTab;

        /// <summary>
        /// The titles of the tabs in the window.
        /// </summary>
        private readonly string[] tabTitles = { "Online", "Installed", "Updates" };

        /// <summary>
        /// The default icon to display for packages.
        /// </summary>
        private Texture2D defaultIcon;

        /// <summary>
        /// Opens the NuGet Package Manager Window.
        /// </summary>
        [MenuItem("NuGet/Manage NuGet Packages", false, 0)]
        protected static void DisplayNugetWindow()
        {
            GetWindow<NugetWindow>();
        }

        /// <summary>
        /// Restores all packages defined in packages.config
        /// </summary>
        [MenuItem("NuGet/Restore Packages", false, 1)]
        protected static void RestorePackages()
        {
            NugetHelper.Restore();
        }

        /// <summary>
        /// Displays the version number of NuGetForUnity.
        /// </summary>
        [MenuItem("NuGet/Version " + NugetPreferences.NuGetForUnityVersion, false, 10)]
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
        /// Creates a new MyPackage.nuspec file.
        /// </summary>
        [MenuItem("Assets/Create/Nuspec File")]
        protected static void CreateNuspecFile()
        {
            string filepath = Application.dataPath;

            if (Selection.activeObject != null && Selection.activeObject != Selection.activeGameObject)
            {
                string selectedFile = AssetDatabase.GetAssetPath(Selection.activeObject);
                filepath = selectedFile.Substring("Assets/".Length);
                filepath = Path.Combine(Application.dataPath, filepath);
            }

            if (!string.IsNullOrEmpty(Path.GetExtension(filepath)))
            {
                // if it was a file that was selected, replace the filename
                filepath = filepath.Replace(Path.GetFileName(filepath), string.Empty);
                filepath += "MyPackage.nuspec";
            }
            else
            {
                // if it was a directory that was selected, simply add the filename
                filepath += "/MyPackage.nuspec";
            }

            UnityEngine.Debug.LogFormat("Creating: {0}", filepath);

            NuspecFile file = new NuspecFile();
            file.Id = "MyPackage";
            file.Version = "0.0.1";
            file.Authors = "Your Name";
            file.Owners = "Your Name";
            file.LicenseUrl = "http://your_license_url_here";
            file.ProjectUrl = "http://your_project_url_here";
            file.Description = "A description of what this packages is and does.";
            file.ReleaseNotes = "Notes for this specific release";
            file.Copyright = "Copyright 2016";
            file.IconUrl = "https://www.nuget.org/Content/Images/packageDefaultIcon-50x50.png";
            file.Save(filepath);

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Called when enabling the window.
        /// </summary>
        private void OnEnable()
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                // if we are entering playmode, don't do anything
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    return;
                }

                NugetHelper.LogVerbose("NugetWindow reloading config and updating packages");

                // reload the NuGet.config file, in case it was changed after Unity opened, but before the manager window opened (now)
                NugetHelper.LoadNugetConfigFile();

                // set the window title
                titleContent = new GUIContent("NuGet");

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
            availablePackages = NugetHelper.Search(onlineSearchTerm != "Search" ? onlineSearchTerm : string.Empty, showAllOnlineVersions, showOnlinePrerelease, numberToGet, numberToSkip);
        }

        /// <summary>
        /// Updates the list of installed packages.
        /// </summary>
        private void UpdateInstalledPackages()
        {
            // load a list of install packages
            installedPackages = NugetHelper.GetInstalledPackages().Values.ToList();
            filteredInstalledPackages = installedPackages;

            if (installedSearchTerm != "Search")
            {
                filteredInstalledPackages = installedPackages.Where(x => x.Id.ToLower().Contains(installedSearchTerm) || x.Title.ToLower().Contains(installedSearchTerm)).ToList();
            }
        }

        /// <summary>
        /// Updates the list of update packages.
        /// </summary>
        private void UpdateUpdatePackages()
        {
            // get any available updates for the installed packages
            updatePackages = NugetHelper.GetUpdates(installedPackages, showPrereleaseUpdates, showAllUpdateVersions);
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
            currentTab = GUILayout.Toolbar(currentTab, tabTitles);

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

        /// <summary>
        /// Creates the alternating background color based upon if the Unity Editor is the free (light) skin or the Pro (dark) skin.
        /// </summary>
        /// <returns>The GUI style with the appropriate background color set.</returns>
        private GUIStyle CreateColoredBackground()
        {
            GUIStyle style = new GUIStyle();
            if (Application.HasProLicense())
            {
                style.normal.background = MakeTex(20, 20, new Color(0.3f, 0.3f, 0.3f));
            }
            else
            {
                style.normal.background = MakeTex(20, 20, new Color(0.6f, 0.6f, 0.6f));
            }

            return style;
        }

        /// <summary>
        /// Draws the list of installed packages that have updates available.
        /// </summary>
        private void DrawUpdates()
        {
            DrawUpdatesHeader();

            // display all of the installed packages
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.BeginVertical();

            GUIStyle style = CreateColoredBackground();

            if (filteredUpdatePackages != null && filteredUpdatePackages.Count > 0)
            {
                for (int i = 0; i < filteredUpdatePackages.Count; i++)
                {
                    // alternate the background color for each package
                    if (i % 2 == 0)
                        EditorGUILayout.BeginVertical();
                    else
                        EditorGUILayout.BeginVertical(style);

                    DrawPackage(filteredUpdatePackages[i]);

                    EditorGUILayout.EndVertical();
                }
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
        /// Draws the list of installed packages.
        /// </summary>
        private void DrawInstalled()
        {
            DrawInstalledHeader();

            // display all of the installed packages
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.BeginVertical();

            GUIStyle style = CreateColoredBackground();

            if (filteredInstalledPackages != null && filteredInstalledPackages.Count > 0)
            {
                for (int i = 0; i < filteredInstalledPackages.Count; i++)
                {
                    // alternate the background color for each package
                    if (i % 2 == 0)
                        EditorGUILayout.BeginVertical();
                    else
                        EditorGUILayout.BeginVertical(style);

                    DrawPackage(filteredInstalledPackages[i]);

                    EditorGUILayout.EndVertical();
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
        /// Draws the current list of available online packages.
        /// </summary>
        private void DrawOnline()
        {
            DrawOnlineHeader();

            // display all of the packages
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.BeginVertical();

            GUIStyle style = CreateColoredBackground();

            if (availablePackages != null)
            {
                for (int i = 0; i < availablePackages.Count; i++)
                {
                    // alternate the background color for each package
                    if (i % 2 == 0)
                        EditorGUILayout.BeginVertical();
                    else
                        EditorGUILayout.BeginVertical(style);

                    DrawPackage(availablePackages[i]);

                    EditorGUILayout.EndVertical();
                }
            }

            GUIStyle showMoreStyle = new GUIStyle();
            if (Application.HasProLicense())
            {
                showMoreStyle.normal.background = MakeTex(20, 20, new Color(0.05f, 0.05f, 0.05f));
            }
            else
            {
                showMoreStyle.normal.background = MakeTex(20, 20, new Color(0.4f, 0.4f, 0.4f));
            }

            EditorGUILayout.BeginVertical(showMoreStyle);
            // allow the user to dislay more results
            if (GUILayout.Button("Show More", GUILayout.Width(120)))
            {
                numberToSkip += numberToGet;
                availablePackages.AddRange(NugetHelper.Search(onlineSearchTerm != "Search" ? onlineSearchTerm : string.Empty, showAllOnlineVersions, showOnlinePrerelease, numberToGet, numberToSkip));
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Draws the header which allows filtering the online list of packages.
        /// </summary>
        private void DrawOnlineHeader()
        {
            GUIStyle headerStyle = new GUIStyle();
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
                    bool showAllVersionsTemp = EditorGUILayout.Toggle("Show All Versions", showAllOnlineVersions);
                    if (showAllVersionsTemp != showAllOnlineVersions)
                    {
                        showAllOnlineVersions = showAllVersionsTemp;
                        UpdateOnlinePackages();
                    }

                    if (GUILayout.Button("Refresh", GUILayout.Width(60)))
                    {
                        OnEnable();
                    }
                }
                EditorGUILayout.EndHorizontal();

                bool showPrereleaseTemp = EditorGUILayout.Toggle("Show Prerelease", showOnlinePrerelease);
                if (showPrereleaseTemp != showOnlinePrerelease)
                {
                    showOnlinePrerelease = showPrereleaseTemp;
                    UpdateOnlinePackages();
                }

                bool enterPressed = Event.current.Equals(Event.KeyboardEvent("return"));

                EditorGUILayout.BeginHorizontal();
                {
                    int oldFontSize = GUI.skin.textField.fontSize;
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
        /// Draws the header which allows filtering the installed list of packages.
        /// </summary>
        private void DrawInstalledHeader()
        {
            GUIStyle headerStyle = new GUIStyle();
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
                bool enterPressed = Event.current.Equals(Event.KeyboardEvent("return"));

                EditorGUILayout.BeginHorizontal();
                {
                    int oldFontSize = GUI.skin.textField.fontSize;
                    GUI.skin.textField.fontSize = 25;
                    installedSearchTerm = EditorGUILayout.TextField(installedSearchTerm, GUILayout.Height(30));

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
                    if (installedSearchTerm != "Search")
                    {
                        filteredInstalledPackages = installedPackages.Where(x => x.Id.ToLower().Contains(installedSearchTerm) || x.Title.ToLower().Contains(installedSearchTerm)).ToList();
                    }
                }
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws the header for the Updates tab.
        /// </summary>
        private void DrawUpdatesHeader()
        {
            GUIStyle headerStyle = new GUIStyle();
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
                    bool showAllVersionsTemp = EditorGUILayout.Toggle("Show All Versions", showAllUpdateVersions);
                    if (showAllVersionsTemp != showAllUpdateVersions)
                    {
                        showAllUpdateVersions = showAllVersionsTemp;
                        UpdateUpdatePackages();
                    }

                    if (GUILayout.Button("Install All Updates", GUILayout.Width(150)))
                    {
                        NugetHelper.UpdateAll(updatePackages, installedPackages);
                        UpdateInstalledPackages();
                        UpdateUpdatePackages();
                    }
                }
                EditorGUILayout.EndHorizontal();

                bool showPrereleaseTemp = EditorGUILayout.Toggle("Show Prerelease", showPrereleaseUpdates);
                if (showPrereleaseTemp != showPrereleaseUpdates)
                {
                    showPrereleaseUpdates = showPrereleaseTemp;
                    UpdateUpdatePackages();
                }

                bool enterPressed = Event.current.Equals(Event.KeyboardEvent("return"));

                EditorGUILayout.BeginHorizontal();
                {
                    int oldFontSize = GUI.skin.textField.fontSize;
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
                        filteredUpdatePackages = updatePackages.Where(x => x.Id.ToLower().Contains(updatesSearchTerm) || x.Title.ToLower().Contains(updatesSearchTerm)).ToList();
                    }
                }
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws the given <see cref="NugetPackage"/>.
        /// </summary>
        /// <param name="package">The <see cref="NugetPackage"/> to draw.</param>
        private void DrawPackage(NugetPackage package)
        {
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
                    const int leftPadding = 5;
                    Rect rect = GUILayoutUtility.GetRect(iconSize, iconSize);
                    // only use GetRect's Y position.  It doesn't correctly set the width or X position.
                    rect.x = leftPadding;
                    rect.y += 3;
                    rect.width = iconSize;
                    rect.height = iconSize;

                    if (package.Icon != null)
                    {
                        GUI.DrawTexture(rect, package.Icon, ScaleMode.StretchToFill);
                    }
                    else
                    {
                        GUI.DrawTexture(rect, defaultIcon, ScaleMode.StretchToFill);
                    }

                    rect = GUILayoutUtility.GetRect(position.width / 2 - (iconSize + leftPadding), 20);
                    rect.x = iconSize + leftPadding;
                    rect.y += 10;

                    EditorStyles.label.fontStyle = FontStyle.Bold;
                    EditorStyles.label.fontSize = 14;
                    ////EditorGUILayout.LabelField(string.Format("{1} [{0}]", package.Version, package.Id), GUILayout.Height(20), GUILayout.Width(position.width / 2 - 32));
                    GUI.Label(rect, string.Format("{1} [{0}]", package.Version, package.Title), EditorStyles.label);
                    EditorStyles.label.fontSize = 10;
                    EditorStyles.label.fontStyle = FontStyle.Normal;
                }
                EditorGUILayout.EndHorizontal();

                if (installedPackages.Contains(package))
                {
                    // This specific version is installed
                    if (GUILayout.Button("Uninstall", installButtonWidth))
                    {
                        // TODO: Perhaps use a "mark as dirty" system instead of updating all of the data all the time? 
                        NugetHelper.Uninstall(package);
                        UpdateInstalledPackages();
                        UpdateUpdatePackages();
                    }
                }
                else
                {
                    var installed = installedPackages.FirstOrDefault(p => p.Id == package.Id);
                    if (installed != null)
                    {
                        if (installed < package)
                        {
                            // An older version is installed
                            if (GUILayout.Button(string.Format("Update [{0}]", installed.Version), installButtonWidth))
                            {
                                NugetHelper.Update(installed, package);
                                UpdateInstalledPackages();
                                UpdateUpdatePackages();
                            }
                        }
                        else if (installed > package)
                        {
                            // A newer version is installed
                            if (GUILayout.Button(string.Format("Downgrade [{0}]", installed.Version), installButtonWidth))
                            {
                                NugetHelper.Update(installed, package);
                                UpdateInstalledPackages();
                                UpdateUpdatePackages();
                            }
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Install", installButtonWidth))
                        {
                            NugetHelper.Install(package);
                            AssetDatabase.Refresh();
                            UpdateInstalledPackages();
                            UpdateUpdatePackages();
                        }
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            // Show the package description
            EditorStyles.label.wordWrap = true;
            //EditorStyles.label.fontStyle = FontStyle.Bold;
            //EditorGUILayout.LabelField(string.Format("Description:"));
            EditorStyles.label.fontStyle = FontStyle.Normal;
            EditorGUILayout.LabelField(string.Format("{0}", package.Description));

            // Show the package release notes
            if (!string.IsNullOrEmpty(package.ReleaseNotes))
            {
                EditorStyles.label.wordWrap = true;
                EditorStyles.label.fontStyle = FontStyle.Bold;
                EditorGUILayout.LabelField(string.Format("Release Notes:"));
                EditorStyles.label.fontStyle = FontStyle.Normal;
                EditorGUILayout.LabelField(string.Format("{0}", package.ReleaseNotes));
            }

            // Show the dependencies
            if (package.Dependencies.Count > 0)
            {
                EditorStyles.label.wordWrap = true;
                EditorStyles.label.fontStyle = FontStyle.Italic;
                StringBuilder builder = new StringBuilder();
                foreach (var dependency in package.Dependencies)
                {
                    builder.Append(string.Format(" {0} {1};", dependency.Id, dependency.Version));
                }
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(string.Format("Depends on:{0}", builder.ToString()));
                EditorStyles.label.fontStyle = FontStyle.Normal;
            }

            // Show the license button
            if (!string.IsNullOrEmpty(package.LicenseUrl) && package.LicenseUrl != "http://your_license_url_here")
            {
                if (GUILayout.Button("View License", GUILayout.Width(120)))
                {
                    Application.OpenURL(package.LicenseUrl);
                }
            }

            EditorGUILayout.Separator();
            EditorGUILayout.Separator();
        }
    }
}