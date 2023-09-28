namespace NugetForUnity.Models
{
    /// <summary>
    ///     The type of repository that a package is from.
    /// </summary>
    public enum RepositoryType
    {
        /// <summary>
        ///     The repository type is not specified.
        /// </summary>
        NotSpecified = 0,

        /// <summary>
        ///     The package is from a Git repository.
        /// </summary>
        Git,

        /// <summary>
        ///     The package is from a Subversion repository.
        /// </summary>
        TfsGit,
    }
}
