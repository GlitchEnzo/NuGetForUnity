using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using NugetForUnity.PackageSource;
using UnityEngine;

namespace NugetForUnity.Models
{
    /// <summary>
    ///     Represents a NuGet package loaded from a local file.
    /// </summary>
    [Serializable]
    internal sealed class NugetPackageLocal : NugetPackageV2Base
    {
        [SerializeField]
        [NotNull]
        private NugetPackageSourceLocal packageSource;

        [SerializeField]
        [NotNull]
        private List<NugetPackageVersion> versions = new List<NugetPackageVersion>();

        /// <summary>
        ///     Initializes a new instance of the <see cref="NugetPackageLocal" /> class.
        /// </summary>
        /// <param name="packageSource">The source this package was downloaded with / provided by.</param>
        public NugetPackageLocal([NotNull] NugetPackageSourceLocal packageSource)
        {
            this.packageSource = packageSource;
        }

        /// <inheritdoc />
        public override List<NugetPackageVersion> Versions => versions;

        /// <inheritdoc />
        public override INugetPackageSource PackageSource => packageSource;

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

        /// <summary>
        ///     Loads a <see cref="NugetPackageLocal" /> from the .nuspec file at the given file-path.
        /// </summary>
        /// <param name="nuspecFilePath">The file-path to the .nuspec file to load.</param>
        /// <param name="packageSource">The source this package was downloaded with / provided by.</param>
        /// <returns>The <see cref="NugetPackageLocal" /> loaded from the .nuspec file.</returns>
        public static NugetPackageLocal FromNuspecFile(string nuspecFilePath, NugetPackageSourceLocal packageSource)
        {
            var package = FromNuspec(NuspecFile.Load(nuspecFilePath), packageSource);
            package.DownloadUrl = nuspecFilePath;
            return package;
        }
    }
}
