namespace NugetForUnity.PluginAPI
{
    /// <summary>
    ///     Tells the uninstall method what kind of request from the user initiated it.
    /// </summary>
    public enum PackageUninstallReason
    {
        /// <summary>
        ///     User has requested individual packages to be uninstalled from the project.
        /// </summary>
        IndividualUninstall,

        /// <summary>
        ///     User has requested all packages to be uninstalled from the project.
        /// </summary>
        UninstallAll,

        /// <summary>
        ///     Use requested individual packages to be updated.
        /// </summary>
        IndividualUpdate,

        /// <summary>
        ///     Use requested all packages to be updated.
        /// </summary>
        UpdateAll,
    }
}
