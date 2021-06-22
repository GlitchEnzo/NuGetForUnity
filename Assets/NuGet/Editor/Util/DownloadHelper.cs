using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using UnityEngine;

namespace NuGet.Editor.Util
{
    public class DownloadHelper : IDownloadHelper
    {
        private IFileHelper fileHelper;

        public DownloadHelper(IFileHelper fileHelper)
        {
            this.fileHelper = fileHelper;
        }

        /// <summary>
        /// Copies the contents of input to output. Doesn't close either stream.
        /// </summary>
        public void CopyStream(Stream input, Stream output)
        {
            byte[] buffer = new byte[8 * 1024];
            int len;
            while ((len = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, len);
            }
        }

        /// <summary>
        /// Get the specified URL from the web. Throws exceptions if the request fails.
        /// </summary>
        /// <param name="url">URL that will be loaded.</param>
        /// <param name="password">Password that will be passed in the Authorization header or the request. If null, authorization is omitted.</param>
        /// <param name="timeOut">Timeout in milliseconds or null to use the default timeout values of HttpWebRequest.</param>
        /// <returns>Stream containing the result.</returns>
        public Stream RequestUrl(string url, string userName, string password, int? timeOut)
        {
            HttpWebRequest getRequest = (HttpWebRequest)WebRequest.Create(url);
            if (timeOut.HasValue)
            {
                getRequest.Timeout = timeOut.Value;
                getRequest.ReadWriteTimeout = timeOut.Value;
            }

            if (string.IsNullOrEmpty(password))
            {
                CredentialProviderResponse? creds = NugetHelper.GetCredentialFromProvider(NugetHelper.GetTruncatedFeedUri(getRequest.RequestUri));
                if (creds.HasValue)
                {
                    userName = creds.Value.Username;
                    password = creds.Value.Password;
                }
            }

            if (password != null)
            {
                // Send password as described by https://docs.microsoft.com/en-us/vsts/integrate/get-started/rest/basics.
                // This works with Visual Studio Team Services, but hasn't been tested with other authentication schemes so there may be additional work needed if there
                // are different kinds of authentication.
                getRequest.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", userName, password))));
            }

            NugetHelper.LogVerbose("HTTP GET {0}", url);
            Stream objStream = getRequest.GetResponse().GetResponseStream();
            return objStream;
        }

        /// <summary>
        /// Downloads an image at the given URL and converts it to a Unity Texture2D.
        /// </summary>
        /// <param name="url">The URL of the image to download.</param>
        /// <returns>The image as a Unity Texture2D object.</returns>
        public Texture2D DownloadImage(string url)
        {
            bool timedout = false;
            uint timeout = 750U;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            bool fromCache = false;
            if (fileHelper.ExistsInDiskCache(url))
            {
                url = "file:///" + fileHelper.GetFilePath(url);
                fromCache = true;
            }

            WWW request = new WWW(url);
            while (!request.isDone)
            {
                if (stopwatch.ElapsedMilliseconds >= timeout)
                {
                    request.Dispose();
                    timedout = true;
                    break;
                }
            }

            Texture2D result = null;

            if (timedout)
            {
                NugetHelper.LogVerbose($"Downloading image {url} timed out! Took more than {timeout}ms.");
            }
            else
            {
                if (string.IsNullOrEmpty(request.error))
                {
                    result = request.textureNonReadable;
                    NugetHelper.LogVerbose("Downloading image {0} took {1} ms", url, stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    NugetHelper.LogVerbose("Request error: " + request.error);
                }
            }


            if (result != null && !fromCache)
            {
                fileHelper.CacheTextureOnDisk(url, request.bytes);
            }

            request.Dispose();
            return result;
        }
    }
}