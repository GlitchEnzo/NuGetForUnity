#pragma warning disable SA1512,SA1124 // Single-line comments should not be followed by blank line

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using UnityEngine;
using Debug = UnityEngine.Debug;

#region No ReShaper

// ReSharper disable All
// needed because 'JetBrains.Annotations.NotNull' and 'System.Diagnostics.CodeAnalysis.NotNull' collide if this file is compiled with a never version of Unity / C#
using SuppressMessageAttribute = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;

// ReSharper restore All

#endregion

#pragma warning restore SA1512,SA1124 // Single-line comments should not be followed by blank line
namespace NugetForUnity.Helper
{
    /// <summary>
    ///     Helper class for retrieving credentials from NuGet credential providers.
    /// </summary>
    internal static class CredentialProviderHelper
    {
        // TODO: Move to settings
        private static readonly List<AuthenticatedFeed> KnownAuthenticatedFeeds = new List<AuthenticatedFeed>
        {
            new AuthenticatedFeed(
                @"^https:\/\/(?<account>[-a-zA-Z0-9]+)\.pkgs\.visualstudio\.com",
                "https://{account}.pkgs.visualstudio.com/_apis/public/nuget/client/CredentialProviderBundle.zip"),
            new AuthenticatedFeed(
                @"^https:\/\/pkgs\.dev\.azure\.com\/(?<account>[-a-zA-Z0-9]+)\/",
                "https://pkgs.dev.azure.com/{account}/_apis/public/nuget/client/CredentialProviderBundle.zip"),
        };

        /// <summary>
        ///     The dictionary of cached credentials retrieved by credential providers, keyed by feed URI.
        /// </summary>
        private static readonly Dictionary<Uri, CredentialProviderResponse?> CachedCredentialsByFeedUri =
            new Dictionary<Uri, CredentialProviderResponse?>();

        /// <summary>
        ///     Helper function to acquired a token to access VSTS hosted NuGet feeds by using the CredentialProvider.VSS.exe
        ///     tool. Downloading it from the VSTS instance if needed.
        ///     See here for more info on NuGet Credential Providers:
        ///     https://docs.microsoft.com/en-us/nuget/reference/extensibility/nuget-exe-credential-providers.
        /// </summary>
        /// <param name="feedUri">The url where the VSTS instance is hosted, the HostName is extracted (such as microsoft.pkgs.visualsudio.com).</param>
        /// <returns>The password in the form of a token, or null if the password could not be acquired.</returns>
        public static (string UserName, string Password)? GetCredentialFromProvider([NotNull] Uri feedUri)
        {
            feedUri = GetTruncatedFeedUri(feedUri);
            if (!CachedCredentialsByFeedUri.TryGetValue(feedUri, out var response))
            {
                response = GetCredentialFromProvider_Uncached(feedUri, true);
                CachedCredentialsByFeedUri[feedUri] = response;
            }

            if (response == null)
            {
                return null;
            }

            return (response.Value.Username, response.Value.Password);
        }

        /// <summary>
        ///     Clears static credentials previously cached by GetCredentialFromProvider.
        /// </summary>
        public static void ClearCachedCredentials()
        {
            CachedCredentialsByFeedUri.Clear();
        }

        /// <summary>
        ///     Given the URI of a NuGet method, returns the URI of the feed itself without the method and query parameters.
        /// </summary>
        /// <param name="methodUri">URI of NuGet method.</param>
        /// <returns>URI of the feed without the method and query parameters.</returns>
        [NotNull]
        private static Uri GetTruncatedFeedUri([NotNull] Uri methodUri)
        {
            var truncatedUriString = methodUri.GetLeftPart(UriPartial.Path);

            // Pull off the function if there is one
            if (truncatedUriString.EndsWith(")", StringComparison.Ordinal))
            {
                var lastSeparatorIndex = truncatedUriString.LastIndexOf('/');
                if (lastSeparatorIndex != -1)
                {
                    truncatedUriString = truncatedUriString.Substring(0, lastSeparatorIndex);
                }
            }

            var truncatedUri = new Uri(truncatedUriString);
            return truncatedUri;
        }

        /// <summary>
        ///     Internal function called by GetCredentialFromProvider to implement retrieving credentials. For performance reasons,
        ///     most functions should call GetCredentialFromProvider in order to take advantage of cached credentials.
        /// </summary>
        private static CredentialProviderResponse? GetCredentialFromProvider_Uncached([NotNull] Uri feedUri, bool downloadIfMissing)
        {
            NugetLogger.LogVerbose("Getting credential for {0}", feedUri);

            // Build the list of possible locations to find the credential provider. In order it should be local APP-Data, paths set on the
            // environment variable, and lastly look at the root of the packages save location.
            var possibleCredentialProviderPaths = new List<string> { GetDefaultCredentialProvidersPath() };

            possibleCredentialProviderPaths.AddRange(GetEnvironmentCredentialProviderPaths());

            // Try to find any nuget.exe in the package tools installation location
            var toolsPackagesFolder = Path.Combine(UnityPathHelper.AbsoluteProjectPath, "Packages");
            possibleCredentialProviderPaths.Add(toolsPackagesFolder);

            // Search through all possible paths to find the credential provider.
            var providerPaths = new List<string>();
            foreach (var possiblePath in possibleCredentialProviderPaths.Distinct())
            {
                if (Directory.Exists(possiblePath))
                {
                    providerPaths.AddRange(Directory.GetFiles(possiblePath, "credentialprovider*.exe", SearchOption.AllDirectories));
                }
            }

            foreach (var providerPath in providerPaths.Distinct())
            {
                // Launch the credential provider executable and get the JSON encoded response from the std output
                using (var process = new Process())
                {
                    process.StartInfo = NugetCliHelper.CreateStartInfoForDotNetExecutable(providerPath, $"-uri \"{feedUri}\"");
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.Start();
                    process.WaitForExit();

                    var output = process.StandardOutput.ReadToEnd();
                    var errors = process.StandardError.ReadToEnd();

                    switch ((CredentialProviderExitCode)process.ExitCode)
                    {
                        case CredentialProviderExitCode.ProviderNotApplicable:
                            break; // Not the right provider
                        case CredentialProviderExitCode.Failure: // Right provider, failure to get credentials
                            Debug.LogErrorFormat(
                                "Failed to get credentials from {0}!\n\tOutput\n\t{1}\n\tErrors\n\t{2}",
                                providerPath,
                                output,
                                errors);
                            return null;

                        case CredentialProviderExitCode.Success:
                            return JsonUtility.FromJson<CredentialProviderResponse>(output);
                        default:
                            Debug.LogWarningFormat(
                                "Unrecognized exit code {0} from {1} {2}",
                                process.ExitCode,
                                providerPath,
                                process.StartInfo.Arguments);
                            break;
                    }
                }
            }

            if (downloadIfMissing && DownloadCredentialProviders(feedUri))
            {
                return GetCredentialFromProvider_Uncached(feedUri, false);
            }

            return null;
        }

        private static IEnumerable<string> GetEnvironmentCredentialProviderPaths()
        {
            var environmentCredentialProviderPaths = Environment.GetEnvironmentVariable("NUGET_CREDENTIALPROVIDERS_PATH");
            if (!string.IsNullOrEmpty(environmentCredentialProviderPaths))
            {
                return environmentCredentialProviderPaths.Split(new[] { Path.PathSeparator, ';' }, StringSplitOptions.RemoveEmptyEntries);
            }

            return Array.Empty<string>();
        }

        private static string GetDefaultCredentialProvidersPath()
        {
            var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(baseDirectory))
            {
                // we need a place to store the credential provider so we fallback to the temp location.
                baseDirectory = Path.GetTempPath();
            }

            return Path.Combine(baseDirectory, "Nuget", "CredentialProviders");
        }

        private static bool DownloadCredentialProviders([NotNull] Uri feedUri)
        {
            var anyDownloaded = false;
            foreach (var feed in KnownAuthenticatedFeeds)
            {
                var account = feed.GetAccount(feedUri.ToString());
                if (string.IsNullOrEmpty(account))
                {
                    continue;
                }

                var providerUrl = feed.GetProviderUrl(account);

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable SYSLIB0014 // Type or member is obsolete
                var credentialProviderRequest = (HttpWebRequest)WebRequest.Create(providerUrl);
#pragma warning restore SYSLIB0014 // Type or member is obsolete
#pragma warning restore IDE0079 // Remove unnecessary suppression

                var tempFileName = Path.GetTempFileName();
                try
                {
                    var credentialProviderDownloadStream = credentialProviderRequest.GetResponse().GetResponseStream() ??
                                                           throw new InvalidOperationException("Response stream is null.");

                    NugetLogger.LogVerbose("Writing {0} to {1}", providerUrl, tempFileName);

                    using (var file = File.Create(tempFileName))
                    {
                        credentialProviderDownloadStream.CopyTo(file);
                    }

                    var providerDestination = GetEnvironmentCredentialProviderPaths().FirstOrDefault();
                    if (string.IsNullOrEmpty(providerDestination))
                    {
                        providerDestination = GetDefaultCredentialProvidersPath();
                    }

                    // Normalizes the path.
                    providerDestination = Path.GetFullPath(providerDestination);

                    // Ensures that the last character on the extraction path is the directory separator char.
                    if (!providerDestination.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                    {
                        providerDestination += Path.DirectorySeparatorChar;
                    }

                    // Unzip the bundle and extract any credential provider exes
                    using (var zip = ZipFile.OpenRead(tempFileName))
                    {
                        foreach (var entry in zip.Entries)
                        {
                            // TODO: probably we should extract all files
                            if (!Regex.IsMatch(entry.FullName, @"^credentialprovider.+\.exe$", RegexOptions.IgnoreCase))
                            {
                                continue;
                            }

                            // Gets the full path to ensure that relative segments are removed.
                            var filePath = Path.GetFullPath(Path.Combine(providerDestination, entry.FullName));
                            if (!filePath.StartsWith(providerDestination, StringComparison.Ordinal))
                            {
                                // disallow leaving destination path
                                continue;
                            }

                            NugetLogger.LogVerbose("Extracting {0} to {1}", entry.FullName, providerDestination);
                            var directory = Path.GetDirectoryName(filePath);
                            Directory.CreateDirectory(directory ?? throw new InvalidOperationException("Path has no directory name."));

                            entry.ExtractToFile(filePath, true);
                            anyDownloaded = true;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogErrorFormat("Failed to download credential provider from {0}: {1}", credentialProviderRequest.Address, e.Message);
                }
                finally
                {
                    try
                    {
                        // Delete the bundle
                        File.Delete(tempFileName);
                    }
                    catch
                    {
                        // ignore error while deleting temp file
                    }
                }
            }

            return anyDownloaded;
        }

        /// <summary>
        ///     Possible response codes returned by a Nuget credential provider as described here:
        ///     https://docs.microsoft.com/en-us/nuget/reference/extensibility/nuget-exe-credential-providers#creating-a-nugetexe-credential-provider.
        /// </summary>
        [SuppressMessage(
            "StyleCop.CSharp.OrderingRules",
            "SA1201:Elements should appear in the correct order",
            Justification = "We like private enums at the botom of the file.")]
        private enum CredentialProviderExitCode
        {
            Success = 0,

            ProviderNotApplicable = 1,

            Failure = 2,
        }

        private readonly struct AuthenticatedFeed
        {
            private readonly string accountUrlPattern;

            private readonly string providerUrlTemplate;

            public AuthenticatedFeed([NotNull] string accountUrlPattern, [NotNull] string providerUrlTemplate)
            {
                this.accountUrlPattern = accountUrlPattern;
                this.providerUrlTemplate = providerUrlTemplate;
            }

            [CanBeNull]
            public string GetAccount([NotNull] string url)
            {
                var match = Regex.Match(url, accountUrlPattern, RegexOptions.IgnoreCase);
                return !match.Success ? null : match.Groups["account"].Value;
            }

            public string GetProviderUrl([NotNull] string account)
            {
                return providerUrlTemplate.Replace("{account}", account);
            }
        }

        /// <summary>
        ///     Data class returned from nuget credential providers in a JSON format. As described here:
        ///     https://docs.microsoft.com/en-us/nuget/reference/extensibility/nuget-exe-credential-providers#creating-a-nugetexe-credential-provider.
        /// </summary>
        [Serializable]
        [SuppressMessage("ReSharper", "UnassignedField.Local", Justification = "Used by serializer.")]
        [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Need to match the serialized name.")]
        [SuppressMessage("Design", "CA1051:Do not declare visible instance fields", Justification = "We use public so it can be serialized.")]
#pragma warning disable 0649 // CS0649: Field 'field' is never assigned to, and will always have its default value 'value'
        private struct CredentialProviderResponse
        {
            [CanBeNull]
            public string Password;

            // Ignore Spelling: Username
            [CanBeNull]
            public string Username;
        }
#pragma warning restore 0649 // CS0649: Field 'field' is never assigned to, and will always have its default value 'value'
    }
}
