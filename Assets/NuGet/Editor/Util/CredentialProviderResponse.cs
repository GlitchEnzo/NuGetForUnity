namespace NuGet.Editor.Util
{
    /// <summary>
    /// Data class returned from nuget credential providers in a JSON format. As described here:
    /// https://docs.microsoft.com/en-us/nuget/reference/extensibility/nuget-exe-credential-providers#creating-a-nugetexe-credential-provider
    /// </summary>
    [System.Serializable]
    public struct CredentialProviderResponse
    {
        public string Username;
        public string Password;
    }
}