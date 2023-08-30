using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NugetForUnity.Helper;
using NugetForUnity.Models;
using UnityEditor;
using UnityEngine;

namespace NugetForUnity.PackageSource
{
    /// <summary>
    ///     API client for NuGet API v3.
    /// </summary>
    internal sealed class NugetApiClientV3 : IDisposable
    {
        private readonly Uri apiIndexJsonUrl;

        private readonly HttpClient httpClient = new HttpClient(
            new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate });

        // Example: https://api.nuget.org/v3-flatcontainer/
        [SerializeField]
        private string packageBaseAddress;

        // Example: https://api.nuget.org/v3/registration5-gz-semver2/
        [SerializeField]
        private string registrationsBaseUrl;

        [SerializeField]
        private List<string> searchQueryServices;

        /// <summary>
        ///     Initializes a new instance of the <see cref="NugetApiClientV3" /> class.
        /// </summary>
        /// <param name="url">The absolute 'index.json' URL of the API.</param>
        /// <param name="packageSource">The package source that owns this client.</param>
        public NugetApiClientV3(string url, NugetPackageSourceV3 packageSource)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException($"'{nameof(url)}' cannot be null or whitespace.", nameof(url));
            }

            if (packageSource is null)
            {
                throw new ArgumentNullException(nameof(packageSource));
            }

            apiIndexJsonUrl = new Uri(url);

            if (!InitializeFromSessionState())
            {
                InitializeApiAddresses(packageSource);
            }
        }

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
        public async Task<List<INugetPackage>> SearchPackage(
            NugetPackageSourceV3 packageSource,
            string searchQuery = "",
            int skip = -1,
            int take = -1,
            bool includePreRelease = false,
            CancellationToken cancellationToken = default)
        {
            while (searchQueryServices == null)
            {
                // waiting for InitializeApiAddresses to complete
                await Task.Yield();
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
            foreach (var queryService in searchQueryServices)
            {
                var responseString = await GetStringFromServerAsync(packageSource, queryService + query, cancellationToken).ConfigureAwait(false);
                var searchResult = JsonUtility.FromJson<SearchResult>(responseString);
                return SearchResultToNugetPackages(searchResult.data, packageSource);
            }

            Debug.LogError($"There are no {nameof(searchQueryServices)} specified in the API '{apiIndexJsonUrl}' so we can't search.");
            return new List<INugetPackage>();
        }

        /// <summary>
        ///     Download the .nupkg file and store it inside a file at <paramref name="outputFilePath" />.
        /// </summary>
        /// <param name="packageSource">The package source that owns this client.</param>
        /// <param name="package">The package to download its .nupkg from.</param>
        /// <param name="outputFilePath">Path where the downloaded file is placed.</param>
        /// <returns>The async task.</returns>
        public async Task DownloadNupkgToFile(NugetPackageSourceV3 packageSource, INugetPackageIdentifier package, string outputFilePath)
        {
            var version = package.Version.ToLowerInvariant();
            var id = package.Id.ToLowerInvariant();
            using (var request = new HttpRequestMessage(HttpMethod.Get, $"{packageBaseAddress}{id}/{version}/{id}.{version}.nupkg"))
            {
                AddHeadersToRequest(request, packageSource);
                using (var response = await httpClient.SendAsync(request).ConfigureAwait(false))
                {
                    await EnsureResponseIsSuccess(response).ConfigureAwait(false);
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
        public async Task<List<NugetFrameworkGroup>> GetPackageDetails(
            NugetPackageSourceV3 packageSource,
            INugetPackageIdentifier package,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(registrationsBaseUrl))
            {
                Debug.LogError(
                    $"There are no {nameof(registrationsBaseUrl)} specified in the API '{apiIndexJsonUrl}' so we can't receive package details.");
                return null;
            }

            var responseString = await GetStringFromServerAsync(
                    packageSource,
                    $"{registrationsBaseUrl}{package.Id.ToLowerInvariant()}/index.json",
                    cancellationToken)
                .ConfigureAwait(false);
            var registrationResponse = JsonUtility.FromJson<RegistrationResponse>(responseString.Replace(@"""@id"":", @"""atId"":"));

            // without a version specified, the latest version is returned
            var getLatestVersion = string.IsNullOrEmpty(package.Version);
            var item = getLatestVersion ?
                registrationResponse.items.OrderByDescending(registrationItem => new NugetPackageVersion(registrationItem.lower)).First() :
                registrationResponse.items.Find(
                    registrationItem => package.PackageVersion.CompareTo(new NugetPackageVersion(registrationItem.lower)) >= 0 &&
                                        package.PackageVersion.CompareTo(new NugetPackageVersion(registrationItem.upper)) <= 0);
            if (item is null)
            {
                Debug.LogError($"There is no package with id '{package.Id}' and version '{package.Version}' on the registration page.");
                return null;
            }

            if (item.items is null || item.items.Count == 0)
            {
                // If the items property is not present in the registration page object, the URL specified in the @id must be used to fetch metadata about individual package versions. The items array is sometimes excluded from the page object as an optimization. If the number of versions of a single package ID is very large, then the registration index document will be massive and wasteful to process for a client that only cares about a specific version or small range of versions.
                var registrationPageString = await GetStringFromServerAsync(packageSource, item.atId, cancellationToken).ConfigureAwait(false);
                var registrationPage = JsonUtility.FromJson<RegistrationPageObject>(registrationPageString);
                item.items = registrationPage.items;
            }

            var leafItem = getLatestVersion ?
                item.items.OrderByDescending(registrationLeaf => new NugetPackageVersion(registrationLeaf.catalogEntry.version)).FirstOrDefault() :
                item.items.Find(leaf => new NugetPackageVersion(leaf.catalogEntry.version) == package.PackageVersion);
            if (leafItem is null)
            {
                Debug.LogError(
                    $"There is no package with id '{package.Id}' and version '{package.PackageVersion}' in the registration page with the matching version rage: {item.lower} to {item.upper} '{item.atId}'.");
                return null;
            }

            return leafItem.catalogEntry.dependencyGroups.ConvertAll(
                dependencyGroup => new NugetFrameworkGroup
                {
                    Dependencies =
                        dependencyGroup.dependencies.ConvertAll(dependency => new NugetPackageIdentifier(dependency.id, dependency.range)),
                    TargetFramework = dependencyGroup.targetFramework,
                });
        }

        private bool InitializeFromSessionState()
        {
            var sessionState = SessionState.GetString(GetSessionStateKey(), string.Empty);
            if (string.IsNullOrEmpty(sessionState))
            {
                return false;
            }

            JsonUtility.FromJsonOverwrite(sessionState, this);
            return searchQueryServices != null;
        }

        private void SaveToSessionState()
        {
            UnityMainThreadDispatcher.Dispatch(() => SessionState.SetString(GetSessionStateKey(), JsonUtility.ToJson(this)));
        }

        private string GetSessionStateKey()
        {
            return $"{nameof(NugetApiClientV3)}:{apiIndexJsonUrl}";
        }

        private async void InitializeApiAddresses(NugetPackageSourceV3 packageSource)
        {
            var responseString = await GetStringFromServerAsync(packageSource, apiIndexJsonUrl.AbsoluteUri, CancellationToken.None)
                .ConfigureAwait(false);
            var resourceList =
                JsonUtility.FromJson<IndexResponse>(responseString.Replace(@"""@id"":", @"""atId"":").Replace(@"""@type"":", @"""atType"":"));
            var foundSearchQueryServices = new List<string>();
            foreach (var resource in resourceList.resources)
            {
                switch (resource.atType)
                {
                    case "SearchQueryService":
                        if (resource.comment.Contains("(primary)"))
                        {
                            foundSearchQueryServices.Insert(0, resource.atId.Trim('/'));
                        }
                        else
                        {
                            foundSearchQueryServices.Add(resource.atId.Trim('/'));
                        }

                        break;
                    case "PackageBaseAddress/3.0.0":
                        packageBaseAddress = resource.atId.Trim('/') + '/';
                        break;
                    case "RegistrationsBaseUrl/3.6.0":
                        registrationsBaseUrl = resource.atId.Trim('/') + '/';
                        break;
                }
            }

            if (string.IsNullOrEmpty(packageBaseAddress))
            {
                Debug.LogErrorFormat("The NuGet package source at '{0}' has no PackageBaseAddress resource defined.", apiIndexJsonUrl);
            }

            searchQueryServices = foundSearchQueryServices;
            SaveToSessionState();
        }

        private async Task<string> GetStringFromServerAsync(NugetPackageSourceV3 packageSource, string url, CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                AddHeadersToRequest(request, packageSource);

                using (var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    await EnsureResponseIsSuccess(response).ConfigureAwait(false);
                    var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false) ?? string.Empty;
                    return responseString;
                }
            }
        }

        private void AddHeadersToRequest(HttpRequestMessage request, NugetPackageSourceV3 packageSource)
        {
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json"));
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

        private List<INugetPackage> SearchResultToNugetPackages(List<SearchResultItem> searchResults, NugetPackageSourceV3 packageSource)
        {
            var packages = new List<INugetPackage>(searchResults.Count);
            foreach (var item in searchResults)
            {
                var versions = item.versions.ConvertAll(searchVersion => new NugetPackageVersion(searchVersion.version));
                versions.Sort();
                packages.Add(
                    new NugetPackageV3(
                        item.id,
                        item.version,
                        item.authors,
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

        private async Task EnsureResponseIsSuccess(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new HttpRequestException(
                $"The request to '{response.RequestMessage.RequestUri}' failed with status code '{response.StatusCode}' and message: {responseString}");
        }

        private class QueryBuilder
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

        [Serializable]
        [SuppressMessage(
            "StyleCop.CSharp.NamingRules",
            "SA1307:Accessible fields should begin with upper-case letter",
            Justification = "The names need to match with the JSON parameter.")]
        [SuppressMessage(
            "StyleCop.CSharp.MaintainabilityRules",
            "SA1401:Fields should be private",
            Justification = "Need to be serializable with the correct name.")]
        private class IndexResponse
        {
            public List<Resource> resources;

            public string version;
        }

        [Serializable]
        [SuppressMessage(
            "StyleCop.CSharp.NamingRules",
            "SA1307:Accessible fields should begin with upper-case letter",
            Justification = "The names need to match with the JSON parameter.")]
        [SuppressMessage(
            "StyleCop.CSharp.MaintainabilityRules",
            "SA1401:Fields should be private",
            Justification = "Need to be serializable with the correct name.")]
        private class Resource
        {
            public string atId;

            public string atType;

            public string clientVersion;

            public string comment;
        }

        [Serializable]
        [SuppressMessage(
            "StyleCop.CSharp.NamingRules",
            "SA1307:Accessible fields should begin with upper-case letter",
            Justification = "The names need to match with the JSON parameter.")]
        [SuppressMessage(
            "StyleCop.CSharp.MaintainabilityRules",
            "SA1401:Fields should be private",
            Justification = "Need to be serializable with the correct name.")]
        private class RegistrationResponse
        {
            /// <summary>
            ///     The number of registration pages in the index.
            /// </summary>
            public int count;

            /// <summary>
            ///     The array of registration pages.
            /// </summary>
            public List<RegistrationPageObject> items;
        }

        [Serializable]
        [SuppressMessage(
            "StyleCop.CSharp.NamingRules",
            "SA1307:Accessible fields should begin with upper-case letter",
            Justification = "The names need to match with the JSON parameter.")]
        [SuppressMessage(
            "StyleCop.CSharp.MaintainabilityRules",
            "SA1401:Fields should be private",
            Justification = "Need to be serializable with the correct name.")]
        private class RegistrationPageObject
        {
            /// <summary>
            ///     The URL to the registration page.
            ///     If the items property is not present in the registration page object, the URL specified in the @id must be used to fetch metadata about
            ///     individual package versions.
            /// </summary>
            public string atId;

            /// <summary>
            ///     The number of registration leaves in the page.
            /// </summary>
            public int count;

            /// <summary>
            ///     The array of registration leaves and their associate metadata.
            /// </summary>
            public List<RegistrationLeafObject> items;

            /// <summary>
            ///     The lowest SemVer 2.0.0 version in the page (inclusive).
            /// </summary>
            public string lower;

            /// <summary>
            ///     The highest SemVer 2.0.0 version in the page (inclusive).
            /// </summary>
            public string upper;
        }

        [Serializable]
        [SuppressMessage(
            "StyleCop.CSharp.NamingRules",
            "SA1307:Accessible fields should begin with upper-case letter",
            Justification = "The names need to match with the JSON parameter.")]
        [SuppressMessage(
            "StyleCop.CSharp.MaintainabilityRules",
            "SA1401:Fields should be private",
            Justification = "Need to be serializable with the correct name.")]
        private class RegistrationLeafObject
        {
            /// <summary>
            ///     The URL to the registration leaf.
            /// </summary>
            public string atId;

            /// <summary>
            ///     The catalog entry containing the package metadata.
            /// </summary>
            public CatalogEntry catalogEntry;

            /// <summary>
            ///     The URL to the package content (.nupkg).
            /// </summary>
            public string packageContent;
        }

        [Serializable]
        [SuppressMessage(
            "StyleCop.CSharp.NamingRules",
            "SA1307:Accessible fields should begin with upper-case letter",
            Justification = "The names need to match with the JSON parameter.")]
        [SuppressMessage(
            "StyleCop.CSharp.MaintainabilityRules",
            "SA1401:Fields should be private",
            Justification = "Need to be serializable with the correct name.")]
        private class CatalogEntry
        {
            /// <summary>
            ///     The URL to the document used to produce this object.
            /// </summary>
            public string atId;

            public string authors;

            /// <summary>
            ///     The dependencies of the package, grouped by target framework.
            /// </summary>
            public List<DependencyGroup> dependencyGroups;

            /// <summary>
            ///     The deprecation associated with the package.
            /// </summary>
            public Deprecation deprecation;

            public string description;

            public string iconUrl;

            /// <summary>
            ///     The ID of the package.
            /// </summary>
            public string id;

            public string language;

            public string licenseExpression;

            public string licenseUrl;

            /// <summary>
            ///     Should be considered as listed if absent.
            /// </summary>
            public bool listed = true;

            public string minClientVersion;

            public string projectUrl;

            /// <summary>
            ///     A string containing a ISO 8601 time-stamp of when the package was published.
            /// </summary>
            public string published;

            /// <summary>
            ///     A URL for the rendered (HTML web page) view of the package README.
            /// </summary>
            public string readmeUrl;

            public bool requireLicenseAcceptance;

            public string summary;

            public string tags;

            public string title;

            /// <summary>
            ///     The full version string after normalization.
            /// </summary>
            public string version;

            /// <summary>
            ///     The security vulnerabilities of the package.
            /// </summary>
            public List<Vulnerability> vulnerabilities;
        }

        [Serializable]
        [SuppressMessage(
            "StyleCop.CSharp.NamingRules",
            "SA1307:Accessible fields should begin with upper-case letter",
            Justification = "The names need to match with the JSON parameter.")]
        [SuppressMessage(
            "StyleCop.CSharp.MaintainabilityRules",
            "SA1401:Fields should be private",
            Justification = "Need to be serializable with the correct name.")]
        private class DependencyGroup
        {
            public List<Dependency> dependencies;

            /// <summary>
            ///     The target framework that these dependencies are applicable to.
            /// </summary>
            public string targetFramework = string.Empty;
        }

        [Serializable]
        [SuppressMessage(
            "StyleCop.CSharp.NamingRules",
            "SA1307:Accessible fields should begin with upper-case letter",
            Justification = "The names need to match with the JSON parameter.")]
        [SuppressMessage(
            "StyleCop.CSharp.MaintainabilityRules",
            "SA1401:Fields should be private",
            Justification = "Need to be serializable with the correct name.")]
        private class Dependency
        {
            /// <summary>
            ///     The ID of the package dependency.
            /// </summary>
            public string id;

            /// <summary>
            ///     The allowed version range of the dependency.
            /// </summary>
            public string range;

            /// <summary>
            ///     The URL to the registration index for this dependency.
            /// </summary>
            public string registration;
        }

        [Serializable]
        [SuppressMessage(
            "StyleCop.CSharp.NamingRules",
            "SA1307:Accessible fields should begin with upper-case letter",
            Justification = "The names need to match with the JSON parameter.")]
        [SuppressMessage(
            "StyleCop.CSharp.MaintainabilityRules",
            "SA1401:Fields should be private",
            Justification = "Need to be serializable with the correct name.")]
        private class Deprecation
        {
            /// <summary>
            ///     The additional details about this deprecation.
            /// </summary>
            public string message;

            /// <summary>
            ///     The reasons why the package was deprecated.
            /// </summary>
            public List<string> reasons;
        }

        [Serializable]
        [SuppressMessage(
            "StyleCop.CSharp.NamingRules",
            "SA1307:Accessible fields should begin with upper-case letter",
            Justification = "The names need to match with the JSON parameter.")]
        [SuppressMessage(
            "StyleCop.CSharp.MaintainabilityRules",
            "SA1401:Fields should be private",
            Justification = "Need to be serializable with the correct name.")]
        private class Vulnerability
        {
            /// <summary>
            ///     Location of security advisory for the package.
            /// </summary>
            public string advisoryUrl;

            /// <summary>
            ///     Severity of advisory: "0" = Low, "1" = Moderate, "2" = High, "3" = Critical.
            /// </summary>
            public string severity;
        }

        [Serializable]
        [SuppressMessage(
            "StyleCop.CSharp.NamingRules",
            "SA1307:Accessible fields should begin with upper-case letter",
            Justification = "The names need to match with the JSON parameter.")]
        [SuppressMessage(
            "StyleCop.CSharp.MaintainabilityRules",
            "SA1401:Fields should be private",
            Justification = "Need to be serializable with the correct name.")]
        private class SearchResult
        {
            /// <summary>
            ///     The search results matched by the request.
            /// </summary>
            public List<SearchResultItem> data;

            /// <summary>
            ///     The total number of matches, disregarding skip and take.
            /// </summary>
            public int totalHits;
        }

        [Serializable]
        [SuppressMessage(
            "StyleCop.CSharp.NamingRules",
            "SA1307:Accessible fields should begin with upper-case letter",
            Justification = "The names need to match with the JSON parameter.")]
        [SuppressMessage(
            "StyleCop.CSharp.MaintainabilityRules",
            "SA1401:Fields should be private",
            Justification = "Need to be serializable with the correct name.")]
        private class SearchResultItem
        {
            public List<string> authors;

            public string description;

            public string iconUrl;

            /// <summary>
            ///     The ID of the matched package.
            /// </summary>
            public string id;

            public string licenseUrl;

            public List<string> owners;

            public string projectUrl;

            /// <summary>
            ///     The absolute URL to the associated registration index.
            /// </summary>
            public string registration;

            public string summary;

            public List<string> tags;

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
            public string version;

            /// <summary>
            ///     All of the versions of the package considering the prerelease parameter.
            /// </summary>
            public List<SearchResultVersion> versions;
        }

        [Serializable]
        [SuppressMessage(
            "StyleCop.CSharp.NamingRules",
            "SA1307:Accessible fields should begin with upper-case letter",
            Justification = "The names need to match with the JSON parameter.")]
        [SuppressMessage(
            "StyleCop.CSharp.MaintainabilityRules",
            "SA1401:Fields should be private",
            Justification = "Need to be serializable with the correct name.")]
        private class SearchResultVersion
        {
            /// <summary>
            ///     The number of downloads for this specific package version.
            /// </summary>
            public long downloads;

            /// <summary>
            ///     The full SemVer 2.0.0 version string of the package (could contain build metadata).
            /// </summary>
            public string version;
        }

        // ReSharper restore InconsistentNaming
        // ReSharper restore UnassignedField.Local
        // ReSharper restore UnusedMember.Local
        // ReSharper restore NotAccessedField.Local
#pragma warning restore CS0649 // Field is assigned on serialize
    }
}
