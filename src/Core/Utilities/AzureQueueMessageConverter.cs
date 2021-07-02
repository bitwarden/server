using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Text;

public class AzureQueueMessageConverter : JsonConverter
{
    private JsonSerializer _jsonSerializer;
    public override bool CanConvert(Type objectType) => true;

    public AzureQueueMessageConverter(JsonSerializerSettings jsonSettings)
    {
        // jsonSettings.ContractResolver = new CircularConverterContractResolver<AzureQueueMessageConverter>();
        _jsonSerializer = JsonSerializer.Create(jsonSettings);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        var encoded = reader.ToString();
        var json = System.Net.WebUtility.HtmlDecode(encoded);
        using (var stringReader = new StringReader(json))
        {
            return _jsonSerializer.Deserialize(stringReader, objectType);
        }
    }
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        using (var stringWriter = new StringWriter())
        {
            _jsonSerializer.Serialize(stringWriter, value);
            var encoded = System.Net.WebUtility.HtmlEncode(stringWriter.ToString());
            writer.WriteValue(encoded);
        }
    }
}

public class EncodedStringConverter : JsonConverter
{
    public override bool CanConvert(Type objectType) => objectType == typeof(string);
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;
        string value = reader.Value as string;
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

        var s = (string)value;

        if (Encoding.UTF8.GetByteCount(s) != s.Length)
        {
            s = System.Net.WebUtility.HtmlEncode(s);
        }

        writer.WriteValue(s);
    }
}
