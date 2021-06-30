using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using App;
using JetBrains.Annotations;
using NuGet.Common;
using NuGet.Editor.Converter;
using NuGet.Editor.Models;
using NuGet.Editor.Util;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Editor
{
    public class NugetPackageBuilder : IBuilder<NugetPackage>
    {
        private NugetPackage nugetPackage = new NugetPackage();
        private IDownloadHelper downloadHelper;

        public NugetPackageBuilder WithID(string id)
        {
            nugetPackage.Id = id;
            return this;
        }

        public NugetPackageBuilder WithTitle(string title)
        {
            nugetPackage.Title = title;
            return this;
        }
        
        public NugetPackageBuilder WithDescription(string description)
        {
            nugetPackage.Description = description;
            return this;
        }
        
        public NugetPackageBuilder WithSummary(string summary)
        {
            nugetPackage.Summary = summary;
            return this;
        }
        
        public NugetPackageBuilder WithReleaseNotes(string releaseNotes)
        {
            nugetPackage.ReleaseNotes = releaseNotes;
            return this;
        }
        
        public NugetPackageBuilder WithLicenseUrl(Uri licenseUrl)
        {
            nugetPackage.LicenseUrl = licenseUrl?.ToString();
            return this;
        }
        
        public NugetPackageBuilder WithDownloadUrl(string downloadUrl)
        {
            nugetPackage.DownloadUrl = downloadUrl;
            return this;
        }
        
        public NugetPackageBuilder WithDownloadCount(long? downloadCount)
        {
            nugetPackage.DownloadCount = ConvertLongToInt(downloadCount);
            return this;
        }
        
        public NugetPackageBuilder WithDownloadCount(int downloadCount)
        {
            nugetPackage.DownloadCount = downloadCount;
            return this;
        }
        
        public NugetPackageBuilder WithAuthors(string authors)
        {
            nugetPackage.Authors = authors;
            return this;
        }
        
        public NugetPackageBuilder WithAuthors(IEnumerable<string> authors)
        {
            nugetPackage.Authors = authors.ToString();
            return this;
        }
        
        public NugetPackageBuilder WithProjectUrl(Uri projectUrl)
        {
            nugetPackage.ProjectUrl = projectUrl?.ToString();
            return this;
        }
        
        public NugetPackageBuilder WithRepositoryUrl(Uri repositoryUrl)
        {
            nugetPackage.RepositoryUrl = repositoryUrl.ToString();
            return this;
        }

        public NugetPackageBuilder WithIconUrl(Uri iconUrl)
        {
            if (iconUrl != null)
            {
                nugetPackage.Icon = downloadHelper.DownloadImage(iconUrl?.ToString());
            }

            return this;
        }

        public NugetPackageBuilder WithVersion(VersionInfo versionInfo)
        {
            return WithVersion(versionInfo.Version);
        }
        
        public NugetPackageBuilder WithVersion(NuGetVersion nugetVersion)
        {
            nugetPackage.Version = nugetVersion.ToString();
            return this;
        }

        public NugetPackageBuilder WithPackageSource(INugetPackageSource nugetPackageSource)
        {
            nugetPackage.PackageSource = nugetPackageSource;
            return this;
        }

        public NugetPackageBuilder WithRepository(RepositoryMetadata repositoryMetadata)
        {
            nugetPackage.RepositoryBranch = repositoryMetadata.Branch;
            nugetPackage.RepositoryCommit = repositoryMetadata.Commit;
            nugetPackage.RepositoryUrl = repositoryMetadata.Url;
            
            return this;
        }

        public NugetPackageBuilder WithDependencies(IEnumerable<PackageDependencyGroup> dependencyGroups)
        {
            foreach(PackageDependencyGroup packageDependencyGroup in dependencyGroups)
            {
                WithDependencies(packageDependencyGroup);
            }
            
            return this;
        }
        
        public NugetPackageBuilder WithDependencies(PackageDependencyGroup dependency)
        {
            List<NugetPackageIdentifier> packageIdentifiers = dependency.Packages.Select(
                package => new NugetPackageIdentifier(package.Id, package.VersionRange.ToString())
            ).ToList();
            NugetFrameworkGroup frameworkGroup = new NugetFrameworkGroup(dependency.TargetFramework.ToString(), packageIdentifiers);
            nugetPackage.Dependencies.Add(frameworkGroup);
            return this;
        }

        public NugetPackage build()
        {
            NugetPackage nugetPackage = this.nugetPackage;
            Reset();
            return nugetPackage;
        }

        public void Reset()
        {
            this.nugetPackage = null;
            this.nugetPackage = new NugetPackage();
        }
        
        private int ConvertLongToInt(long? number)
        {
            int result = -1;
            
            if (number == null)
            {
                return 0;
            }
            
            if (number >= int.MaxValue)
            {
                result = int.MaxValue;
            }
            else
            {
                result = (int) number;
            }

            return result;
        }
        
    }
}