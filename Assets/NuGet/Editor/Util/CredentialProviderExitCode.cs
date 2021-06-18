namespace NuGet.Editor.Util
{
    
    /// <summary>
    /// Possible response codes returned by a Nuget credential provider as described here:
    /// https://docs.microsoft.com/en-us/nuget/reference/extensibility/nuget-exe-credential-providers#creating-a-nugetexe-credential-provider
    /// </summary>
    internal enum CredentialProviderExitCode
    {
        Success = 0,
        ProviderNotApplicable = 1,
        Failure = 2
    }
}