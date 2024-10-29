#nullable enable

namespace NugetForUnity
{
    /// <summary>
    ///     Helper to resolve what libraries are already known / imported by unity.
    /// </summary>
    internal static class UnityPreImportedLibraryResolver
    {
        /// <summary>
        ///     Check if a package is already imported in the Unity project e.g. is a part of Unity.
        /// </summary>
        /// <param name="packageId">The package of which the identifier is checked.</param>
        /// <param name="log">Whether to log a message with the result of the check.</param>
        /// <returns>If it is included in Unity.</returns>
        public static bool IsAlreadyImportedInEngine(string packageId, bool log = true)
        {
            // the CLI is running outside of Unity so we can't easily detect what libraries are imported by the Unity Engine.
            return false;
        }
    }
}
