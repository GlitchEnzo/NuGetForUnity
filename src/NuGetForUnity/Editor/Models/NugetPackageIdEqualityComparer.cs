using System;
using System.Collections.Generic;

namespace NugetForUnity.Models
{
    /// <summary>
    ///     Equality comparer implementation that only compares the <see cref="INugetPackageIdentifier.Id" /> of the packages.
    /// </summary>
    public sealed class NugetPackageIdEqualityComparer : IEqualityComparer<INugetPackageIdentifier>, IEqualityComparer<INugetPackage>
    {
        /// <inheritdoc />
        public bool Equals(INugetPackageIdentifier x, INugetPackageIdentifier y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return string.Equals(x.Id, y.Id, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public bool Equals(INugetPackage x, INugetPackage y)
        {
            return Equals((INugetPackageIdentifier)x, y);
        }

        /// <inheritdoc />
        public int GetHashCode(INugetPackageIdentifier obj)
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Id);
        }

        /// <inheritdoc />
        public int GetHashCode(INugetPackage obj)
        {
            return GetHashCode((INugetPackageIdentifier)obj);
        }
    }
}
