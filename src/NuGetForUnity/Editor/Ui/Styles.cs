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
        private static GUIStyle cachedSearchFieldStyle;
        private static GUIStyle cachedHeaderStyle;
        private static GUIStyle cachedBackgroundStyle;
        private static GUIStyle cachedContrastStyle;

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

        /// <summary>
        ///     Gets a GUI style with a contrasting background color based upon if the Unity Editor is the free (light) skin or the Pro (dark) skin.
        /// </summary>
        public static GUIStyle ContrastStyle
        {
            get
            {
                if (cachedContrastStyle != null)
                {
                    return cachedContrastStyle;
                }

                cachedContrastStyle = new GUIStyle();
                var backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.6f, 0.6f, 0.6f);
                cachedContrastStyle.normal.background = CreateSingleColorTexture(backgroundColor);

                return cachedContrastStyle;
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

                cachedSearchFieldStyle = new GUIStyle(EditorStyles.toolbarSearchField)
                {
                    fontSize = 12,
                    fixedHeight = 20f,
                };

                return cachedSearchFieldStyle;
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
    }
}

#pragma warning restore SA1600
