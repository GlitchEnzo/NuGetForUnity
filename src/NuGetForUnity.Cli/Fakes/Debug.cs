#nullable enable

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace UnityEngine
{
    internal static class Debug
    {
        internal static bool HasError { get; private set; }

        internal static void LogErrorFormat(string format, params object[] args)
        {
            HasError = true;
            Console.Error.WriteLine(format, args);
        }

        internal static void LogFormat(string format, params object[] args)
        {
            Console.WriteLine(format, args);
        }

        internal static void Log(string message)
        {
            Console.WriteLine(message);
        }

        internal static void LogWarning(string message)
        {
            var oldForegroundColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine(message);
            Console.ForegroundColor = oldForegroundColor;
        }

        internal static void LogWarningFormat(string format, params object[] args)
        {
            var oldForegroundColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine(format, args);
            Console.ForegroundColor = oldForegroundColor;
        }

        internal static void LogError(string message)
        {
            HasError = true;
            var oldForegroundColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.Error.WriteLine(message);
            Console.ForegroundColor = oldForegroundColor;
        }

        internal static void LogException(Exception e)
        {
            HasError = true;
            var oldForegroundColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.Error.WriteLine(e.ToString());
            Console.ForegroundColor = oldForegroundColor;
        }

        [Conditional("DEBUG")]
        internal static void Assert([DoesNotReturnIf(false)] bool condition, string message)
        {
            System.Diagnostics.Debug.Assert(condition, message);
        }
    }
}
