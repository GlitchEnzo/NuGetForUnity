#nullable enable
using System.Reflection;
using NuGetForUnity.Cli;
using NugetForUnity.Configuration;

namespace NugetForUnity.Helper
{
    /// <summary>
    /// Assembly load implementation for CLI version. There is another implementation for Unity Editor version.
    /// </summary>
    internal static class AssemblyLoader
    {
        /// <summary>
        /// Loads the assembly for the given pluginId
        /// </summary>
        /// <param name="pluginId">Plugin Id to load.</param>
        /// <returns>Assembly of the loaded plugin.</returns>
        internal static Assembly Load(NugetForUnityPluginId pluginId)
        {
            var loadContext = new NugetAssemblyLoadContext(pluginId.Path);
            var assembly = loadContext.LoadFromAssemblyPath(pluginId.Path);
            return assembly;
        }
    }
}
