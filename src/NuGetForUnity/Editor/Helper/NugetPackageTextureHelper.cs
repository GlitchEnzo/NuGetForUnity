#pragma warning disable SA1512,SA1124 // Single-line comments should not be followed by blank line

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_2022_1_OR_NEWER
using UnityEditor;
#endif

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
    ///     Helper for NuGet package icon download handling.
    /// </summary>
    internal static class NugetPackageTextureHelper
    {
        /// <summary>
        ///     Downloads an image at the given URL and converts it to a Unity Texture2D.
        /// </summary>
        /// <param name="url">The URL of the image to download.</param>
        /// <returns>The image as a Unity Texture2D object.</returns>
        [ItemCanBeNull]
        internal static Task<Texture2D> DownloadImageAsync([NotNull] string url)
        {
            try
            {
#if UNITY_2022_1_OR_NEWER
                if (PlayerSettings.insecureHttpOption == InsecureHttpOption.NotAllowed &&
                    url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    // if insecure http url is not allowed try to use https.
                    url = url.Replace("http://", "https://", StringComparison.OrdinalIgnoreCase);
                }
#endif

                var fromCache = false;
                if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
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
                    operation.completed += _ =>
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(request.error))
                            {
#if UNITY_2020_1_OR_NEWER
                                NugetLogger.LogVerbose(
                                    "Downloading image {0} failed! Web error: {1}, Handler error: {2}.",
                                    url,
                                    request.error,
                                    downloadHandler.error);
#else
                                NugetLogger.LogVerbose("Downloading image {0} failed! Web error: {1}.", url, request.error);
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
                NugetLogger.LogVerbose("Error while downloading image from: '{0}' got error: {1}", url, exception);
                return Task.FromResult<Texture2D>(null);
            }
        }

        private static void CacheTextureOnDisk([NotNull] string url, [NotNull] byte[] bytes)
        {
            var diskPath = GetFilePath(url);
            File.WriteAllBytes(diskPath, bytes);
        }

        private static bool ExistsInDiskCache([NotNull] string url)
        {
            return File.Exists(GetFilePath(url));
        }

        [NotNull]
        private static string GetFilePath([NotNull] string url)
        {
            return Path.Combine(Application.temporaryCachePath, GetHash(url));
        }

        [SuppressMessage("Design", "CA5351", Justification = "Only use MD5 hash as cache key / not securty relevant.")]
        [NotNull]
        private static string GetHash([NotNull] string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                throw new ArgumentNullException(nameof(s));
            }

            using (var md5 = new MD5CryptoServiceProvider())
            {
                var data = md5.ComputeHash(Encoding.Default.GetBytes(s));
                return Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_').Replace("=", null);
            }
        }
    }
}
