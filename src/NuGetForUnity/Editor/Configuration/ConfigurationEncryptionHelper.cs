﻿#if !((UNITY_EDITOR_WIN && UNITY_2023_1_OR_NEWER) || NUGETFORUNITY_CLI)
using JetBrains.Annotations;
#else
using System.Security.Cryptography;
#endif
using System;
using System.Diagnostics.CodeAnalysis;
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

#if !((UNITY_EDITOR_WIN && UNITY_2023_1_OR_NEWER) || NUGETFORUNITY_CLI)

        // on .net framework the type lives in 'System.Security' on .net standard it in 'System.Security.Cryptography.ProtectedData'
        [ItemCanBeNull]
        private static readonly Lazy<Type> ProtectedDataTypeLazy = new Lazy<Type>(
            () => Type.GetType("System.Security.Cryptography.ProtectedData, System.Security.Cryptography.ProtectedData") ??
                  Type.GetType("System.Security.Cryptography.ProtectedData, System.Security"));
#endif

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

            try
            {
                var decryptedByteArray = Encoding.UTF8.GetBytes(value);

#if (UNITY_EDITOR_WIN && UNITY_2023_1_OR_NEWER) || NUGETFORUNITY_CLI
                var encryptedByteArray = ProtectedData.Protect(decryptedByteArray, EntropyBytes, DataProtectionScope.CurrentUser);
#else

                // when not compiled inside a unity editor we need to use reflection to access Windows only API
                var encryptedByteArray = ProtectOrUnprotectUsingReflection("Protect", decryptedByteArray);

                if (encryptedByteArray == null)
                {
                    return value;
                }
#endif

                var encryptedString = Convert.ToBase64String(encryptedByteArray);
                return encryptedString;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to encrypt string, error: {e}");
                return value;
            }
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

            try
            {
                var encryptedByteArray = Convert.FromBase64String(encryptedString);

#if (UNITY_EDITOR_WIN && UNITY_2023_1_OR_NEWER) || NUGETFORUNITY_CLI
                var decryptedByteArray = ProtectedData.Unprotect(encryptedByteArray, EntropyBytes, DataProtectionScope.CurrentUser);
#else

                // when not compiled inside a unity editor we need to use reflection to access Windows only API
                var decryptedByteArray = ProtectOrUnprotectUsingReflection("Unprotect", encryptedByteArray);

                if (decryptedByteArray == null)
                {
                    return encryptedString;
                }
#endif

                return Encoding.UTF8.GetString(decryptedByteArray);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to decrypt string, error: {e}");
                return encryptedString;
            }
        }

#if !((UNITY_EDITOR_WIN && UNITY_2023_1_OR_NEWER) || NUGETFORUNITY_CLI)
        [CanBeNull]
        private static byte[] ProtectOrUnprotectUsingReflection(string methodName, byte[] data)
        {
            var protectedDataType = ProtectedDataTypeLazy.Value;
            if (protectedDataType == null)
            {
                Debug.LogError("Encrypted passwords are not supported: type 'System.Security.Cryptography.ProtectedData' was not found.");
                return null;
            }

            var method = protectedDataType.GetMethod(methodName) ??
                         throw new InvalidOperationException($"Can't find '{methodName}' type inside type: '{protectedDataType}'.");
            return (byte[])method.Invoke(null, new object[] { data, EntropyBytes, 0 });
        }
#endif
    }
}
