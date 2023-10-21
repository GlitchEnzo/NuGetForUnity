#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;

namespace UnityEngine
{
    [SuppressMessage("ReSharper", "IdentifierTypo", Justification = "Conform with Unity naming.")]
    internal static class Mathf
    {
        public static int Clamp(int value, int min, int max)
        {
            return Math.Clamp(value, min, max);
        }
    }
}
