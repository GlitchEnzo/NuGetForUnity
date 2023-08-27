using System;
using System.Collections.Generic;
using UnityEngine;

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
            Dependencies = new List<NugetPackageIdentifier>();
        }

        /// <summary>
        ///     Gets or sets the framework and version that this group targets.
        /// </summary>
        [field: SerializeField]
        public string TargetFramework { get; set; }

        /// <summary>
        ///     Gets or sets the list of package dependencies that belong to this group.
        /// </summary>
        [field: SerializeField]
        public List<NugetPackageIdentifier> Dependencies { get; set; }
    }
}
