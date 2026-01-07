using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class EnumMemberJsonConverterTests
{
    [Fact]
    public void Serialize_WithEnumMemberAttribute_UsesAttributeValue()
    {
        // Arrange
        var obj = new EnumConverterTestObject
        {
            Status = EnumConverterTestStatus.InProgress
        };
        const string expectedJsonString = "{\"Status\":\"in_progress\"}";

        // Act
        var jsonString = JsonSerializer.Serialize(obj);

        // Assert
        Assert.Equal(expectedJsonString, jsonString);
    }

    [Fact]
    public void Serialize_WithoutEnumMemberAttribute_UsesEnumName()
    {
        // Arrange
        var obj = new EnumConverterTestObject
        {
            Status = EnumConverterTestStatus.Pending
        };
        const string expectedJsonString = "{\"Status\":\"Pending\"}";

        // Act
        var jsonString = JsonSerializer.Serialize(obj);

        // Assert
        Assert.Equal(expectedJsonString, jsonString);
    }

    [Fact]
    public void Serialize_MultipleValues_SerializesCorrectly()
    {
        // Arrange
        var obj = new EnumConverterTestObjectWithMultiple
        {
            Status1 = EnumConverterTestStatus.Active,
            Status2 = EnumConverterTestStatus.InProgress,
            Status3 = EnumConverterTestStatus.Pending
        };
        const string expectedJsonString = "{\"Status1\":\"active\",\"Status2\":\"in_progress\",\"Status3\":\"Pending\"}";

        // Act
        var jsonString = JsonSerializer.Serialize(obj);

        // Assert
        Assert.Equal(expectedJsonString, jsonString);
    }

    [Fact]
    public void Deserialize_WithEnumMemberAttribute_ReturnsCorrectEnumValue()
    {
        // Arrange
        const string json = "{\"Status\":\"in_progress\"}";

        // Act
        var obj = JsonSerializer.Deserialize<EnumConverterTestObject>(json);

        // Assert
        Assert.Equal(EnumConverterTestStatus.InProgress, obj.Status);
    }

    [Fact]
    public void Deserialize_WithoutEnumMemberAttribute_ReturnsCorrectEnumValue()
    {
        // Arrange
        const string json = "{\"Status\":\"Pending\"}";

        // Act
        var obj = JsonSerializer.Deserialize<EnumConverterTestObject>(json);

        // Assert
        Assert.Equal(EnumConverterTestStatus.Pending, obj.Status);
    }

    [Fact]
    public void Deserialize_MultipleValues_DeserializesCorrectly()
    {
        // Arrange
        const string json = "{\"Status1\":\"active\",\"Status2\":\"in_progress\",\"Status3\":\"Pending\"}";

        // Act
        var obj = JsonSerializer.Deserialize<EnumConverterTestObjectWithMultiple>(json);

        // Assert
        Assert.Equal(EnumConverterTestStatus.Active, obj.Status1);
        Assert.Equal(EnumConverterTestStatus.InProgress, obj.Status2);
        Assert.Equal(EnumConverterTestStatus.Pending, obj.Status3);
    }

    [Fact]
    public void Deserialize_InvalidEnumString_ThrowsJsonException()
    {
        // Arrange
        const string json = "{\"Status\":\"invalid_value\"}";

        // Act & Assert
        var exception = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EnumConverterTestObject>(json));
        Assert.Contains("Unable to convert 'invalid_value' to EnumConverterTestStatus", exception.Message);
    }

    [Fact]
    public void Deserialize_EmptyString_ThrowsJsonException()
    {
        // Arrange
        const string json = "{\"Status\":\"\"}";

        // Act & Assert
        var exception = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EnumConverterTestObject>(json));
        Assert.Contains("Unable to convert '' to EnumConverterTestStatus", exception.Message);
    }

    [Fact]
    public void RoundTrip_WithEnumMemberAttribute_PreservesValue()
    {
        // Arrange
        var originalObj = new EnumConverterTestObject
        {
            Status = EnumConverterTestStatus.Completed
        };

        // Act
        var json = JsonSerializer.Serialize(originalObj);
        var deserializedObj = JsonSerializer.Deserialize<EnumConverterTestObject>(json);

        // Assert
        Assert.Equal(originalObj.Status, deserializedObj.Status);
    }

    [Fact]
    public void RoundTrip_WithoutEnumMemberAttribute_PreservesValue()
    {
        // Arrange
        var originalObj = new EnumConverterTestObject
        {
            Status = EnumConverterTestStatus.Pending
        };

        // Act
        var json = JsonSerializer.Serialize(originalObj);
        var deserializedObj = JsonSerializer.Deserialize<EnumConverterTestObject>(json);

        // Assert
        Assert.Equal(originalObj.Status, deserializedObj.Status);
    }

    [Fact]
    public void Serialize_AllEnumValues_ProducesExpectedStrings()
    {
        // Arrange & Act & Assert
        Assert.Equal("\"Pending\"", JsonSerializer.Serialize(EnumConverterTestStatus.Pending, CreateOptions()));
        Assert.Equal("\"active\"", JsonSerializer.Serialize(EnumConverterTestStatus.Active, CreateOptions()));
        Assert.Equal("\"in_progress\"", JsonSerializer.Serialize(EnumConverterTestStatus.InProgress, CreateOptions()));
        Assert.Equal("\"completed\"", JsonSerializer.Serialize(EnumConverterTestStatus.Completed, CreateOptions()));
    }

    [Fact]
    public void Deserialize_AllEnumValues_ReturnsCorrectEnums()
    {
        // Arrange & Act & Assert
        Assert.Equal(EnumConverterTestStatus.Pending, JsonSerializer.Deserialize<EnumConverterTestStatus>("\"Pending\"", CreateOptions()));
        Assert.Equal(EnumConverterTestStatus.Active, JsonSerializer.Deserialize<EnumConverterTestStatus>("\"active\"", CreateOptions()));
        Assert.Equal(EnumConverterTestStatus.InProgress, JsonSerializer.Deserialize<EnumConverterTestStatus>("\"in_progress\"", CreateOptions()));
        Assert.Equal(EnumConverterTestStatus.Completed, JsonSerializer.Deserialize<EnumConverterTestStatus>("\"completed\"", CreateOptions()));
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new EnumMemberJsonConverter<EnumConverterTestStatus>());
        return options;
    }
}

public class EnumConverterTestObject
{
    [JsonConverter(typeof(EnumMemberJsonConverter<EnumConverterTestStatus>))]
    public EnumConverterTestStatus Status { get; set; }
}

public class EnumConverterTestObjectWithMultiple
{
    [JsonConverter(typeof(EnumMemberJsonConverter<EnumConverterTestStatus>))]
    public EnumConverterTestStatus Status1 { get; set; }

    [JsonConverter(typeof(EnumMemberJsonConverter<EnumConverterTestStatus>))]
    public EnumConverterTestStatus Status2 { get; set; }

    [JsonConverter(typeof(EnumMemberJsonConverter<EnumConverterTestStatus>))]
    public EnumConverterTestStatus Status3 { get; set; }
}

public enum EnumConverterTestStatus
{
    Pending, // No EnumMemberAttribute

    [EnumMember(Value = "active")]
    Active,

    [EnumMember(Value = "in_progress")]
    InProgress,

    [EnumMember(Value = "completed")]
    Completed
}
