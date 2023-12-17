using System.Reflection;
using JetBrains.Annotations;
using NugetForUnity.Configuration;

namespace NugetForUnity.Helper
{
    /// <summary>
    ///     Assembly load implementation for Unity Editor. There is another implementation for CLI version.
    /// </summary>
    internal static class AssemblyLoader
    {
        /// <summary>
        ///     Loads the assembly for the given <paramref name="pluginId" />.
        /// </summary>
        /// <param name="pluginId">Plugin Id to load.</param>
        /// <returns>Assembly of the loaded plugin.</returns>
        [NotNull]
        internal static Assembly Load([NotNull] NugetForUnityPluginId pluginId)
        {
            return Assembly.Load(pluginId.Name);
        }
    }
}
