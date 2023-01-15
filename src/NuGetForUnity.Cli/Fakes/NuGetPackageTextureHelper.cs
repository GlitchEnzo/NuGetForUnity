#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using UnityEngine;

namespace NugetForUnity
{
    internal static class NuGetPackageTextureHelper
    {
        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Conform with real implementation.")]
        internal static Task<Texture2D>? DownloadImage(string url)
        {
            return null;
        }
    }
}
