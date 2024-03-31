using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NugetForUnity.Models
{
    internal class PackageInstallOperationResult
    {
        public bool Sucessfull { get; set; } = true;

        [NotNull]
        public List<PackageInstallOperationResultEntry> Packages { get; set; } = new List<PackageInstallOperationResultEntry>();

        internal void Combine([NotNull] PackageInstallOperationResult dependencyResult)
        {
            Packages.AddRange(dependencyResult.Packages.Where(package => !Packages.Contains(package)));
        }
    }
}
