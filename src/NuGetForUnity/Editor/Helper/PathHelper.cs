using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using JetBrains.Annotations;
using UnityEngine;

namespace NugetForUnity.Helper
{
    /// <summary>
    ///     Helper class to handle path operations.
    /// </summary>
    internal static class PathHelper
    {
        /// <summary>
        ///     The path comparison type to use for the current platform the editor is running on.
        /// </summary>
        internal static readonly StringComparison PathComparisonType =
            Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.OSXEditor ?
                StringComparison.OrdinalIgnoreCase :
                StringComparison.Ordinal;

        /// <summary>
        ///     Create a relative path from one path to another. Paths will be resolved before calculating the difference.
        ///     Default path comparison for the active platform will be used (OrdinalIgnoreCase for Windows or Mac, Ordinal for Unix).
        /// </summary>
        /// <remarks>
        ///     Should the project upgrade to Unity version that has .NET Standard 2.1, this can be removed and instead we will use:
        ///     Path.GetRelativePath(relativeTo, path).
        ///     <see
        ///         href="https://github.com/dotnet/runtime/blob/4aa77dfb0674fd61e41e574b8cc8ae63146f52c4/src/libraries/System.Private.CoreLib/src/System/IO/Path.cs#L843" />
        ///     .
        /// </remarks>
        /// <param name="relativeTo">The source path the output should be relative to. This path is always considered to be a directory.</param>
        /// <param name="path">The destination path.</param>
        /// <returns>The relative path or <paramref name="path" /> if the paths don't share the same root.</returns>
        [NotNull]
        internal static string GetRelativePath([NotNull] string relativeTo, [NotNull] string path)
        {
            if (string.IsNullOrEmpty(relativeTo))
            {
                throw new ArgumentNullException(nameof(relativeTo));
            }

            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            relativeTo = Path.GetFullPath(relativeTo);
            path = Path.GetFullPath(path);

            // Need to check if the roots are different - if they are we need to return the "to" path.
            if (!AreRootsEqual(relativeTo, path))
            {
                return path;
            }

            var commonLength = GetCommonPathLength(relativeTo, path, PathComparisonType == StringComparison.OrdinalIgnoreCase);

            // If there is nothing in common they can't share the same root, return the "to" path as is.
            if (commonLength == 0)
            {
                return path;
            }

            // Trailing separators aren't significant for comparison
            var relativeToLength = relativeTo.Length;
            if (EndsInDirectorySeparator(relativeTo))
            {
                relativeToLength--;
            }

            var pathEndsInSeparator = EndsInDirectorySeparator(path);
            var pathLength = path.Length;
            if (pathEndsInSeparator)
            {
                pathLength--;
            }

            // If we have effectively the same path, return "."
            if (relativeToLength == pathLength && commonLength >= relativeToLength)
            {
                return ".";
            }

            // We have the same root, we need to calculate the difference now using the
            // common Length and Segment count past the length.
            //
            // Some examples:
            //
            //  C:\Foo C:\Bar L3, S1 -> ..\Bar
            //  C:\Foo C:\Foo\Bar L6, S0 -> Bar
            //  C:\Foo\Bar C:\Bar\Bar L3, S2 -> ..\..\Bar\Bar
            //  C:\Foo\Foo C:\Foo\Bar L7, S1 -> ..\Bar
            var sb = new StringBuilder();
            sb.EnsureCapacity(Math.Max(relativeTo.Length, path.Length));

            // Add parent segments for segments past the common on the "from" path
            if (commonLength < relativeToLength)
            {
                sb.Append("..");

                for (var i = commonLength + 1; i < relativeToLength; i++)
                {
                    if (IsDirectorySeparator(relativeTo[i]))
                    {
                        sb.Append(Path.DirectorySeparatorChar);
                        sb.Append("..");
                    }
                }
            }
            else if (IsDirectorySeparator(path[commonLength]))
            {
                // No parent segments and we need to eat the initial separator
                //  (C:\Foo C:\Foo\Bar case)
                commonLength++;
            }

            // Now add the rest of the "to" path, adding back the trailing separator
            var differenceLength = pathLength - commonLength;
            if (pathEndsInSeparator)
            {
                differenceLength++;
            }

            if (differenceLength > 0)
            {
                if (sb.Length > 0)
                {
                    sb.Append(Path.DirectorySeparatorChar);
                }

                sb.Append(path.Substring(commonLength, differenceLength));
            }

            return sb.ToString();
        }

        /// <summary>
        ///     Get the common path length from the start of the string.
        /// </summary>
        private static int GetCommonPathLength([NotNull] string first, [NotNull] string second, bool ignoreCase)
        {
            var commonChars = EqualStartingCharacterCount(first, second, ignoreCase);

            // If nothing matches
            if (commonChars == 0)
            {
                return commonChars;
            }

            // Or we're a full string and equal length or match to a separator
            if (commonChars == first.Length && (commonChars == second.Length || IsDirectorySeparator(second[commonChars])))
            {
                return commonChars;
            }

            if (commonChars == second.Length && IsDirectorySeparator(first[commonChars]))
            {
                return commonChars;
            }

            // It's possible we matched somewhere in the middle of a segment e.g. C:\Foodie and C:\Foobar.
            while (commonChars > 0 && !IsDirectorySeparator(first[commonChars - 1]))
            {
                commonChars--;
            }

            return commonChars;
        }

        /// <summary>
        ///     Gets the count of common characters from the left optionally ignoring case.
        /// </summary>
        private static int EqualStartingCharacterCount([CanBeNull] string first, [CanBeNull] string second, bool ignoreCase)
        {
            if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second))
            {
                return 0;
            }

            var commonChars = 0;
            for (; commonChars < first.Length && commonChars < second.Length; ++commonChars)
            {
                if (first[commonChars] == second[commonChars] ||
                    (ignoreCase && char.ToUpperInvariant(first[commonChars]) == char.ToUpperInvariant(second[commonChars])))
                {
                    continue;
                }

                break;
            }

            return commonChars;
        }

        /// <summary>
        ///     Returns true if the two paths have the same root.
        /// </summary>
        private static bool AreRootsEqual([NotNull] string first, [NotNull] string second)
        {
            var firstRoot = Path.GetPathRoot(first);
            var secondRoot = Path.GetPathRoot(second);

            return string.Equals(firstRoot, secondRoot, PathComparisonType);
        }

        /// <summary>
        ///     True if the given character is a directory separator.
        /// </summary>
        /// <param name="c">The char to compare.</param>
        /// <returns>True if is a separator.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsDirectorySeparator(char c)
        {
            return c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;
        }

        /// <summary>
        ///     Returns true if the path ends in a directory separator.
        /// </summary>
        private static bool EndsInDirectorySeparator([CanBeNull] string path)
        {
            return !string.IsNullOrEmpty(path) && IsDirectorySeparator(path[path.Length - 1]);
        }
    }
}
