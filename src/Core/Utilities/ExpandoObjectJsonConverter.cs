using System;
using System.Dynamic;
using Newtonsoft.Json;

namespace Bit.Core.Utilities
{
    public class ExpandoObjectJsonConverter : JsonConverter
    {
        public override bool CanWrite => false;
        public override bool CanConvert(Type objectType) => true;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return serializer.Deserialize<ExpandoObject>(reader);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => throw new NotImplementedException();
    }
}
