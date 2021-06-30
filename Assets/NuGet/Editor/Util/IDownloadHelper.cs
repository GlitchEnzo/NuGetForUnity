using System.IO;
using UnityEngine;

namespace NuGet.Editor.Util
{
    public interface IDownloadHelper
    {
        /// <summary>
        /// Copies the contents of input to output. Doesn't close either stream.
        /// </summary>
        void CopyStream(Stream input, Stream output);

        /// <summary>
        /// Get the specified URL from the web. Throws exceptions if the request fails.
        /// </summary>
        /// <param name="url">URL that will be loaded.</param>
        /// <param name="password">Password that will be passed in the Authorization header or the request. If null, authorization is omitted.</param>
        /// <param name="timeOut">Timeout in milliseconds or null to use the default timeout values of HttpWebRequest.</param>
        /// <returns>Stream containing the result.</returns>
        Stream RequestUrl(string url, string userName, string password, int? timeOut);

        /// <summary>
        /// Downloads an image at the given URL and converts it to a Unity Texture2D.
        /// </summary>
        /// <param name="url">The URL of the image to download.</param>
        /// <returns>The image as a Unity Texture2D object.</returns>
        Texture2D DownloadImage(string url);
    }
}