namespace NugetForUnity.PluginAPI
{
    /// <summary>
    ///     In order to register your plugin you need to implement this interface and then call
    ///     methods on the provided registry object in order to provide additional functionalities
    ///     for certain features.
    /// </summary>
    public interface INugetPlugin
    {
        /// <summary>
        ///     NugetForUnity will call this method automatically so you can tell it what custom
        ///     functionalities your plugin is providing.
        /// </summary>
        /// <param name="registry">The registry where extension points can be registered to.</param>
        public void Register(INugetPluginRegistry registry);
    }
}
