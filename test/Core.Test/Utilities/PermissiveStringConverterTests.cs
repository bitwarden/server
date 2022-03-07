using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.Utilities;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Core.Test.Utilities
{
    public class PermissiveStringConverterTests
    {
        private const string numberJson = "{ \"StringProp\": 1, \"EnumerableStringProp\": [ 2, 3 ]}";
        private const string stringJson = "{ \"StringProp\": \"1\", \"EnumerableStringProp\": [ \"2\", \"3\" ]}";
        private const string nullJson = "{ \"StringProp\": null, \"EnumerableStringProp\": [] }";

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
        public void Read_NullJson_Success()
        {
            var obj = JsonSerializer.Deserialize<TestObject>(nullJson);
            Assert.Null(obj.StringProp);
            Assert.Empty(obj.EnumerableStringProp);
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

            AssertHelper.AssertJsonProperty(jsonElement, "StringProp", JsonValueKind.String);
            var list = AssertHelper.AssertJsonProperty(jsonElement, "EnumerableStringProp", JsonValueKind.Array);
            Assert.Equal(2, list.GetArrayLength());
            var firstElement = list[0];
            Assert.Equal(JsonValueKind.String, firstElement.ValueKind);
            Assert.Equal("2", firstElement.GetString());
            var secondElement = list[1];
            Assert.Equal(JsonValueKind.String, secondElement.ValueKind);
            Assert.Equal("3", secondElement.GetString());
        }
    }

    public class TestObject
    {
        [JsonConverter(typeof(PermissiveStringConverter))]
        public string StringProp { get; set; }

        [JsonConverter(typeof(PermissiveStringEnumerableConverter))]
        public IEnumerable<string> EnumerableStringProp { get; set; }
    }
}
