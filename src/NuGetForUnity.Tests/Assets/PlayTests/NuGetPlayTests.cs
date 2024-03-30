using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using NugetForUnity.Configuration;
using NugetForUnity.Models;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
///     Play mode tests allow us to install NuGet packages with Native code before play mode starts, then when play mode
///     runs the native libraries are available for use.
///     There seems to be some Unity internals that prevents an edit mode test from adding a Native library and finding it
///     in the same run.
/// </summary>
public class NuGetPlayTests : IPrebuildSetup, IPostBuildCleanup
{
    /// <summary>
    ///     This is the version number of sqlite with periods replaced with zeros.
    ///     Note: Version of the SQLite library does not match the NuGet package version.
    /// </summary>
    private readonly int expectedVersion = 3035005;

    private readonly NugetPackageIdentifier sqlite = new NugetPackageIdentifier("SQLitePCLRaw.lib.e_sqlite3", "2.0.7");

    [UnityTest]
    public IEnumerator InstallAndRunSqlite()
    {
        yield return new WaitForFixedUpdate();

        Assert.That(Path.Combine(ConfigurationManager.NugetConfigFile.RepositoryPath, $"{sqlite.Id}.{sqlite.Version}"), Does.Exist.IgnoreFiles);

        // Test the actual library by calling a "extern" method
        var version = sqlite3_libversion_number();
        Assert.That(version, Is.EqualTo(expectedVersion));
    }

    /// <inheritdoc />
    public void Setup()
    {
        try
        {
            sqlite3_libversion_number();
            Assert.Fail("e_sqlite3 dll loaded, but should not be available");
        }
        catch (DllNotFoundException)
        {
        }

        var targetPackageDirectory = Path.Combine(ConfigurationManager.NugetConfigFile.RepositoryPath, $"{sqlite.Id}.{sqlite.Version}");
        if (Directory.Exists(targetPackageDirectory))
        {
            Directory.Delete(targetPackageDirectory, true);
        }

        // For windows we end up importing two identical DLLs temporarily, this causes an error log that NUnit detects
        // and would fail the test if we don't tell it to ignore the Failing messages
        LogAssert.ignoreFailingMessages = true;

        var config = PackagesConfigFile.Load();
        config.Packages.Add(new PackageConfig { Id = sqlite.Id, Version = sqlite.Version, IsManuallyInstalled = true });
        config.GetType().GetField("contentIsSameAsInFilePath", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, null);
        config.Save();
        AssetDatabase.Refresh();

        Assert.That(targetPackageDirectory, Does.Exist.IgnoreFiles);
    }

    /// <inheritdoc />
    public void Cleanup()
    {
        var config = PackagesConfigFile.Load();
        config.Packages.Clear();
        config.GetType().GetField("contentIsSameAsInFilePath", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, null);
        config.Save();

        AssetDatabase.Refresh();
    }

    /// <summary>
    ///     Call to the SQLite native file, this actually tests loading and access the library when called.
    ///     On windows the call to sqlite3_libversion returns a garbled string so use the integer
    /// </summary>
    /// <returns>The version number.</returns>
    [DllImport("e_sqlite3")]
    private static extern int sqlite3_libversion_number();
}
