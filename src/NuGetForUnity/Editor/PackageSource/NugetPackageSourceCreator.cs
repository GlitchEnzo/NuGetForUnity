using System;
using System.Collections.Generic;

namespace NugetForUnity.PackageSource
{
    /// <summary>
    ///     Helper to create <see cref="INugetPackageSource" /> instances.
    /// </summary>
    internal static class NugetPackageSourceCreator
    {
        /// <summary>
        ///     Creates a new <see cref="INugetPackageSource" /> instance.
        /// </summary>
        /// <param name="name">The name of the source.</param>
        /// <param name="path">The path or url of the source.</param>
        /// <param name="packageSources">The list of all source that should be used when creating a combined package source.</param>
        /// <returns>The created source.</returns>
        public static INugetPackageSource CreatePackageSource(string name, string path, List<INugetPackageSource> packageSources)
        {
            name = name.Trim();
            path = path.Trim();
            if (name.Equals("All", StringComparison.OrdinalIgnoreCase) && path.Equals("(Aggregate source)", StringComparison.OrdinalIgnoreCase))
            {
                return new NugetPackageSourceCombined(packageSources);
            }

            if (!path.StartsWith("http"))
            {
                return new NugetPackageSourceLocal(name, path);
            }

            // see: https://learn.microsoft.com/en-us/nuget/reference/nuget-config-file
            // The NuGet server protocol version defaults to version "2" when not pointing to a package source URL ending in .json (e.g. https://api.nuget.org/v3/index.json).
            if (path.EndsWith(".json"))
            {
                return new NugetPackageSourceV3(name, path);
            }

            return new NugetPackageSourceV2(name, path);
        }
    }
}
