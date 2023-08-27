using System;
using UnityEngine;

namespace NugetForUnity.Models
{
    /// <summary>
    ///     Wrapper to make a interfaced <see cref="INugetPackage" /> able to be serialized using Unity (Unity can only serialize concrete classes not
    ///     interfaces).
    /// </summary>
    [Serializable]
    internal sealed class SerializableNugetPackage
    {
        [SerializeField]
        private NugetPackageLocal packageLocal;

        [SerializeField]
        private PackageType packageType;

        [SerializeField]
        private NugetPackageV2 packageV2;

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
        public INugetPackage Interfaced
        {
            get
            {
                switch (packageType)
                {
                    case PackageType.Local:
                        return packageLocal;
                    case PackageType.V2:
                        return packageV2;
                    case PackageType.V3:
                        return packageV3;
                    default:
                        throw new InvalidOperationException($"Package has type: {packageType} with is currently not handled.");
                }
            }
        }

        private enum PackageType
        {
            Local,

            V2,

            V3,
        }
    }
}
