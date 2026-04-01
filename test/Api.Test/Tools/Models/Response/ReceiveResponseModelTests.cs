using System.Text.Json;
using Bit.Api.Tools.Models.Response;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Models.Data;
using Xunit;

namespace Bit.Api.Test.Tools.Models.Response;

public class ReceiveResponseModelTests
{
    [Fact]
    public void Constructor_MapsNameFromEntity()
    {
        var receiveData = new ReceiveData
        {
            Files = new List<ReceiveFileData>
            {
                new() { Id = "file_id_123", FileName = "encrypted_file.txt", Size = 2048, Validated = true }
            }
        };

        var receive = new Receive
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = "encrypted_name",
            Data = JsonSerializer.Serialize(receiveData),
            UserKeyWrappedSharedContentEncryptionKey = "encrypted_scek",
            UserKeyWrappedPrivateKey = "encrypted_private_key",
            ScekWrappedPublicKey = "encrypted_public_key",
            Secret = "test_secret",
        };

        var response = new ReceiveResponseModel(receive);

        Assert.Equal("encrypted_name", response.Name);
    }

    [Fact]
    public void Constructor_PopulatesFilesArray()
    {
        var receiveData = new ReceiveData
        {
            Files = new List<ReceiveFileData>
            {
                new() { Id = "file_id_123", FileName = "encrypted_file.txt", Size = 2048, Validated = true },
                new() { Id = "file_id_456", FileName = "another_file.txt", Size = 4096, Validated = true }
            }
        };

        var receive = new Receive
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = "encrypted_name",
            Data = JsonSerializer.Serialize(receiveData),
            UserKeyWrappedSharedContentEncryptionKey = "encrypted_scek",
            UserKeyWrappedPrivateKey = "encrypted_private_key",
            ScekWrappedPublicKey = "encrypted_public_key",
            Secret = "test_secret",
        };

        var response = new ReceiveResponseModel(receive);

        Assert.NotNull(response.Files);
        var files = response.Files.ToList();
        Assert.Equal(2, files.Count);
        Assert.Equal("file_id_123", files[0].Id);
        Assert.Equal("encrypted_file.txt", files[0].FileName);
        Assert.Equal(2048, files[0].Size);
        Assert.Equal("file_id_456", files[1].Id);
    }

    [Fact]
    public void Constructor_EmptyFiles_ReturnsEmptyCollection()
    {
        var receiveData = new ReceiveData();

        var receive = new Receive
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = "encrypted_name",
            Data = JsonSerializer.Serialize(receiveData),
            UserKeyWrappedSharedContentEncryptionKey = "encrypted_scek",
            UserKeyWrappedPrivateKey = "encrypted_private_key",
            ScekWrappedPublicKey = "encrypted_public_key",
            Secret = "test_secret",
        };

        var response = new ReceiveResponseModel(receive);

        Assert.NotNull(response.Files);
        Assert.Empty(response.Files);
    }

    [Fact]
    public void Constructor_MapsAllEntityProperties()
    {
        var receiveData = new ReceiveData();
        var receiveId = Guid.NewGuid();
        var expirationDate = DateTime.UtcNow.AddDays(7);

        var receive = new Receive
        {
            Id = receiveId,
            UserId = Guid.NewGuid(),
            Data = JsonSerializer.Serialize(receiveData),
            Name = "encrypted_name",
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
