#pragma warning disable SA1512,SA1124 // Single-line comments should not be followed by blank line

using System;
using JetBrains.Annotations;
using UnityEngine;

#region No ReShaper

// ReSharper disable All
// needed because 'JetBrains.Annotations.NotNull' and 'System.Diagnostics.CodeAnalysis.NotNull' collide if this file is compiled with a never version of Unity / C#
using SuppressMessageAttribute = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;

// ReSharper restore All

#endregion

#pragma warning restore SA1512,SA1124 // Single-line comments should not be followed by blank line
namespace NugetForUnity.Models
{
    /// <summary>
    ///     Wrapper to make a interfaced <see cref="INugetPackage" /> able to be serialized using Unity (Unity can only serialize concrete classes not
    ///     interfaces).
    /// </summary>
    [Serializable]
    internal sealed class SerializableNugetPackage
    {
        [CanBeNull]
        [SerializeField]
        private NugetPackageLocal packageLocal;

        [SerializeField]
        private PackageType packageType;

        [CanBeNull]
        [SerializeField]
        private NugetPackageV2 packageV2;

        [CanBeNull]
        [SerializeField]
        private NugetPackageV3 packageV3;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SerializableNugetPackage" /> class.
        /// </summary>
        /// <param name="packageInterface">The package.</param>
        public SerializableNugetPackage(INugetPackage packageInterface)
        {
            var type = packageInterface?.GetType() ?? throw new ArgumentNullException(nameof(packageInterface));
            if (type == typeof(NugetPackageLocal))
            {
                packageLocal = (NugetPackageLocal)packageInterface;
                packageType = PackageType.Local;
            }
            else if (type == typeof(NugetPackageV2))
            {
                packageV2 = (NugetPackageV2)packageInterface;
                packageType = PackageType.V2;
            }
            else if (type == typeof(NugetPackageV3))
            {
                packageV3 = (NugetPackageV3)packageInterface;
                packageType = PackageType.V3;
            }
            else
            {
                throw new ArgumentException($"Package has type: {type} with is currently not handled.", nameof(packageInterface));
            }
        }

        /// <summary>
        ///     Gets the package as general interface.
        /// </summary>
        [NotNull]
        public INugetPackage Interfaced
        {
            get
            {
                switch (packageType)
                {
                    case PackageType.Local:
                        return packageLocal ?? throw new InvalidOperationException($"Package is null {packageType}");
                    case PackageType.V2:
                        return packageV2 ?? throw new InvalidOperationException($"Package is null {packageType}");
                    case PackageType.V3:
                        return packageV3 ?? throw new InvalidOperationException($"Package is null {packageType}");
                    default:
                        throw new InvalidOperationException($"Package has type: {packageType} with is currently not handled.");
                }
            }
        }

        [SuppressMessage(
            "StyleCop.CSharp.OrderingRules",
            "SA1201:Elements should appear in the correct order",
            Justification = "We like private enums at the botom of the file.")]
        private enum PackageType
        {
            Local,

            V2,

            V3,
        }
    }
}
