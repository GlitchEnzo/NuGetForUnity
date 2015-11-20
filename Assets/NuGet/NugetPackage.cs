using System;
using System.Collections.Generic;

/// <summary>
/// Represents a package available from NuGet.
/// </summary>
public class NugetPackage : IEquatable<NugetPackage>
{
    public string ID { get; set; }
    public string Version { get; set; }
    public string Description { get; set; }
    public string LicenseURL { get; set; }
    public List<NugetPackage> Dependencies { get; set; } 

    public bool Equals(NugetPackage other)
    {
        return other.ID == ID && other.Version == Version;
    }
}
