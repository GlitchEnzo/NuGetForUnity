namespace NugetForUnity
{
    /// <summary>
    /// Represents a file entry inside a .nuspec file.
    /// </summary>
    public class NuspecContentFile
    {
        /// <summary>
        /// Gets or sets the path for the source file inside the project.
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Gets or sets the path for the target file inside the .nupkg file.
        /// </summary>
        public string Target { get; set; }
    }
}