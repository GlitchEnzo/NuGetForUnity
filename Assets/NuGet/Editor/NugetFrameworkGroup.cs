using System;
using System.Collections.Generic;

namespace NugetForUnity
{
    /// <summary>
    /// Represents a grouping of items by framework type and version.
    /// This could be a group of package dependencies, or a group of package references.
    /// </summary>
    [Serializable]
    public class NugetFrameworkGroup
    {
        /// <summary>
        /// Gets or sets the framework and version that this group targets.
        /// </summary>
        public string TargetFramework;

        /// <summary>
        /// Gets or sets the list of package dependencies that belong to this group.
        /// </summary>
        public List<NugetPackageIdentifier> Dependencies;
    }
}
