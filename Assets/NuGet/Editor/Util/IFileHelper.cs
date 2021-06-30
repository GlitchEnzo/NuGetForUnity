namespace NuGet.Editor.Util
{
    public interface IFileHelper
    {
        /// <summary>
        /// Recursively copies all files and sub-directories from one directory to another.
        /// </summary>
        /// <param name="sourceDirectoryPath">The filepath to the folder to copy from.</param>
        /// <param name="destDirectoryPath">The filepath to the folder to copy to.</param>
        void DirectoryCopy(string sourceDirectoryPath, string destDirectoryPath);

        /// <summary>
        /// Recursively deletes the folder at the given path.
        /// NOTE: Directory.Delete() doesn't delete Read-Only files, whereas this does.
        /// </summary>
        /// <param name="directoryPath">The path of the folder to delete.</param>
        void DeleteDirectory(string directoryPath);

        /// <summary>
        /// Deletes a file at the given filepath.
        /// </summary>
        /// <param name="filePath">The filepath to the file to delete.</param>
        void DeleteFile(string filePath);

        /// <summary>
        /// Deletes all files in the given directory or in any sub-directory, with the given extension.
        /// </summary>
        /// <param name="directoryPath">The path to the directory to delete all files of the given extension from.</param>
        /// <param name="extension">The extension of the files to delete, in the form "*.ext"</param>
        void DeleteAllFiles(string directoryPath, string extension);

        void CacheTextureOnDisk(string url, byte[] bytes);
        bool ExistsInDiskCache(string url);
        string GetFilePath(string url);
    }
}