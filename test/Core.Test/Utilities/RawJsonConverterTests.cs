using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class RawJsonConverterTests
{
    // Representative Data payloads for policies that carry configuration data.
    public static IEnumerable<object[]> PolicyDataPayloads => new List<object[]>
    {
        // MasterPassword
        new object[] { "{\"minComplexity\":3,\"minLength\":14,\"requireLower\":true,\"requireUpper\":true,\"requireNumbers\":false,\"requireSpecial\":false,\"enforceOnLogin\":true}" },
        // SendOptions
        new object[] { "{\"disableHideEmail\":true}" },
        // ResetPassword
        new object[] { "{\"autoEnrollEnabled\":true}" },
        // Nested object and array values
        new object[] { "{\"allowed\":[1,2,3],\"nested\":{\"a\":true,\"b\":null},\"name\":\"Bitwarden\"}" },
        // Empty object
        new object[] { "{}" },
        // Single boolean flag (e.g. SingleOrg / PasswordGenerator style policies with no real data)
        new object[] { "{\"enabled\":true}" },
    };

    [Theory]
    [MemberData(nameof(PolicyDataPayloads))]
    public void Write_ProducesSameOutput_AsDeserializeToDictionaryThenSerialize(string json)
    {
        var plainOptions = new JsonSerializerOptions();

        // This mirrors the previous implementation: Dictionary<string, object> Deserialize + Serialize.
        var oldStyleOutput = JsonSerializer.Serialize(
            JsonSerializer.Deserialize<Dictionary<string, object>>(json, plainOptions), plainOptions);

        var rawJsonOptions = new JsonSerializerOptions();
        rawJsonOptions.Converters.Add(new RawJsonConverter());
        var newStyleOutput = JsonSerializer.Serialize(json, rawJsonOptions);

        Assert.Equal(oldStyleOutput, newStyleOutput);
    }

    [Fact]
    public void Write_Null_WritesJsonNull()
    {
        var wrapper = new RawJsonWrapper { Data = null };

        var json = JsonSerializer.Serialize(wrapper);

        Assert.Equal("{\"Data\":null}", json);
    }

    [Fact]
    public void Read_ValidJson_ReturnsRawText()
    {
        const string json = "{\"Data\":{\"foo\":\"bar\",\"baz\":1}}";

        var wrapper = JsonSerializer.Deserialize<RawJsonWrapper>(json);

        Assert.Equal("{\"foo\":\"bar\",\"baz\":1}", wrapper.Data);
    }

    [Fact]
    public void Read_JsonNull_ReturnsNull()
    {
        const string json = "{\"Data\":null}";

        var wrapper = JsonSerializer.Deserialize<RawJsonWrapper>(json);

        Assert.Null(wrapper.Data);
    }

    [Fact]
    public void Read_InvalidJson_ThrowsJsonException()
    {
        const string json = "{\"Data\":{invalid}}";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RawJsonWrapper>(json));
    }

    [Fact]
    public void RoundTrip_PreservesEquivalentJson()
    {
        const string original = "{\"a\":1,\"b\":[true,false,null],\"c\":\"text\"}";
        var wrapper = new RawJsonWrapper { Data = original };

        var json = JsonSerializer.Serialize(wrapper);
        var deserialized = JsonSerializer.Deserialize<RawJsonWrapper>(json);

        Assert.Equal(original, deserialized.Data);
    }
}

public class RawJsonWrapper
{
    [JsonConverter(typeof(RawJsonConverter))]
    public string Data { get; set; }
}
