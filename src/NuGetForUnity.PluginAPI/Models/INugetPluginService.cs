using System;

namespace NugetForUnity.PluginAPI.Models
{
    /// <summary>
    ///     Service methods that NugetForUnity provides to its plugins.
    /// </summary>
    public interface INugetPluginService
    {
        /// <summary>
        ///     Gets the absolute path to the projects Assets directory.
        /// </summary>
        string ProjectAssetsDir { get; }

        /// <summary>
        ///     Gets the absolute path to the directory where packages are installed.
        /// </summary>
        string PackageInstallDir { get; }

        /// <summary>
        ///     Allows plugin to register a function that will modify the contents of default new nuspec file.
        /// </summary>
        /// <param name="customizer">The function that will receive default nuspec file and modify it.</param>
        void RegisterNuspecCustomizer(Action<INuspecFile> customizer);

        /// <summary>
        ///     Allows plugin to create a new nuspec file on the given location.
        /// </summary>
        /// <param name="destinationDirectory">Either the absolute path within project to an existing directory or path relative to project's Asset folder.</param>
        void CreateNuspecAndOpenEditor(string destinationDirectory);

        /// <summary>
        ///     Logs the given error message.
        /// </summary>
        /// <param name="message">Message to log.</param>
        void LogError(string message);

        /// <summary>
        ///     Logs a formatted error message.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="args">Format arguments.</param>
        void LogErrorFormat(string format, params object[] args);

        /// <summary>
        ///     Logs a formatted error message only if Verbose logging is enabled.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="args">Format arguments.</param>
        void LogVerbose(string format, params object[] args);
    }
}
