using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Editor.Models;
using NuGet.Editor.Util;
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;

namespace NuGet.Editor.Converter
{
    public class PackageMetaDataToNugetPackageConverter : 
        IConverter<IPackageSearchMetadata, NugetPackage>, 
        IConverter<IPackageMetadata, NugetPackage>
    {
        private IDownloadHelper downloadHelper;
        private NugetPackageBuilder builder = new NugetPackageBuilder();

        public PackageMetaDataToNugetPackageConverter(IDownloadHelper downloadHelper)
        {
            this.downloadHelper = downloadHelper;
        }

        public NugetPackage Convert(IPackageSearchMetadata packageSearchMetadata)
        {
            IEnumerable<VersionInfo> versionInfos = Task.Run(
                async () => await packageSearchMetadata.GetVersionsAsync()
            ).Result;
            IList<NugetPackage> result = new List<NugetPackage>(versionInfos.Count());
            
            NugetPackage resultingPackage = builder
                .WithID(packageSearchMetadata.Identity.Id)
                .WithTitle(packageSearchMetadata.Title)
                .WithDescription(packageSearchMetadata.Description)
                .WithSummary(packageSearchMetadata.Summary)
                .WithLicenseUrl(packageSearchMetadata.LicenseUrl)
                .WithProjectUrl(packageSearchMetadata.ProjectUrl)
                .WithAuthors(packageSearchMetadata.Authors)
                .WithDownloadCount(packageSearchMetadata.DownloadCount)
                .WithIconUrl(packageSearchMetadata.IconUrl)
                .WithProjectUrl(packageSearchMetadata.ProjectUrl)
                .WithVersion(versionInfos.First())
                .WithDependencies(packageSearchMetadata.DependencySets)
                .build();
            // TODO: Get IsPrerelease & PackageSource & Dependencies & RepositoryUrl & RepositoryType & RepositoryCommit
            return resultingPackage;
        }

        public NugetPackage Convert(IPackageMetadata packageMetadata)
        {
            NugetPackage resultingPackage = builder
                .WithID(packageMetadata.Id)
                .WithTitle(packageMetadata.Title)
                .WithDescription(packageMetadata.Description)
                .WithSummary(packageMetadata.Summary)
                .WithLicenseUrl(packageMetadata.LicenseUrl)
                .WithProjectUrl(packageMetadata.ProjectUrl)
                .WithAuthors(packageMetadata.Authors)
                .WithDownloadCount(0)
                .WithIconUrl(packageMetadata.IconUrl)
                .WithProjectUrl(packageMetadata.ProjectUrl)
                .WithVersion(packageMetadata.Version)
                .WithDependencies(packageMetadata.DependencyGroups)
                .build();

            // TODO: Get Versions & IsPrerelease & PackageSource & Dependencies & RepositoryUrl & RepositoryType & RepositoryCommit & DownloadCount
            return resultingPackage;
        }

        
    }
}