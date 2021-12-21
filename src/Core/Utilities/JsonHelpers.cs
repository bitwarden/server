using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Bit.Core.Utilities
{
    public static class JsonHelpers
    {
        public static JsonSerializerOptions DefaultJsonOptions { get; }
        public static JsonSerializerOptions IndentedJsonOptions { get; }
        public static JsonSerializerOptions IgnoreNullJsonOptions { get; }
        public static JsonSerializerOptions CamelCaseJsonOptions { get; }

        static JsonHelpers()
        {
            DefaultJsonOptions = new JsonSerializerOptions();

            IndentedJsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
            };

            IgnoreNullJsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };

            CamelCaseJsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
        }

        // NOTE: This is built into .NET 6, it SHOULD be removed when we upgrade
        public static T ToObject<T>(this JsonElement element, JsonSerializerOptions options = null)
        {
            return JsonSerializer.Deserialize<T>(element.GetRawText(), options ?? DefaultJsonOptions);
        }

        public static async ValueTask<T> DeserializeAsync<T>(Stream utf8Json, JsonSerializerOptions options = null, CancellationToken cancellationToken = default)
        {
            return await JsonSerializer.DeserializeAsync<T>(utf8Json, options ?? DefaultJsonOptions, cancellationToken);
        }

        public static T Deserialize<T>(string json, JsonSerializerOptions options = null)
        {
            return JsonSerializer.Deserialize<T>(json, options ?? DefaultJsonOptions);
        }

        public static string Serialize<T>(T value, JsonSerializerOptions options = null)
        {
            return JsonSerializer.Serialize(value, options ?? DefaultJsonOptions);
        }

        public static async Task SerializeAsync<T>(Stream utf8Json, T value, JsonSerializerOptions options = null, CancellationToken cancellationToken = default)
        {
            await JsonSerializer.SerializeAsync<T>(utf8Json, value, options ?? DefaultJsonOptions, cancellationToken);
        }

        public static JsonContent CreateJsonContent<T>(T value, JsonSerializerOptions options = null)
        {
            return JsonContent.Create(value, options: options ?? DefaultJsonOptions);
        }

        public static async Task<T> ReadJsonAsync<T>(this HttpContent content, JsonSerializerOptions options = null, CancellationToken cancellationToken = default)
        {
            return await content.ReadFromJsonAsync<T>(options ?? DefaultJsonOptions, cancellationToken);
        }

        public static JsonDocument Parse(string json)
        {
            return JsonDocument.Parse(json);
        }

        public static async Task<JsonDocument> ParseAsync(Stream utf8Json, CancellationToken cancellationToken = default)
        {
            return await JsonDocument.ParseAsync(utf8Json, cancellationToken: cancellationToken);
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
