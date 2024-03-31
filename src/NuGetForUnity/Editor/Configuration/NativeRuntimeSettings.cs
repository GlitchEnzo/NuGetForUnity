using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NugetForUnity.Configuration
{
    /// <summary>
    ///     Serializable configuration about how native runtime assets (.dll's) are installed.
    /// </summary>
    [Serializable]
    internal class NativeRuntimeSettings
    {
        [SerializeField]
        private List<NativeRuntimeAssetConfiguration> configurations;

        /// <summary>
        ///     Gets the native platform settingf for each NuGet runtimes folder name.
        /// </summary>
        internal List<NativeRuntimeAssetConfiguration> Configurations => configurations;

        /// <summary>
        ///     Loads the configuration from the file at <paramref name="filePath" /> if it exists.
        ///     If the file <paramref name="filePath" /> doesn't exist it is created and filled with <see cref="DefaultSettings" />.
        /// </summary>
        /// <param name="filePath">The path of the configuration file.</param>
        /// <returns>The configuration loaded from the file, or the default settings.</returns>
        internal static NativeRuntimeSettings LoadOrCreateDefault(string filePath)
        {
            if (!File.Exists(filePath))
            {
                var settings = DefaultSettings();
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                settings.Save(filePath);
                return settings;
            }

            var jsonContent = File.ReadAllText(filePath);
            return JsonUtility.FromJson<NativeRuntimeSettings>(jsonContent);
        }

        /// <summary>
        ///     Saves the current configuration in a file at <paramref name="filePath" />.
        /// </summary>
        /// <param name="filePath">The path to the file to write the configuration to.</param>
        internal void Save(string filePath)
        {
            // remove the read only flag on the file, if there is one.
            if (File.Exists(filePath))
            {
                var attributes = File.GetAttributes(filePath);
                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    attributes &= ~FileAttributes.ReadOnly;
                    File.SetAttributes(filePath, attributes);
                }
            }

            var jsonContent = JsonUtility.ToJson(this, true);
            File.WriteAllText(filePath, jsonContent);
        }

        private static NativeRuntimeSettings DefaultSettings()
        {
            return new NativeRuntimeSettings
            {
                configurations = new List<NativeRuntimeAssetConfiguration>
                {
                    new NativeRuntimeAssetConfiguration("win10-x64", "x86_64", "x86_64", "Windows", BuildTarget.StandaloneWindows64),
                    new NativeRuntimeAssetConfiguration("win10-x86", "AnyCPU", "AnyCPU", "Windows", BuildTarget.StandaloneWindows),
                    new NativeRuntimeAssetConfiguration("win7-x64", "x86_64", "x86_64", "Windows", BuildTarget.StandaloneWindows64),
                    new NativeRuntimeAssetConfiguration("win7-x86", "AnyCPU", "AnyCPU", "Windows", BuildTarget.StandaloneWindows),
                    new NativeRuntimeAssetConfiguration("win-x64", "x86_64", "x86_64", "Windows", BuildTarget.StandaloneWindows64),
                    new NativeRuntimeAssetConfiguration("win-x86", "AnyCPU", "AnyCPU", "Windows", BuildTarget.StandaloneWindows),
                    new NativeRuntimeAssetConfiguration("linux-x64", "x86_64", "x86_64", "Linux", BuildTarget.StandaloneLinux64),
                    new NativeRuntimeAssetConfiguration("osx-x64", "x86_64", "x86_64", "OSX", BuildTarget.StandaloneOSX),
                },
            };
        }
    }
}
