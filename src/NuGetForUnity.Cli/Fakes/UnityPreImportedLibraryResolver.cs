#nullable enable

using System.Collections.Generic;

namespace NugetForUnity
{
    /// <summary>
    ///     Helper to resolve what libraries are already known / imported by unity.
    /// </summary>
    internal static class UnityPreImportedLibraryResolver
    {
        /// <summary>
        ///     Gets all libraries that are already imported by unity so we shouldn't / don't need to install them as NuGet packages.
        /// </summary>
        /// <returns>A set of all names of libraries that are already imported by unity.</returns>
        internal static HashSet<string> GetAlreadyImportedLibs()
        {
            // the CLI is running outside of Unity so we can't easily detect what libraries are imported by the Unity Engine.
            return new HashSet<string>();
        }
    }
}
