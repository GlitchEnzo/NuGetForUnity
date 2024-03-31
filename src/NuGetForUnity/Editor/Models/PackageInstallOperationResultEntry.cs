using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace NugetForUnity.Models
{
    internal class PackageInstallOperationResultEntry : IEquatable<PackageInstallOperationResultEntry>
    {
        public PackageInstallOperationResultEntry([NotNull] INugetPackage package)
        {
            Package = package;
        }

        [NotNull]
        public INugetPackage Package { get; }

        [NotNull]
        public List<string> AvailableNativeRuntimes { get; set; } = new List<string>();

        public bool Equals([CanBeNull] PackageInstallOperationResultEntry other)
        {
            if (other is null)
            {
                return false;
            }

            return Package.Equals(other.Package);
        }

        public override bool Equals([CanBeNull] object obj)
        {
            return Equals(obj as PackageInstallOperationResultEntry);
        }

        public override int GetHashCode()
        {
            return Package.GetHashCode();
        }
    }
}
