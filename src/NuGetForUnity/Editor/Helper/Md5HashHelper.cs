#pragma warning disable SA1512,SA1124 // Single-line comments should not be followed by blank line

#region No ReShaper

using System;
using System.Security.Cryptography;
using System.Text;
using JetBrains.Annotations;

// ReSharper disable All
// needed because 'JetBrains.Annotations.NotNull' and 'System.Diagnostics.CodeAnalysis.NotNull' collide if this file is compiled with a never version of Unity / C#
using SuppressMessageAttribute = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;

// ReSharper restore All

#endregion

#pragma warning restore SA1512,SA1124 // Single-line comments should not be followed by blank line

namespace NugetForUnity.Helper
{
    /// <summary>
    ///     Helper class for MD5 hashing.
    /// </summary>
    internal static class Md5HashHelper
    {
        /// <summary>
        ///     Computes the MD5 hash of <paramref name="value" /> and returns it as Base64 string with all chars that are not allowed on file-paths replaced.
        /// </summary>
        /// <param name="value">The string that is hashed.</param>
        /// <returns>The MD5 has of <paramref name="value" /> as a Base64 string with all chars that are not allowed on file-paths replaced.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="value" /> is null or a empty string.</exception>
        [SuppressMessage("Design", "CA5351", Justification = "Only use MD5 hash as cache key / not security relevant.")]
        [NotNull]
        public static string GetFileNameSafeHash([NotNull] string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentNullException(nameof(value));
            }

            using (var md5 = new MD5CryptoServiceProvider())
            {
                var data = md5.ComputeHash(Encoding.Default.GetBytes(value));
                return Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_').TrimEnd('=');
            }
        }
    }
}
