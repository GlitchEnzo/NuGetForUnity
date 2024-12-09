#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using UnityEditor;

namespace UnityEngine
{
    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Conform with Unity naming.")]
    internal static class Application
    {
        [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Conform with Unity naming.")]
        public static string dataPath { get; private set; } = null!;

        [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Conform with Unity naming.")]
        public static string unityVersion { get; private set; } = null!;

        public static ApiCompatibilityLevel ApiCompatibilityLevel { get; private set; }

        public static RuntimePlatform platform
        {
            get
            {
                return Environment.OSVersion.Platform switch
                {
                    PlatformID.MacOSX => RuntimePlatform.OSXEditor,
                    PlatformID.Unix => RuntimePlatform.LinuxEditor,
                    _ => RuntimePlatform.WindowsEditor,
                };
            }
        }

        internal static StackTraceLogType GetStackTraceLogType(LogType log)
        {
            return StackTraceLogType.None;
        }

        internal static void SetStackTraceLogType(LogType log, StackTraceLogType none)
        {
            // do nothing
        }

        internal static void SetUnityProjectPath(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath))
            {
                throw new InvalidOperationException("Unity Project Path is null or empty.");
            }

            projectPath = Path.GetFullPath(projectPath);
            if (!Directory.Exists(projectPath))
            {
                throw new InvalidOperationException($"The specified Unity Project Path '{projectPath}' does not exists.");
            }

            dataPath = Path.Combine(projectPath, "Assets");

            unityVersion = ReadUnityVersionNumber(projectPath);

            ApiCompatibilityLevel = (ApiCompatibilityLevel)ReadApiCompatibilityLevel(projectPath);
        }

        private static string ReadUnityVersionNumber(string projectPath)
        {
            const string editorVersionConfigName = "m_EditorVersion:";
            var projectVersionFilePath = Path.Combine(projectPath, "ProjectSettings/ProjectVersion.txt");
            var versionFileLines = File.ReadLines(projectVersionFilePath);
            foreach (var line in versionFileLines)
            {
                var trimmedLine = line.AsSpan().TrimStart();
                if (trimmedLine.StartsWith(editorVersionConfigName, StringComparison.OrdinalIgnoreCase))
                {
                    return trimmedLine[editorVersionConfigName.Length..].Trim().ToString();
                }
            }

            throw new InvalidOperationException($"Can't find project version ('{editorVersionConfigName}') inside file: '{projectVersionFilePath}'.");
        }

        private static int ReadApiCompatibilityLevel(string projectPath)
        {
            const string apiCompatibilityLevelConfigName = "apiCompatibilityLevel:";
            const string apiCompatibilityLevelPerPlatformConfigName = "apiCompatibilityLevelPerPlatform:";
            var projectSettingsFilePath = Path.Combine(projectPath, "ProjectSettings/ProjectSettings.asset");
            var settingsLines = File.ReadLines(projectSettingsFilePath);
            var isInsidePerPlatformConfigSection = false;
            var perPlatformConfigSectionIndentionCount = 0;
            var perPlatformConfig = new Dictionary<string, int>();
            int? generalApiCompatibilityLevel = null;

            foreach (var line in settingsLines)
            {
                var trimmedLine = line.AsSpan().TrimStart();
                if (trimmedLine.StartsWith(apiCompatibilityLevelConfigName, StringComparison.OrdinalIgnoreCase))
                {
                    var stringValue = trimmedLine[apiCompatibilityLevelConfigName.Length..].Trim();
                    if (int.TryParse(stringValue, out var value))
                    {
                        generalApiCompatibilityLevel = value;
                    }
                }

                if (trimmedLine.StartsWith(apiCompatibilityLevelPerPlatformConfigName, StringComparison.OrdinalIgnoreCase))
                {
                    var restOfLine = trimmedLine[apiCompatibilityLevelPerPlatformConfigName.Length..].Trim();
                    isInsidePerPlatformConfigSection = !restOfLine.StartsWith("{", StringComparison.Ordinal);
                    perPlatformConfigSectionIndentionCount = line.Length - trimmedLine.Length;
                }

                if (isInsidePerPlatformConfigSection && line.Length - trimmedLine.Length > perPlatformConfigSectionIndentionCount)
                {
                    var splitIndex = trimmedLine.IndexOf(':');
                    if (splitIndex >= 0 && int.TryParse(trimmedLine[(splitIndex + 1)..], out var value))
                    {
                        perPlatformConfig.Add(trimmedLine[..splitIndex].ToString(), value);
                    }
                }
                else
                {
                    isInsidePerPlatformConfigSection = false;
                }
            }

            // If there are different settings for different platforms we can only
            // try to use a compatibility setting so that the NuGet packages we restore are compatible with.
            // We use max as it has a higher success chance because .net standard 2 (enum = 6) is compatible with .net framework 4.6 (enum = 2).
            // Until now this was never a issue as .net standard packages work in all configurations
            // and using different 'ApiCompatibilityLevel' for different platforms is a very rare configuration.
            if (perPlatformConfig.Count > 0)
            {
                return perPlatformConfig.Values.Max();
            }

            if (generalApiCompatibilityLevel.HasValue)
            {
                return generalApiCompatibilityLevel.Value;
            }

            throw new InvalidOperationException($"Can't extract {nameof(ApiCompatibilityLevel)} from '{projectSettingsFilePath}'.");
        }
    }
}
