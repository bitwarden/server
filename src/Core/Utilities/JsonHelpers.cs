using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Bit.Core.Utilities
{
    public static class JsonHelpers
    {
        public static JsonSerializerOptions Default { get; }
        public static JsonSerializerOptions Indented { get; }
        public static JsonSerializerOptions IgnoreWritingNull { get; }
        public static JsonSerializerOptions CamelCase { get; }

        public static JsonDocumentOptions DefaultDocument { get; }

        static JsonHelpers()
        {
            Default = new JsonSerializerOptions();

            Indented = new JsonSerializerOptions
            {
                WriteIndented = true,
            };

            IgnoreWritingNull = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };

            CamelCase = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            DefaultDocument = new JsonDocumentOptions();
        }

        // NOTE: This is built into .NET 6, it SHOULD be removed when we upgrade
        public static T ToObject<T>(this JsonElement element, JsonSerializerOptions options = null)
        {
            return JsonSerializer.Deserialize<T>(element.GetRawText(), options ?? Default);
        }

        public static async ValueTask<T> DeserializeAsync<T>(Stream utf8Json, JsonSerializerOptions options = null, CancellationToken cancellationToken = default)
        {
            return await JsonSerializer.DeserializeAsync<T>(utf8Json, options ?? Default, cancellationToken);
        }

        public static T Deserialize<T>(string json, JsonSerializerOptions options = null)
        {
            return JsonSerializer.Deserialize<T>(json, options ?? Default);
        }

        public static JsonNode SerializeToNode<T>(T value, JsonSerializerOptions options = null)
        {
            return JsonSerializer.SerializeToNode(value, options ?? Default);
        }

        public static T DeserializeOrNew<T>(string json, JsonSerializerOptions options = null)
            where T : new()
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new T();
            }

            return Deserialize<T>(json, options);
        }

        public static string Serialize<T>(T value, JsonSerializerOptions options = null)
        {
            return JsonSerializer.Serialize(value, options ?? Default);
        }

        public static async Task SerializeAsync<T>(Stream utf8Json, T value, JsonSerializerOptions options = null, CancellationToken cancellationToken = default)
        {
            await JsonSerializer.SerializeAsync<T>(utf8Json, value, options ?? Default, cancellationToken);
        }

        public static async Task SerializeAsync(Stream utf8Json, object value, Type inputType, JsonSerializerOptions options = null, CancellationToken cancellationToken = default)
        {
            await JsonSerializer.SerializeAsync(utf8Json, value, inputType, options ?? Default, cancellationToken);
        }

        public static JsonContent CreateJsonContent<T>(T value, JsonSerializerOptions options = null)
        {
            return JsonContent.Create(value, options: options ?? Default);
        }

        public static JsonContent CreateJsonContent(object value, Type inputType, JsonSerializerOptions options = null)
        {
            return JsonContent.Create(value, inputType, options: options);
        }

        public static async Task<T> ReadJsonAsync<T>(this HttpContent content, JsonSerializerOptions options = null, CancellationToken cancellationToken = default)
        {
            return await content.ReadFromJsonAsync<T>(options ?? Default, cancellationToken);
        }

        public static async Task WriteJsonAsync<T>(this HttpResponse response, T value, JsonSerializerOptions options = null, CancellationToken cancellationToken = default)
        {
            await response.WriteAsJsonAsync<T>(value, options ?? Default, cancellationToken);
        }

        public static JsonDocument Parse(string json)
        {
            return JsonDocument.Parse(json);
        }

        public static async Task<JsonDocument> ParseAsync(Stream utf8Json, CancellationToken cancellationToken = default)
        {
            return await JsonDocument.ParseAsync(utf8Json, cancellationToken: cancellationToken);
        }

        public static string SerializeDeserialize(string json, JsonSerializerOptions options = null)
        {
            options ??= Default;
            return Serialize(Deserialize<JsonDocument>(json, options), options);
        }
    }

    public class MsEpochConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (!long.TryParse(reader.GetString(), out var milliseconds))
            {
                return null;
            }

            return CoreHelpers.FromEpocMilliseconds(milliseconds);
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (!value.HasValue)
            {
                writer.WriteNullValue();
            }

            writer.WriteStringValue(CoreHelpers.ToEpocMilliseconds(value.Value).ToString());
        }
    }
}
