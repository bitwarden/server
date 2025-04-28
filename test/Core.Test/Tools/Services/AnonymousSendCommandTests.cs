using System.Text.Json;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.SendFeatures.Commands;
using Bit.Core.Tools.Services;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Tools.Services;

public class AnonymousSendCommandTests
{
    private readonly ISendRepository _sendRepository;
    private readonly ISendFileStorageService _sendFileStorageService;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly ISendAuthorizationService _sendAuthorizationService;
    private readonly AnonymousSendCommand _anonymousSendCommand;

    public AnonymousSendCommandTests()
    {
        _sendRepository = Substitute.For<ISendRepository>();
        _sendFileStorageService = Substitute.For<ISendFileStorageService>();
        _pushNotificationService = Substitute.For<IPushNotificationService>();
        _sendAuthorizationService = Substitute.For<ISendAuthorizationService>();

        _anonymousSendCommand = new AnonymousSendCommand(
            _sendRepository,
            _sendFileStorageService,
            _pushNotificationService,
            _sendAuthorizationService);
    }

    [Fact]
    public async Task GetSendFileDownloadUrlAsync_Success_ReturnsDownloadUrl()
    {
        // Arrange
        var send = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.File,
            AccessCount = 0,
            Data = JsonSerializer.Serialize(new { Id = "fileId123" })
        };
        var fileId = "fileId123";
        var password = "testPassword";
        var expectedUrl = "https://example.com/download";

        _sendAuthorizationService
            .SendCanBeAccessed(send, password)
            .Returns(SendAccessResult.Granted);

        _sendFileStorageService
            .GetSendFileDownloadUrlAsync(send, fileId)
            .Returns(expectedUrl);

        // Act
        var result =
            await _anonymousSendCommand.GetSendFileDownloadUrlAsync(send, fileId, password);

        // Assert
        Assert.Equal(expectedUrl, result.Item1);
        Assert.Equal(1, send.AccessCount);

        await _sendRepository.Received(1).ReplaceAsync(send);
        await _pushNotificationService.Received(1).PushSyncSendUpdateAsync(send);
    }

    [Fact]
    public async Task GetSendFileDownloadUrlAsync_AccessDenied_ReturnsNullWithReasons()
    {
        // Arrange
        var send = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.File,
            AccessCount = 0
        };
        var fileId = "fileId123";
        var password = "wrongPassword";

        _sendAuthorizationService
            .SendCanBeAccessed(send, password)
            .Returns(SendAccessResult.Denied);

        // Act
        var result =
            await _anonymousSendCommand.GetSendFileDownloadUrlAsync(send, fileId, password);

        // Assert
        Assert.Null(result.Item1);
        Assert.Equal(SendAccessResult.Denied, result.Item2);
        Assert.Equal(0, send.AccessCount);

        await _sendRepository.DidNotReceiveWithAnyArgs().ReplaceAsync(default);
        await _pushNotificationService.DidNotReceiveWithAnyArgs().PushSyncSendUpdateAsync(default);
    }

    [Fact]
    public async Task GetSendFileDownloadUrlAsync_NotFileSend_ThrowsBadRequestException()
    {
        // Arrange
        var send = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.Text
        };
        var fileId = "fileId123";
        var password = "testPassword";

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _anonymousSendCommand.GetSendFileDownloadUrlAsync(send, fileId, password));
    }
}
