#nullable enable

using Serilog;
using UnityEngine;

namespace ImportAndUseNuGetPackages
{
    /// <summary>
    ///     Test if the import of Serilog works / compiles.
    /// </summary>
    public class UseSerilog : MonoBehaviour
    {
        private void Awake()
        {
            var log = new LoggerConfiguration().CreateLogger();

            log.Information("Hello, Serilog!");
        }
    }
}
