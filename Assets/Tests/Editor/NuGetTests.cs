using System.Linq;
using UnityEngine;
using UnityEditor;
using NUnit.Framework;
using NugetForUnity;

public class NuGetTests
{
    private void UninstallAllPackages()
    {
        var installedPackages = NugetHelper.GetInstalledPackages().Values.ToList();

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
        var id = new NugetPackageIdentifier("Newtonsoft.Json", "6.0.8");

        NugetHelper.InstallIdentifier(id);
        Assert.IsTrue(NugetHelper.IsInstalled(id), "The package was NOT installed: {0} {1}", id.Id, id.Version);

        // install a newer version
        id.Version = "7.0.1";
        NugetHelper.InstallIdentifier(id);
        Assert.IsTrue(NugetHelper.IsInstalled(id), "The package was NOT installed: {0} {1}", id.Id, id.Version);

        // try to install an old version while a newer is already installed
        id.Version = "6.0.8";
        NugetHelper.InstallIdentifier(id);
        id.Version = "7.0.1";
        Assert.IsTrue(NugetHelper.IsInstalled(id), "The package was NOT installed: {0} {1}", id.Id, id.Version);

        NugetHelper.Uninstall(id, false);
        Assert.IsFalse(NugetHelper.IsInstalled(id), "The package is STILL installed: {0} {1}", id.Id, id.Version);
    }

    [Test]
    public void InstallProtobufTest()
    {
        var id = new NugetPackageIdentifier("protobuf-net", "2.0.0.668");

        NugetHelper.InstallIdentifier(id);

        Assert.IsTrue(NugetHelper.IsInstalled(id), "The package was NOT installed: {0} {1}", id.Id, id.Version);

        NugetHelper.Uninstall(id, false);

        Assert.IsFalse(NugetHelper.IsInstalled(id), "The package is STILL installed: {0} {1}", id.Id, id.Version);
    }

    [Test]
    public void InstallBootstrapCSSTest()
    {
        // disable the cache for now to force getting the lowest version of the dependency
        NugetHelper.NugetConfigFile.InstallFromCache = false;

        var bootstrap337 = new NugetPackageIdentifier("bootstrap", "3.3.7");

        NugetHelper.InstallIdentifier(bootstrap337);
        Assert.IsTrue(NugetHelper.IsInstalled(bootstrap337), "The package was NOT installed: {0} {1}", bootstrap337.Id, bootstrap337.Version);

        // Bootstrap CSS 3.3.7 has a dependency on jQuery [1.9.1, 4.0.0) ... 1.9.1 <= x < 4.0.0
        // Therefore it should install 1.9.1 since that is the lowest compatible version available
        var jQuery191 = new NugetPackageIdentifier("jQuery", "1.9.1");
        Assert.IsTrue(NugetHelper.IsInstalled(jQuery191), "The package was NOT installed: {0} {1}", jQuery191.Id, jQuery191.Version);

        // now upgrade jQuery to 3.1.1
        var jQuery311 = new NugetPackageIdentifier("jQuery", "3.1.1");
        NugetHelper.InstallIdentifier(jQuery311);
        Assert.IsTrue(NugetHelper.IsInstalled(jQuery311), "The package was NOT installed: {0} {1}", jQuery311.Id, jQuery311.Version);

        // reinstall bootstrap, which should use the currently installed jQuery 3.1.1
        NugetHelper.Uninstall(bootstrap337, false);
        NugetHelper.InstallIdentifier(bootstrap337);

        Assert.IsFalse(NugetHelper.IsInstalled(jQuery191), "The package IS installed: {0} {1}", jQuery191.Id, jQuery191.Version);
        Assert.IsTrue(NugetHelper.IsInstalled(jQuery311), "The package was NOT installed: {0} {1}", jQuery311.Id, jQuery311.Version);

        // cleanup and uninstall everything
        NugetHelper.Uninstall(bootstrap337, false);
        NugetHelper.Uninstall(jQuery311, false);

        Assert.IsFalse(NugetHelper.IsInstalled(bootstrap337), "The package IS installed: {0} {1}", bootstrap337.Id, bootstrap337.Version);
        Assert.IsFalse(NugetHelper.IsInstalled(jQuery191), "The package IS installed: {0} {1}", jQuery191.Id, jQuery191.Version);
        Assert.IsFalse(NugetHelper.IsInstalled(jQuery311), "The package IS installed: {0} {1}", jQuery311.Id, jQuery311.Version);

        // turn cache back on
        NugetHelper.NugetConfigFile.InstallFromCache = true;
    }

    [Test]
    public void InstallStyleCopTest()
    {
        var styleCopPlusId = new NugetPackageIdentifier("StyleCopPlus.MSBuild", "4.7.49.5");
        var styleCopId = new NugetPackageIdentifier("StyleCop.MSBuild", "4.7.49.1");

        NugetHelper.InstallIdentifier(styleCopPlusId);

        // StyleCopPlus depends on StyleCop, so they should both be installed
        // it depends on version 4.7.49.0, but that version doesn't exist. the next closest is 4.7.49.1
        Assert.IsTrue(NugetHelper.IsInstalled(styleCopPlusId), "The package was NOT installed: {0} {1}", styleCopPlusId.Id, styleCopPlusId.Version);
        Assert.IsTrue(NugetHelper.IsInstalled(styleCopId), "The package was NOT installed: {0} {1}", styleCopId.Id, styleCopId.Version);

        NugetHelper.Uninstall(styleCopPlusId, false);
        NugetHelper.Uninstall(styleCopId, false);

        Assert.IsFalse(NugetHelper.IsInstalled(styleCopPlusId), "The package is STILL installed: {0} {1}", styleCopPlusId.Id, styleCopPlusId.Version);
        Assert.IsFalse(NugetHelper.IsInstalled(styleCopId), "The package is STILL installed: {0} {1}", styleCopId.Id, styleCopId.Version);
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
        var id = new NugetPackageIdentifier("TestPackage", versionRange);

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
        var id = new NugetPackageIdentifier("TestPackage", versionRange);

        Assert.IsFalse(id.InRange(version), "{0} WAS in range of {1}!", version, versionRange);
    }
}
