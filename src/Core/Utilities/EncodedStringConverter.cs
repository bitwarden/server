using System;
using Newtonsoft.Json;
using System.Text;

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
