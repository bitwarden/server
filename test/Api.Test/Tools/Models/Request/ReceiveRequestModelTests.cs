using System.Text.Json;
using Bit.Api.Tools.Models.Request;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Api.Test.Tools.Models.Request;

public class ReceiveRequestModelTests
{
    [Fact]
    public void ToReceive_SetsNameOnEntity()
    {
        var request = new ReceiveRequestModel
        {
            Name = "encrypted_name",
            ScekWrappedPublicKey = "encrypted_public_key",
            UserKeyWrappedSharedContentEncryptionKey = "encrypted_scek",
            UserKeyWrappedPrivateKey = "encrypted_private_key",
            ExpirationDate = DateTime.UtcNow.AddDays(7)
        };

        var receive = request.ToReceive(Guid.NewGuid());

        Assert.Equal("encrypted_name", receive.Name);
    }

    [Fact]
    public void ToReceive_InitializesEmptyFilesArray()
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
        var files = AssertHelper.AssertJsonProperty(root, "Files", JsonValueKind.Array);
        Assert.Equal(0, files.GetArrayLength());
    }

    [Fact]
    public void ToReceive_SetsKeyProperties()
    {
        var userId = Guid.NewGuid();
        var expirationDate = DateTime.UtcNow.AddDays(7);
        var request = new ReceiveRequestModel
        {
            Name = "encrypted_name",
            ScekWrappedPublicKey = "encrypted_public_key",
            UserKeyWrappedSharedContentEncryptionKey = "encrypted_scek",
            UserKeyWrappedPrivateKey = "encrypted_private_key",
            ExpirationDate = expirationDate
        };

        var receive = request.ToReceive(userId);

        Assert.Equal(userId, receive.UserId);
        Assert.Equal("encrypted_public_key", receive.ScekWrappedPublicKey);
        Assert.Equal("encrypted_scek", receive.UserKeyWrappedSharedContentEncryptionKey);
        Assert.Equal("encrypted_private_key", receive.UserKeyWrappedPrivateKey);
        Assert.Equal(expirationDate, receive.ExpirationDate);
    }
}
