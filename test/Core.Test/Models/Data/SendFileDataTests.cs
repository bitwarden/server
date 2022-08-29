using System.Text.Json;
using Bit.Core.Models.Data;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Core.Test.Models.Data;

public class SendFileDataTests
{
    [Fact]
    public void Serialize_Success()
    {
        var sut = new SendFileData
        {
            Id = "test",
            Size = 100,
            FileName = "thing.pdf",
            Validated = true,
        };

        var json = JsonSerializer.Serialize(sut);
        var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        AssertHelper.AssertJsonProperty(root, "Size", JsonValueKind.String);
        Assert.False(root.TryGetProperty("SizeString", out _));
    }
}
