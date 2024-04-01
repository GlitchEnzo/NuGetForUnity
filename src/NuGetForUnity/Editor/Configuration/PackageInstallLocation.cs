namespace NugetForUnity.Configuration
{
    /// <summary>
    ///     Tells the system how to determine where the packages are to be installed and configurations are to be stored.
    /// </summary>
    internal enum PackageInstallLocation
    {
        /// <summary>
        ///     This option will place Nuget.config into the Assets folder and will allow the user to
        ///     specify custom location within Assets folder for packages.config and package installation
        ///     folder.
        /// </summary>
        CustomWithinAssets,

        /// <summary>
        ///     This options will place the Nuget.config and packages.config under Packages/nuget-packages
        ///     and will install the packages under Packages/nuget-packages/InstalledPackages.
        /// </summary>
        /// .
        InPackagesFolder,
    }
}
