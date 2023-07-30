using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using NugetForUnity;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
///     Helper class for retrieving credentials from NuGet credential providers.
/// </summary>
internal static class CredentialProviderHelper
{
    // TODO: Move to ScriptableObjet
    private static readonly List<AuthenticatedFeed> KnownAuthenticatedFeeds = new List<AuthenticatedFeed>
    {
        new AuthenticatedFeed
        {
            AccountUrlPattern = @"^https:\/\/(?<account>[-a-zA-Z0-9]+)\.pkgs\.visualstudio\.com",
            ProviderUrlTemplate = "https://{account}.pkgs.visualstudio.com/_apis/public/nuget/client/CredentialProviderBundle.zip",
        },
        new AuthenticatedFeed
        {
            AccountUrlPattern = @"^https:\/\/pkgs\.dev\.azure\.com\/(?<account>[-a-zA-Z0-9]+)\/",
            ProviderUrlTemplate = "https://pkgs.dev.azure.com/{account}/_apis/public/nuget/client/CredentialProviderBundle.zip",
        },
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
    public static (string UserName, string Password)? GetCredentialFromProvider(Uri feedUri)
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
    private static Uri GetTruncatedFeedUri(Uri methodUri)
    {
        var truncatedUriString = methodUri.GetLeftPart(UriPartial.Path);

        // Pull off the function if there is one
        if (truncatedUriString.EndsWith(")"))
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
    private static CredentialProviderResponse? GetCredentialFromProvider_Uncached(Uri feedUri, bool downloadIfMissing)
    {
        NugetHelper.LogVerbose("Getting credential for {0}", feedUri);

        // Build the list of possible locations to find the credential provider. In order it should be local app data, paths set on the
        // environment varaible, and lastly look at the root of the pacakges save location.
        var possibleCredentialProviderPaths = new List<string>
        {
            Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nuget"), "CredentialProviders"),
        };

        var environmentCredentialProviderPaths = Environment.GetEnvironmentVariable("NUGET_CREDENTIALPROVIDERS_PATH");
        if (!string.IsNullOrEmpty(environmentCredentialProviderPaths))
        {
            possibleCredentialProviderPaths.AddRange(
                environmentCredentialProviderPaths.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries) ?? Enumerable.Empty<string>());
        }

        // Try to find any nuget.exe in the package tools installation location
        var toolsPackagesFolder = Path.Combine(NugetHelper.AbsoluteProjectPath, "Packages");
        possibleCredentialProviderPaths.Add(toolsPackagesFolder);

        // Search through all possible paths to find the credential provider.
        var providerPaths = new List<string>();
        foreach (var possiblePath in possibleCredentialProviderPaths)
        {
            if (Directory.Exists(possiblePath))
            {
                providerPaths.AddRange(Directory.GetFiles(possiblePath, "credentialprovider*.exe", SearchOption.AllDirectories));
            }
        }

        foreach (var providerPath in providerPaths.Distinct())
        {
            // Launch the credential provider executable and get the json encoded response from the std output
            var process = new Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.FileName = providerPath;
            process.StartInfo.Arguments = string.Format("-uri \"{0}\"", feedUri);

            // http://stackoverflow.com/questions/16803748/how-to-decode-cmd-output-correctly
            // Default = 65533, ASCII = ?, Unicode = nothing works at all, UTF-8 = 65533, UTF-7 = 242 = WORKS!, UTF-32 = nothing works at all
            process.StartInfo.StandardOutputEncoding = Encoding.GetEncoding(850);
            process.Start();
            process.WaitForExit();

            var output = process.StandardOutput.ReadToEnd();
            var errors = process.StandardError.ReadToEnd();

            switch ((CredentialProviderExitCode)process.ExitCode)
            {
                case CredentialProviderExitCode.ProviderNotApplicable:
                    break; // Not the right provider
                case CredentialProviderExitCode.Failure: // Right provider, failure to get creds
                    {
                        Debug.LogErrorFormat("Failed to get credentials from {0}!\n\tOutput\n\t{1}\n\tErrors\n\t{2}", providerPath, output, errors);
                        return null;
                    }
                case CredentialProviderExitCode.Success:
                    {
                        return JsonUtility.FromJson<CredentialProviderResponse>(output);
                    }
                default:
                    {
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

    private static bool DownloadCredentialProviders(Uri feedUri)
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

            try
            {
                var credentialProviderDownloadStream = credentialProviderRequest.GetResponse().GetResponseStream();

                var tempFileName = Path.GetTempFileName();
                NugetHelper.LogVerbose("Writing {0} to {1}", providerUrl, tempFileName);

                using (var file = File.Create(tempFileName))
                {
                    credentialProviderDownloadStream.CopyTo(file);
                }

                var providerDestination = Environment.GetEnvironmentVariable("NUGET_CREDENTIALPROVIDERS_PATH");
                if (string.IsNullOrEmpty(providerDestination))
                {
                    providerDestination = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Nuget/CredentialProviders");
                }

                // Unzip the bundle and extract any credential provider exes
                using (var zip = ZipFile.OpenRead(tempFileName))
                {
                    foreach (var entry in zip.Entries)
                    {
                        if (Regex.IsMatch(entry.FullName, @"^credentialprovider.+\.exe$", RegexOptions.IgnoreCase))
                        {
                            NugetHelper.LogVerbose("Extracting {0} to {1}", entry.FullName, providerDestination);
                            var filePath = Path.Combine(providerDestination, entry.FullName);
                            var directory = Path.GetDirectoryName(filePath);
                            Directory.CreateDirectory(directory);

                            entry.ExtractToFile(filePath, true);
                            anyDownloaded = true;
                        }
                    }
                }

                // Delete the bundle
                File.Delete(tempFileName);
            }
            catch (Exception e)
            {
                Debug.LogErrorFormat("Failed to download credential provider from {0}: {1}", credentialProviderRequest.Address, e.Message);
            }
        }

        return anyDownloaded;
    }

    private struct AuthenticatedFeed
    {
        public string AccountUrlPattern;

        public string ProviderUrlTemplate;

        public string GetAccount(string url)
        {
            var match = Regex.Match(url, AccountUrlPattern, RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            return match.Groups["account"].Value;
        }

        public string GetProviderUrl(string account)
        {
            return ProviderUrlTemplate.Replace("{account}", account);
        }
    }

    /// <summary>
    ///     Data class returned from nuget credential providers in a JSON format. As described here:
    ///     https://docs.microsoft.com/en-us/nuget/reference/extensibility/nuget-exe-credential-providers#creating-a-nugetexe-credential-provider.
    /// </summary>
    [Serializable]
    private struct CredentialProviderResponse
    {
        public string Username;

        public string Password;
    }

    /// <summary>
    ///     Possible response codes returned by a Nuget credential provider as described here:
    ///     https://docs.microsoft.com/en-us/nuget/reference/extensibility/nuget-exe-credential-providers#creating-a-nugetexe-credential-provider.
    /// </summary>
    private enum CredentialProviderExitCode
    {
        Success = 0,

        ProviderNotApplicable = 1,

        Failure = 2,
    }
}
