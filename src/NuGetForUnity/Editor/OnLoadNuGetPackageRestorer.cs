using UnityEditor;

namespace NugetForUnity
{
    /// <summary>
    ///     Automatically restores NuGet packages when Unity opens the project.
    /// </summary>
    [InitializeOnLoad]
    public static class OnLoadNuGetPackageRestorer
    {
        /// <summary>
        ///     Initializes static members of the <see cref="OnLoadNuGetPackageRestorer" /> class.
        ///     Static constructor used by Unity to initialize NuGet and restore packages defined in packages.config.
        /// </summary>
        static OnLoadNuGetPackageRestorer()
        {
            if (SessionState.GetBool("NugetForUnity.FirstProjectOpen", false))
            {
                return;
            }

            SessionState.SetBool("NugetForUnity.FirstProjectOpen", true);

            // if we are entering playmode, don't do anything
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            // Load the NuGet.config file
            NugetHelper.LoadNugetConfigFile();

            // restore packages - this will be called EVERY time the project is loaded
            NugetHelper.Restore(!NugetHelper.NugetConfigFile.LockPackagesOnRestore);
        }
    }
}
