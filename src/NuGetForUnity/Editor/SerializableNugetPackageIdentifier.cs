using System;
using UnityEngine;

namespace NugetForUnity
{
    /// <summary>
    ///     Wrapper to make a interfaced <see cref="INugetPackageIdentifier" /> able to be serialized using Unity
    ///     (Unity can only serialize concrete classes not interfaces).
    /// </summary>
    [Serializable]
    internal sealed class SerializableNugetPackageIdentifier
    {
        [SerializeField]
        private bool isIdentifier;

        [SerializeField]
        private NugetPackageIdentifier packageIdentifier;

        [SerializeField]
        private SerializableNugetPackage serializablePackage;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SerializableNugetPackageIdentifier" /> class.
        /// </summary>
        /// <param name="packageIdentifier">The interfaced version of the NuGet package identifier.</param>
        public SerializableNugetPackageIdentifier(INugetPackageIdentifier packageIdentifier)
        {
            var type = packageIdentifier?.GetType() ?? throw new ArgumentNullException(nameof(packageIdentifier));
            if (type == typeof(NugetPackageIdentifier))
            {
                this.packageIdentifier = (NugetPackageIdentifier)packageIdentifier;
                isIdentifier = true;
            }
            else if (packageIdentifier is INugetPackage package)
            {
                serializablePackage = new SerializableNugetPackage(package);
                isIdentifier = false;
            }
            else
            {
                throw new ArgumentException($"PackageIdentifier has type: {type} with is currently not handled.", nameof(packageIdentifier));
            }
        }

        /// <summary>
        ///     Gets the package identifier as general interface.
        /// </summary>
        public INugetPackageIdentifier Interfaced
        {
            get
            {
                if (isIdentifier)
                {
                    return packageIdentifier;
                }

                return serializablePackage.Interfaced;
            }
        }
    }
}
