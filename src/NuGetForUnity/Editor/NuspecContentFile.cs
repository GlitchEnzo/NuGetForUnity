namespace NugetForUnity
{
    /// <summary>
    ///     Represents a file entry inside a .nuspec file.
    /// </summary>
    public class NuspecContentFile
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="NuspecContentFile" /> class.
        /// </summary>
        public NuspecContentFile()
        {
            Source = string.Empty;
            Target = string.Empty;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="NuspecContentFile" /> class.
        /// </summary>
        /// <param name="source">The source path inside the project.</param>
        /// <param name="target">The target path inside the .nupkg file.</param>
        public NuspecContentFile(string source, string target)
        {
            Source = source;
            Target = target;
        }

        /// <summary>
        ///     Gets or sets the path for the source file inside the project.
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        ///     Gets or sets the path for the target file inside the .nupkg file.
        /// </summary>
        public string Target { get; set; }
    }
}
