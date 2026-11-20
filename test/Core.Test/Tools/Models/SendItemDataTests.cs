using Bit.Core.Tools.Models.Data;
using Xunit;

namespace Bit.Core.Test.Tools.Models;

public class SendItemDataTests
{
    private static readonly Guid _itemId = Guid.Parse("5f1c2e0a-7b3d-4c8e-9a6f-1d2e3f4a5b6c");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{ \"metadata\": ")]
    [InlineData("{}")]
    [InlineData("{\"metadata\":null}")]
    [InlineData("{\"metadata\":{\"itemId\":\"00000000-0000-0000-0000-000000000000\"}}")]
    public void TryGetItemId_MissingMalformedOrEmpty_ReturnsNull(string? data)
    {
        Assert.Null(SendItemData.TryGetItemId(data));
    }

    [Fact]
    public void TryGetItemId_WithServerWrittenData_ReturnsItemId()
    {
        // The server writes item data with default (PascalCase) naming, see SendRequestModel.UpdateSend.
        var data = "{\"Metadata\":{\"ItemId\":\"5f1c2e0a-7b3d-4c8e-9a6f-1d2e3f4a5b6c\"}}";

        Assert.Equal(_itemId, SendItemData.TryGetItemId(data));
    }
}
