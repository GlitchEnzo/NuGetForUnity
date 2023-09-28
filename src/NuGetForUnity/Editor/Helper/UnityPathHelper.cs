#pragma warning disable SA1512,SA1124 // Single-line comments should not be followed by blank line

using System;
using System.IO;
using JetBrains.Annotations;
using UnityEngine;

#region No ReShaper

// ReSharper disable All
// needed because 'JetBrains.Annotations.NotNull' and 'System.Diagnostics.CodeAnalysis.NotNull' collide if this file is compiled with a never version of Unity / C#
using SuppressMessageAttribute = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;

// ReSharper restore All

#endregion

#pragma warning restore SA1512,SA1124 // Single-line comments should not be followed by blank line
namespace NugetForUnity.Helper
{
    /// <summary>
    ///     Helper class for Unity-Project related paths.
    /// </summary>
    internal static class UnityPathHelper
    {
        [SuppressMessage("Design", "CA1065:Do not raise exceptions in unexpected locations", Justification = "Unity can handle the exception.")]
        static UnityPathHelper()
        {
            AbsoluteAssetsPath = Path.GetFullPath(Application.dataPath);
            AbsoluteProjectPath = Path.GetDirectoryName(AbsoluteAssetsPath) ?? throw new InvalidOperationException("Can't detect project root.");
        }

        /// <summary>
        ///     Gets the absolute path to the Unity-Project 'Assets' directory.
        /// </summary>
        [NotNull]
        internal static string AbsoluteAssetsPath { get; }

        /// <summary>
        ///     Gets the absolute path to the Unity-Project root directory.
        /// </summary>
        [NotNull]
        internal static string AbsoluteProjectPath { get; }

        /// <summary>
        ///     Checks if given path is within Assets folder.
        /// </summary>
        /// <param name="path">Path to check.</param>
        /// <returns>True if path is within Assets folder, false otherwise.</returns>
        internal static bool IsPathInAssets([NotNull] string path)
        {
            var assetsRelativePath = GetAssetsRelativePath(path);
            return !Path.IsPathRooted(assetsRelativePath) && !assetsRelativePath.StartsWith("..", StringComparison.Ordinal);
        }

        /// <summary>
        ///     Returns the path relative to Assets directory, or <c>"."</c> if it is the Assets directory.
        /// </summary>
        /// <param name="path">The path of witch we calculate the relative path of.</param>
        /// <returns>The path relative to Assets directory, or <c>"."</c> if it is the Assets directory.</returns>
        [NotNull]
        private static string GetAssetsRelativePath([NotNull] string path)
        {
            return PathHelper.GetRelativePath(Application.dataPath, path);
        }
    }
}
