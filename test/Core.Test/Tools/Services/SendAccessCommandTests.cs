using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models;
using Bit.Core.Platform.Push;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.SendFeatures.Commands;
using Bit.Core.Tools.SendFeatures.Commands.Interfaces;
using Bit.Core.Tools.Services;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Tools.Services;

public class SendAccessCommandTests
{
    private readonly ISendRepository _sendRepository;
    private readonly ISendFileStorageService _sendFileStorageService;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly SendAccessCommand _sendAccessCommand;

    public SendAccessCommandTests()
    {
        _sendRepository = Substitute.For<ISendRepository>();
        _sendFileStorageService = Substitute.For<ISendFileStorageService>();
        _pushNotificationService = Substitute.For<IPushNotificationService>();

        _sendAccessCommand = new SendAccessCommand(
            _sendRepository,
            _sendFileStorageService,
            _pushNotificationService
        );
    }

    [Fact]
    public async Task GetSendFileDownloadUrlAsync_WithTextSend_ThrowsBadRequest()
    {
        // Arrange
        var send = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.Text,
            UserId = Guid.NewGuid()
        };
        var fileId = "somefile123";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _sendAccessCommand.GetSendFileDownloadUrlAsync(send, fileId));

        Assert.Equal("Can only get a download URL for a file type of Send", exception.Message);

        // Verify no storage service methods were called
        await _sendFileStorageService.DidNotReceive()
            .GetSendFileDownloadUrlAsync(Arg.Any<Send>(), Arg.Any<string>());
    }

    [Fact]
    public async Task GetSendFileDownloadUrlAsync_WithDisabledSend_ReturnsDenied()
    {
        // Arrange
        var fileId = "file123";
        var send = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.File,
            UserId = Guid.NewGuid(),
            Disabled = true,
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = null,
            AccessCount = 0,
            MaxAccessCount = null
        };

        // Act
        var (url, result) = await _sendAccessCommand.GetSendFileDownloadUrlAsync(send, fileId);

        // Assert
        Assert.Null(url);
        Assert.Equal(SendAccessResult.Denied, result);

        // Verify no repository updates occurred
        await _sendRepository.DidNotReceive().ReplaceAsync(Arg.Any<Send>());
        await _pushNotificationService.DidNotReceive().PushAsync(Arg.Any<PushNotification<SyncSendPushNotification>>());
        await _sendFileStorageService.DidNotReceive()
            .GetSendFileDownloadUrlAsync(Arg.Any<Send>(), Arg.Any<string>());
    }

    [Fact]
    public async Task GetSendFileDownloadUrlAsync_WithMaxAccessCountReached_ReturnsDenied()
    {
        // Arrange
        var fileId = "file123";
        var send = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.File,
            UserId = Guid.NewGuid(),
            Disabled = false,
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = null,
            AccessCount = 5,
            MaxAccessCount = 5
        };

        // Act
        var (url, result) = await _sendAccessCommand.GetSendFileDownloadUrlAsync(send, fileId);

        // Assert
        Assert.Null(url);
        Assert.Equal(SendAccessResult.Denied, result);

        // Verify no repository updates occurred
        await _sendRepository.DidNotReceive().ReplaceAsync(Arg.Any<Send>());
        await _pushNotificationService.DidNotReceive().PushAsync(Arg.Any<PushNotification<SyncSendPushNotification>>());
        await _sendFileStorageService.DidNotReceive()
            .GetSendFileDownloadUrlAsync(Arg.Any<Send>(), Arg.Any<string>());
    }

    [Fact]
    public async Task GetSendFileDownloadUrlAsync_WithExpiredSend_ReturnsDenied()
    {
        // Arrange
        var fileId = "file123";
        var send = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.File,
            UserId = Guid.NewGuid(),
            Disabled = false,
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = DateTime.UtcNow.AddDays(-1), // Expired yesterday
            AccessCount = 0,
            MaxAccessCount = null
        };

        // Act
        var (url, result) = await _sendAccessCommand.GetSendFileDownloadUrlAsync(send, fileId);

        // Assert
        Assert.Null(url);
        Assert.Equal(SendAccessResult.Denied, result);

        // Verify no repository updates occurred
        await _sendRepository.DidNotReceive().ReplaceAsync(Arg.Any<Send>());
        await _pushNotificationService.DidNotReceive().PushAsync(Arg.Any<PushNotification<SyncSendPushNotification>>());
        await _sendFileStorageService.DidNotReceive()
            .GetSendFileDownloadUrlAsync(Arg.Any<Send>(), Arg.Any<string>());
    }

    [Fact]
    public async Task GetSendFileDownloadUrlAsync_WithDeletionDatePassed_ReturnsDenied()
    {
        // Arrange
        var fileId = "file123";
        var send = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.File,
            UserId = Guid.NewGuid(),
            Disabled = false,
            DeletionDate = DateTime.UtcNow.AddDays(-1), // Deletion date has passed
            ExpirationDate = null,
            AccessCount = 0,
            MaxAccessCount = null
        };

        // Act
        var (url, result) = await _sendAccessCommand.GetSendFileDownloadUrlAsync(send, fileId);

        // Assert
        Assert.Null(url);
        Assert.Equal(SendAccessResult.Denied, result);

        // Verify no repository updates occurred
        await _sendRepository.DidNotReceive().ReplaceAsync(Arg.Any<Send>());
        await _pushNotificationService.DidNotReceive().PushAsync(Arg.Any<PushNotification<SyncSendPushNotification>>());
        await _sendFileStorageService.DidNotReceive()
            .GetSendFileDownloadUrlAsync(Arg.Any<Send>(), Arg.Any<string>());
    }

    [Fact]
    public async Task GetSendFileDownloadUrlAsync_WithValidSend_ReturnsUrlAndIncrementsAccessCount()
    {
        // Arrange
        var fileId = "file123";
        var expectedUrl = "https://download.example.com/file123";
        var sendFileData = new SendFileData { Id = fileId, Size = 1000, Validated = true };
        var send = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.File,
            UserId = Guid.NewGuid(),
            Disabled = false,
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = null,
            AccessCount = 3,
            MaxAccessCount = 10,
            Data = JsonSerializer.Serialize(sendFileData)
        };

        _sendFileStorageService.GetSendFileDownloadUrlAsync(send, fileId).Returns(expectedUrl);

        // Act
        var (url, result) = await _sendAccessCommand.GetSendFileDownloadUrlAsync(send, fileId);

        // Assert
        Assert.Equal(expectedUrl, url);
        Assert.Equal(SendAccessResult.Granted, result);

        // Verify access count was incremented
        Assert.Equal(4, send.AccessCount);

        // Verify repository was updated
        await _sendRepository.Received(1).ReplaceAsync(send);
        await _pushNotificationService.Received(1).PushAsync(Arg.Is<PushNotification<SyncSendPushNotification>>(n => n.Type == PushType.SyncSendUpdate && n.Payload.Id == send.Id));

        // Verify file storage service was called
        await _sendFileStorageService.Received(1).GetSendFileDownloadUrlAsync(send, fileId);
    }

    [Fact]
    public async Task GetSendFileDownloadUrlAsync_WithMismatchedFileId_ThrowsNotFoundException()
    {
        // Arrange
        var fileId = "file123";
        var wrongFileId = "wrongfile456";
        var sendFileData = new SendFileData { Id = wrongFileId, Size = 1000, Validated = true };
        var send = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.File,
            UserId = Guid.NewGuid(),
            Disabled = false,
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = null,
            AccessCount = 0,
            MaxAccessCount = 10,
            Data = JsonSerializer.Serialize(sendFileData)
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            _sendAccessCommand.GetSendFileDownloadUrlAsync(send, fileId));

        // Verify repository and storage service were not called
        await _sendRepository.DidNotReceive().ReplaceAsync(Arg.Any<Send>());
        await _sendFileStorageService.DidNotReceive().GetSendFileDownloadUrlAsync(Arg.Any<Send>(), Arg.Any<string>());
        await _pushNotificationService.DidNotReceive().PushAsync(Arg.Any<PushNotification<SyncSendPushNotification>>());
    }

    [Fact]
    public void SendCanBeAccessed_WithDisabledSend_ReturnsFalse()
    {
        // Arrange
        var send = new Send
        {
            Disabled = true,
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = null,
            AccessCount = 0,
            MaxAccessCount = null
        };

        // Act
        var result = ISendAccessCommand.SendCanBeAccessed(send);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SendCanBeAccessed_WithMaxAccessCountReached_ReturnsFalse()
    {
        // Arrange
        var send = new Send
        {
            Disabled = false,
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = null,
            AccessCount = 10,
            MaxAccessCount = 10
        };

        // Act
        var result = ISendAccessCommand.SendCanBeAccessed(send);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SendCanBeAccessed_WithExpiredSend_ReturnsFalse()
    {
        // Arrange
        var send = new Send
        {
            Disabled = false,
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = DateTime.UtcNow.AddDays(-1),
            AccessCount = 0,
            MaxAccessCount = null
        };

        // Act
        var result = ISendAccessCommand.SendCanBeAccessed(send);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SendCanBeAccessed_WithDeletionDatePassed_ReturnsFalse()
    {
        // Arrange
        var send = new Send
        {
            Disabled = false,
            DeletionDate = DateTime.UtcNow.AddDays(-1),
            ExpirationDate = null,
            AccessCount = 0,
            MaxAccessCount = null
        };

        // Act
        var result = ISendAccessCommand.SendCanBeAccessed(send);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SendCanBeAccessed_WithValidSend_ReturnsTrue()
    {
        // Arrange
        var send = new Send
        {
            Disabled = false,
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = DateTime.UtcNow.AddDays(7),
            AccessCount = 5,
            MaxAccessCount = 10
        };

        // Act
        var result = ISendAccessCommand.SendCanBeAccessed(send);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void SendCanBeAccessed_WithNullMaxAccessCount_ReturnsTrue()
    {
        // Arrange
        var send = new Send
        {
            Disabled = false,
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = null,
            AccessCount = 100,
            MaxAccessCount = null
        };

        // Act
        var result = ISendAccessCommand.SendCanBeAccessed(send);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void SendCanBeAccessed_WithNullExpirationDate_ReturnsTrue()
    {
        // Arrange
        var send = new Send
        {
            Disabled = false,
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = null,
            AccessCount = 0,
            MaxAccessCount = 10
        };

        // Act
        var result = ISendAccessCommand.SendCanBeAccessed(send);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IncrementAccessCountAsync_PersistsIncrementedCountAndPushesSync()
    {
        // Arrange
        var send = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.Text,
            UserId = Guid.NewGuid(),
            AccessCount = 3
        };

        // Act
        await _sendAccessCommand.IncrementAccessCountAsync(send);

        // Assert
        Assert.Equal(4, send.AccessCount);
        await _sendRepository.Received(1).ReplaceAsync(send);
        await _pushNotificationService.Received(1).PushSyncSendUpdateAsync(send);
    }
}
