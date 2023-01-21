#nullable enable

using System.Text.Json;

namespace UnityEngine
{
    internal static class JsonUtility
    {
        public static T? FromJson<T>(string output)
        {
            return JsonSerializer.Deserialize<T>(output);
        }
    }
}
