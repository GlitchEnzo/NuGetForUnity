using NugetForUnity.Configuration;
using UnityEditor;

namespace NugetForUnity.Ui
{
#if NUGETFORUNITY_MENU_TOOLS && NUGETFORUNITY_MENU_WINDOW_PACKAGE_MANAGER
#error Define only one NuGetForUnity menu location symbol.
#endif

    /// <summary>
    ///     Registers NuGet menu items at the compile-time selected root path.
    /// </summary>
    internal static class NugetMenu
    {
#if NUGETFORUNITY_MENU_TOOLS
        internal const string MenuRoot = "Tools/NuGet";
#elif NUGETFORUNITY_MENU_WINDOW_PACKAGE_MANAGER
        internal const string MenuRoot = "Window/Package Management/NuGet";
#else
        internal const string MenuRoot = NugetConfigFile.DefaultMenuRoot;
#endif

        internal const string ManagePackagesMenuItem = MenuRoot + "/Manage NuGet Packages";

        internal const string RestorePackagesMenuItem = MenuRoot + "/Restore Packages";

        internal const string DependencyTreeMenuItem = MenuRoot + "/Show Dependency Tree";

        internal const string PreferencesMenuItem = MenuRoot + "/Preferences";

        internal const string VersionMenuItem = MenuRoot + "/Version " + NugetPreferences.NuGetForUnityVersion + " \uD83D\uDD17";

        internal const string CheckForUpdatesMenuItem = MenuRoot + "/Check for Updates...";

        [MenuItem(ManagePackagesMenuItem, false, 0)]
        private static void DisplayNugetWindow()
        {
            NugetWindow.DisplayNugetWindow();
        }

        [MenuItem(RestorePackagesMenuItem, false, 1)]
        private static void RestorePackages()
        {
            NugetWindow.RestorePackages();
        }

        [MenuItem(DependencyTreeMenuItem, false, 5)]
        private static void DisplayDependencyTree()
        {
            DependencyTreeViewer.DisplayDependencyTree();
        }

        [MenuItem(PreferencesMenuItem, false, 9)]
        private static void DisplayPreferences()
        {
            NugetWindow.DisplayPreferences();
        }

        [MenuItem(VersionMenuItem, false, 10)]
        private static void DisplayVersion()
        {
            NuGetForUnityUpdater.DisplayVersion();
        }

        [MenuItem(CheckForUpdatesMenuItem, false, 11)]
        private static void CheckForUpdates()
        {
            NuGetForUnityUpdater.CheckForUpdates();
        }
    }
}
