using UnityEngine;
using System.Collections;

public class NuspecFile
{
    public string Id { get; set; }

    public string Version { get; set; }

    public string Authors { get; set; }
    public string Owners { get; set; }
    public string LicenseUrl { get; set; }
    public string ProjectUrl { get; set; }
    public string IconUrl { get; set; }
    public bool RequireLicenseAcceptance { get; set; }
    public string Description { get; set; }
    public string ReleaseNotes { get; set; }
    public string Copyright { get; set; }
    public string Tags { get; set; }
}
