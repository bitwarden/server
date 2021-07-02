using System;
using Newtonsoft.Json;

namespace Bit.Core.Utilities
{
    public class EncodedStringConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(string);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return existingValue;
            }

            var value = reader.Value as string;
            return System.Net.WebUtility.HtmlDecode(value);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                if (serializer.NullValueHandling == NullValueHandling.Include)
                {
                    writer.WriteNull();
                }
                return;
            }

            writer.WriteValue(System.Net.WebUtility.HtmlEncode((string)value));
        }
    }
}
