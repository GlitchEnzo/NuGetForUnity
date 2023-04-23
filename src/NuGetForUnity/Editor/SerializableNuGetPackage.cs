using System;
using UnityEngine;

namespace NugetForUnity
{
    /// <summary>
    ///     Wrapper to make a interfaced <see cref="INuGetPackage" /> able to be serialized using Unity (Unity can only serialize concrete classes not
    ///     interfaces).
    /// </summary>
    [Serializable]
    internal sealed class SerializableNuGetPackage
    {
        [SerializeField]
        private NugetPackage packageV2;

        [SerializeField]
        private NuGetPackageV3 packageV3;

        public SerializableNuGetPackage(NugetPackage packageV2)
        {
            this.packageV2 = packageV2;
        }

        public SerializableNuGetPackage(NuGetPackageV3 packageV3)
        {
            this.packageV3 = packageV3;
        }

        public SerializableNuGetPackage(INuGetPackage packageInterface)
        {
            switch (packageInterface)
            {
                case NugetPackage packageV2Instance:
                    packageV2 = packageV2Instance;
                    break;
                case NuGetPackageV3 packageV3Instance:
                    packageV3 = packageV3Instance;
                    break;
                default:
                    throw new ArgumentException(
                        $"Package has type: {packageInterface?.GetType()} with is currently not handled.",
                        nameof(packageInterface));
            }
        }

        /// <summary>
        ///     Gets the package as general interface.
        /// </summary>
        /// <remarks>
        ///     We need to check the package id as the serializes instantiates the package even if it is null.
        /// </remarks>
        public INuGetPackage Interfaced => packageV2 is null || string.IsNullOrEmpty(packageV2.Id) ? (INuGetPackage)packageV3 : packageV2;
    }
}
