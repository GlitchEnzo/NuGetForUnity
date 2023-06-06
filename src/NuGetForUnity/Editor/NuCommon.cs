using System.IO;

namespace NugetForUnity {

    /// <summary>
    ///     Common base class for NuGet packages, package identifiers, and package specifications.
    /// </summary>
    public abstract class NuCommon
    {

        /// <summary>
        ///     Initialize Id and Version to empty strings.
        /// </summary>
        internal NuCommon(): this(string.Empty, string.Empty)
        {
        }

        /// <summary>
        ///     Provide explicit identifier and version strings.
        /// </summary>
        ///
        /// <param name="id">Identifier of this NuGet common element.</param>
        ///
        /// <param name="version">Version of this NuGet common element.</param>
        internal NuCommon(string id, string version)
        {
            Id = id;
            Version = version;
        }

        /// <summary>
        ///     NuGet element identifier.
        /// </summary>
        public string Id
        {
            get;
            set;
        }

        /// <summary>
        ///     NuGet element version.
        /// </summary>
        public string Version
        {
            get;
            set;
        }

        /// <summary>
        ///     Base filename of this NuGet package's file.
        /// </summary>
        public string PkgFileName => $"{Id}.{Version}.nupkg";

        /// <summary>
        ///     Base filename of this NuGet package's specification file.
        /// </summary>
        public string SpecFileName => $"{Id}.{Version}.nuspec";

        /// <summary>
        ///     Full filename, including specified path, of this NuGet package's file.
        /// </summary>
        ///
        /// <remarks>
        ///     Do not use this method when attempting to find a package file in a local repository; use <see
        ///     cref="LocalRepoPkgPath" /> instead. The existence of the file is not verified.
        /// </remarks>
        ///
        /// <param name="path">Path in which the package file will be found.</param>
        ///
        /// <returns>Base package filename prefixed by the indicated path.</returns>
        public string PkgPath(string path) => Path.Combine(path, PkgFileName);

        /// <summary>
        ///     Full filename, including full path, of this NuGet package's file in a local NuGet repository.
        /// </summary>
        ///
        /// <remarks>
        ///     Use this method when attempting to find a package file in a local repository; do not use <see
        ///     cref="PkgPath" /> for this purpose. The existence of the file is verified.
        /// </remarks>
        ///
        /// <param name="repoPath">Path to the local repository's root directory.</param>
        ///
        /// <returns>The full path to the file, if it exists in the repository, or <c>null</c> otherwise.</returns>
        public string LocalRepoPkgPath(string repoPath)
        {

            // Find this package's file in the repository.
            var files = Directory.GetFiles(repoPath, PkgFileName, SearchOption.AllDirectories);

            // If we found any, return the first found; otherwise return null.
            return files.Length > 0? files[0]: null;
        }
    }
}
