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
            var oldForegroundColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(title);
            Console.ForegroundColor = oldForegroundColor;
            Console.WriteLine(message);

            ConsoleKey response;
            do
            {
                Console.Write($"'{ok}' (y)? or '{cancel}' (n) [y/n] ");
                response = Console.ReadKey(false).Key;
                if (response != ConsoleKey.Enter)
                {
                    Console.WriteLine();
                }
            }
            while (response is not ConsoleKey.Y and not ConsoleKey.N);

            return response == ConsoleKey.Y;
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
