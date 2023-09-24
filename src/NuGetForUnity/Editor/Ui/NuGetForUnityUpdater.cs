using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NugetForUnity.Models;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Networking;

namespace NugetForUnity.Ui
{
    /// <summary>
    ///     Allows the user to check and perform updates of NuGetForUnity.
    /// </summary>
    internal static class NuGetForUnityUpdater
    {
        private const string UpmPackageName = "com.github-glitchenzo.nugetforunity";

        private const string UpmPackageGitUrl = "https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity";

        private const string GitHubReleasesPageUrl = "https://github.com/GlitchEnzo/NuGetForUnity/releases";

        private const string GitHubReleasesApiUrl = "https://api.github.com/repos/GlitchEnzo/NuGetForUnity/releases?per_page=10";

        /// <summary>
        ///     Opens release notes for the current version.
        /// </summary>
        [MenuItem("NuGet/Version " + NugetPreferences.NuGetForUnityVersion + " \uD83D\uDD17", false, 10)]
        public static void DisplayVersion()
        {
            Application.OpenURL($"{GitHubReleasesPageUrl}/tag/v{NugetPreferences.NuGetForUnityVersion}");
        }

        /// <summary>
        ///     Checks/launches the Releases page to update NuGetForUnity with a new version.
        /// </summary>
        [MenuItem("NuGet/Check for Updates...", false, 11)]
        public static void CheckForUpdates()
        {
            var request = UnityWebRequest.Get(GitHubReleasesApiUrl);
            var operation = request.SendWebRequest();
            NugetLogger.LogVerbose("HTTP GET {0}", GitHubReleasesApiUrl);
            EditorUtility.DisplayProgressBar("Checking updates", null, 0.0f);

            operation.completed += asyncOperation =>
            {
                try
                {
                    string latestVersion = null;
                    string latestVersionDownloadUrl = null;
                    string response = null;
                    if (string.IsNullOrEmpty(request.error))
                    {
                        response = request.downloadHandler.text;
                    }

                    if (response != null)
                    {
                        latestVersion = GetLatestVersionFromReleasesApi(response, out latestVersionDownloadUrl);
                    }

                    EditorUtility.ClearProgressBar();

                    if (latestVersion == null)
                    {
                        EditorUtility.DisplayDialog(
                            "Unable to Determine Updates",
                            $"Couldn't find release information at {GitHubReleasesApiUrl}. Error: {request.error}",
                            "OK");
                        return;
                    }

                    var current = new NugetPackageIdentifier("NuGetForUnity", NugetPreferences.NuGetForUnityVersion);
                    var latest = new NugetPackageIdentifier("NuGetForUnity", latestVersion);
                    if (current >= latest)
                    {
                        EditorUtility.DisplayDialog(
                            "No Updates Available",
                            $"Your version of NuGetForUnity is up to date.\nVersion {NugetPreferences.NuGetForUnityVersion}.",
                            "OK");
                        return;
                    }

                    // New version is available. Give user options for installing it.
                    switch (EditorUtility.DisplayDialogComplex(
                                "Update Available",
                                $"Current Version: {NugetPreferences.NuGetForUnityVersion}\nLatest Version: {latestVersion}",
                                "Install Latest",
                                "Open Releases Page",
                                "Cancel"))
                    {
                        case 0:
                            _ = new NuGetForUnityUpdateInstaller(latestVersionDownloadUrl);
                            break;
                        case 1:
                            Application.OpenURL(GitHubReleasesPageUrl);
                            break;
                        case 2:
                            break;
                    }
                }
                finally
                {
                    request.Dispose();
                }
            };
        }

        [CanBeNull]
        private static string GetLatestVersionFromReleasesApi([NotNull] string response, [CanBeNull] out string unitypackageDownloadUrl)
        {
            // JsonUtility doesn't support top level arrays so we wrap it inside a object.
            var releases = JsonUtility.FromJson<GitHubReleaseApiRequestList>(string.Concat("{ \"list\": ", response, " }"));
            if (releases.list is null)
            {
                Debug.LogWarningFormat("Unable to parse releases response from '{0}', response:\n{1}", GitHubReleasesApiUrl, response);
                unitypackageDownloadUrl = null;
                return null;
            }

            foreach (var release in releases.list)
            {
                if (release.tag_name is null)
                {
                    Debug.LogWarningFormat("Release returned from '{0}' has no 'tag_name', response:\n{1}", GitHubReleasesApiUrl, response);
                    continue;
                }

                // skip beta versions e.g. v2.0.0-preview
                if (release.tag_name.Contains('-'))
                {
                    continue;
                }

                unitypackageDownloadUrl = release.assets
                    ?.Find(asset => asset.name != null && asset.name.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
                    ?.browser_download_url;
                return release.tag_name.TrimStart('v');
            }

            unitypackageDownloadUrl = null;
            return null;
        }

        /// <summary>
        ///     Checks if NuGetForUnity is installed using UPM.
        ///     If installed using UPM we update the package.
        ///     If not we open the browser to download the .unitypackage.
        /// </summary>
        private sealed class NuGetForUnityUpdateInstaller
        {
            [CanBeNull]
            private readonly string latestVersionDownloadUrl;

            [NotNull]
            private readonly ListRequest upmPackageListRequest;

            [CanBeNull]
            private AddRequest upmPackageAddRequest;

            public NuGetForUnityUpdateInstaller([CanBeNull] string latestVersionDownloadUrl)
            {
                this.latestVersionDownloadUrl = latestVersionDownloadUrl;
                EditorUtility.DisplayProgressBar("NuGetForUnity update ...", null, 0.0f);

                // get list of installed packages
                upmPackageListRequest = Client.List(true);
                EditorApplication.update += HandleListRequest;
            }

            private void HandleListRequest()
            {
                if (!upmPackageListRequest.IsCompleted)
                {
                    return;
                }

                try
                {
                    if (upmPackageListRequest.Status == StatusCode.Success &&
                        upmPackageListRequest.Result.Any(package => package.name == UpmPackageName))
                    {
                        // NuGetForUnity is installed as UPM package so we can just re-add it inside the package-manager to get update (see https://docs.unity3d.com/Manual/upm-git.html)
                        var packageInfo = upmPackageListRequest.Result.First(package => package.name == UpmPackageName);
                        if (packageInfo.source == UnityEditor.PackageManager.PackageSource.Git)
                        {
                            EditorUtility.DisplayProgressBar("NuGetForUnity update ...", "Installing with UPM (git url)", 0.1f);
                            upmPackageAddRequest = Client.Add(UpmPackageGitUrl);
                        }
                        else
                        {
                            EditorUtility.DisplayProgressBar("NuGetForUnity update ...", "Installing with UPM (OpenUPM)", 0.1f);
                            upmPackageAddRequest = Client.Add(UpmPackageName);
                        }

                        EditorApplication.update += HandleAddRequest;
                    }
                    else
                    {
                        EditorUtility.ClearProgressBar();
                        Application.OpenURL(string.IsNullOrEmpty(latestVersionDownloadUrl) ? GitHubReleasesPageUrl : latestVersionDownloadUrl);
                    }
                }
                finally
                {
                    EditorApplication.update -= HandleListRequest;
                }
            }

            private void HandleAddRequest()
            {
                if (upmPackageAddRequest != null && !upmPackageAddRequest.IsCompleted)
                {
                    return;
                }

                EditorUtility.ClearProgressBar();
                EditorApplication.update -= HandleAddRequest;
            }
        }

        // ReSharper disable InconsistentNaming
#pragma warning disable 0649
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
#pragma warning disable SA1401 // Fields should be private
#pragma warning disable SA1310 // Field names should not contain underscore
#pragma warning disable CA1051 // Do not declare visible instance fields

        [Serializable]
        private sealed class GitHubReleaseApiRequestList
        {
            [CanBeNull]
            public List<GitHubReleaseApiRequest> list;
        }

        [Serializable]
        private sealed class GitHubReleaseApiRequest
        {
            [CanBeNull]
            public List<GitHubAsset> assets;

            [CanBeNull]
            public string tag_name;
        }

        [Serializable]
        private sealed class GitHubAsset
        {
            [CanBeNull]
            public string browser_download_url;

            [CanBeNull]
            public string name;
        }

        // ReSharper restore InconsistentNaming
#pragma warning restore CA1051 // Do not declare visible instance fields
#pragma warning restore SA1310 // Field names should not contain underscore
#pragma warning restore SA1401 // Fields should be private
#pragma warning restore SA1307 // Accessible fields should begin with upper-case letter
#pragma warning restore 0649
    }
}
