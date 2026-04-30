using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Services;
using Xunit;

namespace Bit.Core.Test.Tools.Services;

public class AzureReceiveFileStorageServiceTests
{
    [Theory]
    [InlineData("e6a1b2c3-d4e5-f6a7-b8c9-d0e1f2a3b4c5/file1.enc", "e6a1b2c3-d4e5-f6a7-b8c9-d0e1f2a3b4c5")]
    [InlineData("abc/def/ghi", "abc")]
    public void ReceiveIdFromBlobName_ExtractsFirstSegment(string blobName, string expected)
    {
        var result = AzureReceiveFileStorageService.ReceiveIdFromBlobName(blobName);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BlobName_FormatsCorrectly()
    {
        var receive = new Receive
        {
            Id = Guid.Parse("e6a1b2c3-d4e5-f6a7-b8c9-d0e1f2a3b4c5"),
            UserId = Guid.NewGuid(),
            Name = "mock-name-data",
            Data = "{}",
            UserKeyWrappedSharedContentEncryptionKey = "key",
            UserKeyWrappedPrivateKey = "privkey",
            ScekWrappedPublicKey = "pubkey",
            Secret = "secret"
        };
        var fileId = "file123.enc";

        var result = AzureReceiveFileStorageService.BlobName(receive, fileId);

        Assert.Equal("e6a1b2c3-d4e5-f6a7-b8c9-d0e1f2a3b4c5/file123.enc", result);
    }

    [Fact]
    public void BlobName_AndReceiveIdFromBlobName_RoundTrip()
    {
        var receiveId = Guid.NewGuid();
        var receive = new Receive
        {
            Id = receiveId,
            UserId = Guid.NewGuid(),
            Data = "{}",
            Name = "2.scek|iv|ct",
            UserKeyWrappedSharedContentEncryptionKey = "key",
            UserKeyWrappedPrivateKey = "privkey",
            ScekWrappedPublicKey = "pubkey",
            Secret = "secret"
        };
        var fileId = "test-file.enc";

        var blobName = AzureReceiveFileStorageService.BlobName(receive, fileId);
        var extractedId = AzureReceiveFileStorageService.ReceiveIdFromBlobName(blobName);

        Assert.Equal(receiveId.ToString(), extractedId);
    }
}
