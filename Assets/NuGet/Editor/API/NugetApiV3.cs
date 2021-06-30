using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;
using ILogger = NuGet.Common.ILogger;

namespace App
{
    public class NugetApi: INugetApi
    {
        private ILogger logger;
        private CancellationToken cancellationToken;
        private SourceCacheContext cache;
        private SourceRepository repository;
        public SearchFilter SearchFilter { get; set; }

        public NugetApi(
            ILogger logger, 
            CancellationToken cancellationToken, 
            SourceCacheContext cache, 
            SourceRepository repository, 
            SearchFilter searchFilter
        )
        {
            this.logger = logger;
            this.cancellationToken = cancellationToken;
            this.cache = cache;
            this.repository = repository;
            this.SearchFilter = searchFilter;
        }
        
        public NugetApi(
            ILogger logger, 
            CancellationToken cancellationToken, 
            SourceCacheContext cache, 
            SourceRepository repository
        )
        {
            this.logger = logger;
            this.cancellationToken = cancellationToken;
            this.cache = cache;
            this.repository = repository;
            this.SearchFilter = new SearchFilter(false, SearchFilterType.IsLatestVersion);
        }

        public IEnumerable<IPackageSearchMetadata> FindPackagesById(string id)
        {
            PackageMetadataResource resource = repository.GetResource<PackageMetadataResource>();
            IEnumerable<IPackageSearchMetadata> metadataResults = Task.Run(
                async () => await resource.GetMetadataAsync(
                    id,
                    includePrerelease: SearchFilter.IncludePrerelease,
                    includeUnlisted: SearchFilter.IncludeDelisted, // Delisted is same as unlisted 
                    cache,
                    logger,
                    cancellationToken
                )
            ).Result;

            return metadataResults;
        }

        public IEnumerable<IPackageSearchMetadata> Search(string searchTerm, int skip = 0, int take = 20)
        {
            PackageSearchResource resource = Task.Run(() => repository.GetResourceAsync<PackageSearchResource>()).Result;

            IEnumerable<IPackageSearchMetadata> search = Task.Run(
                    async () => await resource.SearchAsync(
                        searchTerm,
                        SearchFilter,
                        skip: skip,
                        take: take,
                        logger,
                        cancellationToken
                    )
            )
            .Result;
            
            return search;
        }

        public IEnumerable<IPackageMetadata> GetUpdates(
            IEnumerable<IPackageMetadata> installedPackages, 
            bool includePrerelease = false, 
            bool includeAllVersions = false,
            string targetFrameworks = "", 
            string versionContraints = ""
        )
        {
            throw new NotImplementedException();
        }
    }
}