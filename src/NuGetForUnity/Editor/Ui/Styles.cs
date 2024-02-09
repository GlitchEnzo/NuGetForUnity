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

        private static GUIStyle cachedToolbarStyle;

        private static GUIStyle cachedBackgroundStyle;

        private static GUIStyle cachedBoldLabelStyle;

        private static GUIStyle cachedAuthorsLabelStyle;

        private static GUIStyle cachedDescriptionLabelStyle;

        private static GUIStyle cachedLinkLabelStyle;

        private static GUIStyle cachedPackageNameLabelStyle;

        /// <summary>
        ///     Gets the color used for lines used as seperators.
        /// </summary>
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

        /// <summary>
        ///     Gets the color used for package authors.
        /// </summary>
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

        /// <summary>
        ///     Gets the color used for the foldout header.
        /// </summary>
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

        /// <summary>
        ///     Gets the GUI style used for the header of the NuGet window.
        /// </summary>
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

        /// <summary>
        ///     Gets the GUI style used for the toolbar of the NuGet window.
        /// </summary>
        public static GUIStyle ToolbarStyle
        {
            get
            {
                if (cachedToolbarStyle != null)
                {
                    return cachedToolbarStyle;
                }

                cachedToolbarStyle = new GUIStyle(EditorStyles.toolbar) { fixedHeight = 25f };

                return cachedToolbarStyle;
            }
        }

        /// <summary>
        ///     Gets the GUI style used for the package search field.
        /// </summary>
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

        /// <summary>
        ///     Gets the GUI style used for bold labels.
        /// </summary>
        public static GUIStyle BoldLabelStyle
        {
            get
            {
                if (cachedBoldLabelStyle != null)
                {
                    return cachedBoldLabelStyle;
                }

                cachedBoldLabelStyle = new GUIStyle(EditorStyles.label) { fontSize = 12, fontStyle = FontStyle.Bold };

                return cachedBoldLabelStyle;
            }
        }

        /// <summary>
        ///     Gets the GUI style used for authors labels.
        /// </summary>
        public static GUIStyle AuthorsLabelStyle
        {
            get
            {
                if (cachedAuthorsLabelStyle != null)
                {
                    return cachedAuthorsLabelStyle;
                }

                cachedAuthorsLabelStyle = new GUIStyle(EditorStyles.label) { fontSize = 10, fontStyle = FontStyle.Normal };

                cachedAuthorsLabelStyle.normal.textColor = AuthorsTextColor;
                cachedAuthorsLabelStyle.focused.textColor = AuthorsTextColor;
                cachedAuthorsLabelStyle.hover.textColor = AuthorsTextColor;

                return cachedAuthorsLabelStyle;
            }
        }

        /// <summary>
        ///     Gets the GUI style used for description labels.
        /// </summary>
        public static GUIStyle DescriptionLabelStyle
        {
            get
            {
                if (cachedDescriptionLabelStyle != null)
                {
                    return cachedDescriptionLabelStyle;
                }

                cachedDescriptionLabelStyle = new GUIStyle(EditorStyles.label)
                {
                    wordWrap = true, fontStyle = FontStyle.Normal, alignment = TextAnchor.UpperLeft,
                };

                return cachedDescriptionLabelStyle;
            }
        }

        /// <summary>
        ///     Gets the GUI style used for package name labels.
        /// </summary>
        public static GUIStyle PackageNameLabelStyle
        {
            get
            {
                if (cachedPackageNameLabelStyle != null)
                {
                    return cachedPackageNameLabelStyle;
                }

                cachedPackageNameLabelStyle = new GUIStyle(EditorStyles.label) { fontSize = 15, fontStyle = FontStyle.Bold };

                return cachedPackageNameLabelStyle;
            }
        }

        /// <summary>
        ///     Gets the GUI style used for labels representing a URL / clickable LINK.
        /// </summary>
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
