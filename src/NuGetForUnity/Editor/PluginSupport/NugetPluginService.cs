using System;
using System.Collections.Generic;
using System.IO;
using NugetForUnity.Configuration;
using NugetForUnity.Helper;
using NugetForUnity.PluginAPI.Models;
using NugetForUnity.Ui;
using UnityEngine;

namespace NugetForUnity.PluginSupport
{
    /// <inheritdoc cref="NugetForUnity.PluginAPI.Models.INugetPluginService" />
    public sealed class NugetPluginService : INugetPluginService, IDisposable
    {
        private readonly List<Action<INuspecFile>> registeredNuspecCustomizers = new List<Action<INuspecFile>>();

        /// <inheritdoc />
        public string ProjectAssetsDir => UnityPathHelper.AbsoluteAssetsPath;

        /// <inheritdoc />
        public string PackageInstallDir => ConfigurationManager.NugetConfigFile.RepositoryPath;

        /// <inheritdoc />
        public void RegisterNuspecCustomizer(Action<INuspecFile> customizer)
        {
            registeredNuspecCustomizers.Add(customizer);
            NuspecFile.ProjectSpecificNuspecInitializer += customizer;
        }

        /// <inheritdoc />
        public void CreateNuspecAndOpenEditor(string destinationDirectory)
        {
            if (Path.IsPathRooted(destinationDirectory))
            {
                if (destinationDirectory.StartsWith(UnityPathHelper.AbsoluteAssetsPath))
                {
                    throw new Exception(
                        $"Given directory {destinationDirectory} isn't within project directory {UnityPathHelper.AbsoluteAssetsPath}");
                }
            }
            else
            {
                destinationDirectory = Path.Combine(UnityPathHelper.AbsoluteAssetsPath, destinationDirectory);
            }

            var lastChar = destinationDirectory[destinationDirectory.Length - 1];
            if (lastChar == '/' || lastChar == '\\')
            {
                destinationDirectory = destinationDirectory.Substring(0, destinationDirectory.Length - 1);
            }

            NuspecEditor.CreateNuspecFile(destinationDirectory);
        }

        /// <inheritdoc />
        public void LogError(string message)
        {
            Debug.LogError(message);
        }

        /// <inheritdoc />
        public void LogErrorFormat(string format, params object[] args)
        {
            Debug.LogErrorFormat(format, args);
        }

        /// <inheritdoc />
        public void LogVerbose(string format, params object[] args)
        {
            NugetLogger.LogVerbose(format, args);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            foreach (var registeredNuspecCustomizer in registeredNuspecCustomizers)
            {
                NuspecFile.ProjectSpecificNuspecInitializer -= registeredNuspecCustomizer;
            }

            registeredNuspecCustomizers.Clear();
        }
    }
}
