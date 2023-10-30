using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NugetForUnity.Configuration;
using NugetForUnity.Models;
using UnityEngine;

namespace NugetForUnity.PackageSource
{
    /// <summary>
    ///     Online NuGet package source for NuGet API v3.
    /// </summary>
    [Serializable]
    internal sealed class NugetPackageSourceV3 : INugetPackageSource, ISerializationCallbackReceiver
    {
        /// <summary>
        ///     Default value for <see cref="UpdateSearchBatchSize" />.
        /// </summary>
        public const int DefaultUpdateSearchBatchSize = 20;

        [NotNull]
        private static readonly Dictionary<string, NugetApiClientV3> ApiClientCache = new Dictionary<string, NugetApiClientV3>();

        [CanBeNull]
        [NonSerialized]
        private NugetApiClientV3 apiClient;

        /// <summary>
        ///     Initializes a new instance of the <see cref="NugetPackageSourceV3" /> class.
        /// </summary>
        /// <param name="name">The name of the package source.</param>
        /// <param name="url">The path to the package source.</param>
        /// <param name="savedProtocolVersion">The explicitly defined protocol version stored inside the 'NuGet.config'.</param>
        public NugetPackageSourceV3([NotNull] string name, [NotNull] string url, string savedProtocolVersion)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException($"'{nameof(name)}' cannot be null or empty.", nameof(name));
            }

            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException($"'{nameof(url)}' cannot be null or empty.", nameof(url));
            }

            Name = name;
            SavedPath = url;
            SavedProtocolVersion = savedProtocolVersion;
            IsEnabled = true;

            InitializeApiClient();
        }

        /// <summary>
        ///     Gets password, with the values of environment variables expanded.
        /// </summary>
        [CanBeNull]
        public string ExpandedPassword
        {
            get
            {
                if (SavedPassword == null)
                {
                    return null;
                }

                var expandedPassword = Environment.ExpandEnvironmentVariables(SavedPassword);
                return SavedPasswordIsEncrypted ? ConfigurationEncryptionHelper.DecryptString(expandedPassword) : expandedPassword;
            }
        }

        /// <inheritdoc />
        [field: SerializeField]
        public string Name { get; set; }

        /// <inheritdoc />
        [field: SerializeField]
        public string SavedPath { get; set; }

        /// <inheritdoc />
        [field: SerializeField]
        public string SavedProtocolVersion { get; private set; }

        /// <inheritdoc />
        public bool SavedPasswordIsEncrypted { get; set; }

        /// <inheritdoc cref="NugetApiClientV3.PackageDownloadUrlTemplateOverwrite" />
        [CanBeNull]
        public string PackageDownloadUrlTemplateOverwrite
        {
            get => ApiClient.PackageDownloadUrlTemplateOverwrite;

            set => ApiClient.PackageDownloadUrlTemplateOverwrite = value;
        }

        /// <summary>
        ///     Gets or sets available updates are fetched using the <see cref="SearchAsync" />, to prevent the search query string to exceed the URI lenght
        ///     limit we
        ///     fetch the updates in groups. Defaults to <c>20</c>.
        /// </summary>
        [field: SerializeField]
        public int UpdateSearchBatchSize { get; set; } = DefaultUpdateSearchBatchSize;

        /// <inheritdoc />
        [field: SerializeField]
        public string UserName { get; set; }

        /// <inheritdoc />
        [field: SerializeField]
        public string SavedPassword { get; set; }

        /// <inheritdoc />
        [field: SerializeField]
        public bool IsEnabled { get; set; }

        /// <inheritdoc />
        public bool HasPassword
        {
            get => SavedPassword != null;

            set
            {
                if (value)
                {
                    if (SavedPassword == null)
                    {
                        SavedPassword = string.Empty; // Initialize newly-enabled password to empty string.
                    }
                }
                else
                {
                    SavedPassword = null; // Clear password to null when disabled.
                }
            }
        }

        /// <summary>
        ///     Gets the lazy initialized API-client. We need this because <see cref="InitializeApiClient" /> can't be called in
        ///     <see cref="OnAfterDeserialize" />.
        /// </summary>
        [NotNull]
        private NugetApiClientV3 ApiClient => apiClient ?? InitializeApiClient();

        /// <inheritdoc />
        public List<INugetPackage> FindPackagesById(INugetPackageIdentifier package)
        {
            // see https://github.com/NuGet/docs.microsoft.com-nuget/blob/live/docs/consume-packages/Finding-and-Choosing-Packages.md
            // it supports searching for a version but only if the version is the latest version
            // so we need to fetch the latest version and filter them ourselves
            var searchQuery = $"packageid:{package.Id}";

            var packages = Task.Run(() => ApiClient.SearchPackageAsync(this, searchQuery, 0, 0, package.IsPrerelease, CancellationToken.None))
                .GetAwaiter()
                .GetResult();

            if (packages.Count == 0)
            {
                return packages;
            }

            if (packages.Count != 1)
            {
                Debug.LogWarning($"Found {packages.Count} packages with id {package.Id} in source {Name} but expected 1.");
            }

            var fetchedPackage = (NugetPackageV3)packages[0];
            if (!package.HasVersionRange && fetchedPackage.Equals(package))
            {
                // exact match found
                return packages;
            }

            var matchingVersion = fetchedPackage.Versions.FindLast(package.InRange);
            if (matchingVersion == null)
            {
                // no matching version found
                return new List<INugetPackage>();
            }

            // overwrite the version so it is installed
            fetchedPackage.PackageVersion = matchingVersion;
            return packages;
        }

        /// <inheritdoc />
        public INugetPackage GetSpecificPackage(INugetPackageIdentifier package)
        {
            return FindPackagesById(package).FirstOrDefault();
        }

        /// <inheritdoc />
        public List<INugetPackage> GetUpdates(
            IEnumerable<INugetPackage> packages,
            bool includePrerelease = false,
            string targetFrameworks = "",
            string versionConstraints = "")
        {
            var packagesToFetch = packages as IList<INugetPackage> ?? packages.ToList();

            var packagesFromServer = Task.Run(
                    async () =>
                    {
                        var updates = new List<INugetPackage>();
                        for (var i = 0; i < packagesToFetch.Count;)
                        {
                            var searchQueryBuilder = new StringBuilder();
                            for (var inner = 0; inner < UpdateSearchBatchSize && i < packagesToFetch.Count; inner++, i++)
                            {
                                if (i > 0)
                                {
                                    searchQueryBuilder.Append(' ');
                                }

                                searchQueryBuilder.Append($"packageid:{packagesToFetch[i].Id}");
                            }

                            updates.AddRange(
                                await ApiClient.SearchPackageAsync(
                                    this,
                                    searchQueryBuilder.ToString(),
                                    0,
                                    0,
                                    includePrerelease,
                                    CancellationToken.None));
                        }

                        return updates;
                    })
                .GetAwaiter()
                .GetResult();

            packagesFromServer.Sort();
            return packagesFromServer;
        }

        /// <inheritdoc />
        public Task<List<INugetPackage>> SearchAsync(
            string searchTerm = "",
            bool includePrerelease = false,
            int numberToGet = 15,
            int numberToSkip = 0,
            CancellationToken cancellationToken = default)
        {
            return ApiClient.SearchPackageAsync(this, searchTerm, numberToSkip, numberToGet, includePrerelease, cancellationToken);
        }

        /// <inheritdoc />
        public void DownloadNupkgToFile(INugetPackageIdentifier package, string outputFilePath, string downloadUrlHint)
        {
            Task.Run(() => ApiClient.DownloadNupkgToFileAsync(this, package, outputFilePath)).GetAwaiter().GetResult();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            apiClient?.Dispose();
            ApiClientCache.Remove(SavedPath);
        }

        /// <inheritdoc />
        public void OnBeforeSerialize()
        {
            // do nothing
        }

        /// <inheritdoc />
        public void OnAfterDeserialize()
        {
            if (string.IsNullOrEmpty(SavedPassword))
            {
                SavedPassword = null;
            }
        }

        /// <inheritdoc cref="NugetApiClientV3.GetPackageDetailsAsync" />
        [NotNull]
        [ItemNotNull]
        public Task<List<NugetFrameworkGroup>> GetPackageDetailsAsync(
            [NotNull] INugetPackageIdentifier package,
            CancellationToken cancellationToken = default)
        {
            return ApiClient.GetPackageDetailsAsync(this, package, cancellationToken);
        }

        [NotNull]
        private NugetApiClientV3 InitializeApiClient()
        {
            if (ApiClientCache.TryGetValue(SavedPath, out apiClient))
            {
                Debug.Assert(apiClient != null, nameof(apiClient) + " != null");
                return apiClient;
            }

            apiClient = new NugetApiClientV3(SavedPath);
            ApiClientCache.Add(SavedPath, apiClient);
            return apiClient;
        }
    }
}
