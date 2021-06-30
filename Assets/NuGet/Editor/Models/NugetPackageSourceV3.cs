using System.Collections.Generic;
using System.Linq;
using App;
using NuGet.Editor.Converter;
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;

namespace NuGet.Editor.Models
{
    public class NugetPackageSourceV3 : NugetPackageSource
    {
        private INugetApi api;
        private IConverter<IPackageSearchMetadata, NugetPackage> searchMetaDataToPackageConverter;
        private IConverter<IPackageMetadata, NugetPackage> metaDataToPackageConverter;

        public NugetPackageSourceV3(
            string name, 
            string path, 
            INugetApi api,
            IConverter<IPackageSearchMetadata, NugetPackage> searchMetaDataToPackageConverter, 
            IConverter<IPackageMetadata, NugetPackage> metaDataToPackageConverter 
        ) : base(name, path)
        {
            this.searchMetaDataToPackageConverter = searchMetaDataToPackageConverter;
            this.metaDataToPackageConverter = metaDataToPackageConverter;
            this.api = api;
        }

        public override IEnumerable<NugetPackage> FindPackagesById(NugetPackageIdentifier package)
        {
            IEnumerable<IPackageSearchMetadata> packagesById = api.Search(package.Id);

            return packagesById
                .Select(packageSearchMetadata => searchMetaDataToPackageConverter.Convert(packageSearchMetadata))
                .ToList();
        }

        public override NugetPackage GetSpecificPackage(NugetPackageIdentifier package)
        {
            return FindPackagesById(package).First();
        }

        public override IEnumerable<NugetPackage> Search(
            string searchTerm = "", 
            bool includeAllVersions = false, 
            bool includePrerelease = false,
            int numberToGet = 15, 
            int numberToSkip = 0
        )
        {
            // TODO: Make includeAllVersions do something
            api.SearchFilter = new SearchFilter(includePrerelease);
            IEnumerable<IPackageSearchMetadata> search = api.Search(searchTerm, numberToSkip, numberToGet);

            return search
                .Select(packageSearchMetadata => searchMetaDataToPackageConverter.Convert(packageSearchMetadata))
                .ToList();
        }

        public override IEnumerable<NugetPackage> GetUpdates(IEnumerable<NugetPackage> installedPackages, bool includePrerelease = false, bool includeAllVersions = false,
            string targetFrameworks = "", string versionContraints = "")
        {
            return base.GetUpdates(installedPackages, includePrerelease, includeAllVersions, targetFrameworks, versionContraints);
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            return base.ToString();
        }
    }
}