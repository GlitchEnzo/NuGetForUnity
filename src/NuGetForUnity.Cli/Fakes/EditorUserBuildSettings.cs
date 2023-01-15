#nullable enable

using System.Diagnostics.CodeAnalysis;

namespace UnityEditor
{
    internal static class EditorUserBuildSettings
    {
        [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Conform with Unity naming.")]
        public static object? selectedBuildTargetGroup { get; }
    }
}
