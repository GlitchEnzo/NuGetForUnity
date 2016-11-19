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

    [Test]
    [TestCase("1.0", "1.0")]
    [TestCase("1.0", "2.0")]
    [TestCase("(1.0,)", "2.0")]
    [TestCase("[1.0]", "1.0")]
    [TestCase("(,1.0]", "0.5")]
    [TestCase("(,1.0]", "1.0")]
    [TestCase("(,1.0)", "0.5")]
    [TestCase("[1.0,2.0]", "1.0")]
    [TestCase("[1.0,2.0]", "2.0")]
    [TestCase("(1.0,2.0)", "1.5")]
    public void VersionInRangeTest(string versionRange, string version)
    {
        var id = new NugetPackageIdentifier();
        id.Id = "TestPackage";
        id.Version = versionRange;

        Assert.IsTrue(id.InRange(version), "{0} was NOT in range of {1}!", version, versionRange);
    }

    [Test]
    [TestCase("1.0", "0.5")]
    [TestCase("(1.0,)", "1.0")]
    [TestCase("[1.0]", "2.0")]
    [TestCase("(,1.0]", "2.0")]
    [TestCase("(,1.0)", "1.0")]
    [TestCase("[1.0,2.0]", "0.5")]
    [TestCase("[1.0,2.0]", "3.0")]
    [TestCase("(1.0,2.0)", "1.0")]
    [TestCase("(1.0,2.0)", "2.0")]
    public void VersionOutOfRangeTest(string versionRange, string version)
    {
        var id = new NugetPackageIdentifier();
        id.Id = "TestPackage";
        id.Version = versionRange;

        Assert.IsFalse(id.InRange(version), "{0} WAS in range of {1}!", version, versionRange);
    }
}
