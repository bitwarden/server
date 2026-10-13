using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Services;
using Xunit;

namespace Bit.Core.Test.Tools.SendFeatures.Services;

public class AzureSendFileStorageServiceTests
{
    [Fact]
    public void BlobName_WithValidFileId_ReturnsCorrectPath()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var fileId = "validfileid123";
        var send = new Send { Id = sendId };

        // Act
        var result = AzureSendFileStorageService.BlobName(send, fileId);

        // Assert
        Assert.Equal($"{sendId}/{fileId}", result);
    }

    [Fact]
    public void BlobName_WithFileIdContainingForwardSlash_ThrowsArgumentException()
    {
        // Arrange
        var send = new Send { Id = Guid.NewGuid() };
        var fileId = "file/path/traversal";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            AzureSendFileStorageService.BlobName(send, fileId));

        Assert.Equal("fileId", exception.ParamName);
        Assert.Contains("invalid characters", exception.Message);
    }

    [Fact]
    public void BlobName_WithFileIdContainingDoubleDot_ThrowsArgumentException()
    {
        // Arrange
        var send = new Send { Id = Guid.NewGuid() };
        var fileId = "file..traversal";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            AzureSendFileStorageService.BlobName(send, fileId));

        Assert.Equal("fileId", exception.ParamName);
        Assert.Contains("invalid characters", exception.Message);
    }

    [Fact]
    public void BlobName_WithFileIdContainingDoubleOpenBrace_ThrowsArgumentException()
    {
        // Arrange
        var send = new Send { Id = Guid.NewGuid() };
        var fileId = "file{{malicious";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            AzureSendFileStorageService.BlobName(send, fileId));

        Assert.Equal("fileId", exception.ParamName);
        Assert.Contains("invalid characters", exception.Message);
    }

    [Fact]
    public void BlobName_WithFileIdContainingDoubleCloseBrace_ThrowsArgumentException()
    {
        // Arrange
        var send = new Send { Id = Guid.NewGuid() };
        var fileId = "file}}malicious";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            AzureSendFileStorageService.BlobName(send, fileId));

        Assert.Equal("fileId", exception.ParamName);
        Assert.Contains("invalid characters", exception.Message);
    }

    [Fact]
    public void BlobName_WithFileIdContainingMultipleInvalidCharacters_ThrowsArgumentException()
    {
        // Arrange
        var send = new Send { Id = Guid.NewGuid() };
        var fileId = "file../{{evil";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            AzureSendFileStorageService.BlobName(send, fileId));

        Assert.Equal("fileId", exception.ParamName);
        Assert.Contains("invalid characters", exception.Message);
    }
}
