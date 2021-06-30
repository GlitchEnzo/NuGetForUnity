using NuGet.Editor.Nuget;

namespace NuGet.Editor.Services
{
    public interface INugetService
    {
        /// <summary>
        /// Saves this NuGet.config file to disk.
        /// </summary>
        /// <param name="nugetConfigFile">The NugetConfigFile one wants to save</param>
        /// <param name="filepath">The filepath to where this NuGet.config will be saved.</param>
        void Save(string filepath);

        /// <summary>
        /// Loads a NuGet.config file at the given filepath.
        /// </summary>
        /// <param name="filePath">The full filepath to the NuGet.config file to load.</param>
        /// <param name="nugetConfigFile">The NugetConfigFile one wants to load</param>
        /// <returns>The newly loaded <see cref="NugetConfigFile"/>.</returns>
        NugetConfigFile Load(string filePath);

        /// <summary>
        /// Creates a NuGet.config file with the default settings at the given full filepath.
        /// </summary>
        /// <param name="filePath">The full filepath where to create the NuGet.config file.</param>
        /// <returns>The loaded <see cref="NugetConfigFile"/> loaded off of the newly created default file.</returns>
        NugetConfigFile CreateDefaultFile(string filePath);
    }
}