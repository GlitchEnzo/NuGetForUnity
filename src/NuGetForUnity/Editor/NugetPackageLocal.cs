using System;
using System.Collections.Generic;
using UnityEngine;

namespace NugetForUnity
{
    /// <summary>
    ///     Represents a NuGet package loaded from a local file.
    /// </summary>
    [Serializable]
    internal sealed class NugetPackageLocal : NugetPackageV2Base
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="NugetPackageLocal" /> class.
        /// </summary>
        /// <param name="packageSource">The source this package was downloaded with / provided by.</param>
        public NugetPackageLocal(NugetPackageSourceLocal packageSource)
        {
            PackageSource = packageSource;
        }

        /// <inheritdoc />
        [field: SerializeField]
        public override List<NugetPackageVersion> Versions { get; } = new List<NugetPackageVersion>();

        /// <inheritdoc />
        [field: SerializeField]
        public override INugetPackageSource PackageSource { get; }

        /// <summary>
        ///     Creates a new <see cref="NugetPackageLocal" /> from the given <see cref="NuspecFile" />.
        /// </summary>
        /// <param name="nuspec">The <see cref="NuspecFile" /> to use to create the <see cref="NugetPackageLocal" />.</param>
        /// <param name="packageSource">The source this package was downloaded with / provided by.</param>
        /// <returns>The newly created <see cref="NugetPackageLocal" />.</returns>
        public static NugetPackageLocal FromNuspec(NuspecFile nuspec, NugetPackageSourceLocal packageSource)
        {
            var package = new NugetPackageLocal(packageSource);
            FillFromNuspec(nuspec, package);
            return package;
        }

        /// <summary>
        ///     Loads a <see cref="NugetPackageLocal" /> from the .nupkg file at the given file-path.
        /// </summary>
        /// <param name="nupkgFilePath">The file-path to the .nupkg file to load.</param>
        /// <param name="packageSource">The source this package was downloaded with / provided by.</param>
        /// <returns>The <see cref="NugetPackageLocal" /> loaded from the .nupkg file.</returns>
        public static NugetPackageLocal FromNupkgFile(string nupkgFilePath, NugetPackageSourceLocal packageSource)
        {
            var package = FromNuspec(NuspecFile.FromNupkgFile(nupkgFilePath), packageSource);
            package.DownloadUrl = nupkgFilePath;
            return package;
        }
    }
}
