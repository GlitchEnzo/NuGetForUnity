using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace NugetForUnity
{
    /// <summary>
    ///     Helper for NuGet package icon download handling.
    /// </summary>
    internal static class NuGetPackageTextureHelper
    {
        /// <summary>
        ///     Downloads an image at the given URL and converts it to a Unity Texture2D.
        /// </summary>
        /// <param name="url">The URL of the image to download.</param>
        /// <returns>The image as a Unity Texture2D object.</returns>
        internal static Task<Texture2D> DownloadImage(string url)
        {
            try
            {
                var fromCache = false;
                if (url.StartsWith("file://"))
                {
                    // we only cache images coming from a remote server.
                    fromCache = true;
                }
                else if (ExistsInDiskCache(url))
                {
                    url = "file:///" + GetFilePath(url);
                    fromCache = true;
                }

                var taskCompletionSource = new TaskCompletionSource<Texture2D>();
                var request = UnityWebRequest.Get(url);
                {
                    var downloadHandler = new DownloadHandlerTexture(false);

                    request.downloadHandler = downloadHandler;
                    request.timeout = 1; // 1 second
                    var operation = request.SendWebRequest();
                    operation.completed += asyncOperation =>
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(request.error))
                            {
#if UNITY_2020_1_OR_NEWER
                                NugetHelper.LogVerbose("Downloading image {0} failed! Web error: {1}, Handler error: {2}.", url, request.error, downloadHandler.error);
#else
                                NugetHelper.LogVerbose("Downloading image {0} failed! Web error: {1}.", url, request.error);
#endif

                                taskCompletionSource.TrySetResult(null);
                                return;
                            }

                            var result = downloadHandler.texture;

                            if (result != null && !fromCache)
                            {
                                CacheTextureOnDisk(url, downloadHandler.data);
                            }

                            taskCompletionSource.TrySetResult(result);
                        }
                        finally
                        {
                            request.Dispose();
                        }
                    };

                    return taskCompletionSource.Task;
                }
            }
            catch (Exception exception)
            {
                NugetHelper.LogVerbose("Error while downloading image from: '{0}' got error: {1}", url, exception);
                return Task.FromResult<Texture2D>(null);
            }
        }

        private static void CacheTextureOnDisk(string url, byte[] bytes)
        {
            var diskPath = GetFilePath(url);
            File.WriteAllBytes(diskPath, bytes);
        }

        private static bool ExistsInDiskCache(string url)
        {
            return File.Exists(GetFilePath(url));
        }

        private static string GetFilePath(string url)
        {
            return Path.Combine(Application.temporaryCachePath, GetHash(url));
        }

        private static string GetHash(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return null;
            }

            var md5 = new MD5CryptoServiceProvider();
            var data = md5.ComputeHash(Encoding.Default.GetBytes(s));
            var sBuilder = new StringBuilder();
            for (var i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            return sBuilder.ToString();
        }
    }
}
