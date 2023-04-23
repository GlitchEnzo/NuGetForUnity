using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace NugetForUnity
{
    /// <summary>
    ///     Represents an identifier for a NuGet package. It contains only an ID and a Version number.
    /// </summary>
    [Serializable]
    public class NugetPackageIdentifier : INuGetPackageIdentifier,
        IEquatable<NugetPackageIdentifier>,
        IComparable<NugetPackageIdentifier>,
        ISerializationCallbackReceiver
    {
        [SerializeField]
        private string versionForSerialization;

        /// <summary>
        ///     Gets or sets whether this package was installed manually or just as a dependency.
        /// </summary>
        public bool IsManuallyInstalled;

        private string normalizedVersion;

        private SemVer2Version semVer2Version;

        /// <summary>
        ///     Initializes a new instance of the <see cref="NugetPackageIdentifier" /> class with empty ID and Version.
        /// </summary>
        public NugetPackageIdentifier()
        {
            Id = string.Empty;
            normalizedVersion = string.Empty;
            FullVersion = string.Empty;
            semVer2Version = new SemVer2Version(false);
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="NugetPackageIdentifier" /> class with the given ID and Version.
        /// </summary>
        /// <param name="id">The ID of the package.</param>
        /// <param name="version">The version number of the package.</param>
        public NugetPackageIdentifier(string id, string version)
        {
            Id = id;
            Version = version;
        }

        /// <summary>
        ///     Gets or sets the version number of the NuGet package.
        ///     This is the normalized version number without build-metadata e.g. <b>1.0.0+b3a8</b> is normalized to <b>1.0.0</b>.
        /// </summary>
        public string Version
        {
            get => normalizedVersion;

            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    semVer2Version = new SemVer2Version(false);
                    normalizedVersion = string.Empty;
                    FullVersion = string.Empty;
                    IsPrerelease = false;
                    IsMinInclusive = false;
                    IsMaxInclusive = false;
                    HasVersionRange = false;
                    return;
                }

                if (FullVersion == value)
                {
                    return;
                }

                IsMinInclusive = value.StartsWith("[");
                HasVersionRange = IsMinInclusive || value.StartsWith("(");
                if (HasVersionRange)
                {
                    semVer2Version = new SemVer2Version(false);
                    normalizedVersion = value;
                    FullVersion = value;
                    IsPrerelease = value.Contains("-");
                    IsMaxInclusive = value.EndsWith("]");
                }
                else
                {
                    semVer2Version = new SemVer2Version(value);
                    normalizedVersion = semVer2Version.ToString(); // normalize the version string
                    FullVersion = semVer2Version.ToString(true); // get the full version string with build-metadata
                    IsPrerelease = semVer2Version.PreRelease != null;
                    IsMaxInclusive = false;
                }
            }
        }

        /// <summary>
        ///     Gets the full version number of the NuGet package.
        ///     This contains the build-metadata e.g. <b>1.0.0+b3a8</b>.
        /// </summary>
        [field: SerializeField]
        public string FullVersion { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether this is a prerelease package or an official release package.
        /// </summary>
        public bool IsPrerelease { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the version number specified is a range of values.
        /// </summary>
        public bool HasVersionRange { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the minimum version number (only valid when HasVersionRange is true) is inclusive (true) or exclusive (false).
        /// </summary>
        public bool IsMinInclusive { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the maximum version number (only valid when HasVersionRange is true) is inclusive (true) or exclusive (false).
        /// </summary>
        public bool IsMaxInclusive { get; private set; }

        /// <summary>
        ///     Gets the minimum version number of the NuGet package. Only valid when HasVersionRange is true.
        /// </summary>
        public string MinimumVersion => HasVersionRange ? Version.TrimStart('[', '(').TrimEnd(']', ')').Split(',')[0].Trim() : Version;

        /// <summary>
        ///     Gets the maximum version number of the NuGet package. Only valid when HasVersionRange is true.
        /// </summary>
        public string MaximumVersion
        {
            get
            {
                if (!HasVersionRange)
                {
                    return null;
                }

                // if there is no MaxVersion specified, but the Max is Inclusive, then it is an EXACT version match with the stored MINIMUM
                var minMax = Version.TrimStart('[', '(').TrimEnd(']', ')').Split(',');
                return minMax.Length == 2 ? minMax[1].Trim() : null;
            }
        }

        /// <summary>
        ///     Gets the name of the '.nupkg' file that contains the whole package content as a ZIP.
        /// </summary>
        public string PackageFileName => $"{Id}.{Version}.nupkg";

        /// <summary>
        ///     Gets the name of the '.nuspec' file that contains metadata of this NuGet package's.
        /// </summary>
        public string SpecificationFileName => $"{Id}.{Version}.nuspec";

        /// <inheritdoc />
        public int CompareTo(NugetPackageIdentifier other)
        {
            var idCompareResult = string.Compare(Id, other.Id, StringComparison.Ordinal);
            if (idCompareResult != 0)
            {
                return idCompareResult;
            }

            if (HasVersionRange || other.HasVersionRange)
            {
                return Version.CompareTo(other.Version);
            }

            return semVer2Version.Compare(other.semVer2Version);
        }

        /// <summary>
        ///     Checks to see if this <see cref="NugetPackageIdentifier" /> is equal to the given one.
        /// </summary>
        /// <param name="other">The other <see cref="NugetPackageIdentifier" /> to check equality with.</param>
        /// <returns>True if the package identifiers are equal, otherwise false.</returns>
        public bool Equals(NugetPackageIdentifier other)
        {
            return other != null && other.Id == Id && other.Version == Version;
        }

        /// <inheritdoc />
        [field: SerializeField]
        public string Id { get; set; }

        /// <inheritdoc />
        public string Version
        {
            get => PackageVersion.ToString();

            set => PackageVersion = new NuGetPackageVersion(value);
        }

        /// <inheritdoc />
        public NuGetPackageVersion PackageVersion { get; internal set; }

        /// <inheritdoc />
        public bool IsPrerelease => PackageVersion.IsPrerelease;

        /// <inheritdoc />
        public bool HasVersionRange => PackageVersion.HasVersionRange;

        /// <inheritdoc />
        public bool InRange(INuGetPackageIdentifier otherPackage)
        {
            return PackageVersion.InRange(otherPackage.PackageVersion);
        }

        /// <inheritdoc />
        public bool InRange(NuGetPackageVersion otherVersion)
        {
            return PackageVersion.InRange(otherVersion);
        }

        /// <inheritdoc />
        public bool Equals(INuGetPackageIdentifier other)
        {
            return Equals(other as NugetPackageIdentifier);
        }

        /// <inheritdoc />
        public int CompareTo(INuGetPackageIdentifier other)
        {
            return CompareTo(other as NugetPackageIdentifier);
        }

        /// <inheritdoc />
        public virtual void OnBeforeSerialize()
        {
            versionForSerialization = PackageVersion?.ToString();
        }

        /// <inheritdoc />
        public virtual void OnAfterDeserialize()
        {
            Version = versionForSerialization;
        }

        /// <summary>
        ///     Called by Unity when this object is about to be serialized.
        /// </summary>
        public virtual void OnBeforeSerialize()
        {
            // nothing to do
        }

        /// <summary>
        ///     Called by Unity when this object has been deserialized.
        /// </summary>
        public virtual void OnAfterDeserialize()
        {
            // the full version is serialized so it is used to populate the SemVer2Version
            var cached = FullVersion;
            FullVersion = null;
            Version = cached;
        }

        /// <summary>
        ///     Full filename, including specified path, of this NuGet package's file.
        /// </summary>
        /// <remarks>
        ///     Do not use this method when attempting to find a package file in a local repository; use
        ///     <see
        ///         cref="GetLocalPackageFilePath" />
        ///     instead. The existence of the file is not verified.
        /// </remarks>
        /// <param name="baseDirectoryPath">Path in which the package file will be found.</param>
        /// <returns>Base package filename prefixed by the indicated path.</returns>
        public string GetPackageFilePath(string baseDirectoryPath)
        {
            return Path.Combine(baseDirectoryPath, PackageFileName);
        }

        /// <summary>
        ///     Full filename, including full path, of this NuGet package's file in a local NuGet repository.
        /// </summary>
        /// <remarks>
        ///     Use this method when attempting to find a package file in a local repository. Do not use
        ///     <see cref="GetPackageFilePath(string)" /> for this purpose. The existence of the file is verified.
        /// </remarks>
        /// <param name="baseDirectoryPath">Path to the local repository's root directory.</param>
        /// <returns>The full path to the file, if it exists in the repository, or <c>null</c> otherwise.</returns>
        public string GetLocalPackageFilePath(string baseDirectoryPath)
        {
            // Find this package's file in the repository.
            var files = Directory.GetFiles(baseDirectoryPath, PackageFileName, SearchOption.AllDirectories);

            // If we found any, return the first found; otherwise return null.
            return files.FirstOrDefault();
        }

        /// <summary>
        ///     Checks to see if the first <see cref="NugetPackageIdentifier" /> is less than the second.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is less than the second.</returns>
        public static bool operator <(NugetPackageIdentifier first, NugetPackageIdentifier second)
        {
            return first.CompareTo(second) < 0;
        }

        /// <summary>
        ///     Checks to see if the first <see cref="NugetPackageIdentifier" /> is greater than the second.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is greater than the second.</returns>
        public static bool operator >(NugetPackageIdentifier first, NugetPackageIdentifier second)
        {
            return first.CompareTo(second) > 0;
        }

        /// <summary>
        ///     Checks to see if the first <see cref="NugetPackageIdentifier" /> is less than or equal to the second.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is less than or equal to the second.</returns>
        public static bool operator <=(NugetPackageIdentifier first, NugetPackageIdentifier second)
        {
            return first.CompareTo(second) <= 0;
        }

        /// <summary>
        ///     Checks to see if the first <see cref="NugetPackageIdentifier" /> is greater than or equal to the second.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is greater than or equal to the second.</returns>
        public static bool operator >=(NugetPackageIdentifier first, NugetPackageIdentifier second)
        {
            return first.CompareTo(second) >= 0;
        }

        /// <summary>
        ///     Checks to see if the first <see cref="NugetPackageIdentifier" /> is equal to the second.
        ///     They are equal if the Id and the Version match.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is equal to the second.</returns>
        public static bool operator ==(NugetPackageIdentifier first, NugetPackageIdentifier second)
        {
            if (ReferenceEquals(first, second))
            {
                return true;
            }

            if (first is null)
            {
                return false;
            }

            return first.Equals(second);
        }

        /// <summary>
        ///     Checks to see if the first <see cref="NugetPackageIdentifier" /> is not equal to the second.
        ///     They are not equal if the Id or the Version differ.
        /// </summary>
        /// <param name="first">The first to compare.</param>
        /// <param name="second">The second to compare.</param>
        /// <returns>True if the first is not equal to the second.</returns>
        public static bool operator !=(NugetPackageIdentifier first, NugetPackageIdentifier second)
        {
            return !(first == second);
        }

        /// <summary>
        ///     Determines if a given object is equal to this <see cref="NugetPackageIdentifier" />.
        /// </summary>
        /// <param name="obj">The object to check.</param>
        /// <returns>True if the given object is equal to this <see cref="NugetPackageIdentifier" />, otherwise false.</returns>
        public override bool Equals(object obj)
        {
            return Equals(obj as NugetPackageIdentifier);
        }

        /// <summary>
        ///     Gets the hash-code for this <see cref="NugetPackageIdentifier" />.
        /// </summary>
        /// <returns>The hash-code for this instance.</returns>
        public override int GetHashCode()
        {
            return Id.GetHashCode() ^ Version.GetHashCode();
        }

        /// <summary>
        ///     Returns the string representation of this <see cref="NugetPackageIdentifier" /> in the form "{ID}.{Version}".
        /// </summary>
        /// <returns>A string in the form "{ID}.{Version}".</returns>
        public override string ToString()
        {
            return string.Format("{0}.{1}", Id, Version);
        }

        /// <summary>
        ///     Determines if the given <see cref="NugetPackageIdentifier" />'s version is in the version range of this <see cref="NugetPackageIdentifier" />.
        ///     See here: https://docs.nuget.org/ndocs/create-packages/dependency-versions.
        /// </summary>
        /// <param name="otherPackage">The <see cref="NugetPackageIdentifier" /> whose version to check if is in the range.</param>
        /// <returns>True if the given version is in the range, otherwise false.</returns>
        public bool InRange(NugetPackageIdentifier otherPackage)
        {
            if (HasVersionRange)
            {
                var comparison = CompareVersion(otherPackage.semVer2Version);
                if (comparison == 0)
                {
                    return true;
                }

                return false;
            }

            // if it has no version range specified (e.g. only a single version number)
            // The NuGet's specs state that it is the minimum version number, inclusive
            return semVer2Version.Compare(otherPackage.semVer2Version) <= 0;
        }

        /// <summary>
        ///     Compares the given version string with the version range of this <see cref="NugetPackageIdentifier" />.
        ///     See here: https://docs.nuget.org/ndocs/create-packages/dependency-versions.
        /// </summary>
        /// <param name="other">The package of witch the version to check if is in the range.</param>
        /// <returns>
        ///     -1 if otherVersion is less than the version range. 0 if otherVersion is inside the version range. +1 if otherVersion is greater than the
        ///     version range.
        /// </returns>
        public int CompareVersion(NugetPackageIdentifier other)
        {
            return CompareVersion(other.semVer2Version);
        }

        private int CompareVersion(in SemVer2Version otherSemVer2)
        {
            if (!HasVersionRange)
            {
                return semVer2Version.Compare(otherSemVer2);
            }

            var compareMinimum = 0;
            if (!string.IsNullOrEmpty(MinimumVersion))
            {
                compareMinimum = new SemVer2Version(MinimumVersion).Compare(otherSemVer2);

                // -1 = Min < other <-- Inclusive & Exclusive
                //  0 = Min = other <-- Inclusive Only
                // +1 = Min > other <-- OUT OF RANGE
                if (IsMinInclusive)
                {
                    if (compareMinimum > 0)
                    {
                        return -1;
                    }
                }
                else
                {
                    if (compareMinimum >= 0)
                    {
                        return -1;
                    }
                }
            }

            if (!string.IsNullOrEmpty(MaximumVersion))
            {
                var compare = new SemVer2Version(MaximumVersion).Compare(otherSemVer2);

                // -1 = Max < other <-- OUT OF RANGE
                //  0 = Max = other <-- Inclusive Only
                // +1 = Max > other <-- Inclusive & Exclusive
                if (IsMaxInclusive)
                {
                    if (compare < 0)
                    {
                        return 1;
                    }
                }
                else
                {
                    if (compare <= 0)
                    {
                        return 1;
                    }
                }
            }
            else
            {
                if (IsMaxInclusive)
                {
                    // if there is no MaxVersion specified, but the Max is Inclusive, then it is an EXACT version match with the stored MINIMUM
                    return compareMinimum;
                }
            }

            return 0;
        }

        /// <summary>
        ///     SemVer2 <see href="https://semver.org/" />.
        ///     Complying with NuGet comparison rules <see href="https://learn.microsoft.com/en-us/nuget/concepts/package-versioning" />.
        /// </summary>
        private readonly struct SemVer2Version
        {
            private readonly int major;

            private readonly int minor;

            private readonly int? patch;

            private readonly int? build;

            private readonly string buildMetadata;

            public string PreRelease { get; }

            /// <summary>
            ///     Initializes a new instance of the <see cref="SemVer2Version" /> struct.
            /// </summary>
            /// <param name="dummy">Dummy to allow calling this constructor.</param>
            public SemVer2Version(bool dummy)
            {
                _ = dummy;
                buildMetadata = null;
                PreRelease = null;
                major = -1;
                minor = -1;
                patch = null;
                build = null;
            }

            /// <summary>
            ///     Initializes a new instance of the <see cref="SemVer2Version" /> struct.
            /// </summary>
            /// <param name="version">The version number as string.</param>
            public SemVer2Version(string version)
            {
                try
                {
                    buildMetadata = null;
                    var buildMetadataStartIndex = version.IndexOf('+');
                    if (buildMetadataStartIndex > 0)
                    {
                        buildMetadata = version.Substring(buildMetadataStartIndex + 1);
                        version = version.Substring(0, buildMetadataStartIndex);
                    }

                    PreRelease = null;
                    var preReleaseStartIndex = version.IndexOf('-');
                    if (preReleaseStartIndex > 0)
                    {
                        PreRelease = version.Substring(preReleaseStartIndex + 1);
                        version = version.Substring(0, preReleaseStartIndex);
                    }

                    var split = version.Split('.');
                    major = int.Parse(split[0]);
                    minor = int.Parse(split[1]);
                    patch = null;
                    if (split.Length >= 3)
                    {
                        patch = int.Parse(split[2]);
                    }

                    build = null;
                    if (split.Length >= 4)
                    {
                        build = int.Parse(split[3]);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogErrorFormat("Invalid version number: '{0}'\n{1}", version, ex);
                    buildMetadata = null;
                    PreRelease = null;
                    major = -1;
                    minor = -1;
                    patch = null;
                    build = null;
                }
            }

            /// <summary>
            ///     Compares two version numbers in the form "1.2". Also supports an optional 3rd and 4th number as well as a prerelease tag, such as
            ///     "1.3.0.1-alpha2".
            ///     Returns:
            ///     -1 if this is less than other
            ///     0 if this is equal to other
            ///     +1 if this is greater than other.
            /// </summary>
            /// <param name="other">The second version number to compare.</param>
            /// <returns>-1 if this is less than other. 0 if this is equal to other. +1 if this is greater than other.</returns>
            public int Compare(in SemVer2Version other)
            {
                try
                {
                    var majorComparison = major.CompareTo(other.major);
                    if (majorComparison == 0)
                    {
                        // if major versions are equal, compare minor versions
                        var minorComparison = minor.CompareTo(other.minor);
                        if (minorComparison == 0)
                        {
                            var patchNumber = patch ?? 0;
                            var otherPatch = other.patch ?? 0;
                            var patchComparison = patchNumber.CompareTo(otherPatch);
                            if (patchComparison == 0)
                            {
                                // if patch versions are equal, compare build versions
                                var buildNumber = build ?? 0;
                                var otherBuild = other.build ?? 0;
                                var buildComparison = buildNumber.CompareTo(otherBuild);
                                if (buildComparison == 0)
                                {
                                    // if the build versions are equal, just return the prerelease version comparison
                                    var prerelease = PreRelease ?? "\uFFFF";
                                    var otherPrerelease = other.PreRelease ?? "\uFFFF";
                                    var prereleaseComparison = string.Compare(prerelease, otherPrerelease, StringComparison.Ordinal);
                                    return prereleaseComparison;
                                }

                                // the build versions are different, so use them
                                return buildComparison;
                            }

                            // the patch versions are different, so use them
                            return patchComparison;
                        }

                        // the minor versions are different, so use them
                        return minorComparison;
                    }

                    // the major versions are different, so use them
                    return majorComparison;
                }
                catch (Exception)
                {
                    Debug.LogErrorFormat("Compare Error: {0} {1}", this, other);
                    return -1;
                }
            }

            /// <inheritdoc />
            public override string ToString()
            {
                return ToString();
            }

            /// <summary>
            ///     Returns the version number as a string in the form <see cref="major" />.<see cref="minor" />.<see cref="patch" />-<see cref="PreRelease" />+
            ///     <see cref="buildMetadata" />.
            ///     The <see cref="buildMetadata" /> can be removed.
            /// </summary>
            /// <param name="withBuildMetadata">If <c>true</c> the <see cref="buildMetadata" /> is included.</param>
            /// <returns>The formatted string.</returns>
            public string ToString(bool withBuildMetadata = false)
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.Append(major);
                stringBuilder.Append('.');
                stringBuilder.Append(minor);
                if (patch.HasValue)
                {
                    stringBuilder.Append('.');
                    stringBuilder.Append(patch.Value);
                }

                if (build.HasValue)
                {
                    stringBuilder.Append('.');
                    stringBuilder.Append(build.Value);
                }

                if (!string.IsNullOrEmpty(PreRelease))
                {
                    stringBuilder.Append('-');
                    stringBuilder.Append(PreRelease);
                }

                if (withBuildMetadata && !string.IsNullOrEmpty(buildMetadata))
                {
                    stringBuilder.Append('+');
                    stringBuilder.Append(buildMetadata);
                }

                return stringBuilder.ToString();
            }
        }
    }
}
