using System.IO.Compression;
using NugetForUnity.PluginAPI.Models;

namespace NugetForUnity.PluginAPI.ExtensionPoints
{
    /// <summary>
    /// Implement this interface to add additional handling of files being extracted from nupkg during installation.
    /// </summary>
    public interface IPackageInstallFileHandler
    {
        /// <summary>
        /// This will be called for each entry that is about to be processed from nupkg that is being installed.
        /// </summary>
        /// <param name="package">Package that is being installed.</param>
        /// <param name="entry">Zip entry that is about to be processed.</param>
        /// <param name="extractDirectory">The directory where the package is being installed.</param>
        /// <returns>True if this method handled the entry and doesn't want default handling to be executed, false otherwise.</returns>
        bool HandleFileExtraction(INugetPackage package, ZipArchiveEntry entry, string extractDirectory);
    }
}
