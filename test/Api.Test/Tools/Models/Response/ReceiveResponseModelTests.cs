using System.Text.Json;
using Bit.Api.Tools.Models.Response;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Api.Test.Tools.Models.Response;

public class ReceiveResponseModelTests
{
    [Fact]
    public void Constructor_UnpacksNameFromData()
    {
        var fileData = new ReceiveFileData("encrypted_name", "encrypted_file.txt")
        {
            Id = "file_id_123",
            Size = 2048,
            Validated = true
        };

        var receive = new Receive
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Data = JsonSerializer.Serialize(fileData, JsonHelpers.IgnoreWritingNull),
            UserKeyWrappedSharedContentEncryptionKey = "encrypted_scek",
            UserKeyWrappedPrivateKey = "encrypted_private_key",
            ScekWrappedPublicKey = "encrypted_public_key",
            Secret = "test_secret",
        };

        var response = new ReceiveResponseModel(receive);

        Assert.Equal("encrypted_name", response.Name);
    }

    [Fact]
    public void Constructor_PopulatesFileModel()
    {
        var fileData = new ReceiveFileData("encrypted_name", "encrypted_file.txt")
        {
            Id = "file_id_123",
            Size = 2048,
            Validated = true
        };

        var receive = new Receive
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Data = JsonSerializer.Serialize(fileData, JsonHelpers.IgnoreWritingNull),
            UserKeyWrappedSharedContentEncryptionKey = "encrypted_scek",
            UserKeyWrappedPrivateKey = "encrypted_private_key",
            ScekWrappedPublicKey = "encrypted_public_key",
            Secret = "test_secret",
        };

        var response = new ReceiveResponseModel(receive);

        Assert.NotNull(response.File);
        Assert.Equal("file_id_123", response.File.Id);
        Assert.Equal("encrypted_file.txt", response.File.FileName);
        Assert.Equal(2048, response.File.Size);
    }

    [Fact]
    public void Constructor_MapsAllEntityProperties()
    {
        var fileData = new ReceiveFileData("encrypted_name", "encrypted_file.txt");
        var receiveId = Guid.NewGuid();
        var expirationDate = DateTime.UtcNow.AddDays(7);

        var receive = new Receive
        {
            Id = receiveId,
            UserId = Guid.NewGuid(),
            Data = JsonSerializer.Serialize(fileData, JsonHelpers.IgnoreWritingNull),
            UserKeyWrappedSharedContentEncryptionKey = "encrypted_scek",
            UserKeyWrappedPrivateKey = "encrypted_private_key",
            ScekWrappedPublicKey = "encrypted_public_key",
            Secret = "test_secret",
            UploadCount = 5,
            ExpirationDate = expirationDate,
        };

        var response = new ReceiveResponseModel(receive);

        Assert.Equal(receiveId, response.Id);
        Assert.Equal("encrypted_scek", response.UserKeyWrappedSharedContentEncryptionKey);
        Assert.Equal("encrypted_private_key", response.UserKeyWrappedPrivateKey);
        Assert.Equal("encrypted_public_key", response.ScekWrappedPublicKey);
        Assert.Equal("test_secret", response.Secret);
        Assert.Equal(5, response.UploadCount);
        Assert.Equal(expirationDate, response.ExpirationDate);
    }

}
