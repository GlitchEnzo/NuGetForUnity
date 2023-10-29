using System;
using System.Reflection;

namespace NugetForUnity.Configuration
{
    /// <summary>
    ///     Represents a plugin in the configuration.
    /// </summary>
    internal sealed class NugetForUnityPluginId : IEquatable<NugetForUnityPluginId>
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="NugetForUnityPluginId" /> class.
        /// </summary>
        /// <param name="name">Name of plugin assembly.</param>
        /// <param name="path">Path to the plugin assembly.</param>
        internal NugetForUnityPluginId(string name, string path)
        {
            Name = name;
            Path = path;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="NugetForUnityPluginId" /> class.
        /// </summary>
        /// <param name="assembly">Plugin assembly.</param>
        internal NugetForUnityPluginId(Assembly assembly)
            : this(assembly.GetName().Name, assembly.Location)
        {
        }

        /// <summary>
        ///     Gets the name of the Plugin assembly.
        /// </summary>
        internal string Name { get; }

        /// <summary>
        ///     Gets the path to the plugin assembly.
        /// </summary>
        internal string Path { get; }

        public static bool operator ==(NugetForUnityPluginId left, NugetForUnityPluginId right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(NugetForUnityPluginId left, NugetForUnityPluginId right)
        {
            return !Equals(left, right);
        }

        /// <inheritdoc />
        public bool Equals(NugetForUnityPluginId other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((NugetForUnityPluginId)obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.OrdinalIgnoreCase.GetHashCode(Name) * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(Path);
            }
        }
    }
}
