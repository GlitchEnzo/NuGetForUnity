using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using NugetForUnity.PackageSource;
using UnityEngine;

namespace NugetForUnity.Models
{
    /// <summary>
    ///     Represents a package available from NuGet.
    /// </summary>
    [Serializable]
    internal sealed class NugetPackageV2 : NugetPackageV2Base
    {
        [NotNull]
        [SerializeField]
        private NugetPackageSourceV2 packageSourceV2;

        [CanBeNull]
        [SerializeField]
        private List<NugetPackageVersion> versions;

        /// <summary>
        ///     Initializes a new instance of the <see cref="NugetPackageV2" /> class.
        /// </summary>
        /// <param name="packageSourceV2">The source this package was downloaded with / provided by.</param>
        public NugetPackageV2([NotNull] NugetPackageSourceV2 packageSourceV2)
        {
            this.packageSourceV2 = packageSourceV2;
        }

        /// <inheritdoc />
        public override INugetPackageSource PackageSource => packageSourceV2;

        /// <inheritdoc />
        public override List<NugetPackageVersion> Versions
        {
            get
            {
                if (versions == null)
                {
                    versions = new List<NugetPackageVersion> { PackageVersion };
                }

                return versions;
            }
        }

        /// <summary>
        ///     Creates a new <see cref="NugetPackageLocal" /> from the given <see cref="NuspecFile" />.
        /// </summary>
        /// <param name="nuspec">The <see cref="NuspecFile" /> to use to create the <see cref="NugetPackageLocal" />.</param>
        /// <param name="packageSource">The source this package was downloaded with / provided by.</param>
        /// <returns>The newly created <see cref="NugetPackageLocal" />.</returns>
        [NotNull]
        public static NugetPackageV2 FromNuspec([NotNull] NuspecFile nuspec, [NotNull] NugetPackageSourceV2 packageSource)
        {
            var package = new NugetPackageV2(packageSource);
            FillFromNuspec(nuspec, package);
            return package;
        }
    }
}
