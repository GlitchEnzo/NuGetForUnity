#pragma warning disable SA1512,SA1124 // Single-line comments should not be followed by blank line

using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

#region No ReShaper

// ReSharper disable All
// needed because 'JetBrains.Annotations.NotNull' and 'System.Diagnostics.CodeAnalysis.NotNull' collide if this file is compiled with a never version of Unity / C#
using SuppressMessageAttribute = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;

// ReSharper restore All

#endregion

#pragma warning restore SA1512,SA1124 // Single-line comments should not be followed by blank line

namespace NugetForUnity.Models
{
    /// <summary>
    ///     Represents a grouping of items by framework type and version.
    ///     This could be a group of package dependencies, or a group of package references.
    /// </summary>
    [Serializable]
    public class NugetFrameworkGroup
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="NugetFrameworkGroup" /> class.
        /// </summary>
        public NugetFrameworkGroup()
        {
            TargetFramework = string.Empty;
            Dependencies = new List<INugetPackageIdentifier>();
        }

        /// <summary>
        ///     Gets or sets the framework and version that this group targets.
        /// </summary>
        [NotNull]
        [field: SerializeField]
        public string TargetFramework { get; set; }

        /// <summary>
        ///     Gets or sets the list of package dependencies that belong to this group.
        /// </summary>
        [NotNull]
        [field: SerializeField]
        [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Setter required for serialization.")]
        public List<INugetPackageIdentifier> Dependencies { get; set; }
    }
}
