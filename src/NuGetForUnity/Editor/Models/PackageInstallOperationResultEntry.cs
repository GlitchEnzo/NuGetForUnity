using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace NugetForUnity.Models
{
    /// <summary>
    ///     Information about a single installed package.
    /// </summary>
    internal class PackageInstallOperationResultEntry : IEquatable<PackageInstallOperationResultEntry>
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="PackageInstallOperationResultEntry" /> class.
        /// </summary>
        /// <param name="package">The package that was installed.</param>
        public PackageInstallOperationResultEntry([NotNull] INugetPackage package)
        {
            Package = package;
        }

        /// <summary>
        ///     Gets the package that was installed.
        /// </summary>
        [NotNull]
        public INugetPackage Package { get; }

        /// <summary>
        ///     Gets the native runtime's of the package.
        /// </summary>
        [NotNull]
        public List<string> AvailableNativeRuntimes { get; } = new List<string>();

        /// <inheritdoc />
        public bool Equals([CanBeNull] PackageInstallOperationResultEntry other)
        {
            if (other is null)
            {
                return false;
            }

            return Package.Equals(other.Package);
        }

        /// <inheritdoc />
        public override bool Equals([CanBeNull] object obj)
        {
            return Equals(obj as PackageInstallOperationResultEntry);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return Package.GetHashCode();
        }
    }
}
