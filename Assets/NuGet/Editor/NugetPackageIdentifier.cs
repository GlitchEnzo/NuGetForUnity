namespace NugetForUnity
{
    using System;

    /// <summary>
    /// Represents an identifier for a NuGet package.  It contains only an ID and a Version number.
    /// </summary>
    public class NugetPackageIdentifier : IEquatable<NugetPackageIdentifier>
    {
        /// <summary>
        /// Gets or sets the ID of the NuGet package.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the version number of the NuGet package.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Checks to see if this <see cref="NugetPackageIdentifier"/> is equal to the given one.
        /// </summary>
        /// <param name="other">The other <see cref="NugetPackageIdentifier"/> to check equality with.</param>
        /// <returns>True if the package identifiers are equal, otherwise false.</returns>
        public bool Equals(NugetPackageIdentifier other)
        {
            return other.Id == Id && other.Version == Version;
        }
    }
}