using UnityEngine;

namespace NugetForUnity
{
    /// <summary>
    ///     Extension methods for manipulating Rect objects.
    /// </summary>
    internal static class RectHelper
    {
        /// <summary>
        ///     Expands the boundaries of the Rect by a specified value in all directions.
        /// </summary>
        /// <param name="rect">The rect to update.</param>
        /// <param name="value">The value to add.</param>
        /// <returns>The updated Rect.</returns>
        public static Rect Expand(this Rect rect, float value)
        {
            rect.xMin -= value;
            rect.xMax += value;
            rect.yMin -= value;
            rect.yMax += value;
            return rect;
        }

        /// <summary>
        ///     Expands the Rect horizontally by a specified value.
        /// </summary>
        /// <param name="rect">The rect to update.</param>
        /// <param name="value">The value to add.</param>
        /// <returns>The updated Rect.</returns>
        public static Rect ExpandX(this Rect rect, float value)
        {
            rect.xMin -= value;
            rect.xMax += value;
            return rect;
        }

        /// <summary>
        ///     Expands the Rect vertically by a specified value.
        /// </summary>
        /// <param name="rect">The rect to update.</param>
        /// <param name="value">The value to add.</param>
        /// <returns>The updated Rect.</returns>
        public static Rect ExpandY(this Rect rect, float value)
        {
            rect.yMin -= value;
            rect.yMax += value;
            return rect;
        }

        /// <summary>
        ///     Adds a specified value to the X-coordinate of the Rect.
        /// </summary>
        /// <param name="rect">The rect to update.</param>
        /// <param name="value">The value to add.</param>
        /// <returns>The updated Rect.</returns>
        public static Rect AddX(this Rect rect, float value)
        {
            rect.x += value;
            return rect;
        }

        /// <summary>
        ///     Adds a specified value to the Y-coordinate of the Rect.
        /// </summary>
        /// <param name="rect">The rect to update.</param>
        /// <param name="value">The value to add.</param>
        /// <returns>The updated Rect.</returns>
        public static Rect AddY(this Rect rect, float value)
        {
            rect.y += value;
            return rect;
        }

        /// <summary>
        ///     Sets the width of the Rect to a specified value.
        /// </summary>
        /// <param name="rect">The rect to update.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>The updated Rect.</returns>
        public static Rect SetWidth(this Rect rect, float value)
        {
            rect.width = value;
            return rect;
        }

        /// <summary>
        ///     Sets the height of the Rect to a specified value.
        /// </summary>
        /// <param name="rect">The rect to update.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>The updated Rect.</returns>
        public static Rect SetHeight(this Rect rect, float value)
        {
            rect.height = value;
            return rect;
        }
    }
}
