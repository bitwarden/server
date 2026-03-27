using System.Text.Json;
using Bit.Api.Tools.Models.Request;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Api.Test.Tools.Models.Request;

public class ReceiveRequestModelTests
{
    [Fact]
    public void ToReceive_SerializesNameIntoData()
    {
        var request = new ReceiveRequestModel
        {
            Name = "encrypted_name",
            ScekWrappedPublicKey = "encrypted_public_key",
            UserKeyWrappedSharedContentEncryptionKey = "encrypted_scek",
            UserKeyWrappedPrivateKey = "encrypted_private_key",
            ExpirationDate = null
        };

        var receive = request.ToReceive(Guid.NewGuid());

        using var jsonDocument = JsonDocument.Parse(receive.Data);
        var root = jsonDocument.RootElement;
        var name = AssertHelper.AssertJsonProperty(root, "Name", JsonValueKind.String).GetString();
        Assert.Equal("encrypted_name", name);
    }

    [Fact]
    public void ToReceive_SetsEmptyFileName()
    {
        var request = new ReceiveRequestModel
        {
            Name = "encrypted_name",
            ScekWrappedPublicKey = "encrypted_public_key",
            UserKeyWrappedSharedContentEncryptionKey = "encrypted_scek",
            UserKeyWrappedPrivateKey = "encrypted_private_key",
        };

        var receive = request.ToReceive(Guid.NewGuid());

        using var jsonDocument = JsonDocument.Parse(receive.Data);
        var root = jsonDocument.RootElement;
        var fileName = AssertHelper.AssertJsonProperty(root, "FileName", JsonValueKind.String).GetString();
        Assert.Equal(string.Empty, fileName);
    }

    [Fact]
    public void ToReceive_SetsKeyProperties()
    {
        var userId = Guid.NewGuid();
        var request = new ReceiveRequestModel
        {
            Name = "encrypted_name",
            ScekWrappedPublicKey = "encrypted_public_key",
            UserKeyWrappedSharedContentEncryptionKey = "encrypted_scek",
            UserKeyWrappedPrivateKey = "encrypted_private_key",
            ExpirationDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        };

        var receive = request.ToReceive(userId);

        Assert.Equal(userId, receive.UserId);
        Assert.Equal("encrypted_public_key", receive.ScekWrappedPublicKey);
        Assert.Equal("encrypted_scek", receive.UserKeyWrappedSharedContentEncryptionKey);
        Assert.Equal("encrypted_private_key", receive.UserKeyWrappedPrivateKey);
        Assert.Equal(new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc), receive.ExpirationDate);
    }
}
