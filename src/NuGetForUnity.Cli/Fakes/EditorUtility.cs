#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;

namespace UnityEditor
{
    internal static class EditorUtility
    {
        private static string currentProgressBarTitle = string.Empty;

        internal static void ClearProgressBar()
        {
            currentProgressBarTitle = string.Empty;
        }

        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Used by Unity.")]
        internal static void DisplayProgressBar(string title, string status, float progress)
        {
            if (title == currentProgressBarTitle)
            {
                return;
            }

            currentProgressBarTitle = title;
            Console.WriteLine($"{title} ...");
        }
    }
}
