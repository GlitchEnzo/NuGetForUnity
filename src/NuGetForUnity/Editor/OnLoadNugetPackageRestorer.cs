using NugetForUnity.Configuration;
using UnityEditor;

namespace NugetForUnity
{
    /// <summary>
    ///     Automatically restores NuGet packages when Unity opens the project.
    /// </summary>
    [InitializeOnLoad]
    public static class OnLoadNugetPackageRestorer
    {
        /// <summary>
        ///     Initializes static members of the <see cref="OnLoadNugetPackageRestorer" /> class.
        ///     Static constructor used by Unity to initialize NuGet and restore packages defined in packages.config.
        /// </summary>
        static OnLoadNugetPackageRestorer()
        {
            if (SessionState.GetBool("NugetForUnity.FirstProjectOpen", false))
            {
                return;
            }

            SessionState.SetBool("NugetForUnity.FirstProjectOpen", true);

            // if we are entering play-mode, don't do anything
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            // Load the NuGet.config file
            ConfigurationManager.LoadNugetConfigFile();

            // restore packages - this will be called EVERY time the project is loaded
            PackageRestorer.Restore(ConfigurationManager.NugetConfigFile.SlimRestore);
        }
    }
}
