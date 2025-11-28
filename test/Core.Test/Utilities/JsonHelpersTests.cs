using System.Text.Json;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Core.Test.Helpers;

public class JsonHelpersTests
{
    private static void CompareJson<T>(T value, JsonSerializerOptions options, Newtonsoft.Json.JsonSerializerSettings settings)
    {
        var stgJson = JsonSerializer.Serialize(value, options);
        var nsJson = Newtonsoft.Json.JsonConvert.SerializeObject(value, settings);

        Assert.Equal(stgJson, nsJson);
    }


    [Fact]
    public void DefaultJsonOptions()
    {
        var testObject = new SimpleTestObject
        {
            Id = 0,
            Name = "Test",
        };

        CompareJson(testObject, JsonHelpers.Default, new Newtonsoft.Json.JsonSerializerSettings());
    }

    [Fact]
    public void IndentedJsonOptions()
    {
        var testObject = new SimpleTestObject
        {
            Id = 10,
            Name = "Test Name"
        };

        CompareJson(testObject, JsonHelpers.Indented, new Newtonsoft.Json.JsonSerializerSettings
        {
            Formatting = Newtonsoft.Json.Formatting.Indented,
        });
    }

    [Fact]
    public void NullValueHandlingJsonOptions()
    {
        var testObject = new SimpleTestObject
        {
            Id = 14,
            Name = null,
        };

        CompareJson(testObject, JsonHelpers.IgnoreWritingNull, new Newtonsoft.Json.JsonSerializerSettings
        {
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
        });
    }
}

public class SimpleTestObject
{
    public int Id { get; set; }
    public string Name { get; set; }
}
