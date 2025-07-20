using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NugetForUnity.Configuration;
using NUnit.Framework;

namespace NuGetForUnity.Cli.Tests
{
    /// <summary>
    ///     Contains tests for the NuGetForUnity CLI restore command, verifying that packages can be restored
    ///     with various package sources and that the CLI behaves as expected.
    /// </summary>
    public class CliRestoreTests
    {
        private const string TestPackageName = "Newtonsoft.Json";

        private const string TestPackageVersion = "13.0.1";

        // We keep the temp directory static to avoid creating different directories for each test
        // We can't use different directories for each test because NuGetForUnity uses static fields to store the project path
        private static readonly string TestProjectDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        [Test]
        public async Task Restore_WithPackageSources_ShouldExitZero([Values] bool useAdditionalSource)
        {
            // If useAdditionalSource, start a local HTTP server that responds to all request (simulates a empty nuget V3 source)
            string? emptySourceUrl = null;
            HttpListener? listener = null;
            if (useAdditionalSource)
            {
                // Find a free port
                var tcpListener = new TcpListener(IPAddress.Loopback, 0);
                tcpListener.Start();
                var listenerPort = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
                tcpListener.Stop();

                // Start the HTTP listener on the free port
                emptySourceUrl = $"http://127.0.0.1:{listenerPort}/empty/v3/index.json";
                listener = new HttpListener();
                listener.Prefixes.Add($"http://127.0.0.1:{listenerPort}/");
                listener.Start();

                await Task.Delay(100); // Give the listener some time to start

                // Respond to all requests (run in background)
                _ = Task.Run(
                    async () =>
                    {
                        while (listener.IsListening)
                        {
                            try
                            {
                                var httpContext = await listener.GetContextAsync();
                                httpContext.Response.StatusCode = 200;
                                httpContext.Response.ContentType = "application/json";
                                var responseJson = HandleNuGetSourceRequest(httpContext);
                                var bytes = Encoding.UTF8.GetBytes(responseJson);
                                httpContext.Response.OutputStream.Write(bytes);
                                httpContext.Response.OutputStream.Flush();
                                httpContext.Response.Close();
                            }
                            catch (ObjectDisposedException)
                            {
                                break;
                            }
                            catch
                            {
                                break;
                            }
                        }
                    });
            }

            using var disposeTcpListener = listener;
            var additionalPackageSourceXml = useAdditionalSource && emptySourceUrl != null ?
                $"<add key=\"EmptySource\" value=\"{emptySourceUrl}\" />" :
                string.Empty;

            // Arrange: create a temporary fake Unity project directory
            Directory.CreateDirectory(TestProjectDirectory);
            var assetsDirectory = Path.Combine(TestProjectDirectory, "Assets");
            Directory.CreateDirectory(assetsDirectory);
            var projectSettingsDirectory = Path.Combine(TestProjectDirectory, "ProjectSettings");
            Directory.CreateDirectory(projectSettingsDirectory);
            try
            {
                File.WriteAllText(Path.Combine(projectSettingsDirectory, "ProjectVersion.txt"), "m_EditorVersion: 2021.3.16f1\n");

                // Create ProjectSettings.asset with valid apiCompatibilityLevel
                File.WriteAllText(Path.Combine(projectSettingsDirectory, "ProjectSettings.asset"), "apiCompatibilityLevel: 6\n");

                // Create packages.config with a package that only exists in one source
                File.WriteAllText(
                    Path.Combine(assetsDirectory, "packages.config"),
                    $"""
                         <?xml version="1.0" encoding="utf-8"?>
                         <packages>
                           <package id="{TestPackageName}" version="{TestPackageVersion}" />
                         </packages>
                         """.Trim());

                // Create NuGet.config with the given sources and verbose logging
                var nugetConfigPath = Path.Combine(assetsDirectory, "NuGet.config");
                File.WriteAllText(
                    nugetConfigPath,
                    $"""
                         <?xml version="1.0" encoding="utf-8"?>
                         <configuration>
                           <packageSources>
                             {additionalPackageSourceXml}
                             <add key="NugetOrg" value="https://api.nuget.org/v3/index.json" />
                           </packageSources>
                           <config>
                             <add key="verbose" value="true" />
                             <add key="InstallFromCache" value="false" />
                           </config>
                         </configuration>
                         """.Trim());

                // reset values that are static
                typeof(ConfigurationManager).Assembly.GetType("UnityEngine.Debug", true)!.GetProperty(
                    "HasError",
                    BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, false);
                var projectPathFromLastRun =
                    typeof(Program).Assembly.GetType("UnityEngine.Application", true)!.GetProperty("dataPath")!.GetValue(null);
                if (projectPathFromLastRun is not null)
                {
                    // Force reload configuration to ensure the new NuGet.config is loaded
                    ConfigurationManager.LoadNugetConfigFile();
                }

                // Act: call CLI directly and capture stdout/stderr
                var initialConsoleOutputStream = Console.Out;
                var initialErrorOutputStream = Console.Error;
                using var consoleOutputWriter = new StringWriter();
                using var errorOutputWriter = new StringWriter();
                int exitCode;
                try
                {
                    Console.SetOut(consoleOutputWriter);
                    Console.SetError(errorOutputWriter);
                    exitCode = Program.Main(["restore", TestProjectDirectory]);
                }
                finally
                {
                    Console.SetOut(initialConsoleOutputStream);
                    Console.SetError(initialErrorOutputStream);
                }

                var consoleOutput = consoleOutputWriter.ToString();
                var errorOutput = errorOutputWriter.ToString();

                // Output for debugging
                TestContext.Out.WriteLine($"CLI Console output:\n{consoleOutput}");
                TestContext.Out.WriteLine($"CLI Console error output:\n{errorOutput}");

                // Assert: ExitCode == 0 and package is installed
                Assert.That(
                    exitCode,
                    Is.EqualTo(0),
                    () => $"Exit code was not zero:\nConsole error output:\n{errorOutput}\nConsole output:\n{consoleOutput}");
                var repoPath = Path.Combine(TestProjectDirectory, "Assets", "Packages", $"{TestPackageName}.{TestPackageVersion}");
                Assert.That(
                    repoPath,
                    Does.Exist.IgnoreFiles,
                    $"Expecting package to be installed at: {repoPath}\nConsole output:\n{consoleOutput}\nConsole error output:\n{errorOutput}");
                Assert.That(errorOutput, Is.Empty, () => $"Console error output was not empty:\n{errorOutput}\nConsole output:\n{consoleOutput}");
            }
            finally
            {
                Directory.Delete(TestProjectDirectory, true);
            }
        }

        private static string HandleNuGetSourceRequest(HttpListenerContext httpContext)
        {
            var requestBaseUrl = httpContext.Request.Url!.GetLeftPart(UriPartial.Authority);
            var absolutePath = httpContext.Request.Url.AbsolutePath;

            if (absolutePath == "/empty/v3/index.json")
            {
                return $$"""
                         {
                             "version": "3.0.0",
                             "resources": [
                                 {
                                     "@id": "{{requestBaseUrl}}/query",
                                     "@type": "SearchQueryService",
                                     "comment": "Query endpoint of NuGet Search service (secondary)"
                                 },
                                 {
                                     "@id": "{{requestBaseUrl}}/registration/",
                                     "@type": "RegistrationsBaseUrl",
                                     "comment": "Base URL of storage where NuGet package registration info is stored"
                                 },
                                 {
                                     "@id": "{{requestBaseUrl}}/v3-flatcontainer/",
                                     "@type": "PackageBaseAddress/3.0.0",
                                     "comment": "Base URL of where NuGet packages are stored, in the format https://api.nuget.org/v3-flatcontainer/{id-lower}/{version-lower}/{id-lower}.{version-lower}.nupkg"
                                 }
                             ]
                         }
                         """;
            }

            if (absolutePath.StartsWith("/registration/", StringComparison.Ordinal))
            {
                httpContext.Response.StatusCode = 404;
                return "Package doesn't exist";
            }

            return "{}";
        }
    }
}
