using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NS = Newtonsoft.Json;

namespace Bit.Core.Utilities
{
    public static class JsonHelpers
    {
        public static JsonSerializerOptions Default { get; }
        public static JsonSerializerOptions Indented { get; }
        public static JsonSerializerOptions IgnoreCase { get; }
        public static JsonSerializerOptions IgnoreWritingNull { get; }
        public static JsonSerializerOptions CamelCase { get; }
        public static JsonSerializerOptions IgnoreWritingNullAndCamelCase { get; }

        static JsonHelpers()
        {
            Default = new JsonSerializerOptions();

            Indented = new JsonSerializerOptions
            {
                WriteIndented = true,
            };

            IgnoreCase = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };

            IgnoreWritingNull = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };

            CamelCase = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            IgnoreWritingNullAndCamelCase = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };
        }

        // NOTE: This is built into .NET 6, it SHOULD be removed when we upgrade
        public static T ToObject<T>(this JsonElement element, JsonSerializerOptions options = null)
        {
            return JsonSerializer.Deserialize<T>(element.GetRawText(), options ?? Default);
        }

        public static T DeserializeOrNew<T>(string json, JsonSerializerOptions options = null)
            where T : new()
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new T();
            }

            return JsonSerializer.Deserialize<T>(json, options);
        }

        #region Legacy Newtonsoft.Json usage
        private const string LegacyMessage = "Usage of Newtonsoft.Json should be kept to a minimum and will further be removed when we move to .NET 6";

        [Obsolete(LegacyMessage)]
        public static NS.JsonSerializerSettings LegacyEnumKeyResolver { get; } = new NS.JsonSerializerSettings
        {
            ContractResolver = new EnumKeyResolver<byte>(),
        };

        [Obsolete(LegacyMessage)]
        public static string LegacySerialize(object value, NS.JsonSerializerSettings settings = null)
        {
            return NS.JsonConvert.SerializeObject(value, settings);
        }

        [Obsolete(LegacyMessage)]
        public static T LegacyDeserialize<T>(string value, NS.JsonSerializerSettings settings = null)
        {
            return NS.JsonConvert.DeserializeObject<T>(value, settings);
        }
        #endregion
    }

    public class EnumKeyResolver<T> : NS.Serialization.DefaultContractResolver
        where T : struct
    {
        protected override NS.Serialization.JsonDictionaryContract CreateDictionaryContract(Type objectType)
        {
            var contract = base.CreateDictionaryContract(objectType);
            var keyType = contract.DictionaryKeyType;

            if (keyType.BaseType == typeof(Enum))
            {
                contract.DictionaryKeyResolver = propName => ((T)Enum.Parse(keyType, propName)).ToString();
            }

            return contract;
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
