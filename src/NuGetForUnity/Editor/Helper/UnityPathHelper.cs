using System;
using System.IO;
using UnityEngine;

namespace NugetForUnity.Helper
{
    /// <summary>
    ///     Helper class for Unity-Project related paths.
    /// </summary>
    internal static class UnityPathHelper
    {
        static UnityPathHelper()
        {
            AbsoluteAssetsPath = Path.GetFullPath(Application.dataPath);
            AbsoluteProjectPath = Path.GetDirectoryName(AbsoluteAssetsPath) ?? throw new InvalidOperationException("Can't detect project root.");
        }

        /// <summary>
        ///     Gets the absolute path to the Unity-Project 'Assets' directory.
        /// </summary>
        internal static string AbsoluteAssetsPath { get; }

        /// <summary>
        ///     Gets the absolute path to the Unity-Project root directory.
        /// </summary>
        internal static string AbsoluteProjectPath { get; }

        /// <summary>
        ///     Checks if given path is within Assets folder.
        /// </summary>
        /// <param name="path">Path to check.</param>
        /// <returns>True if path is within Assets folder, false otherwise.</returns>
        internal static bool IsPathInAssets(string path)
        {
            var assetsRelativePath = GetAssetsRelativePath(path);
            return !Path.IsPathRooted(assetsRelativePath) && !assetsRelativePath.StartsWith("..");
        }

        /// <summary>
        ///     Returns the path relative to Assets directory, or <c>"."</c> if it is the Assets directory.
        /// </summary>
        /// <param name="path">The path of witch we calculate the relative path of.</param>
        /// <returns>The path relative to Assets directory, or <c>"."</c> if it is the Assets directory.</returns>
        private static string GetAssetsRelativePath(string path)
        {
            return PathHelper.GetRelativePath(Application.dataPath, path);
        }
    }
}
