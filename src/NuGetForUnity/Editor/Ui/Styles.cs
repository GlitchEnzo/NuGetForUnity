#pragma warning disable SA1600

using UnityEditor;
using UnityEngine;

namespace NugetForUnity.Ui
{
    /// <summary>
    /// Collection of GUIStyle.
    /// </summary>
    internal static class Styles
    {
        public static Color LineColor
        {
            get
            {
                if (EditorGUIUtility.isProSkin)
                {
                    return new Color(0.33f, 0.33f, 0.33f);
                }
                else
                {
                    return new Color(0.6f, 0.6f, 0.6f);
                }
            }
        }
    }
}

#pragma warning restore SA1600
