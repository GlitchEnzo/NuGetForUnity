using System.Collections.Generic;
using UnityEditor;

namespace NugetForUnity
{
    public static class UnityProjectHelper
    {
        // TODO find compatible targets
        private static readonly Dictionary<int, string> _tfmMap = new Dictionary<int, string>
        {
            { 1, "net20" },
            { 3, "net46" },
            { 6, "netstandard2.0" }
        };

        /// <summary>
        /// Get target framework moniker (TFM) related to unity project api compatible level
        /// </summary>
        /// <param name="apiCompatibilityLevel"></param>
        /// <returns>TFM</returns>
        /// <see cref="https://docs.microsoft.com/en-us/nuget/reference/target-frameworks"/>
        /// <see cref="https://docs.unity3d.com/ScriptReference/ApiCompatibilityLevel.html"/>
        public static string GetTargetFramework(ApiCompatibilityLevel apiCompatibilityLevel)
        {
            string tfm;
            if (!_tfmMap.TryGetValue((int) apiCompatibilityLevel, out tfm)) // use the int value to ensure it works in earlier versions of Unity
            {
                tfm = "unity_custom";
            }

            return tfm;
        }
    }
}