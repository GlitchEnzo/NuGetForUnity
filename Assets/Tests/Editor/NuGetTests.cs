using NUnit.Framework;
using NugetForUnity;
using System.IO;
using UnityEditor;

public class NuGetTests
{
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
        // install a specific version
        var json608 = new NugetPackageIdentifier("Newtonsoft.Json", "6.0.8");
        NugetHelper.InstallIdentifier(json608);
        Assert.IsTrue(NugetHelper.IsInstalled(json608), "The package was NOT installed: {0} {1}", json608.Id, json608.Version);

        // install a newer version
        var json701 = new NugetPackageIdentifier("Newtonsoft.Json", "7.0.1");
        NugetHelper.InstallIdentifier(json701);
        Assert.IsTrue(NugetHelper.IsInstalled(json701), "The package was NOT installed: {0} {1}", json701.Id, json701.Version);

        // try to install an old version while a newer is already installed
        NugetHelper.InstallIdentifier(json608);
        Assert.IsTrue(NugetHelper.IsInstalled(json701), "The package was NOT installed: {0} {1}", json701.Id, json701.Version);

        NugetHelper.UninstallAll();
        Assert.IsFalse(NugetHelper.IsInstalled(json608), "The package is STILL installed: {0} {1}", json608.Id, json608.Version);
        Assert.IsFalse(NugetHelper.IsInstalled(json701), "The package is STILL installed: {0} {1}", json701.Id, json701.Version);
    }

    [Test]
    public void InstallProtobufTest()
    {
        var protobuf = new NugetPackageIdentifier("protobuf-net", "2.0.0.668");

        // install the package
        NugetHelper.InstallIdentifier(protobuf);
        Assert.IsTrue(NugetHelper.IsInstalled(protobuf), "The package was NOT installed: {0} {1}", protobuf.Id, protobuf.Version);

        // uninstall the package
        NugetHelper.UninstallAll();
        Assert.IsFalse(NugetHelper.IsInstalled(protobuf), "The package is STILL installed: {0} {1}", protobuf.Id, protobuf.Version);
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
        NugetHelper.UninstallAll();

        // confirm they are uninstalled
        Assert.IsFalse(NugetHelper.IsInstalled(bootstrap337), "The package is STILL installed: {0} {1}", bootstrap337.Id, bootstrap337.Version);
        Assert.IsFalse(NugetHelper.IsInstalled(jQuery191), "The package is STILL installed: {0} {1}", jQuery191.Id, jQuery191.Version);
        Assert.IsFalse(NugetHelper.IsInstalled(jQuery311), "The package is STILL installed: {0} {1}", jQuery311.Id, jQuery311.Version);

        // turn cache back on
        NugetHelper.NugetConfigFile.InstallFromCache = true;
    }

    [Test]
    public void InstallStyleCopTest()
    {
        var styleCopPlusId = new NugetPackageIdentifier("StyleCopPlus.MSBuild", "4.7.49.5");
        var styleCopId = new NugetPackageIdentifier("StyleCop.MSBuild", "4.7.49.0");

        NugetHelper.InstallIdentifier(styleCopPlusId);

        // StyleCopPlus depends on StyleCop, so they should both be installed
        // it depends on version 4.7.49.0, so ensure it is also installed
        Assert.IsTrue(NugetHelper.IsInstalled(styleCopPlusId), "The package was NOT installed: {0} {1}", styleCopPlusId.Id, styleCopPlusId.Version);
        Assert.IsTrue(NugetHelper.IsInstalled(styleCopId), "The package was NOT installed: {0} {1}", styleCopId.Id, styleCopId.Version);

        // cleanup and uninstall everything
        NugetHelper.UninstallAll();

        Assert.IsFalse(NugetHelper.IsInstalled(styleCopPlusId), "The package is STILL installed: {0} {1}", styleCopPlusId.Id, styleCopPlusId.Version);
        Assert.IsFalse(NugetHelper.IsInstalled(styleCopId), "The package is STILL installed: {0} {1}", styleCopId.Id, styleCopId.Version);
    }

    [Test]
    public void InstallSignalRClientTest()
    {
        var signalRClient = new NugetPackageIdentifier("Microsoft.AspNet.SignalR.Client", "2.2.2");

        NugetHelper.InstallIdentifier(signalRClient);
        Assert.IsTrue(NugetHelper.IsInstalled(signalRClient), "The package was NOT installed: {0} {1}", signalRClient.Id, signalRClient.Version);

        var directory45 = Path.Combine(NugetHelper.NugetConfigFile.RepositoryPath, string.Format("{0}.{1}\\lib\\net45", signalRClient.Id, signalRClient.Version));

        // SignalR 2.2.2 only contains .NET 4.0 and .NET 4.5 libraries, so it should install .NET 4.5 when using .NET 4.6 in Unity, and be empty in other cases
        if (PlayerSettings.GetApiCompatibilityLevel(EditorUserBuildSettings.selectedBuildTargetGroup) == ApiCompatibilityLevel.NET_4_6) // 3 = NET_4_6
        {
            Assert.IsTrue(Directory.Exists(directory45), "The directory does NOT exist: {0}", directory45);
        }
        else // it must be using .NET 2.0 (actually 3.5 in Unity)
        {
            Assert.IsTrue(!Directory.Exists(directory45), "The directory DOES exist: {0}", directory45);
        }

        // cleanup and uninstall everything
        NugetHelper.UninstallAll();
        Assert.IsFalse(NugetHelper.IsInstalled(signalRClient), "The package is STILL installed: {0} {1}", signalRClient.Id, signalRClient.Version);
    }

    [Test]
    [TestCase("1.0.0-rc1", "1.0.0")]
    [TestCase("1.0.0-rc1", "1.0.0-rc2")]
    [TestCase("1.2.3", "1.2.4")]
    [TestCase("1.2.3", "1.3.0")]
    [TestCase("1.2.3", "2.0.0")]
    [TestCase("1.2.3-rc1", "1.2.4")]
    [TestCase("1.2.3-rc1", "1.3.0")]
    [TestCase("1.2.3-rc1", "2.0.0")]
    public void VersionComparison(string smallerVersion, string greaterVersion)
    {
        var smallerPackage = new NugetPackage { Id = "TestPackage", Version = smallerVersion };
        var greaterPackage = new NugetPackage { Id = "TestPackage", Version = greaterVersion };

        Assert.IsTrue(smallerPackage.CompareTo(greaterPackage) < 0, "{0} was NOT smaller than {1}", smallerVersion, greaterVersion);
        Assert.IsTrue(greaterPackage.CompareTo(smallerPackage) > 0, "{0} was NOT greater than {1}", greaterVersion, smallerVersion);
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
