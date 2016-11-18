using UnityEngine;
using UnityEditor;
using NUnit.Framework;
using NugetForUnity;

public class NuGetTests
{
    private void UninstallAllPackages()
    {
        var installedPackages = NugetHelper.GetInstalledPackages();

        foreach (var installedPackage in installedPackages)
        {
            NugetHelper.Uninstall(installedPackage);
        }
    }

    [Test]
    public void SimpleRestoreTest()
    {
        NugetHelper.Restore();
    }

    [Test]
    public void LoadConfigFileTest()
    {
        NugetHelper.LoadNugetConfigFile();
    }

    [Test]
    public void InstallJsonTest()
    {
        var id = new NugetPackageIdentifier();
        id.Id = "Newtonsoft.Json";
        id.Version = "6.0.8";

        NugetHelper.InstallIdentifier(id);

        Assert.IsTrue(NugetHelper.IsInstalled(id), "The package was NOT installed: {0} {1}", id.Id, id.Version);

        NugetHelper.Uninstall(id, false);

        Assert.IsFalse(NugetHelper.IsInstalled(id), "The package is STILL installed: {0} {1}", id.Id, id.Version);
    }
}
