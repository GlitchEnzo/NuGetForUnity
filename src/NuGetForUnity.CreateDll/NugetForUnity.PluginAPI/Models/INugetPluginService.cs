namespace NugetForUnity.PluginAPI.Models
{
    /// <summary>
    /// Service methods that NugetForUnity provides to its plugins.
    /// </summary>
    public interface INugetPluginService
    {
        /// <summary>
        /// Gets the absolute path to the projects Assets directory.
        /// </summary>
        string ProjectAssetsDir { get; }

        /// <summary>
        /// Logs the given error message.
        /// </summary>
        /// <param name="message">Message to log.</param>
        void LogError(string message);

        /// <summary>
        /// Logs a formatted error message.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="args">Format arguments.</param>
        void LogErrorFormat(string format, params object[] args);

        /// <summary>
        /// Logs a formatted error message only if Verbose logging is enabled.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="args">Format arguments.</param>
        void LogVerbose(string format, params object[] args);
    }
}
