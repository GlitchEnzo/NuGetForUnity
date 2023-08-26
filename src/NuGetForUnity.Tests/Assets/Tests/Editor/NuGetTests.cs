using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NugetForUnity;
using NugetForUnity.Configuration;
using NugetForUnity.Data;
using NugetForUnity.Helper;
using NugetForUnity.PackageSource;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class NuGetTests
{
    public enum InstallMode
    {
        ApiV2Only,

        ApiV3Only,

        ApiV2AllowCached,
    }

    private Stopwatch stopwatch;

    [SetUp]
    public void Setup()
    {
        stopwatch = Stopwatch.StartNew();
        TestContext.Progress.WriteLine($"Test: {TestContext.CurrentContext.Test.FullName}");
    }

    [TearDown]
    public void Cleanup()
    {
        NugetPackageUninstaller.UninstallAll(InstalledPackagesManager.InstalledPackages.ToList());
        TestContext.Progress.WriteLine($"Test: {TestContext.CurrentContext.Test.FullName}, Duration: {stopwatch.Elapsed}");
    }

    [Test]
    [Order(1)]
    public void SimpleRestoreTest()
    {
        PackageRestorer.Restore();
    }

    [Test]
    public void LoadConfigFileTest()
    {
        ConfigurationManager.LoadNugetConfigFile();
    }

    [Test]
    [Order(2)]
    public void InstallJsonTest([Values] InstallMode installMode)
    {
        ConfigureNugetConfig(installMode);

        // install a specific version
        var json608 = new NugetPackageIdentifier("Newtonsoft.Json", "6.0.8") { IsManuallyInstalled = true };
        NugetPackageInstaller.InstallIdentifier(json608);
        Assert.IsTrue(InstalledPackagesManager.IsInstalled(json608), "The package was NOT installed: {0} {1}", json608.Id, json608.Version);

        // install a newer version
        var json701 = new NugetPackageIdentifier("Newtonsoft.Json", "7.0.1");
        NugetPackageInstaller.InstallIdentifier(json701);
        Assert.IsTrue(InstalledPackagesManager.IsInstalled(json701), "The package was NOT installed: {0} {1}", json701.Id, json701.Version);

        // try to install an old version while a newer is already installed
        NugetPackageInstaller.InstallIdentifier(json608);
        Assert.IsTrue(InstalledPackagesManager.IsInstalled(json701), "The package was NOT installed: {0} {1}", json701.Id, json701.Version);

        NugetPackageUninstaller.UninstallAll(InstalledPackagesManager.InstalledPackages.ToList());
        Assert.IsFalse(InstalledPackagesManager.IsInstalled(json608), "The package is STILL installed: {0} {1}", json608.Id, json608.Version);
        Assert.IsFalse(InstalledPackagesManager.IsInstalled(json701), "The package is STILL installed: {0} {1}", json701.Id, json701.Version);
    }

    [Test]
    public void InstallRoslynAnalyzerTest([Values] InstallMode installMode)
    {
        ConfigureNugetConfig(installMode);
        var analyzer = new NugetPackageIdentifier("ErrorProne.NET.CoreAnalyzers", "0.1.2") { IsManuallyInstalled = true };

        // install the package
        NugetPackageInstaller.InstallIdentifier(analyzer);
        try
        {
            AssetDatabase.Refresh();
            var path = $"Assets/Packages/{analyzer.Id}.{analyzer.Version}/analyzers/dotnet/cs/ErrorProne.NET.Core.dll";
            var meta = (PluginImporter)AssetImporter.GetAtPath(path);
            meta.SaveAndReimport();
            AssetDatabase.Refresh();

            Assert.IsTrue(InstalledPackagesManager.IsInstalled(analyzer), "The package was NOT installed: {0} {1}", analyzer.Id, analyzer.Version);

            // Verify analyzer dll import settings
            meta = AssetImporter.GetAtPath(path) as PluginImporter;
            Assert.IsNotNull(meta, "Get meta file");
            Assert.IsFalse(meta.GetCompatibleWithAnyPlatform(), "Not compatible any platform");
            Assert.IsFalse(meta.GetCompatibleWithEditor(), "Not compatible editor");
            foreach (var platform in Enum.GetValues(typeof(BuildTarget)))
            {
                Assert.IsFalse(
                    meta.GetExcludeFromAnyPlatform((BuildTarget)platform),
                    $"Not compatible {Enum.GetName(typeof(BuildTarget), platform)}");
            }

            Assert.IsTrue(AssetDatabase.GetLabels(meta).Contains("RoslynAnalyzer"), "Set RoslynAnalyzer label");
        }
        finally
        {
            // uninstall the package
            NugetPackageUninstaller.UninstallAll(InstalledPackagesManager.InstalledPackages.ToList());
            Assert.IsFalse(InstalledPackagesManager.IsInstalled(analyzer), "The package is STILL installed: {0} {1}", analyzer.Id, analyzer.Version);
        }
    }

    [Test]
    public void InstallProtobufTest([Values] InstallMode installMode)
    {
        ConfigureNugetConfig(installMode);

        var protobuf = new NugetPackageIdentifier("protobuf-net", "2.0.0.668") { IsManuallyInstalled = true };

        // install the package
        NugetPackageInstaller.InstallIdentifier(protobuf);
        Assert.IsTrue(InstalledPackagesManager.IsInstalled(protobuf), "The package was NOT installed: {0} {1}", protobuf.Id, protobuf.Version);

        // uninstall the package
        NugetPackageUninstaller.UninstallAll(InstalledPackagesManager.InstalledPackages.ToList());
        Assert.IsFalse(InstalledPackagesManager.IsInstalled(protobuf), "The package is STILL installed: {0} {1}", protobuf.Id, protobuf.Version);
    }

    [Test]
    public void InstallBootstrapCSSTest([Values] InstallMode installMode)
    {
        ConfigureNugetConfig(installMode);

        // disable the cache for now to force getting the lowest version of the dependency
        ConfigurationManager.NugetConfigFile.InstallFromCache = false;

        var bootstrap337 = new NugetPackageIdentifier("bootstrap", "3.3.7") { IsManuallyInstalled = true };

        NugetPackageInstaller.InstallIdentifier(bootstrap337);
        Assert.IsTrue(
            InstalledPackagesManager.IsInstalled(bootstrap337),
            "The package was NOT installed: {0} {1}",
            bootstrap337.Id,
            bootstrap337.Version);

        // Bootstrap CSS 3.3.7 has a dependency on jQuery [1.9.1, 4.0.0) ... 1.9.1 <= x < 4.0.0
        // Therefore it should install 1.9.1 since that is the lowest compatible version available
        var jQuery191 = new NugetPackageIdentifier("jQuery", "1.9.1");
        Assert.IsTrue(InstalledPackagesManager.IsInstalled(jQuery191), "The package was NOT installed: {0} {1}", jQuery191.Id, jQuery191.Version);

        // now upgrade jQuery to 3.1.1
        var jQuery311 = new NugetPackageIdentifier("jQuery", "3.1.1") { IsManuallyInstalled = true };
        NugetPackageInstaller.InstallIdentifier(jQuery311);
        Assert.IsTrue(InstalledPackagesManager.IsInstalled(jQuery311), "The package was NOT installed: {0} {1}", jQuery311.Id, jQuery311.Version);

        // reinstall bootstrap, which should use the currently installed jQuery 3.1.1
        NugetPackageUninstaller.Uninstall(bootstrap337, false);
        NugetPackageInstaller.InstallIdentifier(bootstrap337);

        Assert.IsFalse(InstalledPackagesManager.IsInstalled(jQuery191), "The package IS installed: {0} {1}", jQuery191.Id, jQuery191.Version);
        Assert.IsTrue(InstalledPackagesManager.IsInstalled(jQuery311), "The package was NOT installed: {0} {1}", jQuery311.Id, jQuery311.Version);

        // cleanup and uninstall everything
        NugetPackageUninstaller.UninstallAll(InstalledPackagesManager.InstalledPackages.ToList());

        // confirm they are uninstalled
        Assert.IsFalse(
            InstalledPackagesManager.IsInstalled(bootstrap337),
            "The package is STILL installed: {0} {1}",
            bootstrap337.Id,
            bootstrap337.Version);
        Assert.IsFalse(InstalledPackagesManager.IsInstalled(jQuery191), "The package is STILL installed: {0} {1}", jQuery191.Id, jQuery191.Version);
        Assert.IsFalse(InstalledPackagesManager.IsInstalled(jQuery311), "The package is STILL installed: {0} {1}", jQuery311.Id, jQuery311.Version);

        // turn cache back on
        ConfigurationManager.NugetConfigFile.InstallFromCache = true;
    }

    [Test]
    public void InstallStyleCopTest([Values] InstallMode installMode)
    {
        ConfigureNugetConfig(installMode);

        var styleCopPlusId = new NugetPackageIdentifier("StyleCopPlus.MSBuild", "4.7.49.5") { IsManuallyInstalled = true };
        var styleCopId = new NugetPackageIdentifier("StyleCop.MSBuild", "4.7.49.0");

        NugetPackageInstaller.InstallIdentifier(styleCopPlusId);

        // StyleCopPlus depends on StyleCop, so they should both be installed
        // it depends on version 4.7.49.0, so ensure it is also installed
        Assert.That(InstalledPackagesManager.InstalledPackages, Is.EquivalentTo(new[] { styleCopPlusId, styleCopId }));
    }

    [Test]
    public void InstallStyleCopWithoutDependenciesTest()
    {
        var styleCopPlusId = new NugetPackageIdentifier("StyleCopPlus.MSBuild", "4.7.49.5");

        NugetPackageInstaller.InstallIdentifier(styleCopPlusId, installDependencies: false);

        // StyleCopPlus depends on StyleCop, so without 'installDependencies' they are both installed
        Assert.That(InstalledPackagesManager.InstalledPackages, Is.EquivalentTo(new[] { styleCopPlusId }));
    }

    [Test]
    public void InstallSignalRClientTest([Values] InstallMode installMode)
    {
        ConfigureNugetConfig(installMode);

        var signalRClient = new NugetPackageIdentifier("Microsoft.AspNet.SignalR.Client", "2.2.2") { IsManuallyInstalled = true };

        NugetPackageInstaller.InstallIdentifier(signalRClient);
        Assert.IsTrue(
            InstalledPackagesManager.IsInstalled(signalRClient),
            "The package was NOT installed: {0} {1}",
            signalRClient.Id,
            signalRClient.Version);

        var directory45 = Path.Combine(
            ConfigurationManager.NugetConfigFile.RepositoryPath,
            $"{signalRClient.Id}.{signalRClient.Version}",
            "lib",
            "net45");

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
        NugetPackageUninstaller.UninstallAll(InstalledPackagesManager.InstalledPackages.ToList());
        Assert.IsFalse(
            InstalledPackagesManager.IsInstalled(signalRClient),
            "The package is STILL installed: {0} {1}",
            signalRClient.Id,
            signalRClient.Version);
    }

    [Test]
    public void InstallMicrosoftMlProbabilisticCompilerTest([Values] InstallMode installMode)
    {
        ConfigureNugetConfig(installMode);

        var probabilisticCompiler = new NugetPackageIdentifier("Microsoft.ML.Probabilistic.Compiler", "0.4.2301.301") { IsManuallyInstalled = true };

        NugetPackageInstaller.InstallIdentifier(probabilisticCompiler);
        Assert.IsTrue(
            InstalledPackagesManager.IsInstalled(probabilisticCompiler),
            "The package was NOT installed: {0} {1}",
            probabilisticCompiler.Id,
            probabilisticCompiler.Version);

        var libraryDirectory = Path.Combine(
            ConfigurationManager.NugetConfigFile.RepositoryPath,
            $"{probabilisticCompiler.Id}.{probabilisticCompiler.Version}",
            "lib",
            "netstandard2.0");

        Assert.That(libraryDirectory, Does.Exist);
        Assert.That(Path.Combine(libraryDirectory, "cs"), Does.Not.Exist);

        // cleanup and uninstall everything
        NugetPackageUninstaller.UninstallAll(InstalledPackagesManager.InstalledPackages.ToList());
        Assert.IsFalse(
            InstalledPackagesManager.IsInstalled(probabilisticCompiler),
            "The package is STILL installed: {0} {1}",
            probabilisticCompiler.Id,
            probabilisticCompiler.Version);
    }

    [Test]
    public void InstallPolySharp([Values] InstallMode installMode)
    {
        ConfigureNugetConfig(installMode);

        var polySharp = new NugetPackageIdentifier("PolySharp", "1.13.2+0596138b111ff552137684c6f7c3373805d2e3d2") { IsManuallyInstalled = true };
        NugetPackageInstaller.InstallIdentifier(polySharp);
        Assert.IsTrue(InstalledPackagesManager.IsInstalled(polySharp), "The package was NOT installed: {0} {1}", polySharp.Id, polySharp.Version);
        NugetPackageUninstaller.UninstallAll(InstalledPackagesManager.InstalledPackages.ToList());
        Assert.IsFalse(InstalledPackagesManager.IsInstalled(polySharp), "The package is STILL installed: {0} {1}", polySharp.Id, polySharp.Version);
    }

    [Test]
    public void InstallPrereleasPackage([Values] InstallMode installMode)
    {
        ConfigureNugetConfig(installMode);

        var package = new NugetPackageIdentifier("StyleCop.Analyzers", "1.2.0-beta.507") { IsManuallyInstalled = true };
        NugetPackageInstaller.InstallIdentifier(package);
        Assert.IsTrue(InstalledPackagesManager.IsInstalled(package), "The package was NOT installed: {0} {1}", package.Id, package.Version);
    }

    [Test]
    public void InstallAndSearchLocalPackageSource([Values] bool hierarchical)
    {
        var package = new NugetPackageIdentifier("protobuf-net", "2.0.0.668") { IsManuallyInstalled = true };
        var tempDirectoryPath = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "TempUnitTestFolder"));
        Directory.CreateDirectory(tempDirectoryPath);
        File.Copy(ConfigurationManager.NugetConfigFilePath, Path.Combine(tempDirectoryPath, NugetConfigFile.FileName));

        try
        {
            // get the package file by installing it
            NugetPackageInstaller.InstallIdentifier(package);
            Assert.IsTrue(InstalledPackagesManager.IsInstalled(package), "The package was NOT installed: {0} {1}", package.Id, package.Version);
            var packageFilePath = Path.Combine(
                ConfigurationManager.NugetConfigFile.RepositoryPath,
                $"{package.Id}.{package.Version}",
                package.PackageFileName);
            Assert.That(packageFilePath, Does.Exist.IgnoreDirectories);

            // Hierarchical folder structures are supported in NuGet 3.3+.
            // └─<packageID>
            //   └─<version>
            //     └─<packageID>.<version>.nupkg
            var targetDirectory = Path.Combine(
                tempDirectoryPath,
                hierarchical ? $"{package.Id}{Path.DirectorySeparatorChar}{package.Version}" : string.Empty);
            Directory.CreateDirectory(targetDirectory);
            File.Copy(packageFilePath, Path.Combine(targetDirectory, Path.GetFileName(packageFilePath)));
            NugetPackageUninstaller.UninstallAll(InstalledPackagesManager.InstalledPackages.ToList());
            Assert.IsFalse(InstalledPackagesManager.IsInstalled(package), "The package is STILL installed: {0} {1}", package.Id, package.Version);

            // force the package source to be the local directory
            var nugetConfig = ConfigurationManager.NugetConfigFile;
            nugetConfig.InstallFromCache = false;
            nugetConfig.PackageSources.Clear();
            nugetConfig.PackageSources.Add(new NugetPackageSourceLocal("LocalUnitTestSource", tempDirectoryPath));
            nugetConfig.Save(ConfigurationManager.NugetConfigFilePath);
            ConfigurationManager.LoadNugetConfigFile();

            // install the package from the local file
            NugetPackageInstaller.InstallIdentifier(package);
            Assert.IsTrue(InstalledPackagesManager.IsInstalled(package), "The package was NOT installed: {0} {1}", package.Id, package.Version);

            NugetPackageUninstaller.UninstallAll(InstalledPackagesManager.InstalledPackages.ToList());
            Assert.IsFalse(InstalledPackagesManager.IsInstalled(package), "The package is STILL installed: {0} {1}", package.Id, package.Version);

            // search local package source
            var localPackages = Task.Run(() => ConfigurationManager.Search()).GetAwaiter().GetResult();
            Assert.That(localPackages, Is.EqualTo(new[] { package }));

            // install without a version number
            var packageWithoutVersion = new NugetPackageIdentifier("protobuf-net", string.Empty) { IsManuallyInstalled = true };
            NugetPackageInstaller.InstallIdentifier(packageWithoutVersion);
            Assert.IsTrue(InstalledPackagesManager.IsInstalled(package), "The package was NOT installed: {0} {1}", package.Id, package.Version);

            NugetPackageUninstaller.UninstallAll(InstalledPackagesManager.InstalledPackages.ToList());
            Assert.IsFalse(InstalledPackagesManager.IsInstalled(package), "The package is STILL installed: {0} {1}", package.Id, package.Version);

            // install with a version range
            var packageWithRange = new NugetPackageIdentifier("protobuf-net", "[1.0)") { IsManuallyInstalled = true };
            NugetPackageInstaller.InstallIdentifier(packageWithRange);
            Assert.IsTrue(InstalledPackagesManager.IsInstalled(package), "The package was NOT installed: {0} {1}", package.Id, package.Version);

            NugetPackageUninstaller.UninstallAll(InstalledPackagesManager.InstalledPackages.ToList());
            Assert.IsFalse(InstalledPackagesManager.IsInstalled(package), "The package is STILL installed: {0} {1}", package.Id, package.Version);
        }
        finally
        {
            File.Copy(Path.Combine(tempDirectoryPath, NugetConfigFile.FileName), ConfigurationManager.NugetConfigFilePath, true);
            ConfigurationManager.LoadNugetConfigFile();
            Directory.Delete(tempDirectoryPath, true);
        }
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
    [TestCase("1.2.3+1234", "1.2.4")]
    [TestCase("1.2.3-rc1+1234", "1.2.4")]
    [TestCase("1.2.3-rc1+1234", "1.2.3-rc2")]
    [TestCase("1.2.3-rc1+1234", "1.2.3-rc2+1234")]
    [TestCase("1.0.0", "1.0.0.10")]
    public void VersionComparison(string smallerVersion, string greaterVersion)
    {
        var localNugetPackageSource = new NugetPackageSourceLocal("test", "test");
        var smallerPackage = new NugetPackageLocal(localNugetPackageSource) { Id = "TestPackage", Version = smallerVersion };
        var greaterPackage = new NugetPackageLocal(localNugetPackageSource) { Id = "TestPackage", Version = greaterVersion };

        Assert.IsTrue(smallerPackage.CompareTo(greaterPackage) < 0, "{0} was NOT smaller than {1}", smallerVersion, greaterVersion);
        Assert.IsTrue(greaterPackage.CompareTo(smallerPackage) > 0, "{0} was NOT greater than {1}", greaterVersion, smallerVersion);
    }

    [Test]
    [TestCase("1.0.0", "1.00.0")]
    [TestCase("1.0.0", "1.0.00")]
    [TestCase("1.0.0", "01.0.0")]
    [TestCase("1.0.0", "1.0")]
    [TestCase("1.0.0", "1.0.0.0")]
    [TestCase("1.0.0-rc1", "1.0.00-rc1")]
    [TestCase("1.0.0-rc1", "1.0-rc1")]
    [TestCase("1.0.0-rc.1", "1.0.0-RC.1")]
    [TestCase("1.0.0+123", "1.0.0")]
    [TestCase("1.0.0+123", "1.0")]
    [TestCase("1.0.0+123", "1.0.0+478")]
    [TestCase("1.0.0-rc1+123", "1.0.0-rc1")]
    [TestCase("1.0.0-rc1+123", "1.0.0-rc1+478")]
    public void VersionComparisonEqual(string version1, string version2)
    {
        var package1 = new NugetPackageIdentifier { Id = "TestPackage", Version = version1 };
        var package2 = new NugetPackageIdentifier { Id = "TestPackage", Version = version2 };

        Assert.IsTrue(package1.CompareTo(package2) == 0, "{0} was NOT equal to {1}", version1, version2);
        Assert.IsTrue(package2.CompareTo(package1) == 0, "{0} was NOT equal to {1}", version2, version1);
        Assert.IsTrue(package2.Equals(package1), "{0} was NOT equal to {1}", version2, version1);
        Assert.IsTrue(package1.Equals(package2), "{0} was NOT equal to {1}", version1, version2);
        Assert.IsTrue(package1.GetHashCode() == package2.GetHashCode(), "{0} has NOT equal hash-code to {1}", version1, version2);
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
        var versionIdentifier = new NugetPackageIdentifier("TestPackage", version);

        Assert.IsTrue(id.InRange(versionIdentifier), "{0} was NOT in range of {1}!", version, versionRange);
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
        var versionIdentifier = new NugetPackageIdentifier("TestPackage", version);

        Assert.IsFalse(id.InRange(versionIdentifier), "{0} WAS in range of {1}!", version, versionRange);
    }

    [Test]
    [TestCase("Illegal space")]
    [TestCase("Illegal@at")]
    [TestCase("SimpleName")]
    public void PackageSourceCredentialsTest(string name)
    {
        var resourcesFolder = Path.Combine(Directory.GetCurrentDirectory(), "Assets/Tests/Resources");
        var path = Path.Combine(resourcesFolder, NugetConfigFile.FileName);

        var username = "username";
        var password = "password";

        var file = NugetConfigFile.CreateDefaultFile(path);

        var inputSource = new NugetPackageSourceV2(name, "http://localhost") { UserName = username, SavedPassword = password };

        file.PackageSources.Add(inputSource);
        file.Save(path);

        var loaded = NugetConfigFile.Load(path);
        var parsedSource = loaded.PackageSources.Find(p => p.Name == name);
        Assert.That(parsedSource.HasPassword, Is.True);
        Assert.That(parsedSource.UserName, Is.EqualTo(username));
        Assert.That(parsedSource.SavedPassword, Is.EqualTo(password));
    }

    [Test]
    [TestCase("2018.4.30f1", false, false, false)]
    [TestCase("2018.4.30f1", true, false, false)]
    [TestCase("2021.3.16f1", false, true, true)]
    [TestCase("2021.3.16f1", true, true, true)]
    public void TryGetBestTargetFrameworkForCurrentSettingsTest(
        string unityVersion,
        bool useNetStandard,
        bool supportsNetStandard21,
        bool supportsNet48)
    {
        var unityVersionType = typeof(TargetFrameworkResolver).GetNestedType("UnityVersion", BindingFlags.NonPublic);
        Assume.That(unityVersionType, Is.Not.Null);
        var currentUnityVersionProperty = unityVersionType.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
        Assume.That(currentUnityVersionProperty, Is.Not.Null);
        Assume.That(currentUnityVersionProperty.CanRead, Is.True);
        Assume.That(currentUnityVersionProperty.CanWrite, Is.True);
        var oldValue = currentUnityVersionProperty.GetValue(null);
        var oldApiCompatibilityLevel = PlayerSettings.GetApiCompatibilityLevel(EditorUserBuildSettings.selectedBuildTargetGroup);

        try
        {
            currentUnityVersionProperty.SetValue(null, Activator.CreateInstance(unityVersionType, unityVersion));

            PlayerSettings.SetApiCompatibilityLevel(
                EditorUserBuildSettings.selectedBuildTargetGroup,
                useNetStandard ? ApiCompatibilityLevel.NET_Standard_2_0 : ApiCompatibilityLevel.NET_4_6);

            var allFrameworks = new List<string>
            {
                "unity",
                "netstandard21",
                "netstandard20",
                "netstandard16",
                "netstandard15",
                "netstandard14",
                "netstandard13",
                "netstandard12",
                "netstandard11",
                "netstandard10",
                "net48",
                "net472",
                "net471",
                "net47",
                "net462",
                "net461",
                "net46",
                "net452",
                "net451",
                "net45",
                "net403",
                "net40",
                "net4",
                "net35-unity full v3.5",
                "net35-unity subset v3.5",
                "net35",
                "net20",
                "net11",
            };

            var expectedBestMatch = new List<string>();
            var foundBestMatch = new List<string>();
            while (allFrameworks.Count > 0)
            {
                var bestMatch = TargetFrameworkResolver.TryGetBestTargetFrameworkForCurrentSettings(allFrameworks);
                foundBestMatch.Add(bestMatch);
                var expectedMatchIndex = 0;
                if (!useNetStandard)
                {
                    while (allFrameworks[expectedMatchIndex].StartsWith("netstandard"))
                    {
                        ++expectedMatchIndex;
                    }
                }
                else if (allFrameworks[expectedMatchIndex] == "net48")
                {
                    // stop when we reached the net-framework part as we only support net-standard
                    expectedBestMatch.Add(null);
                    break;
                }

                if (!supportsNetStandard21 && allFrameworks[expectedMatchIndex] == "netstandard21")
                {
                    ++expectedMatchIndex;
                }

                if (!supportsNet48 && allFrameworks[expectedMatchIndex] == "net48")
                {
                    ++expectedMatchIndex;
                }

                expectedBestMatch.Add(allFrameworks[expectedMatchIndex]);
                allFrameworks.RemoveAt(0);
            }

            Assert.That(foundBestMatch, Is.EqualTo(expectedBestMatch));
        }
        finally
        {
            currentUnityVersionProperty.SetValue(null, oldValue);
            PlayerSettings.SetApiCompatibilityLevel(EditorUserBuildSettings.selectedBuildTargetGroup, oldApiCompatibilityLevel);
        }
    }

    [Test]
    public void TestUpgrading()
    {
        var componentModelAnnotation47 = new NugetPackageIdentifier("System.ComponentModel.Annotations", "4.7.0") { IsManuallyInstalled = true };
        var componentModelAnnotation5 = new NugetPackageIdentifier("System.ComponentModel.Annotations", "5.0.0") { IsManuallyInstalled = true };

        NugetPackageInstaller.InstallIdentifier(componentModelAnnotation47);
        Assert.IsTrue(
            InstalledPackagesManager.IsInstalled(componentModelAnnotation47),
            "The package was NOT installed: {0} {1}",
            componentModelAnnotation47.Id,
            componentModelAnnotation47.Version);

        // Force NuGetHelper to reload the "alreadyImportedLibs" (like if the editor is re-opend)
        var field = typeof(UnityPreImportedLibraryResolver).GetField("alreadyImportedLibs", BindingFlags.Static | BindingFlags.NonPublic);
        Assume.That(field, Is.Not.Null, "Failed to find the field 'alreadyImportedLibs' in UnityPreImportedLibraryResolver");
        field.SetValue(null, null);

        NugetPackageInstaller.InstallIdentifier(componentModelAnnotation5);
        Assert.IsTrue(
            InstalledPackagesManager.IsInstalled(componentModelAnnotation5),
            "The package was NOT installed: {0} {1}",
            componentModelAnnotation5.Id,
            componentModelAnnotation5.Version);
        Assert.IsFalse(
            InstalledPackagesManager.IsInstalled(componentModelAnnotation47),
            "The package is STILL installed: {0} {1}",
            componentModelAnnotation47.Id,
            componentModelAnnotation47.Version);

        NugetPackageUninstaller.UninstallAll(InstalledPackagesManager.InstalledPackages.ToList());

        Assert.IsFalse(
            InstalledPackagesManager.IsInstalled(componentModelAnnotation5),
            "The package is STILL installed: {0} {1}",
            componentModelAnnotation5.Id,
            componentModelAnnotation5.Version);
    }

    [Test]
    [TestCase("4.7.0-pre-release+a5b8d")]
    [TestCase("4.7.0+a5b8d")]
    [TestCase("4.7.0")]
    [TestCase("[1.0,2.0]")]
    public void TestSerializeNugetPackageIdentifier(string version)
    {
        var identifier = new NugetPackageIdentifier("System.ComponentModel.Annotations", version) { IsManuallyInstalled = true };
        var serialized = JsonUtility.ToJson(identifier);
        var deserialized = JsonUtility.FromJson<NugetPackageIdentifier>(serialized);

        Assert.That(deserialized, Is.EqualTo(identifier));
        Assert.That(deserialized.IsPrerelease, Is.EqualTo(identifier.IsPrerelease));
        Assert.That(deserialized.Version, Is.EqualTo(identifier.Version));
        Assert.That(deserialized.PackageVersion.FullVersion, Is.EqualTo(identifier.PackageVersion.FullVersion));
        Assert.That(deserialized.IsManuallyInstalled, Is.EqualTo(identifier.IsManuallyInstalled));
        Assert.That(deserialized.CompareTo(identifier), Is.EqualTo(0));

        if (!identifier.HasVersionRange)
        {
            Assert.That(deserialized.InRange(identifier), Is.True);
        }
    }

    [Test]
    [TestCase("jQuery", "3.7.0")]
    public void TestPostprocessInstall(string packageId, string packageVersion)
    {
        var package = new NugetPackageIdentifier(packageId, packageVersion) { IsManuallyInstalled = true };

        var filepath = ConfigurationManager.NugetConfigFile.PackagesConfigFilePath;
        Assume.That(InstalledPackagesManager.IsInstalled(package), Is.False, "The package IS installed: {0} {1}", package.Id, package.Version);

        var packagesConfigFile = new PackagesConfigFile();
        packagesConfigFile.AddPackage(package);
        packagesConfigFile.Save();

        AssetDatabase.Refresh();
        AssetDatabase.ForceReserializeAssets(new[] { filepath });
        AssetDatabase.Refresh();

        //NugetAssetPostprocessor.OnPostprocessAllAssets(new[] { filepath }, null, null, null);

        Assert.IsTrue(InstalledPackagesManager.IsInstalled(package), "The package was NOT installed: {0} {1}", package.Id, package.Version);
    }

    [Test]
    [TestCase("jQuery", "3.7.0")]
    public void TestPostprocessUninstall(string packageId, string packageVersion)
    {
        var package = new NugetPackageIdentifier(packageId, packageVersion) { IsManuallyInstalled = true };

        var filepath = ConfigurationManager.NugetConfigFile.PackagesConfigFilePath;

        NugetPackageInstaller.InstallIdentifier(package);
        Assume.That(InstalledPackagesManager.IsInstalled(package), "The package was NOT installed: {0} {1}", package.Id, package.Version);

        var packagesConfigFile = new PackagesConfigFile();
        packagesConfigFile.Save();

        //NugetAssetPostprocessor.OnPostprocessAllAssets(new[] { filepath }, null, null, null);
        AssetDatabase.Refresh();
        AssetDatabase.ForceReserializeAssets(new[] { filepath });
        AssetDatabase.Refresh();

        Assert.IsFalse(InstalledPackagesManager.IsInstalled(package), "The package is STILL installed: {0} {1}", package.Id, package.Version);
    }

    [Test]
    [TestCase("jQuery", "3.6.4", "3.7.0")]
    [TestCase("jQuery", "3.7.0", "3.6.4")]
    public void TestPostprocessDifferentVersion(string packageId, string packageVersionOld, string packageVersionNew)
    {
        var packageOld = new NugetPackageIdentifier(packageId, packageVersionOld) { IsManuallyInstalled = true };
        var packageNew = new NugetPackageIdentifier(packageId, packageVersionNew) { IsManuallyInstalled = true };

        var filepath = ConfigurationManager.NugetConfigFile.PackagesConfigFilePath;

        NugetPackageInstaller.InstallIdentifier(packageOld);
        Assume.That(InstalledPackagesManager.IsInstalled(packageOld), "The package was NOT installed: {0} {1}", packageOld.Id, packageOld.Version);

        var packagesConfigFile = new PackagesConfigFile();
        packagesConfigFile.AddPackage(packageNew);
        packagesConfigFile.Save();

        //NugetAssetPostprocessor.OnPostprocessAllAssets(new[] { filepath }, null, null, null);
        AssetDatabase.Refresh();
        AssetDatabase.ForceReserializeAssets(new[] { filepath });
        AssetDatabase.Refresh();



        Assert.IsFalse(
            InstalledPackagesManager.IsInstalled(packageOld),
            "The old package version IS STILL installed: {0} {1}",
            packageOld.Id,
            packageOld.Version);

        Assert.IsTrue(
            InstalledPackagesManager.IsInstalled(packageNew),
            "The new package version was NOT installed: {0} {1}",
            packageNew.Id,
            packageNew.Version);
    }

    [Test]
    [TestCase("Assets", "Assets")]
    [TestCase(".", ".")]
    [TestCase("", "")]
    [TestCase("Assets/../../", "..")]
    [TestCase("Assets/../../../", "../..")]
    [TestCase("M:/Test", "M:/Test")]
    [TestCase("./Assets", "Assets")]
    [TestCase("a/b/c", "a/b/c")]
    [TestCase("../../", "../..")]
    [TestCase("../..", "../..")]
    [TestCase("M:/Test/", "M:/Test/")]
    [TestCase("M:/Test/test.txt", "M:/Test/test.txt")]
    public void GetRelativePathTest(string path, string expected)
    {
        // allow running tests on windows and linux
        expected = expected.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var relativePath = PathHelper.GetRelativePath(UnityPathHelper.AbsoluteProjectPath, path);
        Assert.That(relativePath, Is.EqualTo(expected));
    }

    private static void ConfigureNugetConfig(InstallMode installMode)
    {
        var nugetConfigFile = ConfigurationManager.NugetConfigFile;
        var packageSources = nugetConfigFile.PackageSources;
        packageSources.Single(source => source.Name == "V3").IsEnabled = installMode == InstallMode.ApiV3Only;
        packageSources.Single(source => source.Name == "NuGet").IsEnabled =
            installMode == InstallMode.ApiV2Only || installMode == InstallMode.ApiV2AllowCached;
        nugetConfigFile.InstallFromCache = installMode == InstallMode.ApiV2AllowCached;
    }
}
