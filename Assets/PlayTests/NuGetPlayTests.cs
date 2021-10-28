using System;
using System.Collections;
using System.Runtime.InteropServices;
using NugetForUnity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[assembly: PrebuildSetup(typeof(NuGetPlayTests))]
[assembly: PostBuildCleanup(typeof(NuGetPlayTests))]

/// <summary>
/// Play mode tests allow us to install NuGet packages with Native code before play mode starts, then when play mode
/// runs the native libraries are available for use.
///
/// There seems to be some Unity internals that prevents an edit mode test from adding a Native library and finding it
/// in the same run. 
/// </summary>
public class NuGetPlayTests : IPrebuildSetup, IPostBuildCleanup
{
    readonly NugetPackageIdentifier sqlite = new NugetPackageIdentifier("SQLitePCLRaw.lib.e_sqlite3", "2.0.7");

    /// <summary>
    /// This is the version number of sqlite with periods replaced with zeros.
    ///
    /// Note: Version of the SQLite library does not match the NuGet package version 
    /// </summary>
    private readonly int _expectedVersion = 3035005;

    [UnityTest]
    public IEnumerator InstallAndRunSqlite()
    {
        yield return new WaitForFixedUpdate();

        // Test the actual library by calling a "extern" method
        var version = sqlite3_libversion_number();
        Assert.That(version, Is.EqualTo(_expectedVersion));
    }

    /// <summary>
    /// Call to the SQLite native file, this actually tests loading and access the library when called.
    ///
    /// On windows the call to sqlite3_libversion returns a garbled string so use the integer
    /// </summary>
    /// <returns></returns>
    [DllImport("e_sqlite3")]
    private static extern int sqlite3_libversion_number();

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

        // For windows we end up importing two identical DLLs temporarily, this causes an error log that NUnit detects
        // and would fail the test if we don't tell it to ignore the Failing messages
        NugetHelper.LoadNugetConfigFile();
        NugetHelper.LoadSettingFile();
        LogAssert.ignoreFailingMessages = true;
        NugetHelper.InstallIdentifier(sqlite);
        Assert.IsTrue(NugetHelper.IsInstalled(sqlite), "The package was NOT installed: {0} {1}", sqlite.Id,
            sqlite.Version);
    }

    public void Cleanup()
    {
        NugetHelper.Uninstall(sqlite);
        Assert.IsFalse(NugetHelper.IsInstalled(sqlite), "The packages are STILL installed: {0} {1}", sqlite.Id,
            sqlite.Version);
    }
}