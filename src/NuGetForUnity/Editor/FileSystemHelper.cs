using System;
using System.IO;
using UnityEngine;

namespace NugetForUnity
{
    /// <summary>
    ///     Helper class for file system operations.
    /// </summary>
    internal static class FileSystemHelper
    {
        /// <summary>
        ///     Recursively moves all files and sub-directories from one directory to another.
        ///     If a file or directory already exists inside the target directory, it will be overwritten.
        /// </summary>
        /// <param name="sourceDirectoryPath">The path to the folder to move from.</param>
        /// <param name="destDirectoryPath">The path to the folder to move to.</param>
        internal static void DirectoryMove(string sourceDirectoryPath, string destDirectoryPath)
        {
            if (!Directory.Exists(destDirectoryPath))
            {
                // target doesn't exist so we can simply move the folder
                NugetLogger.LogVerbose("Moving {0} to {1}", sourceDirectoryPath, destDirectoryPath);
                Directory.Move(sourceDirectoryPath, destDirectoryPath);
                return;
            }

            // move the files
            var files = Directory.GetFiles(sourceDirectoryPath);
            foreach (var file in files)
            {
                var newFilePath = Path.Combine(destDirectoryPath, Path.GetFileName(file));

                try
                {
                    NugetLogger.LogVerbose("Moving {0} to {1}", file, newFilePath);
                    DeleteFile(newFilePath);
                    File.Move(file, newFilePath);
                }
                catch (Exception e)
                {
                    Debug.LogWarningFormat("'{0}' couldn't be moved to '{1}'. \n{2}", file, newFilePath, e);
                }
            }

            // move the directories
            var directories = Directory.GetDirectories(sourceDirectoryPath);
            foreach (var directory in directories)
            {
                var newDirectoryPath = Path.Combine(destDirectoryPath, Path.GetFileName(directory));

                try
                {
                    NugetLogger.LogVerbose("Moving {0} to {1}", directory, newDirectoryPath);
                    if (Directory.Exists(newDirectoryPath))
                    {
                        DeleteDirectory(newDirectoryPath, false);
                    }

                    Directory.Move(directory, newDirectoryPath);
                }
                catch (Exception e)
                {
                    Debug.LogWarningFormat("'{0}' couldn't be moved to '{1}'. \n{2}", directory, newDirectoryPath, e);
                }
            }
        }

        /// <summary>
        ///     Recursively copies all files and sub-directories from one directory to another.
        /// </summary>
        /// <param name="sourceDirectoryPath">The path to the folder to copy from.</param>
        /// <param name="destDirectoryPath">The path to the folder to copy to.</param>
        internal static void DirectoryCopy(string sourceDirectoryPath, string destDirectoryPath)
        {
            var sourceDirectory = new DirectoryInfo(sourceDirectoryPath);
            if (!sourceDirectory.Exists)
            {
                throw new DirectoryNotFoundException("Source directory does not exist or could not be found: " + sourceDirectoryPath);
            }

            // if the destination directory doesn't exist, create it
            if (!Directory.Exists(destDirectoryPath))
            {
                NugetLogger.LogVerbose("Creating new directory: {0}", destDirectoryPath);
                Directory.CreateDirectory(destDirectoryPath);
            }

            // get the files in the directory and copy them to the new location
            var files = sourceDirectory.GetFiles();
            foreach (var file in files)
            {
                var newFilePath = Path.Combine(destDirectoryPath, file.Name);

                try
                {
                    NugetLogger.LogVerbose("Moving {0} to {1}", file.ToString(), newFilePath);
                    file.CopyTo(newFilePath, true);
                }
                catch (Exception e)
                {
                    Debug.LogWarningFormat(
                        "{0} couldn't be moved to {1}. It may be a native plugin already locked by Unity. Please trying closing Unity and manually moving it. \n{2}",
                        file,
                        newFilePath,
                        e);
                }
            }

            // copy sub-directories and their contents to new location
            var directories = sourceDirectory.GetDirectories();
            foreach (var subdir in directories)
            {
                var temppath = Path.Combine(destDirectoryPath, subdir.Name);
                DirectoryCopy(subdir.FullName, temppath);
            }
        }

        /// <summary>
        ///     Recursively deletes the folder at the given path.
        ///     NOTE: Directory.Delete() doesn't delete Read-Only files, whereas this does.
        /// </summary>
        /// <param name="directoryPath">The path of the folder to delete.</param>
        /// <param name="log">Whether to log the deletion.</param>
        internal static void DeleteDirectory(string directoryPath, bool log)
        {
            if (!Directory.Exists(directoryPath))
            {
                return;
            }

            if (log)
            {
                NugetLogger.LogVerbose("Deleting directory: {0}", directoryPath);
            }

            var directoryInfo = new DirectoryInfo(directoryPath);

            // delete any sub-folders first
            foreach (var childInfo in directoryInfo.GetFileSystemInfos())
            {
                DeleteDirectory(childInfo.FullName, false);
            }

            // remove the read-only flag on all files
            var files = directoryInfo.GetFiles();
            foreach (var file in files)
            {
                file.Attributes = FileAttributes.Normal;
            }

            // remove the read-only flag on the directory
            directoryInfo.Attributes = FileAttributes.Normal;

            // recursively delete the directory
            directoryInfo.Delete(true);
        }

        /// <summary>
        ///     Deletes a file at the given file-path.
        /// </summary>
        /// <param name="filePath">The file-path to the file to delete.</param>
        internal static void DeleteFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            File.SetAttributes(filePath, FileAttributes.Normal);
            File.Delete(filePath);
        }

        /// <summary>
        ///     Moves a file from one location to another. If the destination file already exists, it is overwritten.
        /// </summary>
        /// <param name="sourceFilePath">The path of the file that should be moved.</param>
        /// <param name="destFilePath">The target path where the file should be moved to.</param>
        /// <param name="checkSourceExists">Whether we should check if there is a file located at <paramref name="sourceFilePath" />.</param>
        internal static void MoveFile(string sourceFilePath, string destFilePath, bool checkSourceExists)
        {
            if (checkSourceExists && !File.Exists(sourceFilePath))
            {
                throw new FileNotFoundException($"Source file does not exist or could not be found: {sourceFilePath}");
            }

            NugetLogger.LogVerbose("Moving {0} to {1}", sourceFilePath, destFilePath);
            DeleteFile(destFilePath);
            File.Move(sourceFilePath, destFilePath);
        }

        /// <summary>
        ///     Deletes all files in the given directory and in any sub-directory, with the given filter.
        /// </summary>
        /// <param name="directoryPath">The path to the directory to delete all files of the given extension from.</param>
        /// <param name="filter">The filter of the files to delete, in the form "*.ext".</param>
        internal static void DeleteAllFiles(string directoryPath, string filter)
        {
            var files = Directory.GetFiles(directoryPath, filter, SearchOption.AllDirectories);
            foreach (var file in files)
            {
                DeleteFile(file);
            }
        }

        /// <summary>
        ///     Replace all %20 encodings with a normal space.
        /// </summary>
        /// <param name="directoryPath">The path to the directory.</param>
        internal static void FixSpaces(string directoryPath)
        {
            if (directoryPath.Contains("%20"))
            {
                NugetLogger.LogVerbose("Removing %20 from {0}", directoryPath);
                Directory.Move(directoryPath, directoryPath.Replace("%20", " "));
                directoryPath = directoryPath.Replace("%20", " ");
            }

            var subdirectories = Directory.GetDirectories(directoryPath);
            foreach (var subDir in subdirectories)
            {
                FixSpaces(subDir);
            }

            var files = Directory.GetFiles(directoryPath);
            foreach (var file in files)
            {
                if (file.Contains("%20"))
                {
                    NugetLogger.LogVerbose("Removing %20 from {0}", file);
                    File.Move(file, file.Replace("%20", " "));
                }
            }
        }
    }
}
