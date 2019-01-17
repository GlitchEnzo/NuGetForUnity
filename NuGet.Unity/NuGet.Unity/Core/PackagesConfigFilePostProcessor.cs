using NugetForUnity;
using UnityEditor;
using UnityEngine;

namespace NuGet.Unity.Core
{
    public class PackagesConfigFilePostProcessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach(var importedAsset in importedAssets)
            {
                if(importedAsset.EndsWith(NugetHelper.PackagesFileName))
                {
                    // Reload file and restore...
                    NugetHelper.LoadPackagesConfigFile();
                    NugetHelper.Restore();
                }
            }
        }
    }
}
