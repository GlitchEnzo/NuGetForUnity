using JetBrains.Annotations;
using NugetForUnity.Configuration;
using UnityEngine;
#if !UNITY_2019_1_OR_NEWER
using System.Threading;
#endif

namespace NugetForUnity
{
    /// <summary>
    ///     A logger for the NugetForUnity package.
    /// </summary>
    public static class NugetLogger
    {
        /// <summary>
        ///     Outputs the given message to the log only if verbose mode is active.  Otherwise it does nothing.
        /// </summary>
        /// <param name="format">The formatted message string.</param>
        /// <param name="args">The arguments for the formatted message string.</param>
        public static void LogVerbose([NotNull] string format, [CanBeNull] [ItemCanBeNull] params object[] args)
        {
            if (!ConfigurationManager.IsVerboseLoggingEnabled)
            {
                return;
            }

#if UNITY_2019_1_OR_NEWER
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, format, args);
#else

            // application state changes need to run on main thread
            var isMainThread = !Thread.CurrentThread.IsThreadPoolThread;
            StackTraceLogType stackTraceLogType = default;
            if (isMainThread)
            {
                stackTraceLogType = Application.GetStackTraceLogType(LogType.Log);
                Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            }

            Debug.LogFormat(format, args);
            if (isMainThread)
            {
                Application.SetStackTraceLogType(LogType.Log, stackTraceLogType);
            }
#endif
        }
    }
}
