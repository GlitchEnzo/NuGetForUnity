#pragma warning disable SA1512,SA1124 // Single-line comments should not be followed by blank line

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NugetForUnity.Configuration;
using NugetForUnity.Helper;
using NugetForUnity.Models;
using UnityEditor;
using UnityEngine;

#region No ReShaper

// ReSharper disable All
// needed because 'JetBrains.Annotations.NotNull' and 'System.Diagnostics.CodeAnalysis.NotNull' collide if this file is compiled with a never version of Unity / C#
using SuppressMessageAttribute = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;

// ReSharper restore All

#endregion

#pragma warning restore SA1512,SA1124 // Single-line comments should not be followed by blank line

namespace NugetForUnity.PackageSource
{
    /// <summary>
    ///     API client for NuGet API v3.
    /// </summary>
    [Serializable]
    internal sealed class NugetApiClientV3 : IDisposable
    {
        [NonSerialized]
        [NotNull]
        private readonly Uri apiIndexJsonUrl;

        [NonSerialized]
        [NotNull]
        private readonly HttpClient httpClient;

        [NonSerialized]
        [CanBeNull]
        private TaskCompletionSource<bool> initializationTaskCompletionSource;

        // Example: https://api.nuget.org/v3-flatcontainer/
        [CanBeNull]
        [SerializeField]
        private string packageBaseAddress;

        // Example: https://api.nuget.org/v3-flatcontainer/{0}/{1}/{0}.{1}.nupkg
        [CanBeNull]
        [SerializeField]
        private string packageDownloadUrlTemplate;

        // Example: https://api.nuget.org/v3/registration5-gz-semver2/
        [CanBeNull]
        [SerializeField]
        private string registrationsBaseUrl;

        [CanBeNull]
        [SerializeField]
        private List<string> searchQueryServices;

        /// <summary>
        ///     Initializes a new instance of the <see cref="NugetApiClientV3" /> class.
        /// </summary>
        /// <param name="url">The absolute 'index.json' URL of the API.</param>
        public NugetApiClientV3([NotNull] string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException($"'{nameof(url)}' cannot be null or whitespace.", nameof(url));
            }

            apiIndexJsonUrl = new Uri(url);

            var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate };
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                // On Windows, Mono HttpClient does not automatically pick up proxy settings.
                handler.Proxy = WebRequest.GetSystemWebProxy();
            }

            httpClient = new HttpClient(handler);

            InitializeFromSessionState();
        }

        /// <summary>
        ///     Gets or sets a optional overwrite for the URL used to download '.nupkg' files (see: <see cref="DownloadNupkgToFileAsync" />).
        /// </summary>
        [CanBeNull]
        [field: SerializeField]
        public string PackageDownloadUrlTemplateOverwrite { get; set; }

        /// <inheritdoc />
        public void Dispose()
        {
            httpClient.Dispose();
        }

        /// <summary>
        ///     Searches for NuGet packages available on the server.
        /// </summary>
        /// <remarks>
        ///     https://github.com/NuGet/docs.microsoft.com-nuget/blob/live/docs/api/search-query-service-resource.md.
        /// </remarks>
        /// <param name="packageSource">The package source that owns this client.</param>
        /// <param name="searchQuery">
        ///     The search query. See https://learn.microsoft.com/en-us/nuget/api/search-query-service-resource#request-parameters.
        /// </param>
        /// <param name="skip">
        ///     The number of results to skip. See https://learn.microsoft.com/en-us/nuget/api/search-query-service-resource#request-parameters.
        /// </param>
        /// <param name="take">
        ///     The number of results to return. See https://learn.microsoft.com/en-us/nuget/api/search-query-service-resource#request-parameters.
        /// </param>
        /// <param name="includePreRelease">
        ///     Whether to include pre-release packages. See https://learn.microsoft.com/en-us/nuget/api/search-query-service-resource#request-parameters.
        /// </param>
        /// <param name="cancellationToken">
        ///     Token to cancel the HTTP request.
        /// </param>
        /// <returns>
        ///     A list of <see cref="INugetPackage" />s that match the search query.
        /// </returns>
        public async Task<List<INugetPackage>> SearchPackageAsync(
            NugetPackageSourceV3 packageSource,
            string searchQuery = "",
            int skip = -1,
            int take = -1,
            bool includePreRelease = false,
            CancellationToken cancellationToken = default)
        {
            var successfullyInitialized = await EnsureInitializedAsync(packageSource);
            if (!successfullyInitialized || searchQueryServices == null)
            {
                return new List<INugetPackage>();
            }

            if (searchQueryServices.Count == 0)
            {
                Debug.LogError($"There are no {nameof(searchQueryServices)} specified in the API '{apiIndexJsonUrl}' so we can't search.");
                return new List<INugetPackage>();
            }

            var queryBuilder = new QueryBuilder();

            // so both SemVer 1.0.0 and SemVer 2.0.0 compatible packages are returned
            queryBuilder.Add("semVerLevel", "2.0.0");
            queryBuilder.Add("q", searchQuery);

            if (skip > 0)
            {
                queryBuilder.Add("skip", skip.ToString(CultureInfo.InvariantCulture));
            }

            if (take > 0)
            {
                queryBuilder.Add("take", take.ToString(CultureInfo.InvariantCulture));
            }

            if (includePreRelease)
            {
                queryBuilder.Add("prerelease", "true");
            }

            var query = queryBuilder.ToString();
            var queryService = searchQueryServices[0];
            var responseString = await GetStringFromServerAsync(packageSource, queryService + query, cancellationToken).ConfigureAwait(false);
            var searchResult = JsonUtility.FromJson<SearchResult>(responseString);
            var results = searchResult.data ?? throw new InvalidOperationException($"missing 'data' property in search response:\n{responseString}");
            return SearchResultToNugetPackages(results, packageSource);
        }

        /// <summary>
        ///     Download the .nupkg file and store it inside a file at <paramref name="outputFilePath" />.
        /// </summary>
        /// <param name="packageSource">The package source that owns this client.</param>
        /// <param name="package">The package to download its .nupkg from.</param>
        /// <param name="outputFilePath">Path where the downloaded file is placed.</param>
        /// <param name="downloadUrlHint">Hint for the url used to download the .nupkg file from.</param>
        /// <returns>The async task.</returns>
        [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "We intentionally use lower case.")]
        public async Task DownloadNupkgToFileAsync(
            NugetPackageSourceV3 packageSource,
            INugetPackageIdentifier package,
            string outputFilePath,
            string downloadUrlHint)
        {
            string downloadUrl;
            if (!string.IsNullOrEmpty(downloadUrlHint))
            {
                downloadUrl = downloadUrlHint;
            }
            else
            {
                var successfullyInitialized = await EnsureInitializedAsync(packageSource);
                if (!successfullyInitialized)
                {
                    return;
                }

                if (string.IsNullOrEmpty(packageDownloadUrlTemplate))
                {
                    Debug.LogError(
                        $"There are no {nameof(packageBaseAddress)} specified in the API '{apiIndexJsonUrl}' so we can't download packages.");
                    return;
                }

                var version = package.Version.ToLowerInvariant();
                var id = package.Id.ToLowerInvariant();
                downloadUrl = string.Format(packageDownloadUrlTemplate, id, version);
            }

            using (var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl))
            {
                AddHeadersToRequest(request, packageSource, false);
                using (var response = await httpClient.SendAsync(request).ConfigureAwait(false))
                {
                    await EnsureResponseIsSuccessAsync(response).ConfigureAwait(false);
                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    {
                        using (var fileStream = File.Create(outputFilePath))
                        {
                            await stream.CopyToAsync(fileStream).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     Fetches a single NuGet package from the package registration. Other than <see cref="GetPackageWithDetailsAsync" /> this also fetches a list of
        ///     all available versions.
        /// </summary>
        /// <param name="packageSource">The package source that owns this client.</param>
        /// <param name="package">The package identifier to receive including the details.</param>
        /// <param name="cancellationToken">Token to cancel the HTTP request.</param>
        /// <returns>The package or null if we didn't find it.</returns>
        public async Task<NugetPackageV3> GetPackageWithAllVersionsAsync(
            NugetPackageSourceV3 packageSource,
            INugetPackageIdentifier package,
            CancellationToken cancellationToken = default)
        {
            var registrationItems = await GetRegistrationPageItemsAsync(packageSource, package, cancellationToken);
            if (registrationItems is null)
            {
                return null;
            }

            var versions = new List<NugetPackageVersion>();
            RegistrationLeafObject latestVersionItem = null;
            NugetPackageVersion latestVersion = null;
            foreach (var item in registrationItems)
            {
                if (item.items is null || item.items.Count == 0)
                {
                    item.items = await GetRegistrationPageLeafItems(packageSource, item, cancellationToken).ConfigureAwait(false);
                }

                foreach (var leafObject in item.items)
                {
                    var catalogEntry = leafObject.CatalogEntry;

                    if (catalogEntry.version is null)
                    {
                        throw new InvalidOperationException(
                            $"missing '{nameof(catalogEntry.version)}' property in catalog entry:\n{JsonUtility.ToJson(catalogEntry)}");
                    }

                    var version = new NugetPackageVersion(catalogEntry.version);
                    versions.Add(version);
                    if (latestVersion != null && version <= latestVersion)
                    {
                        continue;
                    }

                    latestVersion = version;
                    latestVersionItem = leafObject;
                }
            }

            versions.Sort((v1, v2) => v2.CompareTo(v1));
            return CreatePackageFromRegistrationLeaf(packageSource, latestVersionItem, versions);
        }

        /// <summary>
        ///     Gets a single NuGet package including the package details <see cref="GetPackageDetailsAsync" /> but not containing all available versions.
        /// </summary>
        /// <param name="packageSource">The package source that owns this client.</param>
        /// <param name="package">The package identifier to receive including the details.</param>
        /// <param name="cancellationToken">Token to cancel the HTTP request.</param>
        /// <returns>The package or null if we didn't find it.</returns>
        [ItemCanBeNull]
        public async Task<NugetPackageV3> GetPackageWithDetailsAsync(
            NugetPackageSourceV3 packageSource,
            INugetPackageIdentifier package,
            CancellationToken cancellationToken = default)
        {
            var leafItem = await GetPackageRegistrationLeafAsync(packageSource, package, cancellationToken).ConfigureAwait(false);
            if (leafItem is null)
            {
                return null;
            }

            return CreatePackageFromRegistrationLeaf(packageSource, leafItem);
        }

        /// <summary>
        ///     Fetches the package details from the server.
        /// </summary>
        /// <param name="packageSource">The package source that owns this client.</param>
        /// <param name="package">The package to receive the details for.</param>
        /// <param name="cancellationToken">
        ///     Token to cancel the request.
        /// </param>
        /// <returns>
        ///     The package details or null if the package is not found.
        /// </returns>
        [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "API uses lower case.")]
        [NotNull]
        [ItemNotNull]
        public async Task<List<NugetFrameworkGroup>> GetPackageDetailsAsync(
            NugetPackageSourceV3 packageSource,
            INugetPackageIdentifier package,
            CancellationToken cancellationToken = default)
        {
            var leafItem = await GetPackageRegistrationLeafAsync(packageSource, package, cancellationToken).ConfigureAwait(false);
            return leafItem is null ? new List<NugetFrameworkGroup>() : ConvertDependencyGroups(leafItem.CatalogEntry);
        }

        private static NugetPackageV3 CreatePackageFromRegistrationLeaf(
            NugetPackageSourceV3 packageSource,
            RegistrationLeafObject leafItem,
            List<NugetPackageVersion> allVersions = null)
        {
            var entry = leafItem.CatalogEntry;
            if (entry.id is null)
            {
                throw new InvalidOperationException($"missing '{nameof(entry.id)}' property in catalog entry:\n{JsonUtility.ToJson(entry)}");
            }

            if (entry.version is null)
            {
                throw new InvalidOperationException($"missing '{nameof(entry.version)}' property in catalog entry:\n{JsonUtility.ToJson(entry)}");
            }

            return new NugetPackageV3(
                entry.id,
                entry.version,
                new List<string> { entry.authors },
                entry.description,
                0,
                entry.licenseUrl,
                packageSource,
                entry.projectUrl,
                entry.summary,
                entry.title,
                entry.iconUrl,
                allVersions ?? new List<NugetPackageVersion> { new NugetPackageVersion(entry.version) })
            {
                DownloadUrl = leafItem.packageContent, Dependencies = ConvertDependencyGroups(entry),
            };
        }

        private static List<NugetFrameworkGroup> ConvertDependencyGroups(CatalogEntry entry)
        {
            if (entry.dependencyGroups != null)
            {
                return entry.dependencyGroups.ConvertAll(
                    dependencyGroup =>
                    {
                        var dependencies = dependencyGroup.dependencies is null ?
                            new List<INugetPackageIdentifier>() :
                            dependencyGroup.dependencies.ConvertAll(
                                dependency => (INugetPackageIdentifier)new NugetPackageIdentifier(
                                    dependency.id ??
                                    throw new InvalidOperationException(
                                        $"missing '{nameof(dependency.id)}' inside '{nameof(dependencyGroup.dependencies)}' for dependency group: '{JsonUtility.ToJson(dependencyGroup)}'"),
                                    dependency.range));

                        return new NugetFrameworkGroup { Dependencies = dependencies, TargetFramework = dependencyGroup.targetFramework };
                    });
            }

            if (ConfigurationManager.IsVerboseLoggingEnabled)
            {
                NugetLogger.LogVerbose(
                    "missing '{0}.{1}' property for CatalogEntry: '{2}'",
                    nameof(CatalogEntry),
                    nameof(CatalogEntry.dependencyGroups),
                    JsonUtility.ToJson(entry));
            }

            return new List<NugetFrameworkGroup>();
        }

        private static List<INugetPackage> SearchResultToNugetPackages(List<SearchResultItem> searchResults, NugetPackageSourceV3 packageSource)
        {
            var packages = new List<INugetPackage>(searchResults.Count);
            foreach (var item in searchResults)
            {
                if (item.versions is null)
                {
                    throw new InvalidOperationException(
                        $"missing '{nameof(item.versions)}' property in search result item:\n{JsonUtility.ToJson(item)}");
                }

                if (item.id is null)
                {
                    throw new InvalidOperationException($"missing '{nameof(item.id)}' property in search result item:\n{JsonUtility.ToJson(item)}");
                }

                if (item.version is null)
                {
                    throw new InvalidOperationException(
                        $"missing '{nameof(item.version)}' property in search result item:\n{JsonUtility.ToJson(item)}");
                }

                var versions = item.versions.ConvertAll(searchVersion => new NugetPackageVersion(searchVersion.version));
                versions.Sort((v1, v2) => v2.CompareTo(v1));
                packages.Add(
                    new NugetPackageV3(
                        item.id,
                        item.version,
                        item.authors ?? new List<string>(),
                        item.description,
                        item.totalDownloads,
                        item.licenseUrl,
                        packageSource,
                        item.projectUrl,
                        item.summary,
                        item.title,
                        item.iconUrl,
                        versions));
            }

            return packages;
        }

        private static async Task EnsureResponseIsSuccessAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new HttpRequestException(
                $"The request to '{response.RequestMessage.RequestUri}' failed with status code '{response.StatusCode}' and message: {responseString}");
        }

        [ItemCanBeNull]
        private async Task<RegistrationLeafObject> GetPackageRegistrationLeafAsync(
            NugetPackageSourceV3 packageSource,
            INugetPackageIdentifier package,
            CancellationToken cancellationToken = default)
        {
            var registrationItems = await GetRegistrationPageItemsAsync(packageSource, package, cancellationToken);
            if (registrationItems is null)
            {
                return null;
            }

            var getLatestVersion = string.IsNullOrEmpty(package.Version);
            var pageItem = getLatestVersion ?
                registrationItems.OrderByDescending(registrationItem => new NugetPackageVersion(registrationItem.lower)).First() :
                registrationItems.Find(
                    registrationItem =>
                        new NugetPackageVersion($"[{registrationItem.lower},{registrationItem.upper}]").InRange(package.PackageVersion));
            if (pageItem is null)
            {
                Debug.LogError($"There is no package with id '{package.Id}' and version '{package.Version}' on the registration page.");
                return null;
            }

            if (pageItem.items is null || pageItem.items.Count == 0)
            {
                pageItem.items = await GetRegistrationPageLeafItems(packageSource, pageItem, cancellationToken).ConfigureAwait(false);
            }

            var leafItem = getLatestVersion ?
                pageItem.items.OrderByDescending(registrationLeaf => new NugetPackageVersion(registrationLeaf.CatalogEntry.version))
                    .FirstOrDefault() :
                pageItem.items.Find(leaf => package.PackageVersion.InRange(new NugetPackageVersion(leaf.CatalogEntry.version)));
            if (leafItem is null)
            {
                Debug.LogError(
                    $"There is no package with id '{package.Id}' and version '{package.PackageVersion}' in the registration page with the matching version rage: {pageItem.lower} to {pageItem.upper} '{pageItem.atId}'.");
                return null;
            }

            return leafItem;
        }

        [ItemNotNull]
        private async Task<List<RegistrationLeafObject>> GetRegistrationPageLeafItems(
            NugetPackageSourceV3 packageSource,
            RegistrationPageObject item,
            CancellationToken cancellationToken)
        {
            // If the items property is not present in the registration page object,
            // the URL specified in the @id must be used to fetch metadata about individual package versions.
            // The items array is sometimes excluded from the page object as an optimization.
            // If the number of versions of a single package ID is very large, then the registration index document will be massive and wasteful
            // to process for a client that only cares about a specific version or small range of versions.
            var itemAtId = item.atId ?? throw new InvalidOperationException($"missing '@id' for item inside item:\n{JsonUtility.ToJson(item)}");
            var registrationPageString = await GetStringFromServerAsync(packageSource, itemAtId, cancellationToken).ConfigureAwait(false);
            var registrationPage = JsonUtility.FromJson<RegistrationPageObject>(registrationPageString);
            return registrationPage.items ??
                   throw new InvalidOperationException(
                       $"missing 'items' property inside page request for URL: {itemAtId}, response:\n{registrationPageString}");
        }

        private async Task<List<RegistrationPageObject>> GetRegistrationPageItemsAsync(
            NugetPackageSourceV3 packageSource,
            INugetPackageIdentifier package,
            CancellationToken cancellationToken)
        {
            var successfullyInitialized = await EnsureInitializedAsync(packageSource);
            if (!successfullyInitialized)
            {
                return null;
            }

            if (string.IsNullOrEmpty(registrationsBaseUrl))
            {
                Debug.LogError(
                    $"There are no {nameof(registrationsBaseUrl)} specified in the API '{apiIndexJsonUrl}' so we can't receive package details.");
                return null;
            }

            string responseString;
            try
            {
                responseString = await GetStringFromServerAsync(
                        packageSource,
                        $"{registrationsBaseUrl}{package.Id.ToLowerInvariant()}/index.json",
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException exception)
            {
                Debug.LogWarning($"Failed to get '{package.Id}' from the package source '{apiIndexJsonUrl}'. Error: {exception}");
                return null;
            }

            var registrationResponse = JsonUtility.FromJson<RegistrationResponse>(responseString.Replace(@"""@id"":", @"""atId"":"));

            // without a version specified, the latest version is returned
            var registrationItems = registrationResponse.items ??
                                    throw new InvalidOperationException(
                                        $"missing 'items' property inside registration request for package: {package.Id}, response:\n{responseString}");
            return registrationItems;
        }

        private void InitializeFromSessionState()
        {
            var sessionState = SessionState.GetString(GetSessionStateKey(), string.Empty);
            if (string.IsNullOrEmpty(sessionState))
            {
                return;
            }

            JsonUtility.FromJsonOverwrite(sessionState, this);
        }

        private void SaveToSessionState()
        {
            UnityMainThreadDispatcher.Dispatch(() => SessionState.SetString(GetSessionStateKey(), JsonUtility.ToJson(this)));
        }

        private string GetSessionStateKey()
        {
            return $"{nameof(NugetApiClientV3)}:{apiIndexJsonUrl}";
        }

        private async Task<bool> EnsureInitializedAsync(NugetPackageSourceV3 packageSource)
        {
            if (searchQueryServices != null)
            {
                return true;
            }

            try
            {
                var successful = await AwaitableInitializeApiAddressesAsync(packageSource);
                return successful;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Initialization of api client for '{apiIndexJsonUrl}' failed so we can't use it. error (cached): {exception}.");
                return false;
            }
        }

        private Task<bool> AwaitableInitializeApiAddressesAsync(NugetPackageSourceV3 packageSource)
        {
            if (initializationTaskCompletionSource != null)
            {
                return initializationTaskCompletionSource.Task;
            }

            lock (httpClient)
            {
                if (initializationTaskCompletionSource != null)
                {
                    return initializationTaskCompletionSource.Task;
                }

                initializationTaskCompletionSource = new TaskCompletionSource<bool>();
            }

            _ = InitializeApiAddressesAsync(packageSource);

            return initializationTaskCompletionSource.Task;
        }

        private async Task InitializeApiAddressesAsync(NugetPackageSourceV3 packageSource)
        {
            Debug.Assert(initializationTaskCompletionSource != null, "initializationTaskCompletionSource != null");
            try
            {
                var responseString = await GetStringFromServerAsync(packageSource, apiIndexJsonUrl.AbsoluteUri, CancellationToken.None)
                    .ConfigureAwait(false);
                var resourceList = JsonUtility.FromJson<IndexResponse>(
                    responseString.Replace(@"""@id"":", @"""atId"":").Replace(@"""@type"":", @"""atType"":"));
                var foundSearchQueryServices = new List<string>();
                var resources = resourceList.resources ??
                                throw new InvalidOperationException(
                                    $"missing '{nameof(resourceList.resources)}' property inside index response:\n{responseString}");

                // we only support v3 so if v4 is released we skip it.
                var maxSupportedApiVersion = new NugetPackageVersion("4.0.0");
                NugetPackageVersion highestPackageBaseAddressApiVersion = null;
                NugetPackageVersion highestRegistrationsBaseUrlApiVersion = null;
                NugetPackageVersion highestSearchQueryServiceApiVersion = null;
                foreach (var resource in resources)
                {
                    var resourceAtId = resource.atId ??
                                       throw new InvalidOperationException($"missing '@id' property inside resource of type '{resource.atType}'");
                    var resourceAtType = resource.atType ??
                                         throw new InvalidOperationException($"missing '@type' property inside resource with id '{resource.atId}'");

                    var resourceTypeParts = resourceAtType.Split('/');
                    NugetPackageVersion resourceTypeVersion = null;

                    // need to skip if version is no number like in: 'RegistrationsBaseUrl/Versioned'
                    if (resourceTypeParts.Length > 1 && !string.IsNullOrEmpty(resourceTypeParts[1]) && char.IsDigit(resourceTypeParts[1][0]))
                    {
                        resourceTypeVersion = new NugetPackageVersion(resourceTypeParts[1]);
                    }

                    switch (resourceTypeParts[0])
                    {
                        case "SearchQueryService":
                            if (highestSearchQueryServiceApiVersion == null ||
                                (resourceTypeVersion > highestSearchQueryServiceApiVersion && resourceTypeVersion < maxSupportedApiVersion))
                            {
                                highestSearchQueryServiceApiVersion = resourceTypeVersion;
                                var comment = resource.comment ?? string.Empty;
                                if (comment.IndexOf("(primary)", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    foundSearchQueryServices.Insert(0, resourceAtId.Trim('/'));
                                }
                                else
                                {
                                    foundSearchQueryServices.Add(resourceAtId.Trim('/'));
                                }
                            }

                            break;
                        case "PackageBaseAddress":
                            if (highestPackageBaseAddressApiVersion == null ||
                                (resourceTypeVersion > highestPackageBaseAddressApiVersion && resourceTypeVersion < maxSupportedApiVersion))
                            {
                                highestPackageBaseAddressApiVersion = resourceTypeVersion;
                                packageBaseAddress = resourceAtId.Trim('/') + '/';
                            }

                            break;
                        case "RegistrationsBaseUrl":
                            if (highestRegistrationsBaseUrlApiVersion == null ||
                                (resourceTypeVersion > highestRegistrationsBaseUrlApiVersion && resourceTypeVersion < maxSupportedApiVersion))
                            {
                                highestRegistrationsBaseUrlApiVersion = resourceTypeVersion;
                                registrationsBaseUrl = resourceAtId.Trim('/') + '/';
                            }

                            break;
                    }
                }

                if (!string.IsNullOrEmpty(PackageDownloadUrlTemplateOverwrite))
                {
                    packageDownloadUrlTemplate = PackageDownloadUrlTemplateOverwrite;
                }
                else if (string.IsNullOrEmpty(packageBaseAddress))
                {
                    UnityMainThreadDispatcher.Dispatch(
                        () =>
                        {
                            var displayDialog = EditorUtility.DisplayDialog(
                                "Missing 'PackageBaseAddress' endpoint",
                                $"The used NuGet V3 API '{apiIndexJsonUrl}' doesn't support the 'PackageBaseAddress' endpoint. You need to provide the url manually by specifying '{NugetConfigFile.PackageDownloadUrlTemplateOverwriteAttributeName}' on the package source. If this is a NuGet source provided by Artifactory you can click on 'Configure for Artifactory' to configure it.",
                                "Configure for Artifactory",
                                "Cancel");
                            if (displayDialog)
                            {
                                packageDownloadUrlTemplate = $"{registrationsBaseUrl}Download/{{0}}/{{1}}";
                                PackageDownloadUrlTemplateOverwrite = packageDownloadUrlTemplate;

                                // Artifactory somehow can't handle search queries containing multiple packageId's.
                                packageSource.UpdateSearchBatchSize = 1;
                                SaveToSessionState();
                                ConfigurationManager.NugetConfigFile.Save(ConfigurationManager.NugetConfigFilePath);
                            }
                            else
                            {
                                Debug.LogErrorFormat(
                                    "The NuGet package source at '{0}' has no PackageBaseAddress resource defined, please specify it manually by adding the '{1}' attribute on the package-source configuration inside the '{2}' file.",
                                    apiIndexJsonUrl,
                                    NugetConfigFile.PackageDownloadUrlTemplateOverwriteAttributeName,
                                    NugetConfigFile.FileName);
                            }
                        });
                }
                else
                {
                    packageDownloadUrlTemplate = $"{packageBaseAddress}{{0}}/{{1}}/{{0}}.{{1}}.nupkg";
                    PackageDownloadUrlTemplateOverwrite = null;
                }

                searchQueryServices = foundSearchQueryServices;
                SaveToSessionState();
                var successful = searchQueryServices.Count > 0;
                initializationTaskCompletionSource.SetResult(successful);
            }
            catch (Exception exception)
            {
                Debug.LogErrorFormat("Failed to initialize the NuGet package source '{0}'. Error: {1}", apiIndexJsonUrl, exception);
                initializationTaskCompletionSource.SetException(exception);
            }
        }

        private async Task<string> GetStringFromServerAsync(NugetPackageSourceV3 packageSource, string url, CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                AddHeadersToRequest(request, packageSource, true);

                using (var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    await EnsureResponseIsSuccessAsync(response).ConfigureAwait(false);
                    var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false) ?? string.Empty;
                    return responseString;
                }
            }
        }

        private void AddHeadersToRequest(HttpRequestMessage request, NugetPackageSourceV3 packageSource, bool expectJsonResponse)
        {
            if (expectJsonResponse)
            {
                request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json"));
            }

            request.Headers.Add("User-Agent", "NuGetForUnity");
            var password = packageSource.ExpandedPassword;
            var userName = packageSource.UserName;

            if (string.IsNullOrEmpty(password))
            {
                var creds = CredentialProviderHelper.GetCredentialFromProvider(apiIndexJsonUrl);
                if (creds.HasValue)
                {
                    userName = creds.Value.UserName;
                    password = creds.Value.Password;
                }
            }

            if (!string.IsNullOrEmpty(password))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(Encoding.ASCII.GetBytes($"{userName}:{password}")));
            }
        }

        private sealed class QueryBuilder
        {
            private readonly StringBuilder builder = new StringBuilder();

            public void Add(string parameterName, string parameterValue)
            {
                if (string.IsNullOrEmpty(parameterValue))
                {
                    return;
                }

                builder.Append(builder.Length == 0 ? '?' : '&');
                builder.Append(parameterName);
                builder.Append('=');
                builder.Append(Uri.EscapeDataString(parameterValue));
            }

            public override string ToString()
            {
                return builder.ToString();
            }
        }

        // ReSharper disable InconsistentNaming
        // ReSharper disable UnassignedField.Local
        // ReSharper disable UnusedMember.Local
        // ReSharper disable NotAccessedField.Local
#pragma warning disable CS0649 // Field is assigned on serialize
#pragma warning disable CA1051 // Do not declare visible instance fields
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
#pragma warning disable SA1401 // Fields should be private
#pragma warning disable S1144 // Unused private types or members should be removed

        [Serializable]
        private sealed class IndexResponse
        {
            [CanBeNull]
            public List<Resource> resources;

            [CanBeNull]
            public string version;
        }

        [Serializable]
        private sealed class Resource
        {
            [CanBeNull]
            public string atId;

            [CanBeNull]
            public string atType;

            [CanBeNull]
            public string clientVersion;

            [CanBeNull]
            public string comment;
        }

        [Serializable]
        private sealed class RegistrationResponse
        {
            /// <summary>
            ///     The number of registration pages in the index.
            /// </summary>
            public int count;

            /// <summary>
            ///     The array of registration pages.
            /// </summary>
            [CanBeNull]
            public List<RegistrationPageObject> items;
        }

        [Serializable]
        private sealed class RegistrationPageObject
        {
            /// <summary>
            ///     The URL to the registration page.
            ///     If the items property is not present in the registration page object, the URL specified in the @id must be used to fetch metadata about
            ///     individual package versions.
            /// </summary>
            [CanBeNull]
            public string atId;

            /// <summary>
            ///     The number of registration leaves in the page.
            /// </summary>
            public int count;

            /// <summary>
            ///     The array of registration leaves and their associate metadata.
            /// </summary>
            [CanBeNull]
            public List<RegistrationLeafObject> items;

            /// <summary>
            ///     The lowest SemVer 2.0.0 version in the page (inclusive).
            /// </summary>
            [CanBeNull]
            public string lower;

            /// <summary>
            ///     The highest SemVer 2.0.0 version in the page (inclusive).
            /// </summary>
            [CanBeNull]
            public string upper;
        }

        [Serializable]
        private sealed class RegistrationLeafObject
        {
            /// <summary>
            ///     The URL to the registration leaf.
            /// </summary>
            [CanBeNull]
            public string atId;

            /// <summary>
            ///     The catalog entry containing the package metadata.
            /// </summary>
            [CanBeNull]
            public CatalogEntry catalogEntry;

            /// <summary>
            ///     The URL to the package content (.nupkg).
            /// </summary>
            [CanBeNull]
            public string packageContent;

            public CatalogEntry CatalogEntry =>
                catalogEntry ?? throw new InvalidOperationException($"missing '{nameof(catalogEntry)}' property in registration leaf '{atId}'.");
        }

        // Removed property because it has a 'string or array of strings' representation and therefor causes parse errors when using the CLI: 'tags'
        [Serializable]
        private sealed class CatalogEntry
        {
            /// <summary>
            ///     The URL to the document used to produce this object.
            /// </summary>
            [CanBeNull]
            public string atId;

            [CanBeNull]
            public string authors;

            /// <summary>
            ///     The dependencies of the package, grouped by target framework.
            /// </summary>
            [CanBeNull]
            public List<DependencyGroup> dependencyGroups;

            /// <summary>
            ///     The deprecation associated with the package.
            /// </summary>
            [CanBeNull]
            public Deprecation deprecation;

            [CanBeNull]
            public string description;

            [CanBeNull]
            public string iconUrl;

            /// <summary>
            ///     The ID of the package.
            /// </summary>
            [CanBeNull]
            public string id;

            [CanBeNull]
            public string language;

            [CanBeNull]
            public string licenseExpression;

            [CanBeNull]
            public string licenseUrl;

            /// <summary>
            ///     Should be considered as listed if absent.
            /// </summary>
            public bool listed = true;

            [CanBeNull]
            public string minClientVersion;

            [CanBeNull]
            public string projectUrl;

            /// <summary>
            ///     A string containing a ISO 8601 time-stamp of when the package was published.
            /// </summary>
            [CanBeNull]
            public string published;

            /// <summary>
            ///     A URL for the rendered (HTML web page) view of the package README.
            /// </summary>
            [CanBeNull]
            public string readmeUrl;

            public bool requireLicenseAcceptance;

            [CanBeNull]
            public string summary;

            [CanBeNull]
            public string title;

            /// <summary>
            ///     The full version string after normalization.
            /// </summary>
            [CanBeNull]
            public string version;

            /// <summary>
            ///     The security vulnerabilities of the package.
            /// </summary>
            [CanBeNull]
            public List<Vulnerability> vulnerabilities;
        }

        [Serializable]
        private sealed class DependencyGroup
        {
            [CanBeNull]
            public List<Dependency> dependencies;

            /// <summary>
            ///     The target framework that these dependencies are applicable to.
            /// </summary>
            public string targetFramework = string.Empty;
        }

        [Serializable]
        private sealed class Dependency
        {
            /// <summary>
            ///     The ID of the package dependency.
            /// </summary>
            [CanBeNull]
            public string id;

            /// <summary>
            ///     The allowed version range of the dependency.
            /// </summary>
            [CanBeNull]
            public string range;

            /// <summary>
            ///     The URL to the registration index for this dependency.
            /// </summary>
            [CanBeNull]
            public string registration;
        }

        [Serializable]
        private sealed class Deprecation
        {
            /// <summary>
            ///     The additional details about this deprecation.
            /// </summary>
            [CanBeNull]
            public string message;

            /// <summary>
            ///     The reasons why the package was deprecated.
            /// </summary>
            [CanBeNull]
            public List<string> reasons;
        }

        [Serializable]
        private sealed class Vulnerability
        {
            /// <summary>
            ///     Location of security advisory for the package.
            /// </summary>
            [CanBeNull]
            public string advisoryUrl;

            /// <summary>
            ///     Severity of advisory: "0" = Low, "1" = Moderate, "2" = High, "3" = Critical.
            /// </summary>
            [CanBeNull]
            public string severity;
        }

        [Serializable]
        private sealed class SearchResult
        {
            /// <summary>
            ///     The search results matched by the request.
            /// </summary>
            [CanBeNull]
            public List<SearchResultItem> data;

            /// <summary>
            ///     The total number of matches, disregarding skip and take.
            /// </summary>
            public int totalHits;
        }

        [Serializable]
        private sealed class SearchResultItem
        {
            [CanBeNull]
            public List<string> authors;

            [CanBeNull]
            public string description;

            [CanBeNull]
            public string iconUrl;

            /// <summary>
            ///     The ID of the matched package.
            /// </summary>
            [CanBeNull]
            public string id;

            [CanBeNull]
            public string licenseUrl;

            [CanBeNull]
            public List<string> owners;

            [CanBeNull]
            public string projectUrl;

            /// <summary>
            ///     The absolute URL to the associated registration index.
            /// </summary>
            [CanBeNull]
            public string registration;

            [CanBeNull]
            public string summary;

            [CanBeNull]
            public List<string> tags;

            [CanBeNull]
            public string title;

            /// <summary>
            ///     This value can be inferred by the sum of downloads in the versions array.
            /// </summary>
            public long totalDownloads;

            /// <summary>
            ///     A JSON boolean indicating whether the package is verified.
            /// </summary>
            public bool verified;

            /// <summary>
            ///     The full SemVer 2.0.0 version string of the package (could contain build metadata).
            /// </summary>
            [CanBeNull]
            public string version;

            /// <summary>
            ///     All of the versions of the package considering the prerelease parameter.
            /// </summary>
            [CanBeNull]
            public List<SearchResultVersion> versions;
        }

        [Serializable]
        private sealed class SearchResultVersion
        {
            /// <summary>
            ///     The number of downloads for this specific package version.
            /// </summary>
            public long downloads;

            /// <summary>
            ///     The full SemVer 2.0.0 version string of the package (could contain build metadata).
            /// </summary>
            [CanBeNull]
            public string version;
        }

        // ReSharper restore InconsistentNaming
        // ReSharper restore UnassignedField.Local
        // ReSharper restore UnusedMember.Local
        // ReSharper restore NotAccessedField.Local
#pragma warning restore CS0649 // Field is assigned on serialize
#pragma warning restore CA1051 // Do not declare visible instance fields
#pragma warning restore SA1307 // Accessible fields should begin with upper-case letter
#pragma warning restore SA1401 // Fields should be private
#pragma warning restore S1144 // Unused private types or members should be removed
    }
}
