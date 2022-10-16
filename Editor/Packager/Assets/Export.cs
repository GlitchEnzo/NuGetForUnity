using UnityEditor;

namespace NugetForUnity
{
    /// <summary>
    /// Enables export of the package from Menus or the Cmd Line
    /// </summary>
    public static class Export
    {
        [MenuItem("Packager/Export", priority = -1)]
        public static void Execute()
        {
            AssetDatabase.ExportPackage(
                new string[] { "Packages\\NuGetForUnity\\Editor\\Packager\\Assets\\NuGet" },
                "NugetForUnity.unitypackage",
                ExportPackageOptions.Recurse);
        }
    }
}

