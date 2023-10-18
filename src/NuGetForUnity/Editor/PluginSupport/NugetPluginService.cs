using NugetForUnity.Helper;
using NugetForUnity.PluginAPI.Models;
using UnityEngine;

namespace NugetForUnity.PluginSupport
{
    /// <inheritdoc />
    public sealed class NugetPluginService : INugetPluginService
    {
        /// <inheritdoc />
        public string ProjectAssetsDir => UnityPathHelper.AbsoluteAssetsPath;

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
    }
}
