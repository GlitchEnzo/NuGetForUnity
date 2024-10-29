using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NugetForUnity.Models
{
    /// <summary>
    ///     Class containing information about a package install operation.
    /// </summary>
    internal class PackageInstallOperationResult
    {
        /// <summary>
        ///     Gets or sets a value indicating whether the install was successful.
        /// </summary>
        public bool Successful { get; set; } = true;

        /// <summary>
        ///     Gets the list containing information about the installed packages.
        /// </summary>
        [NotNull]
        public List<PackageInstallOperationResultEntry> Packages { get; } = new List<PackageInstallOperationResultEntry>();

        /// <summary>
        ///     Combines this result withe the information of the <paramref name="otherResult" /> by adding all installed package information from the other
        ///     result to this result.
        /// </summary>
        /// <param name="otherResult">The other result to combine with this result.</param>
        public void Combine([NotNull] PackageInstallOperationResult otherResult)
        {
            Successful = Successful && otherResult.Successful;
            Packages.AddRange(otherResult.Packages.Where(package => !Packages.Contains(package)));
        }
    }
}
