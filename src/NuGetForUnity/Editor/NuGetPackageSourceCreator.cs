using System;
using System.Collections.Generic;

namespace NugetForUnity
{
    internal static class NuGetPackageSourceCreator
    {
        public static INuGetPackageSource CreatePackageSource(string name, string path, List<INuGetPackageSource> packageSources)
        {
            if (name.Equals("All", StringComparison.OrdinalIgnoreCase) && path.Equals("(Aggregate source)", StringComparison.OrdinalIgnoreCase))
            {
                return new CombinedNuGetPackageSource(packageSources);
            }

            if (!path.StartsWith("http"))
            {
                return new LocalNuGetPackageSource(name, path);
            }

            // see: https://learn.microsoft.com/en-us/nuget/reference/nuget-config-file
            // The NuGet server protocol version defaults to version "2" when not pointing to a package source URL ending in .json (e.g. https://api.nuget.org/v3/index.json).
            if (path.EndsWith(".json"))
            {
                return new NuGetPackageSourceV3(name, path);
            }

            return new NuGetPackageSourceV2(name, path);
        }
    }
}
