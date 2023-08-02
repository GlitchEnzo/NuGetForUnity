#nullable enable

using System.Text.Json;

namespace UnityEngine
{
    internal static class JsonUtility
    {
        public static T? FromJson<T>(string output)
        {
            return JsonSerializer.Deserialize<T>(output, new JsonSerializerOptions { IncludeFields = true });
        }

        public static void FromJsonOverwrite<T>(string jsonString, T target)
        {
            // not needed for the CLI
        }

        internal static string ToJson<T>(T value)
        {
            return JsonSerializer.Serialize(value, new JsonSerializerOptions { IncludeFields = true });
        }
    }
}
