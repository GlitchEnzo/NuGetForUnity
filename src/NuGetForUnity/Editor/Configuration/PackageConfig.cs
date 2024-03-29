﻿using JetBrains.Annotations;
using NugetForUnity.Models;

namespace NugetForUnity.Configuration
{
    /// <summary>
    ///     The configuration for a single NuGet package, stored in the <c>package.conf</c> (<see cref="PackagesConfigFile" />).
    /// </summary>
    public class PackageConfig : NugetPackageIdentifier
    {
        /// <summary>
        ///     Gets or sets a value indicating whether the assemblies (*.dll files) of this NuGet package should be automatically referenced.
        ///     Assemblies with this setting are automatically referenced by the sup projects generated by Unity.
        ///     When this setting is set to <c>false</c> the assemblies of this NuGet package are only referenced
        ///     by Unity projects that explicitly list them inside there <c>*.asmdef</c> file.
        /// </summary>
        public bool AutoReferenced { get; set; } = true;

        /// <summary>
        ///     Gets or sets the configured target framework moniker that is used to install this NuGet package instead of
        ///     automatically determining the best matching target framework from the Unity settings ('Api Compatibility Level').
        /// </summary>
        [CanBeNull]
        public string TargetFramework { get; set; }
    }
}
