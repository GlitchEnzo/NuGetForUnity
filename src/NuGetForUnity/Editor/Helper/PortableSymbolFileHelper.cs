using System.IO;

namespace NugetForUnity.Helper
{
    /// <summary>
    ///     Provides methods for working with symbol files.
    /// </summary>
    internal static class PortableSymbolFileHelper
    {
        /// <summary>
        ///     Determines whether the specified PDB file is a portable symbol file.
        /// </summary>
        /// <param name="filePath">The path to the PDB file.</param>
        /// <returns>
        ///     <c>true</c> if the specified PDB file is a portable symbol file; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsPortableSymbolFile(string filePath)
        {
            using (var stream = File.OpenRead(filePath))
            {
                return stream.ReadByte() == 'B' && stream.ReadByte() == 'S' && stream.ReadByte() == 'J' && stream.ReadByte() == 'B';
            }
        }
    }
}
