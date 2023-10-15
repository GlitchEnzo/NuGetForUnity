using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace NugetForUnity.Configuration
{
    /// <summary>
    ///     Helper to encrypt sensitive data so they don't need to be stored in plaintext inside the configuration file.
    /// </summary>
    internal static class ConfigurationEncryptionHelper
    {
        private static readonly byte[] EntropyBytes = Encoding.UTF8.GetBytes("NuGet");

        /// <summary>
        ///     Encrypts a string using the Windows only protected data library.
        /// </summary>
        /// <param name="value">The value to encrypt.</param>
        /// <returns>The encrypted string.</returns>
        [SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "Keep for later.")]
        public static string EncryptString(string value)
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                Debug.LogError("Encrypted passwords are not supported on non-Windows platforms.");
                return value;
            }

            var decryptedByteArray = Encoding.UTF8.GetBytes(value);
            var encryptedByteArray = ProtectedData.Protect(decryptedByteArray, EntropyBytes, DataProtectionScope.CurrentUser);
            var encryptedString = Convert.ToBase64String(encryptedByteArray);
            return encryptedString;
        }

        /// <summary>
        ///     Decrypts a string using the Windows only protected data library.
        /// </summary>
        /// <param name="encryptedString">The value to decrypt.</param>
        /// <returns>The encrypted string.</returns>
        public static string DecryptString(string encryptedString)
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                Debug.LogError("Encrypted passwords are not supported on non-Windows platforms.");
                return encryptedString;
            }

            var encryptedByteArray = Convert.FromBase64String(encryptedString);
            var decryptedByteArray = ProtectedData.Unprotect(encryptedByteArray, EntropyBytes, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decryptedByteArray);
        }
    }
}
