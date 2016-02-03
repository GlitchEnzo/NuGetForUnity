namespace NugetForUnity
{
    using System;
    using System.Collections.Generic;
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
        private List<NugetPackage> packages = new List<NugetPackage>();

        /// <summary>
        /// The list of NugetPackages already installed.
        /// </summary>
        private List<NugetPackage> installedPackages = new List<NugetPackage>();

        /// <summary>
        /// True to show all old package versions.  False to only show the latest version.
        /// </summary>
        private bool showAllVersions;

        /// <summary>
        /// True to show beta and alpha package versions.  False to only show stable versions.
        /// </summary>
        private bool showPrerelease;

        /// <summary>
        /// The width to use for the install/uninstall/update/downgrade button
        /// </summary>
        private readonly GUILayoutOption installButtonWidth = GUILayout.Width(160);

        /// <summary>
        /// The search term to search the packages for.
        /// </summary>
        private string searchTerm = "Search";

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
        /// The list of package updates available, based on the already installed packages.
        /// </summary>
        private List<NugetPackage> updates = new List<NugetPackage>();

        /// <summary>
        /// The default icon to display for packages.
        /// </summary>
        private Texture2D defaultIcon;

        /// <summary>
        /// Opens the NuGet Package Manager Window.
        /// </summary>
        [MenuItem("NuGet/Manage NuGet Packages")]
        protected static void DisplayNugetWindow()
        {
            GetWindow<NugetWindow>();
        }

        /// <summary>
        /// Restores all packages defined in packages.config
        /// </summary>
        [MenuItem("NuGet/Restore Packages")]
        protected static void RestorePackages()
        {
            NugetHelper.Restore();
        }

        /// <summary>
        /// Reloads the NuGet.config file to get any changes that have occurred.
        /// </summary>
        [MenuItem("NuGet/Reload NuGet.config")]
        protected static void ReloadNugetConfigFile()
        {
            NugetHelper.LoadNugetConfigFile();
        }

        /// <summary>
        /// Creates a new MyPackage.nuspec file.
        /// </summary>
        [MenuItem("Assets/Create/Nuspec File")]
        protected static void CreateNuspecFile()
        {
            string selectedFile = AssetDatabase.GetAssetPath(Selection.activeObject);

            // TODO: Use a better method than string.Replace, since that could remove subfolders
            string filepath = selectedFile.Replace("Assets/", string.Empty);
            filepath = Path.Combine(Application.dataPath, filepath);

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

            ////Debug.LogFormat("Created: {0}", filepath);

            NuspecFile file = new NuspecFile();
            file.Id = "MyPackage";
            file.Version = "0.0.1";
            file.Authors = "Your Name";
            file.Owners = "Your Name";
            file.LicenseUrl = "http://your_license_url_here";
            file.ProjectUrl = "http://your_project_url_here";
            file.Description = "A description of what this packages is and does.";
            file.ReleaseNotes = "Notes for this specific release";
            file.Copyright = "Copyright 2015";
            file.IconUrl = "https://www.nuget.org/Content/Images/packageDefaultIcon-50x50.png";
            file.Save(filepath);

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Called when enabling the window.
        /// </summary>
        private void OnEnable()
        {
            try
            {
                // set the window title
                titleContent = new GUIContent("NuGet");

                // reset the number to skip
                numberToSkip = 0;

                EditorUtility.DisplayProgressBar("Opening NuGet", "Fetching packages from server", 0.3f);

                // update the available packages list
                UpdatePackages();

                EditorUtility.DisplayProgressBar("Opening NuGet", "Getting installed packages and available updates", 0.75f);

                // update the list of installed packages
                UpdateInstalledPackages();

                // load the default icon from the Resources folder
                defaultIcon = (Texture2D)Resources.Load("defaultIcon", typeof(Texture2D));
            }
            catch (Exception e)
            {
                Debug.LogErrorFormat("{0}", e.ToString());
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Updates the list of available packages by running a search with the server using the currently set parameters (# get, # skip, etc).
        /// </summary>
        private void UpdatePackages()
        {
            packages = NugetHelper.Search(searchTerm != "Search" ? searchTerm : string.Empty, showAllVersions, showPrerelease, numberToGet, numberToSkip);
        }

        /// <summary>
        /// Updates the list of installed packages as well as the list of updates available.
        /// </summary>
        private void UpdateInstalledPackages()
        {
            // load a list of install packages
            installedPackages = NugetHelper.GetInstalledPackages();

            // get any available updates for the installed packages
            updates = NugetHelper.GetUpdates(installedPackages, showPrerelease, showAllVersions);
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

            if (updates != null && updates.Count > 0)
            {
                for (int i = 0; i < updates.Count; i++)
                {
                    // alternate the background color for each package
                    if (i % 2 == 0)
                        EditorGUILayout.BeginVertical();
                    else
                        EditorGUILayout.BeginVertical(style);

                    DrawPackage(updates[i]);

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
            // display all of the installed packages
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.BeginVertical();

            GUIStyle style = CreateColoredBackground();

            if (installedPackages != null && installedPackages.Count > 0)
            {
                for (int i = 0; i < installedPackages.Count; i++)
                {
                    // alternate the background color for each package
                    if (i % 2 == 0)
                        EditorGUILayout.BeginVertical();
                    else
                        EditorGUILayout.BeginVertical(style);

                    DrawPackage(installedPackages[i]);

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

            if (packages != null)
            {
                for (int i = 0; i < packages.Count; i++)
                {
                    // alternate the background color for each package
                    if (i % 2 == 0)
                        EditorGUILayout.BeginVertical();
                    else
                        EditorGUILayout.BeginVertical(style);

                    DrawPackage(packages[i]);

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
                packages.AddRange(NugetHelper.Search(searchTerm != "Search" ? searchTerm : string.Empty, showAllVersions, showPrerelease, numberToGet, numberToSkip));
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
                    bool showAllVersionsTemp = EditorGUILayout.Toggle("Show All Versions", showAllVersions);
                    if (showAllVersionsTemp != showAllVersions)
                    {
                        showAllVersions = showAllVersionsTemp;
                        UpdatePackages();
                        UpdateInstalledPackages();
                    }

                    if (GUILayout.Button("Refresh", GUILayout.Width(60)))
                    {
                        OnEnable();
                    }
                }
                EditorGUILayout.EndHorizontal();

                bool showPrereleaseTemp = EditorGUILayout.Toggle("Show Prerelease", showPrerelease);
                if (showPrereleaseTemp != showPrerelease)
                {
                    showPrerelease = showPrereleaseTemp;
                    UpdatePackages();
                    UpdateInstalledPackages();
                }

                bool enterPressed = Event.current.Equals(Event.KeyboardEvent("return"));

                // search only if the enter key is pressed
                searchTerm = EditorGUILayout.TextField(searchTerm);
                if (enterPressed)
                {
                    // reset the number to skip
                    numberToSkip = 0;
                    UpdatePackages();
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
                    bool showAllVersionsTemp = EditorGUILayout.Toggle("Show All Versions", showAllVersions);
                    if (showAllVersionsTemp != showAllVersions)
                    {
                        showAllVersions = showAllVersionsTemp;
                        UpdatePackages();
                        UpdateInstalledPackages();
                    }

                    //if (GUILayout.Button("Refresh", GUILayout.Width(60)))
                    //{
                    //    OnEnable();
                    //}
                }
                EditorGUILayout.EndHorizontal();

                bool showPrereleaseTemp = EditorGUILayout.Toggle("Show Prerelease", showPrerelease);
                if (showPrereleaseTemp != showPrerelease)
                {
                    showPrerelease = showPrereleaseTemp;
                    UpdatePackages();
                    UpdateInstalledPackages();
                }

                // search if the search term was changed
                //string searchTermTemp = EditorGUILayout.TextField(searchTerm);
                //if (searchTermTemp != searchTerm)
                //{
                //    searchTerm = searchTermTemp;

                //    // reset the number to skip
                //    numberToSkip = 0;
                //    UpdatePackages();
                //}
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
                        ////installedPackages.Remove(package);
                        NugetHelper.Uninstall(package);
                        UpdateInstalledPackages();
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
                            }
                        }
                        else if (installed > package)
                        {
                            // A newer version is installed
                            if (GUILayout.Button(string.Format("Downgrade [{0}]", installed.Version), installButtonWidth))
                            {
                                NugetHelper.Update(installed, package);
                                UpdateInstalledPackages();
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
                        }
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            // Show the package description
            EditorStyles.label.wordWrap = true;
            EditorStyles.label.fontStyle = FontStyle.Normal;
            EditorGUILayout.LabelField(string.Format("{0}", package.Description));

            // Show the dependencies
            EditorStyles.label.wordWrap = true;
            EditorStyles.label.fontStyle = FontStyle.Italic;
            StringBuilder builder = new StringBuilder();
            foreach (var dependency in package.Dependencies)
            {
                builder.Append(string.Format(" {0} {1};", dependency.Id, dependency.Version));
            }
            if (package.Dependencies.Count == 0)
            {
                builder.Append("NONE;");
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(string.Format("Depends on:{0}", builder.ToString()));
            EditorStyles.label.fontStyle = FontStyle.Normal;

            // Show the license button
            if (GUILayout.Button("View License", GUILayout.Width(120)))
            {
                Application.OpenURL(package.LicenseUrl);
            }

            EditorGUILayout.Separator();
            EditorGUILayout.Separator();
        }
    }
}