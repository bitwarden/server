using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class HtmlEncodingStringConverterTests
{
    [Fact]
    public void Serialize_WhenEncodedValueIsNotNull_SerializesHtmlEncodedString()
    {
        // Arrange
        var obj = new HtmlEncodedString
        {
            EncodedValue = "This is &lt;b&gt;bold&lt;/b&gt;",
            NonEncodedValue = "This is <b>bold</b>",
        };
        const string expectedJsonString =
            "{\"EncodedValue\":\"This is <b>bold</b>\",\"NonEncodedValue\":\"This is <b>bold</b>\"}";

        // This is necessary to prevent the serializer from double encoding the string
        var serializerOptions = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        // Act
        var jsonString = JsonSerializer.Serialize(obj, serializerOptions);

        // Assert
        Assert.Equal(expectedJsonString, jsonString);
    }

    [Fact]
    public void Serialize_WhenEncodedValueIsNull_SerializesNull()
    {
        // Arrange
        var obj = new HtmlEncodedString { EncodedValue = null, NonEncodedValue = null };
        const string expectedJsonString = "{\"EncodedValue\":null,\"NonEncodedValue\":null}";

        // Act
        var jsonString = JsonSerializer.Serialize(obj);

        // Assert
        Assert.Equal(expectedJsonString, jsonString);
    }

    [Fact]
    public void Deserialize_WhenJsonContainsHtmlEncodedString_ReturnsDecodedString()
    {
        // Arrange
        const string json =
            "{\"EncodedValue\":\"This is <b>bold</b>\",\"NonEncodedValue\":\"This is <b>bold</b>\"}";
        const string expectedEncodedValue = "This is &lt;b&gt;bold&lt;/b&gt;";
        const string expectedNonEncodedValue = "This is <b>bold</b>";

        // Act
        var obj = JsonSerializer.Deserialize<HtmlEncodedString>(json);

        // Assert
        Assert.Equal(expectedEncodedValue, obj.EncodedValue);
        Assert.Equal(expectedNonEncodedValue, obj.NonEncodedValue);
    }

    [Fact]
    public void Deserialize_WhenJsonContainsNull_ReturnsNull()
    {
        // Arrange
        const string json = "{\"EncodedValue\":null,\"NonEncodedValue\":null}";

        // Act
        var obj = JsonSerializer.Deserialize<HtmlEncodedString>(json);

        // Assert
        Assert.Null(obj.EncodedValue);
        Assert.Null(obj.NonEncodedValue);
    }
}

public class HtmlEncodedString
{
    [JsonConverter(typeof(HtmlEncodingStringConverter))]
    public string EncodedValue { get; set; }

    public string NonEncodedValue { get; set; }
}
