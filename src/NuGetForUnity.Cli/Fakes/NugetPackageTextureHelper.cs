#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using UnityEngine;

namespace NugetForUnity.Helper
{
    internal static class NugetPackageTextureHelper
    {
        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Conform with real implementation.")]
        internal static Task<Texture2D?> DownloadImageAsync(string url)
        {
            return Task.FromResult<Texture2D?>(null);
        }
    }
}
