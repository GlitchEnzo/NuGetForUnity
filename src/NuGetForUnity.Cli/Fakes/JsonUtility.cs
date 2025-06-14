#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using NugetForUnity.Configuration;

namespace UnityEngine
{
    internal static class JsonUtility
    {
        private static JsonSerializerOptions? options;

        private static JsonSerializerOptions? prettyOptions;

        private static JsonSerializerOptions Options
        {
            get
            {
                if (options != null)
                {
                    return options;
                }

                options = new JsonSerializerOptions { IncludeFields = true };
                options.Converters.Add(new PrivateFieldConverter<NativeRuntimeSettings>());
                options.Converters.Add(new PrivateFieldConverter<NativeRuntimeAssetConfiguration>());

                return options;
            }
        }

        private static JsonSerializerOptions PrettyOptions => prettyOptions ??= new JsonSerializerOptions(Options) { WriteIndented = true };

        public static T? FromJson<T>(string output)
        {
            return JsonSerializer.Deserialize<T>(output, Options);
        }

        public static void FromJsonOverwrite<T>(string jsonString, T target)
        {
            // not needed for the CLI
        }

        internal static string ToJson<T>(T value, bool pretty = false)
        {
            return JsonSerializer.Serialize(value, pretty ? PrettyOptions : Options);
        }

        // special converter for classes with private fields e.g. NativeRuntimeSettings and NativeRuntimeAssetConfiguration
        private sealed class PrivateFieldConverter<T> : JsonConverter<T>
            where T : new()
        {
            public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    throw new JsonException($"Expected first token to be {nameof(JsonTokenType.StartObject)} but got {reader.TokenType}");
                }

                var result = new T();

                var fieldMap = new Dictionary<string, FieldInfo>();
                foreach (var field in typeof(T).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    fieldMap[field.Name] = field;
                }

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        return result;
                    }

                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        throw new JsonException($"Expected {nameof(JsonTokenType.PropertyName)} but got {reader.TokenType}");
                    }

                    var propName = reader.GetString();

                    if (propName == null || !reader.Read())
                    {
                        throw new JsonException("Expected the name of the property but got null");
                    }

                    if (fieldMap.TryGetValue(propName, out var field))
                    {
                        var value = JsonSerializer.Deserialize(ref reader, field.FieldType, options);
                        field.SetValue(result, value);
                    }
                    else
                    {
                        // Skip unknown field
                        reader.Skip();
                    }
                }

                throw new JsonException($"Incomplete JSON object. Not reached {nameof(JsonTokenType.EndObject)}");
            }

            public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                var fields = typeof(T).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                foreach (var field in fields)
                {
                    var fieldValue = field.GetValue(value);
                    writer.WritePropertyName(field.Name);
                    JsonSerializer.Serialize(writer, fieldValue, fieldValue?.GetType() ?? typeof(object), options);
                }

                writer.WriteEndObject();
            }
        }
    }
}
