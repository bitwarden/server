using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.Utilities;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class PermissiveStringConverterTests
{
    private const string numberJson = "{ \"StringProp\": 1, \"EnumerableStringProp\": [ 2, 3 ]}";
    private const string stringJson = "{ \"StringProp\": \"1\", \"EnumerableStringProp\": [ \"2\", \"3\" ]}";
    private const string nullAndEmptyJson = "{ \"StringProp\": null, \"EnumerableStringProp\": [] }";
    private const string singleValueJson = "{ \"StringProp\": 1, \"EnumerableStringProp\": \"Hello!\" }";
    private const string nullJson = "{ \"StringProp\": null, \"EnumerableStringProp\": null }";
    private const string boolJson = "{ \"StringProp\": true, \"EnumerableStringProp\": [ false, 1.2]}";
    private const string objectJsonOne = "{ \"StringProp\": { \"Message\": \"Hi\"}, \"EnumerableStringProp\": []}";
    private const string objectJsonTwo = "{ \"StringProp\": \"Hi\", \"EnumerableStringProp\": {}}";
    private readonly string bigNumbersJson =
    "{ \"StringProp\":" + decimal.MinValue + ", \"EnumerableStringProp\": [" + ulong.MaxValue + ", " + long.MinValue + "]}";

    [Theory]
    [InlineData(numberJson)]
    [InlineData(stringJson)]
    public void Read_Success(string json)
    {
        var obj = JsonSerializer.Deserialize<TestObject>(json);
        Assert.Equal("1", obj.StringProp);
        Assert.Equal(2, obj.EnumerableStringProp.Count());
        Assert.Equal("2", obj.EnumerableStringProp.ElementAt(0));
        Assert.Equal("3", obj.EnumerableStringProp.ElementAt(1));
    }

    [Fact]
    public void Read_Boolean_Success()
    {
        var obj = JsonSerializer.Deserialize<TestObject>(boolJson);
        Assert.Equal("True", obj.StringProp);
        Assert.Equal(2, obj.EnumerableStringProp.Count());
        Assert.Equal("False", obj.EnumerableStringProp.ElementAt(0));
        Assert.Equal("1.2", obj.EnumerableStringProp.ElementAt(1));
    }

    [Fact]
    public void Read_Float_Success_Culture()
    {
        var ci = new CultureInfo("sv-SE");
        Thread.CurrentThread.CurrentCulture = ci;
        Thread.CurrentThread.CurrentUICulture = ci;

        var obj = JsonSerializer.Deserialize<TestObject>(boolJson);
        Assert.Equal("1.2", obj.EnumerableStringProp.ElementAt(1));
    }

    [Fact]
    public void Read_BigNumbers_Success()
    {
        var obj = JsonSerializer.Deserialize<TestObject>(bigNumbersJson);
        Assert.Equal(decimal.MinValue.ToString(), obj.StringProp);
        Assert.Equal(2, obj.EnumerableStringProp.Count());
        Assert.Equal(ulong.MaxValue.ToString(), obj.EnumerableStringProp.ElementAt(0));
        Assert.Equal(long.MinValue.ToString(), obj.EnumerableStringProp.ElementAt(1));
    }

    [Fact]
    public void Read_SingleValue_Success()
    {
        var obj = JsonSerializer.Deserialize<TestObject>(singleValueJson);
        Assert.Equal("1", obj.StringProp);
        Assert.Single(obj.EnumerableStringProp);
        Assert.Equal("Hello!", obj.EnumerableStringProp.ElementAt(0));
    }

    [Fact]
    public void Read_NullAndEmptyJson_Success()
    {
        var obj = JsonSerializer.Deserialize<TestObject>(nullAndEmptyJson);
        Assert.Null(obj.StringProp);
        Assert.Empty(obj.EnumerableStringProp);
    }

    [Fact]
    public void Read_Null_Success()
    {
        var obj = JsonSerializer.Deserialize<TestObject>(nullJson);
        Assert.Null(obj.StringProp);
        Assert.Null(obj.EnumerableStringProp);
    }

    [Theory]
    [InlineData(objectJsonOne)]
    [InlineData(objectJsonTwo)]
    public void Read_Object_Throws(string json)
    {
        var exception = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TestObject>(json));
    }

    [Fact]
    public void Write_Success()
    {
        var json = JsonSerializer.Serialize(new TestObject
        {
            StringProp = "1",
            EnumerableStringProp = new List<string>
            {
                "2",
                "3",
            },
        });

        var jsonElement = JsonDocument.Parse(json).RootElement;

        var stringProp = AssertHelper.AssertJsonProperty(jsonElement, "StringProp", JsonValueKind.String);
        Assert.Equal("1", stringProp.GetString());
        var list = AssertHelper.AssertJsonProperty(jsonElement, "EnumerableStringProp", JsonValueKind.Array);
        Assert.Equal(2, list.GetArrayLength());
        var firstElement = list[0];
        Assert.Equal(JsonValueKind.String, firstElement.ValueKind);
        Assert.Equal("2", firstElement.GetString());
        var secondElement = list[1];
        Assert.Equal(JsonValueKind.String, secondElement.ValueKind);
        Assert.Equal("3", secondElement.GetString());
    }

    [Fact]
    public void Write_Null()
    {
        // When the values are null the converters aren't actually ran and it automatically serializes null
        var json = JsonSerializer.Serialize(new TestObject
        {
            StringProp = null,
            EnumerableStringProp = null,
        });

        var jsonElement = JsonDocument.Parse(json).RootElement;

        AssertHelper.AssertJsonProperty(jsonElement, "StringProp", JsonValueKind.Null);
        AssertHelper.AssertJsonProperty(jsonElement, "EnumerableStringProp", JsonValueKind.Null);
    }

    [Fact]
    public void Write_Empty()
    {
        // When the values are null the converters aren't actually ran and it automatically serializes null
        var json = JsonSerializer.Serialize(new TestObject
        {
            StringProp = "",
            EnumerableStringProp = Enumerable.Empty<string>(),
        });

        var jsonElement = JsonDocument.Parse(json).RootElement;

        var stringVal = AssertHelper.AssertJsonProperty(jsonElement, "StringProp", JsonValueKind.String).GetString();
        Assert.Equal("", stringVal);
        var array = AssertHelper.AssertJsonProperty(jsonElement, "EnumerableStringProp", JsonValueKind.Array);
        Assert.Equal(0, array.GetArrayLength());
    }
}

public class TestObject
{
    [JsonConverter(typeof(PermissiveStringConverter))]
    public string StringProp { get; set; }

    [JsonConverter(typeof(PermissiveStringEnumerableConverter))]
    public IEnumerable<string> EnumerableStringProp { get; set; }
}
