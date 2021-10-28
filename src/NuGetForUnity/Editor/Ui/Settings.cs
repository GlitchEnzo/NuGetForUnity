using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace NugetForUnity
{
    public class Settings
    {
        /// <summary>
        /// Gets or sets the mapping from NuGet runtimes folder naming convention to Unity BuildTargets
        /// </summary>
        public Dictionary<string, List<BuildTarget>> NativeRuntimesMappings { get; set; }

        public void Save(string filepath)
        {
            XDocument configFile = new XDocument();

            XElement nativeRuntimesMapping = new XElement("nativeRuntimesMapping");
            foreach (var mapping in NativeRuntimesMappings)
            {
                var platform = new XElement("platform");
                platform.Add(new XAttribute("name", mapping.Key));

                foreach (var target in mapping.Value)
                {
                    var buildTarget = new XElement("buildTarget");
                    buildTarget.Add(new XAttribute("name", target.ToString()));
                    platform.Add(buildTarget);
                }

                nativeRuntimesMapping.Add(platform);
            }

            XElement settings = new XElement("settings");
            settings.Add(nativeRuntimesMapping);
            configFile.Add(settings);

            bool fileExists = File.Exists(filepath);
            // remove the read only flag on the file, if there is one.
            if (fileExists)
            {
                FileAttributes attributes = File.GetAttributes(filepath);
                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    attributes &= ~FileAttributes.ReadOnly;
                    File.SetAttributes(filepath, attributes);
                }
            }

            configFile.Save(filepath);
        }

        public static Settings Load(string filepath)
        {
            Settings settingsFile = DefaultSettings();
            XDocument file = XDocument.Load(filepath);

            XElement nativeRuntimesMapping = file.Root.Element("nativeRuntimesMapping");
            if (nativeRuntimesMapping != null)
            {
                settingsFile.NativeRuntimesMappings = new Dictionary<string, List<BuildTarget>>();
                var platforms = nativeRuntimesMapping.Elements("platform");
                foreach (var platform in platforms)
                {
                    var platformName = platform.Attribute("name").Value;
                    var buildTargets = new List<BuildTarget>();
                    var targets = platform.Elements("buildTarget");

                    foreach (var target in targets)
                    {
                        var targetName = target.Attribute("name").Value;
                        BuildTarget parsedTarget;
                        if (BuildTarget.TryParse(targetName, true, out parsedTarget))
                        {
                            buildTargets.Add(parsedTarget);
                        }
                        else
                        {
                            Debug.LogWarning(string.Format("{0} of {1} not found", targetName, platformName));
                        }
                    }

                    settingsFile.NativeRuntimesMappings.Add(platformName, buildTargets);
                }
            }

            return settingsFile;
        }

        public static Settings CreateDefault(string filepath)
        {
            var settings = DefaultSettings();
            settings.Save(filepath);

            return settings;
        }

        private static Settings DefaultSettings()
        {
            var settings = new Settings();

            var nativeRuntimes = new Dictionary<string, List<BuildTarget>>
            {
                { "win7-x64", new List<BuildTarget>() { BuildTarget.StandaloneWindows64 } },
                { "win7-x86", new List<BuildTarget>() { BuildTarget.StandaloneWindows } },
                { "win-x64", new List<BuildTarget>() { BuildTarget.StandaloneWindows64 } },
                { "win-x86", new List<BuildTarget>() { BuildTarget.StandaloneWindows } },
                { "linux-x64", new List<BuildTarget>() { BuildTarget.StandaloneLinux64 } },
                { "osx-x64", new List<BuildTarget>() { BuildTarget.StandaloneOSX } }
            };

            settings.NativeRuntimesMappings = nativeRuntimes;

            return settings;
        }
    }
}