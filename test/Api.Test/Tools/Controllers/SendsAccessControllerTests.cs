using System.Security.Claims;
using System.Text.Json;
using AutoFixture.Xunit2;
using Bit.Api.Tools.Controllers;
using Bit.Api.Tools.Models.Response;
using Bit.Core;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Services;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.SendFeatures.Commands.Interfaces;
using Bit.Core.Tools.SendFeatures.Queries.Interfaces;
using Bit.Core.Tools.SendFeatures.Services.Interfaces;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Tools.Controllers;

public class SendsAccessControllerTests : IDisposable
{
    private readonly SendsAccessController _sut;
    private readonly ISendAccessQuery _sendAccessQuery;
    private readonly IUserService _userService;
    private readonly ISendAccessCommand _sendAccessCommand;
    private readonly IEventService _eventService;
    private readonly ISendEventClassifier _sendEventClassifier;
    private readonly Bitwarden.Server.Sdk.Features.IFeatureService _featureService;

    public SendsAccessControllerTests()
    {
        _sendAccessQuery = Substitute.For<ISendAccessQuery>();
        _userService = Substitute.For<IUserService>();
        _sendAccessCommand = Substitute.For<ISendAccessCommand>();
        _eventService = Substitute.For<IEventService>();
        _sendEventClassifier = Substitute.For<ISendEventClassifier>();
        _featureService = Substitute.For<Bitwarden.Server.Sdk.Features.IFeatureService>();

        _sut = new SendsAccessController(
            _sendAccessQuery,
            _userService,
            _sendAccessCommand,
            _eventService,
            _sendEventClassifier,
            _featureService
        );
    }

    public void Dispose()
    {
        _sut?.Dispose();
    }

    [Theory, AutoData]
    public async Task AccessUsingAuth_WithValidSend_ReturnsSendAccessResponse(Guid sendId, User creator)
    {
        var send = new Send
        {
            Id = sendId,
            UserId = creator.Id,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("Test", "Notes", "Text", false)),
            HideEmail = false,
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = null,
            Disabled = false,
            AccessCount = 0,
            MaxAccessCount = null
        };
        var user = CreateUserWithSendIdClaim(sendId);
        _sut.ControllerContext = CreateControllerContextWithUser(user);
        _sendAccessQuery.Get(sendId).Returns(send);
        _userService.GetUserByIdAsync(creator.Id).Returns(creator);

        var result = await _sut.AccessUsingAuth();

        Assert.NotNull(result);
        var objectResult = Assert.IsType<ObjectResult>(result);
        var response = Assert.IsType<SendAccessResponseModel>(objectResult.Value);
        Assert.Equal(CoreHelpers.Base64UrlEncode(sendId.ToByteArray()), response.Id);
        Assert.Equal(creator.Email, response.CreatorIdentifier);
        await _sendAccessQuery.Received(1).Get(sendId);
        await _userService.Received(1).GetUserByIdAsync(creator.Id);
    }

    [Theory, AutoData]
    public async Task AccessUsingAuth_WithHideEmail_DoesNotIncludeCreatorIdentifier(Guid sendId, User creator)
    {
        var send = new Send
        {
            Id = sendId,
            UserId = creator.Id,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("Test", "Notes", "Text", false)),
            HideEmail = true,
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = null,
            Disabled = false,
            AccessCount = 0,
            MaxAccessCount = null
        };
        var user = CreateUserWithSendIdClaim(sendId);
        _sut.ControllerContext = CreateControllerContextWithUser(user);
        _sendAccessQuery.Get(sendId).Returns(send);

        var result = await _sut.AccessUsingAuth();

        Assert.NotNull(result);
        var objectResult = Assert.IsType<ObjectResult>(result);
        var response = Assert.IsType<SendAccessResponseModel>(objectResult.Value);
        Assert.Equal(CoreHelpers.Base64UrlEncode(sendId.ToByteArray()), response.Id);
        Assert.Null(response.CreatorIdentifier);
        await _sendAccessQuery.Received(1).Get(sendId);
        await _userService.DidNotReceive().GetUserByIdAsync(Arg.Any<Guid>());
    }

    [Theory, AutoData]
    public async Task AccessUsingAuth_WithNoUserId_DoesNotIncludeCreatorIdentifier(Guid sendId)
    {
        var send = new Send
        {
            Id = sendId,
            UserId = null,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("Test", "Notes", "Text", false)),
            HideEmail = false,
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = null,
            Disabled = false,
            AccessCount = 0,
            MaxAccessCount = null
        };
        var user = CreateUserWithSendIdClaim(sendId);
        _sut.ControllerContext = CreateControllerContextWithUser(user);
        _sendAccessQuery.Get(sendId).Returns(send);

        var result = await _sut.AccessUsingAuth();

        Assert.NotNull(result);
        var objectResult = Assert.IsType<ObjectResult>(result);
        var response = Assert.IsType<SendAccessResponseModel>(objectResult.Value);
        Assert.Equal(CoreHelpers.Base64UrlEncode(sendId.ToByteArray()), response.Id);
        Assert.Null(response.CreatorIdentifier);
        await _sendAccessQuery.Received(1).Get(sendId);
        await _userService.DidNotReceive().GetUserByIdAsync(Arg.Any<Guid>());
    }

    [Theory, AutoData]
    public async Task AccessUsingAuth_WithNonExistentSend_ThrowsBadRequestException(Guid sendId)
    {
        var user = CreateUserWithSendIdClaim(sendId);
        _sut.ControllerContext = CreateControllerContextWithUser(user);
        _sendAccessQuery.Get(sendId).Returns((Send)null);

        var exception =
            await Assert.ThrowsAsync<BadRequestException>(() => _sut.AccessUsingAuth());

        Assert.Equal("Could not locate send", exception.Message);
        await _sendAccessQuery.Received(1).Get(sendId);
    }

    [Theory, AutoData]
    public async Task AccessUsingAuth_WithFileSend_ReturnsCorrectResponse(Guid sendId, User creator)
    {
        var fileData = new SendFileData("Test File", "Notes", "document.pdf") { Id = "file-123", Size = 2048 };
        var send = new Send
        {
            Id = sendId,
            UserId = creator.Id,
            Type = SendType.File,
            Data = JsonSerializer.Serialize(fileData),
            HideEmail = false,
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = null,
            Disabled = false,
            AccessCount = 0,
            MaxAccessCount = null
        };
        var user = CreateUserWithSendIdClaim(sendId);
        _sut.ControllerContext = CreateControllerContextWithUser(user);
        _sendAccessQuery.Get(sendId).Returns(send);
        _userService.GetUserByIdAsync(creator.Id).Returns(creator);

        var result = await _sut.AccessUsingAuth();

        Assert.NotNull(result);
        var objectResult = Assert.IsType<ObjectResult>(result);
        var response = Assert.IsType<SendAccessResponseModel>(objectResult.Value);
        Assert.Equal(CoreHelpers.Base64UrlEncode(sendId.ToByteArray()), response.Id);
        Assert.Equal(SendType.File, response.Type);
        Assert.NotNull(response.File);
        Assert.Equal("file-123", response.File.Id);
        Assert.Equal(creator.Email, response.CreatorIdentifier);
    }

    [Theory]
    [InlineAutoData(SendType.Text)]
    [InlineAutoData(SendType.Item)]
    public async Task AccessUsingAuth_WithCountableSendType_IncrementsAccessCount(SendType sendType, Guid sendId)
    {
        _featureService.IsEnabled(FeatureFlagKeys.TemporaryItemSharing).Returns(true);
        var send = new Send
        {
            Id = sendId,
            Type = sendType,
            Data = JsonSerializer.Serialize(new SendTextData("Test", "Notes", "Text", false)),
            DeletionDate = DateTime.UtcNow.AddDays(7),
            Disabled = false,
        };
        _sut.ControllerContext = CreateControllerContextWithUser(CreateUserWithSendIdClaim(sendId));
        _sendAccessQuery.Get(sendId).Returns(send);

        await _sut.AccessUsingAuth();

        await _sendAccessCommand.Received(1).IncrementAccessCountAsync(send);
    }

    [Theory, AutoData]
    public async Task AccessUsingAuth_WithFileSend_DoesNotIncrementAccessCount(Guid sendId)
    {
        var fileData = new SendFileData("Test File", "Notes", "document.pdf") { Id = "file-123", Size = 2048 };
        var send = new Send
        {
            Id = sendId,
            Type = SendType.File,
            Data = JsonSerializer.Serialize(fileData),
            DeletionDate = DateTime.UtcNow.AddDays(7),
            Disabled = false,
        };
        _sut.ControllerContext = CreateControllerContextWithUser(CreateUserWithSendIdClaim(sendId));
        _sendAccessQuery.Get(sendId).Returns(send);

        await _sut.AccessUsingAuth();

        await _sendAccessCommand.DidNotReceive().IncrementAccessCountAsync(Arg.Any<Send>());
    }

    [Theory, AutoData]
    public async Task AccessUsingAuth_WithDisabledSend_ThrowsNotFoundException(Guid sendId)
    {
        var send = new Send
        {
            Id = sendId,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("Test", "Notes", "Text", false)),
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = null,
            Disabled = true,
            AccessCount = 0,
            MaxAccessCount = null
        };
        var user = CreateUserWithSendIdClaim(sendId);
        _sut.ControllerContext = CreateControllerContextWithUser(user);
        _sendAccessQuery.Get(sendId).Returns(send);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.AccessUsingAuth());

        await _sendAccessQuery.Received(1).Get(sendId);
        await _userService.DidNotReceive().GetUserByIdAsync(Arg.Any<Guid>());
        await _sendAccessCommand.DidNotReceive().IncrementAccessCountAsync(Arg.Any<Send>());
    }

    [Theory, AutoData]
    public async Task AccessUsingAuth_WithMaxAccessCountReached_ThrowsNotFoundException(Guid sendId)
    {
        var send = new Send
        {
            Id = sendId,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("Test", "Notes", "Text", false)),
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = null,
            Disabled = false,
            AccessCount = 10,
            MaxAccessCount = 10
        };
        var user = CreateUserWithSendIdClaim(sendId);
        _sut.ControllerContext = CreateControllerContextWithUser(user);
        _sendAccessQuery.Get(sendId).Returns(send);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.AccessUsingAuth());

        await _sendAccessQuery.Received(1).Get(sendId);
        await _userService.DidNotReceive().GetUserByIdAsync(Arg.Any<Guid>());
        await _sendAccessCommand.DidNotReceive().IncrementAccessCountAsync(Arg.Any<Send>());
    }

    [Theory, AutoData]
    public async Task AccessUsingAuth_WithExpiredSend_ThrowsNotFoundException(Guid sendId)
    {
        var send = new Send
        {
            Id = sendId,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("Test", "Notes", "Text", false)),
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = DateTime.UtcNow.AddDays(-1), // Expired yesterday
            Disabled = false,
            AccessCount = 0,
            MaxAccessCount = null
        };
        var user = CreateUserWithSendIdClaim(sendId);
        _sut.ControllerContext = CreateControllerContextWithUser(user);
        _sendAccessQuery.Get(sendId).Returns(send);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.AccessUsingAuth());

        await _sendAccessQuery.Received(1).Get(sendId);
        await _userService.DidNotReceive().GetUserByIdAsync(Arg.Any<Guid>());
        await _sendAccessCommand.DidNotReceive().IncrementAccessCountAsync(Arg.Any<Send>());
    }

    [Theory, AutoData]
    public async Task AccessUsingAuth_WithDeletionDatePassed_ThrowsNotFoundException(Guid sendId)
    {
        var send = new Send
        {
            Id = sendId,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("Test", "Notes", "Text", false)),
            DeletionDate = DateTime.UtcNow.AddDays(-1), // Deletion date has passed
            ExpirationDate = null,
            Disabled = false,
            AccessCount = 0,
            MaxAccessCount = null
        };
        var user = CreateUserWithSendIdClaim(sendId);
        _sut.ControllerContext = CreateControllerContextWithUser(user);
        _sendAccessQuery.Get(sendId).Returns(send);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.AccessUsingAuth());

        await _sendAccessQuery.Received(1).Get(sendId);
        await _userService.DidNotReceive().GetUserByIdAsync(Arg.Any<Guid>());
        await _sendAccessCommand.DidNotReceive().IncrementAccessCountAsync(Arg.Any<Send>());
    }

    [Theory, AutoData]
    public async Task AccessUsingAuth_WithItemSendAndFeatureFlagOff_ThrowsNotFoundException(Guid sendId)
    {
        var send = new Send
        {
            Id = sendId,
            Type = SendType.Item,
            Data = JsonSerializer.Serialize(new SendItemData("Test Send", "Notes", SendEncryptionType.V1, "{ encrypted_field: \"ENCRYPTED_STRING\" }",
            new SendItemMetadata { ItemId = Guid.NewGuid(), CreationDate = DateTime.UtcNow, RevisionDate = DateTime.UtcNow })),
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = null,
            Disabled = false,
            AccessCount = 0,
            MaxAccessCount = null
        };
        var user = CreateUserWithSendIdClaim(sendId);
        _sut.ControllerContext = CreateControllerContextWithUser(user);
        _sendAccessQuery.Get(sendId).Returns(send);
        _featureService.IsEnabled(FeatureFlagKeys.TemporaryItemSharing).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.AccessUsingAuth());

        await _sendAccessQuery.Received(1).Get(sendId);
        await _userService.DidNotReceive().GetUserByIdAsync(Arg.Any<Guid>());
        await _sendAccessCommand.DidNotReceive().IncrementAccessCountAsync(Arg.Any<Send>());
    }

    [Theory, AutoData]
    public async Task GetSendFileDownloadDataUsingAuth_WithValidFileId_ReturnsDownloadUrl(
        Guid sendId, string fileId, string expectedUrl)
    {
        var fileData = new SendFileData("Test File", "Notes", "document.pdf") { Id = fileId, Size = 2048 };
        var send = new Send
        {
            Id = sendId,
            Type = SendType.File,
            Data = JsonSerializer.Serialize(fileData),
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = null,
            Disabled = false,
            AccessCount = 0,
            MaxAccessCount = null
        };
        var user = CreateUserWithSendIdClaim(sendId);
        _sut.ControllerContext = CreateControllerContextWithUser(user);
        _sendAccessQuery.Get(sendId).Returns(send);
        _sendAccessCommand.GetSendFileDownloadUrlAsync(send, fileId)
            .Returns((expectedUrl, SendAccessResult.Granted));

        var result = await _sut.GetSendFileDownloadDataUsingAuth(fileId);

        Assert.NotNull(result);
        var objectResult = Assert.IsType<ObjectResult>(result);
        var response = Assert.IsType<SendFileDownloadDataResponseModel>(objectResult.Value);
        Assert.Equal(fileId, response.Id);
        Assert.Equal(expectedUrl, response.Url);
        await _sendAccessQuery.Received(1).Get(sendId);
        await _sendAccessCommand.Received(1).GetSendFileDownloadUrlAsync(send, fileId);
    }

    [Theory, AutoData]
    public async Task GetSendFileDownloadDataUsingAuth_WithNonExistentSend_ThrowsBadRequestException(
        Guid sendId, string fileId)
    {
        var user = CreateUserWithSendIdClaim(sendId);
        _sut.ControllerContext = CreateControllerContextWithUser(user);
        _sendAccessQuery.Get(sendId).Returns((Send)null);

        var exception =
            await Assert.ThrowsAsync<BadRequestException>(() => _sut.GetSendFileDownloadDataUsingAuth(fileId));

        Assert.Equal("Could not locate send", exception.Message);
        await _sendAccessQuery.Received(1).Get(sendId);
        await _sendAccessCommand.DidNotReceive()
            .GetSendFileDownloadUrlAsync(Arg.Any<Send>(), Arg.Any<string>());
    }

    [Theory, AutoData]
    public async Task GetSendFileDownloadDataUsingAuth_WithTextSend_ThrowsBadRequestException(
        Guid sendId, string fileId)
    {
        var send = new Send
        {
            Id = sendId,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("Test", "Notes", "Text", false)),
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = null,
            Disabled = false,
            AccessCount = 0,
            MaxAccessCount = null
        };
        var user = CreateUserWithSendIdClaim(sendId);
        _sut.ControllerContext = CreateControllerContextWithUser(user);
        _sendAccessQuery.Get(sendId).Returns(send);
        _sendAccessCommand
            .When(x => x.GetSendFileDownloadUrlAsync(send, fileId))
            .Do(x => throw new BadRequestException("Can only get a download URL for a file type of Send"));

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _sut.GetSendFileDownloadDataUsingAuth(fileId));

        Assert.Equal("Can only get a download URL for a file type of Send", exception.Message);
        await _sendAccessQuery.Received(1).Get(sendId);
        await _sendAccessCommand.Received(1).GetSendFileDownloadUrlAsync(send, fileId);
    }

    [Theory, AutoData]
    public async Task GetSendFileDownloadDataUsingAuth_WithAccessDenied_ThrowsNotFoundException(
        Guid sendId, string fileId)
    {
        var fileData = new SendFileData("Test File", "Notes", "document.pdf") { Id = fileId, Size = 2048 };
        var send = new Send
        {
            Id = sendId,
            Type = SendType.File,
            Data = JsonSerializer.Serialize(fileData),
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = null,
            Disabled = false,
            AccessCount = 0,
            MaxAccessCount = null
        };
        var user = CreateUserWithSendIdClaim(sendId);
        _sut.ControllerContext = CreateControllerContextWithUser(user);
        _sendAccessQuery.Get(sendId).Returns(send);
        _sendAccessCommand.GetSendFileDownloadUrlAsync(send, fileId)
            .Returns((null, SendAccessResult.Denied));

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetSendFileDownloadDataUsingAuth(fileId));

        await _sendAccessQuery.Received(1).Get(sendId);
        await _sendAccessCommand.Received(1).GetSendFileDownloadUrlAsync(send, fileId);
    }

    [Theory]
    [InlineData("file/with/slash")]
    [InlineData("file..traversal")]
    [InlineData("file{{template")]
    [InlineData("file}}template")]
    public async Task GetSendFileDownloadDataUsingAuth_WithPathTraversalCharactersInFileId_ThrowsArgumentException(
        string fileIdWithInvalidChars)
    {
        // This test validates that path traversal attempts in fileId are rejected.
        // AzureSendFileStorageService.BlobName throws ArgumentException when fileId contains:
        // - / (path separator)
        // - .. (parent directory traversal)
        // - {{ or }} (template injection characters)
        //
        // Attack vectors:
        // - fileId="file/with/slash" -> attempts blob path traversal
        // - fileId="file..traversal" -> attempts parent directory access
        // - fileId="file{{template" -> attempts template injection
        // - fileId="file}}template" -> attempts template injection

        // Arrange
        var sendId = Guid.NewGuid();
        var fileData = new SendFileData("Test File", "Notes", "document.pdf")
        {
            Id = "validfileid123",  // Valid stored fileId
            Size = 2048
        };
        var send = new Send
        {
            Id = sendId,
            Type = SendType.File,
            Data = JsonSerializer.Serialize(fileData),
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = null,
            Disabled = false,
            AccessCount = 0,
            MaxAccessCount = null
        };
        var user = CreateUserWithSendIdClaim(sendId);
        _sut.ControllerContext = CreateControllerContextWithUser(user);
        _sendAccessQuery.Get(sendId).Returns(send);

        // Mock GetSendFileDownloadUrlAsync to throw ArgumentException when invalid characters detected
        _sendAccessCommand
            .When(x => x.GetSendFileDownloadUrlAsync(send, fileIdWithInvalidChars))
            .Do(x => throw new ArgumentException("File ID contains invalid characters", nameof(fileIdWithInvalidChars)));

        // Act & Assert - Should throw ArgumentException
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.GetSendFileDownloadDataUsingAuth(fileIdWithInvalidChars));

        Assert.Contains("File ID contains invalid characters", exception.Message);

        // Verify that the command was called with the invalid fileId
        await _sendAccessCommand.Received(1).GetSendFileDownloadUrlAsync(send, fileIdWithInvalidChars);
    }

    [Fact]
    public async Task AccessUsingAuth_TextSend_LogsSendAccessedText()
    {
        var sendId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var send = new Send
        {
            Id = sendId,
            UserId = userId,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("a", "b", "c", false)),
            AuthType = AuthType.None,
            HideEmail = true,
            DeletionDate = DateTime.UtcNow.AddDays(7),
        };

        _sut.ControllerContext = CreateControllerContextWithUser(CreateUserWithSendIdClaim(sendId));
        _sendAccessQuery.Get(sendId).Returns(send);

        await _sut.AccessUsingAuth();

        await _eventService.Received(1).LogSendEventAsync(
            userId,
            Arg.Any<Guid>(),
            EventType.Send_Accessed_Text,
            Arg.Any<IReadOnlyDictionary<Guid, SendAccessEventOrgContext>>());
    }

    [Fact]
    public async Task AccessUsingAuth_ItemSend_LogsSendAccessedItem()
    {
        _featureService.IsEnabled(FeatureFlagKeys.TemporaryItemSharing).Returns(true);
        var sendId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var send = new Send
        {
            Id = sendId,
            UserId = userId,
            Type = SendType.Item,
            Data = JsonSerializer.Serialize(new SendItemData
            {
                Data = "ENCRYPTED_DATA",
                EncryptionVersion = SendEncryptionType.V1,
                Metadata = new SendItemMetadata
                {
                    ItemId = Guid.NewGuid(),
                }
            }),
            AuthType = AuthType.Email,
            Emails = "recipient@bitwarden.com",
            HideEmail = true,
            DeletionDate = DateTime.UtcNow.AddDays(7),
        };

        _sut.ControllerContext = CreateControllerContextWithUser(CreateUserWithSendIdClaim(sendId));
        _sendAccessQuery.Get(sendId).Returns(send);

        await _sut.AccessUsingAuth();

        await _eventService.Received(1).LogSendEventAsync(
            userId,
            Arg.Any<Guid>(),
            EventType.Send_Accessed_Item,
            Arg.Any<IReadOnlyDictionary<Guid, SendAccessEventOrgContext>>());
    }

    [Fact]
    public async Task AccessUsingAuth_FileSend_DoesNotLogAccessEvent()
    {
        var sendId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var fileData = new SendFileData("name", "notes", "file.pdf") { Id = "fileid", Size = 1024 };
        var send = new Send
        {
            Id = sendId,
            UserId = userId,
            Type = SendType.File,
            Data = JsonSerializer.Serialize(fileData),
            AuthType = AuthType.None,
            HideEmail = true,
            DeletionDate = DateTime.UtcNow.AddDays(7),
        };

        _sut.ControllerContext = CreateControllerContextWithUser(CreateUserWithSendIdClaim(sendId));
        _sendAccessQuery.Get(sendId).Returns(send);

        await _sut.AccessUsingAuth();

        await _eventService.DidNotReceiveWithAnyArgs().LogSendEventAsync(default, default, default, default);
    }

    [Fact]
    public async Task AccessUsingAuth_TextSend_NullUserId_DoesNotLogAccessEvent()
    {
        var sendId = Guid.NewGuid();
        var send = new Send
        {
            Id = sendId,
            UserId = null,
            OrganizationId = Guid.NewGuid(),
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("a", "b", "c", false)),
            AuthType = AuthType.None,
            DeletionDate = DateTime.UtcNow.AddDays(7),
        };

        _sut.ControllerContext = CreateControllerContextWithUser(CreateUserWithSendIdClaim(sendId));
        _sendAccessQuery.Get(sendId).Returns(send);

        await _sut.AccessUsingAuth();

        await _eventService.DidNotReceiveWithAnyArgs().LogSendEventAsync(default, default, default, default);
    }

    [Fact]
    public async Task GetSendFileDownloadDataUsingAuth_LogsSendAccessedFile()
    {
        var sendId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var fileId = "fileid";
        var fileData = new SendFileData("name", "notes", "file.pdf") { Id = fileId, Size = 1024 };
        var send = new Send
        {
            Id = sendId,
            UserId = userId,
            Type = SendType.File,
            Data = JsonSerializer.Serialize(fileData),
            AuthType = AuthType.None,
        };

        _sut.ControllerContext = CreateControllerContextWithUser(CreateUserWithSendIdClaim(sendId));
        _sendAccessQuery.Get(sendId).Returns(send);
        _sendAccessCommand.GetSendFileDownloadUrlAsync(send, fileId)
            .Returns(("https://example.test/url", SendAccessResult.Granted));

        await _sut.GetSendFileDownloadDataUsingAuth(fileId);

        await _eventService.Received(1).LogSendEventAsync(
            userId,
            Arg.Any<Guid>(),
            EventType.Send_Accessed_File,
            Arg.Any<IReadOnlyDictionary<Guid, SendAccessEventOrgContext>>());
    }

    [Fact]
    public async Task GetSendFileDownloadDataUsingAuth_NullUserId_DoesNotLogAccessEvent()
    {
        var sendId = Guid.NewGuid();
        var fileId = "fileid";
        var fileData = new SendFileData("name", "notes", "file.pdf") { Id = fileId, Size = 1024 };
        var send = new Send
        {
            Id = sendId,
            UserId = null,
            OrganizationId = Guid.NewGuid(),
            Type = SendType.File,
            Data = JsonSerializer.Serialize(fileData),
            AuthType = AuthType.None,
        };

        _sut.ControllerContext = CreateControllerContextWithUser(CreateUserWithSendIdClaim(sendId));
        _sendAccessQuery.Get(sendId).Returns(send);
        _sendAccessCommand.GetSendFileDownloadUrlAsync(send, fileId)
            .Returns(("https://example.test/url", SendAccessResult.Granted));

        await _sut.GetSendFileDownloadDataUsingAuth(fileId);

        await _eventService.DidNotReceiveWithAnyArgs().LogSendEventAsync(default, default, default, default);
    }

    [Fact]
    public async Task AccessUsingAuth_TextSend_PassesAccessorEmail_ToClassifier()
    {
        var sendId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var send = new Send
        {
            Id = sendId,
            UserId = userId,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("a", "b", "c", false)),
            AuthType = AuthType.Email,
            Emails = "alice@example.com",
            HideEmail = true,
            DeletionDate = DateTime.UtcNow.AddDays(7),
        };

        _sut.ControllerContext = CreateControllerContextWithUser(
            CreateUserWithSendIdAndEmailClaims(sendId, "alice@example.com"));
        _sendAccessQuery.Get(sendId).Returns(send);

        await _sut.AccessUsingAuth();

        await _sendEventClassifier.Received(1).BuildAccessContextAsync(
            userId,
            "alice@example.com");
    }

    [Fact]
    public async Task GetSendFileDownloadDataUsingAuth_PassesAccessorEmail_ToClassifier()
    {
        var sendId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var fileId = "fileid";
        var fileData = new SendFileData("name", "notes", "file.pdf") { Id = fileId, Size = 1024 };
        var send = new Send
        {
            Id = sendId,
            UserId = userId,
            Type = SendType.File,
            Data = JsonSerializer.Serialize(fileData),
            AuthType = AuthType.Email,
            Emails = "alice@example.com",
        };

        _sut.ControllerContext = CreateControllerContextWithUser(
            CreateUserWithSendIdAndEmailClaims(sendId, "alice@example.com"));
        _sendAccessQuery.Get(sendId).Returns(send);
        _sendAccessCommand.GetSendFileDownloadUrlAsync(send, fileId)
            .Returns(("https://example.test/url", SendAccessResult.Granted));

        await _sut.GetSendFileDownloadDataUsingAuth(fileId);

        await _sendEventClassifier.Received(1).BuildAccessContextAsync(
            userId,
            "alice@example.com");
    }

    private static ClaimsPrincipal CreateUserWithSendIdClaim(Guid sendId)
    {
        var claims = new List<Claim> { new Claim("send_id", sendId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal CreateUserWithSendIdAndEmailClaims(Guid sendId, string email)
    {
        var claims = new List<Claim>
        {
            new Claim("send_id", sendId.ToString()),
            new Claim("send_email", email),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    private static ControllerContext CreateControllerContextWithUser(ClaimsPrincipal user)
    {
        return new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };
    }
}
