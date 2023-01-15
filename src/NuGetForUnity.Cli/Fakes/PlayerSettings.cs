#nullable enable

using System;
using UnityEngine;

namespace UnityEditor
{
    internal static class PlayerSettings
    {
        internal static ApiCompatibilityLevel GetApiCompatibilityLevel(object? selectedBuildTargetGroup)
        {
            if (Application.ApiCompatibilityLevel == ApiCompatibilityLevel.None)
            {
                throw new InvalidOperationException($"Invalid {nameof(ApiCompatibilityLevel)}.");
            }

            return Application.ApiCompatibilityLevel;
        }
    }
}
