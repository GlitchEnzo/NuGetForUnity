using System;
using System.Reflection;
using JetBrains.Annotations;
using NugetForUnity.Helper;

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
        internal NugetForUnityPluginId([NotNull] string name, [NotNull] string path)
        {
            Name = name;
            if (System.IO.Path.IsPathRooted(path))
            {
                path = PathHelper.GetRelativePath(UnityPathHelper.AbsoluteProjectPath, path);
            }

            // We make sure forward slashes are used, so it works on non Windows platforms as well
            Path = path.Replace("\\", "/");
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="NugetForUnityPluginId" /> class.
        /// </summary>
        /// <param name="assembly">Plugin assembly.</param>
        internal NugetForUnityPluginId([NotNull] Assembly assembly)
            : this(assembly.GetName().Name, assembly.Location)
        {
        }

        /// <summary>
        ///     Gets the name of the Plugin assembly.
        /// </summary>
        [NotNull]
        internal string Name { get; }

        /// <summary>
        ///     Gets the path to the plugin assembly.
        /// </summary>
        [NotNull]
        internal string Path { get; }

        /// <summary>
        ///     Checks to see if the left <see cref="NugetForUnityPluginId" /> is equal to the right.
        ///     They are equal if the Id and the Version match.
        /// </summary>
        /// <param name="left">The left to compare.</param>
        /// <param name="right">The right to compare.</param>
        /// <returns>True if the left is equal to the right.</returns>
        public static bool operator ==([CanBeNull] NugetForUnityPluginId left, [CanBeNull] NugetForUnityPluginId right)
        {
            return Equals(left, right);
        }

        /// <summary>
        ///     Checks to see if the left <see cref="NugetForUnityPluginId" /> is not equal to the right.
        ///     They are equal if the Id and the Version match.
        /// </summary>
        /// <param name="left">The left to compare.</param>
        /// <param name="right">The right to compare.</param>
        /// <returns>True if the left is equal to the right.</returns>
        public static bool operator !=([CanBeNull] NugetForUnityPluginId left, [CanBeNull] NugetForUnityPluginId right)
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
