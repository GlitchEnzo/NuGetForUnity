using System;
using System.Collections.Generic;
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
        [SerializeField]
        private NugetPackageSourceV2 packageSourceV2;

        /// <summary>
        ///     Initializes a new instance of the <see cref="NugetPackageV2" /> class.
        /// </summary>
        /// <param name="packageSourceV2">The source this package was downloaded with / provided by.</param>
        public NugetPackageV2(NugetPackageSourceV2 packageSourceV2)
        {
            this.packageSourceV2 = packageSourceV2;
        }

        /// <inheritdoc />
        public override INugetPackageSource PackageSource => packageSourceV2;

        /// <inheritdoc />
        public override List<NugetPackageVersion> Versions => null;

        /// <summary>
        ///     Creates a new <see cref="NugetPackageLocal" /> from the given <see cref="NuspecFile" />.
        /// </summary>
        /// <param name="nuspec">The <see cref="NuspecFile" /> to use to create the <see cref="NugetPackageLocal" />.</param>
        /// <param name="packageSource">The source this package was downloaded with / provided by.</param>
        /// <returns>The newly created <see cref="NugetPackageLocal" />.</returns>
        public static NugetPackageV2 FromNuspec(NuspecFile nuspec, NugetPackageSourceV2 packageSource)
        {
            var package = new NugetPackageV2(packageSource);
            FillFromNuspec(nuspec, package);
            return package;
        }

        /// <summary>
        ///     Loads a <see cref="NugetPackageV2" /> from the .nupkg file at the given file-path.
        /// </summary>
        /// <param name="nupkgFilePath">The file-path to the .nupkg file to load.</param>
        /// <param name="packageSource">The source this package was downloaded with / provided by.</param>
        /// <returns>The <see cref="NugetPackageV2" /> loaded from the .nupkg file.</returns>
        public static NugetPackageV2 FromNupkgFile(string nupkgFilePath, NugetPackageSourceV2 packageSource)
        {
            var package = FromNuspec(NuspecFile.FromNupkgFile(nupkgFilePath), packageSource);
            package.DownloadUrl = nupkgFilePath;
            return package;
        }
    }
}
