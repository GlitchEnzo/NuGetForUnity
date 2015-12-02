namespace NugetForUnity
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a package available from NuGet.
    /// </summary>
    public class NugetPackage : NugetPackageIdentifier, IEquatable<NugetPackage>
    {
        /// <summary>
        /// Gets or sets the description of the NuGet package.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the URL for the location of the license of the NuGet package.
        /// </summary>
        public string LicenseUrl { get; set; }

        /// <summary>
        /// Gets or sets the URL for the location of the actual (.nupkg) NuGet package.
        /// </summary>
        public string DownloadUrl { get; set; }

        /// <summary>
        /// Gets or sets the NuGet packages that this NuGet package depends on.
        /// </summary>
        public List<NugetPackageIdentifier> Dependencies { get; set; }

        /// <summary>
        /// Checks to see if this <see cref="NugetPackage"/> is equal to the given one.
        /// </summary>
        /// <param name="other">The other <see cref="NugetPackage"/> to check equality with.</param>
        /// <returns>True if the packages are equal, otherwise false.</returns>
        public bool Equals(NugetPackage other)
        {
            return other.Id == Id && other.Version == Version;
        }
    }
}