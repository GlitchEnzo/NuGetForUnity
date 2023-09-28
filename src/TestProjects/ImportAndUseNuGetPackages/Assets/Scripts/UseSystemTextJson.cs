#nullable enable

using System.Text.Json;
using UnityEngine;

namespace ImportAndUseNuGetPackages
{
    /// <summary>
    ///     Test if the import of Serilog works / compiles.
    /// </summary>
    public class UseSystemTextJson : MonoBehaviour
    {
        private void Awake()
        {
            _ = JsonSerializer.Serialize("test");
        }
    }
}
