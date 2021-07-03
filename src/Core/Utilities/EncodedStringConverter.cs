using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bit.Core.Utilities
{
    public class EncodedStringConverter : CircularJsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(string);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return existingValue;
            }
            else if (reader.TokenType == JsonToken.String)
            {
                return reader.Value.ToString();
            }

            var o = JObject.Load(reader);
            var property = CoreHelpers.GetJProperty(o, nameof(EncodedString.v));

            var value = property.Value.ToString();
            return CoreHelpers.Base64DecodeString(value);
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

            RemoveConverterAndAct(serializer, () =>
            {
                serializer.Serialize(writer, new EncodedString { v = CoreHelpers.Base64EncodeString((string)value) });
            });
        }
    }

    public class EncodedString
    {
        public string v { get; set; }
    }
}
