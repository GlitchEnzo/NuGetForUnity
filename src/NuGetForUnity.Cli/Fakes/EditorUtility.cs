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

        internal static bool DisplayDialog(string title, string message, string ok, string cancel)
        {
            throw new InvalidOperationException(
                $"Trying to open a dialog with title: '{title}' and message '{message}' but we are inside a CLI. Dialogs are currently not supported by the CLI as it is intended to be used in non-interactive environments like CI/CD pipelines.");
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
