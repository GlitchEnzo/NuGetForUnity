using System.Reflection;
using System.Runtime.Loader;

namespace NuGetForUnity.Cli
{
    /// <summary>
    ///     Class for loading an assembly.
    /// </summary>
    internal sealed class NugetAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver resolver;

        /// <summary>
        ///     Initializes a new instance of the <see cref="NugetAssemblyLoadContext" /> class.
        /// </summary>
        /// <param name="pluginPath">Path to the plugin that will be loaded.</param>
        internal NugetAssemblyLoadContext(string pluginPath)
        {
            resolver = new AssemblyDependencyResolver(pluginPath);
        }

        /// <inheritdoc />
        protected override Assembly Load(AssemblyName assemblyName)
        {
            var assemblyPath = resolver.ResolveAssemblyToPath(assemblyName);
            return assemblyPath != null ? LoadFromAssemblyPath(assemblyPath) : null;
        }
    }
}
