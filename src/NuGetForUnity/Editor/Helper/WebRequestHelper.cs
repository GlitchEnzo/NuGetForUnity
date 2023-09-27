using System;
using System.IO;
using System.Net;
using System.Text;
using JetBrains.Annotations;
using NugetForUnity.Configuration;
using UnityEngine;

namespace NugetForUnity.Helper
{
    /// <summary>
    ///     Helper class for making web requests using <see cref="WebRequest" />.
    /// </summary>
    internal static class WebRequestHelper
    {
        /// <summary>
        ///     Get the specified URL from the web. Throws exceptions if the request fails.
        /// </summary>
        /// <param name="url">URL that will be loaded.</param>
        /// <param name="userName">UserName that will be passed in the Authorization header or the request. If null, authorization is omitted.</param>
        /// <param name="password">Password that will be passed in the Authorization header or the request. If null, authorization is omitted.</param>
        /// <param name="timeOut">Timeout in milliseconds or null to use the default timeout values of HttpWebRequest.</param>
        /// <returns>Stream containing the result.</returns>
        [NotNull]
        internal static Stream RequestUrl([NotNull] string url, [CanBeNull] string userName, [CanBeNull] string password, int? timeOut)
        {
            // Mono doesn't have a Certificate Authority, so we have to provide all validation manually. Currently just accept anything.
            // See here: http://stackoverflow.com/questions/4926676/mono-webrequest-fails-with-https
            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            try
            {
#pragma warning disable SYSLIB0014 // Type or member is obsolete
                var getRequest = (HttpWebRequest)WebRequest.Create(url);
#pragma warning restore SYSLIB0014 // Type or member is obsolete
                getRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.None;
                getRequest.Timeout = timeOut ?? ConfigurationManager.NugetConfigFile.RequestTimeoutSeconds * 1000;

                if (string.IsNullOrEmpty(password))
                {
                    var credentials = CredentialProviderHelper.GetCredentialFromProvider(getRequest.RequestUri);
                    if (credentials.HasValue)
                    {
                        userName = credentials.Value.UserName;
                        password = credentials.Value.Password;
                    }
                }

                if (password != null)
                {
                    // Send password as described by https://docs.microsoft.com/en-us/vsts/integrate/get-started/rest/basics.
                    // This works with Visual Studio Team Services, but hasn't been tested with other authentication schemes so there may be additional work needed if there
                    // are different kinds of authentication.
                    getRequest.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes($"{userName}:{password}")));
                }

                NugetLogger.LogVerbose("HTTP GET {0}", url);
                var objStream = getRequest.GetResponse().GetResponseStream();
                return objStream ?? throw new InvalidOperationException("Response stream is null.");
            }
            catch (WebException webException)
            {
                WarnIfDotNetAuthenticationIssue(webException);
                throw;
            }
        }

        /// <summary>
        ///     Checks if the exception is due to a known issue with .NET 3.5 and authentication and if so a custom warning is created..
        /// </summary>
        /// <param name="webException">The exception to check.</param>
        private static void WarnIfDotNetAuthenticationIssue([NotNull] WebException webException)
        {
            if (webException.Response is HttpWebResponse webResponse &&
                webResponse.StatusCode == HttpStatusCode.BadRequest &&
                webException.Message.IndexOf(
                    "Authentication information is not given in the correct format",
                    StringComparison.OrdinalIgnoreCase) >=
                0)
            {
                // This error occurs when downloading a package with authentication using .NET 3.5, but seems to be fixed by the new .NET 4.6 runtime.
                // Inform users when this occurs.
                Debug.LogError(
                    "Authentication failed. This can occur due to a known issue in .NET 3.5. This can be fixed by changing Scripting Runtime to Experimental (.NET 4.6 Equivalent) in Player Settings.");
            }
        }
    }
}
