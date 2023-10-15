using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
#if !UNITY_EDITOR_WIN
using System.Reflection;
#endif

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

#if UNITY_EDITOR_WIN
            var encryptedByteArray = ProtectedData.Protect(decryptedByteArray, EntropyBytes, DataProtectionScope.CurrentUser);
#else

            // when not compiled inside a unity editor we need to use reflection to access Windows only API
            var encryptedByteArray = ProtectOrUnprotectUsingReflection("Protect", decryptedByteArray);
#endif

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

#if UNITY_EDITOR_WIN
            var decryptedByteArray = ProtectedData.Unprotect(encryptedByteArray, EntropyBytes, DataProtectionScope.CurrentUser);
#else

            // when not compiled inside a unity editor we need to use reflection to access Windows only API
            var decryptedByteArray = ProtectOrUnprotectUsingReflection("Unprotect", encryptedByteArray);
#endif

            return Encoding.UTF8.GetString(decryptedByteArray);
        }

#if !UNITY_EDITOR_WIN
        private static byte[] ProtectOrUnprotectUsingReflection(string methodName, byte[] data)
        {
            var protectedDataType = Type.GetType("System.Security.Cryptography.ProtectedData, System.Security", true);
            var method = protectedDataType.GetMethod(methodName) ??
                         throw new InvalidOperationException($"Can't find '{methodName}' type inside type: '{protectedDataType}'.");
            return (byte[])method.Invoke(null, new object[] { data, EntropyBytes, 0 });
        }
#endif
    }
}
