using System;
using System.IO;
using UnityEngine;

namespace NuGet.Editor.Util
{
    public class NugetFileHelper
    {
        /// <summary>
        /// Recursively copies all files and sub-directories from one directory to another.
        /// </summary>
        /// <param name="sourceDirectoryPath">The filepath to the folder to copy from.</param>
        /// <param name="destDirectoryPath">The filepath to the folder to copy to.</param>
        internal void DirectoryCopy(string sourceDirectoryPath, string destDirectoryPath)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDirectoryPath);
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirectoryPath);
            }

            // if the destination directory doesn't exist, create it
            if (!Directory.Exists(destDirectoryPath))
            {
                NugetHelper.LogVerbose("Creating new directory: {0}", destDirectoryPath);
                Directory.CreateDirectory(destDirectoryPath);
            }

            // get the files in the directory and copy them to the new location
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string newFilePath = Path.Combine(destDirectoryPath, file.Name);

                try
                {
                    NugetHelper.LogVerbose("Moving {0} to {1}", file.ToString(), newFilePath);
                    file.CopyTo(newFilePath, true);
                }
                catch (Exception e)
                {
                    Debug.LogWarningFormat("{0} couldn't be moved to {1}. It may be a native plugin already locked by Unity. Please trying closing Unity and manually moving it. \n{2}", file.ToString(), newFilePath, e.ToString());
                }
            }

            // copy sub-directories and their contents to new location
            DirectoryInfo[] dirs = dir.GetDirectories();
            foreach (DirectoryInfo subdir in dirs)
            {
                string temppath = Path.Combine(destDirectoryPath, subdir.Name);
                DirectoryCopy(subdir.FullName, temppath);
            }
        }

        /// <summary>
        /// Recursively deletes the folder at the given path.
        /// NOTE: Directory.Delete() doesn't delete Read-Only files, whereas this does.
        /// </summary>
        /// <param name="directoryPath">The path of the folder to delete.</param>
        internal void DeleteDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                return;
            }

            DirectoryInfo directoryInfo = new DirectoryInfo(directoryPath);

            // delete any sub-folders first
            foreach (FileSystemInfo childInfo in directoryInfo.GetFileSystemInfos())
            {
                DeleteDirectory(childInfo.FullName);
            }

            // remove the read-only flag on all files
            FileInfo[] files = directoryInfo.GetFiles();
            foreach (FileInfo file in files)
            {
                file.Attributes = FileAttributes.Normal;
            }

            // remove the read-only flag on the directory
            directoryInfo.Attributes = FileAttributes.Normal;

            // recursively delete the directory
            directoryInfo.Delete(true);
        }

        /// <summary>
        /// Deletes a file at the given filepath.
        /// </summary>
        /// <param name="filePath">The filepath to the file to delete.</param>
        internal void DeleteFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.SetAttributes(filePath, FileAttributes.Normal);
                File.Delete(filePath);
            }
        }

        /// <summary>
        /// Deletes all files in the given directory or in any sub-directory, with the given extension.
        /// </summary>
        /// <param name="directoryPath">The path to the directory to delete all files of the given extension from.</param>
        /// <param name="extension">The extension of the files to delete, in the form "*.ext"</param>
        internal void DeleteAllFiles(string directoryPath, string extension)
        {
            string[] files = Directory.GetFiles(directoryPath, extension, SearchOption.AllDirectories);
            foreach (string file in files)
            {
                DeleteFile(file);
            }
        }
    }
}