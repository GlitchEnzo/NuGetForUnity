#pragma warning disable SA1600

using UnityEditor;
using UnityEngine;

namespace NugetForUnity.Ui
{
    /// <summary>
    ///     Collection of GUIStyle.
    /// </summary>
    internal static class Styles
    {
        private static GUIStyle cachedSearchFieldStyle;

        private static GUIStyle cachedHeaderStyle;

        private static GUIStyle cachedBackgroundStyle;

        private static GUIStyle cachedLinkLabelStyle;

        public static Color LineColor
        {
            get
            {
                if (EditorGUIUtility.isProSkin)
                {
                    return new Color(0.05f, 0.05f, 0.05f);
                }

                return new Color(0.6f, 0.6f, 0.6f);
            }
        }

        public static Color AuthorsTextColor
        {
            get
            {
                if (EditorGUIUtility.isProSkin)
                {
                    return new Color(0.55f, 0.55f, 0.55f);
                }

                return new Color(0.45f, 0.45f, 0.45f);
            }
        }

        public static Color FoldoutHeaderColor
        {
            get
            {
                if (EditorGUIUtility.isProSkin)
                {
                    return new Color(0.2f, 0.2f, 0.2f);
                }

                return new Color(0.85f, 0.85f, 0.85f);
            }
        }

        /// <summary>
        ///     Gets a GUI style with a background color the same as the editor's current background color.
        /// </summary>
        public static GUIStyle BackgroundStyle
        {
            get
            {
                if (cachedBackgroundStyle != null)
                {
                    return cachedBackgroundStyle;
                }

                cachedBackgroundStyle = new GUIStyle();
                var backgroundColor = EditorGUIUtility.isProSkin ? new Color32(56, 56, 56, 255) : new Color32(194, 194, 194, 255);
                cachedBackgroundStyle.normal.background = CreateSingleColorTexture(backgroundColor);

                return cachedBackgroundStyle;
            }
        }

        public static GUIStyle HeaderStyle
        {
            get
            {
                if (cachedHeaderStyle != null)
                {
                    return cachedHeaderStyle;
                }

                cachedHeaderStyle = new GUIStyle();
                var backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.1f, 0.1f, 0.1f) : new Color(0.4f, 0.4f, 0.4f);
                cachedHeaderStyle.alignment = TextAnchor.MiddleLeft;
                cachedHeaderStyle.normal.background = CreateSingleColorTexture(backgroundColor);
                cachedHeaderStyle.normal.textColor = Color.white;

                return cachedHeaderStyle;
            }
        }

        public static GUIStyle SearchFieldStyle
        {
            get
            {
                if (cachedSearchFieldStyle != null)
                {
                    return cachedSearchFieldStyle;
                }

                var original = GetStyle("ToolbarSearchTextField");
                cachedSearchFieldStyle = new GUIStyle(original) { fontSize = 12, fixedHeight = 20f };

                return cachedSearchFieldStyle;
            }
        }

        public static GUIStyle LinkLabelStyle
        {
            get
            {
                if (cachedLinkLabelStyle != null)
                {
                    return cachedLinkLabelStyle;
                }

                cachedLinkLabelStyle = GetStyle("LinkLabel");
                return cachedLinkLabelStyle;
            }
        }

        /// <summary>
        ///     From here: http://forum.unity3d.com/threads/changing-the-background-color-for-beginhorizontal.66015/.
        /// </summary>
        /// <param name="color">The color to fill the texture with.</param>
        /// <returns>The generated texture.</returns>
        private static Texture2D CreateSingleColorTexture(Color color)
        {
            const int width = 16;
            const int height = 16;
            var pix = new Color32[width * height];
            Color32 color32 = color;
            for (var index = 0; index < pix.Length; index++)
            {
                pix[index] = color32;
            }

            var result = new Texture2D(width, height);
            result.SetPixels32(pix);
            result.Apply();

            return result;
        }

        private static GUIStyle GetStyle(string styleName)
        {
            var style = GUI.skin.FindStyle(styleName);

            if (style == null)
            {
                style = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle(styleName);
            }

            if (style == null)
            {
                Debug.LogError("Missing built-in guistyle " + styleName);
                style = new GUIStyle();
            }

            return style;
        }
    }
}

#pragma warning restore SA1600
