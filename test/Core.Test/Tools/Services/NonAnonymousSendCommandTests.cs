using System.Text.Json;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.CurrentContextFixtures;
using Bit.Core.Test.Tools.AutoFixture.SendFixtures;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.SendFeatures.Commands;
using Bit.Core.Tools.Services;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.Tools.Services;

[SutProviderCustomize]
[CurrentContextCustomize]
[UserSendCustomize]
public class NonAnonymousSendCommandTests
{
    private readonly ISendRepository _sendRepository;
    private readonly ISendFileStorageService _sendFileStorageService;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly ISendAuthorizationService _sendAuthorizationService;
    private readonly ISendValidationService _sendValidationService;
    private readonly IFeatureService _featureService;
    private readonly ICurrentContext _currentContext;
    private readonly ISendCoreHelperService _sendCoreHelperService;
    private readonly NonAnonymousSendCommand _nonAnonymousSendCommand;

    private readonly ILogger<NonAnonymousSendCommand> _logger;

    public NonAnonymousSendCommandTests()
    {
        _sendRepository = Substitute.For<ISendRepository>();
        _sendFileStorageService = Substitute.For<ISendFileStorageService>();
        _pushNotificationService = Substitute.For<IPushNotificationService>();
        _sendAuthorizationService = Substitute.For<ISendAuthorizationService>();
        _featureService = Substitute.For<IFeatureService>();
        _sendValidationService = Substitute.For<ISendValidationService>();
        _currentContext = Substitute.For<ICurrentContext>();
        _sendCoreHelperService = Substitute.For<ISendCoreHelperService>();
        _logger = Substitute.For<ILogger<NonAnonymousSendCommand>>();

        _nonAnonymousSendCommand = new NonAnonymousSendCommand(
            _sendRepository,
            _sendFileStorageService,
            _pushNotificationService,
            _sendAuthorizationService,
            _sendValidationService,
            _sendCoreHelperService,
            _logger
        );
    }

    // Disable Send policy check
    [Theory]
    [InlineData(SendType.File)]
    [InlineData(SendType.Text)]
    public async Task SaveSendAsync_DisableSend_Applies_throws(SendType sendType)
    {
        // Arrange
        var send = new Send
        {
            Id = default,
            Type = sendType,
            UserId = Guid.NewGuid()
        };

        var user = new User
        {
            Id = send.UserId.Value,
            Email = "test@example.com"
        };

        // Configure validation service to throw when DisableSend policy applies
        _sendValidationService.ValidateUserCanSaveAsync(send.UserId.Value, send)
            .Throws(new BadRequestException("Due to an Enterprise Policy, you are only able to delete an existing Send."));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _nonAnonymousSendCommand.SaveSendAsync(send));

        Assert.Contains("Enterprise Policy", exception.Message);

        // Verify the validation service was called
        await _sendValidationService.Received(1).ValidateUserCanSaveAsync(send.UserId.Value, send);

        // Verify repository was not called since exception was thrown
        await _sendRepository.DidNotReceive().CreateAsync(Arg.Any<Send>());
    }

    [Theory]
    [InlineData(true)]  // New Send (Id is default)
    [InlineData(false)] // Existing Send (Id is not default)
    public async Task SaveSendAsync_DisableSend_DoesntApply_success(bool isNewSend)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var send = new Send
        {
            Id = isNewSend ? default : Guid.NewGuid(),
            Type = SendType.Text,
            UserId = userId,
            Data = "Text with Notes"
        };

        var initialDate = DateTime.UtcNow.AddMinutes(-5);
        send.RevisionDate = initialDate;

        // Configure validation service to NOT throw (policy doesn't apply)
        _sendValidationService.ValidateUserCanSaveAsync(userId, send).Returns(Task.CompletedTask);

        // Set up context for reference event
        _currentContext.ClientId.Returns("test-client");
        _currentContext.ClientVersion.Returns(Version.Parse("1.0.0"));

        // Act
        await _nonAnonymousSendCommand.SaveSendAsync(send);

        // Assert
        // Verify validation was checked
        await _sendValidationService.Received(1).ValidateUserCanSaveAsync(userId, send);

        if (isNewSend)
        {
            // For new Sends
            await _sendRepository.Received(1).CreateAsync(send);
            await _pushNotificationService.Received(1).PushSyncSendCreateAsync(send);
        }
        else
        {
            // For existing Sends
            await _sendRepository.Received(1).UpsertAsync(send);
            Assert.NotEqual(initialDate, send.RevisionDate);
            await _pushNotificationService.Received(1).PushSyncSendUpdateAsync(send);
        }
    }

    [Theory]
    [InlineData(true)]  // New Send (Id is default)
    [InlineData(false)] // Existing Send (Id is not default)
    public async Task SaveSendAsync_DisableHideEmail_Applies_throws(bool isNewSend)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var send = new Send
        {
            Id = isNewSend ? default : Guid.NewGuid(),
            Type = SendType.Text,
            UserId = userId,
            HideEmail = true
        };

        // Configure validation service to throw when HideEmail policy applies
        _sendValidationService.ValidateUserCanSaveAsync(userId, send)
            .Throws(new BadRequestException("Due to an Enterprise Policy, you are not allowed to hide your email address from recipients when creating or editing a Send."));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _nonAnonymousSendCommand.SaveSendAsync(send));

        Assert.Contains("hide your email address", exception.Message);

        // Verify validation was called
        await _sendValidationService.Received(1).ValidateUserCanSaveAsync(userId, send);

        // Verify repository was not called (exception prevented save)
        if (isNewSend)
        {
            await _sendRepository.DidNotReceive().CreateAsync(Arg.Any<Send>());
        }
        else
        {
            await _sendRepository.DidNotReceive().UpsertAsync(Arg.Any<Send>());
        }

        // Verify push notification wasn't sent
        await _pushNotificationService.DidNotReceive().PushSyncSendCreateAsync(Arg.Any<Send>());
        await _pushNotificationService.DidNotReceive().PushSyncSendUpdateAsync(Arg.Any<Send>());
    }

    [Theory]
    [InlineData(true)]  // New Send (Id is default)
    [InlineData(false)] // Existing Send (Id is not default)
    public async Task SaveSendAsync_DisableHideEmail_DoesntApply_success(bool isNewSend)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var send = new Send
        {
            Id = isNewSend ? default : Guid.NewGuid(),
            Type = SendType.Text,
            UserId = userId,
            HideEmail = true  // Setting HideEmail to true
        };

        var initialDate = DateTime.UtcNow.AddMinutes(-5);
        send.RevisionDate = initialDate;

        // Configure validation service to NOT throw (policy doesn't apply)
        _sendValidationService.ValidateUserCanSaveAsync(userId, send).Returns(Task.CompletedTask);

        // Set up context for reference event
        _currentContext.ClientId.Returns("test-client");
        _currentContext.ClientVersion.Returns(Version.Parse("1.0.0"));

        // Act
        await _nonAnonymousSendCommand.SaveSendAsync(send);

        // Assert
        // Verify validation was checked
        await _sendValidationService.Received(1).ValidateUserCanSaveAsync(userId, send);

        if (isNewSend)
        {
            // For new Sends
            await _sendRepository.Received(1).CreateAsync(send);
            await _pushNotificationService.Received(1).PushSyncSendCreateAsync(send);
        }
        else
        {
            // For existing Sends
            await _sendRepository.Received(1).UpsertAsync(send);
            Assert.NotEqual(initialDate, send.RevisionDate);
            await _pushNotificationService.Received(1).PushSyncSendUpdateAsync(send);
        }
    }

    [Theory]
    [InlineData(SendType.File)]
    [InlineData(SendType.Text)]
    public async Task SaveSendAsync_DisableSend_Applies_Throws_vNext(SendType sendType)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var send = new Send
        {
            Id = default,
            Type = sendType,
            UserId = userId
        };

        // Configure validation service to throw when DisableSend policy applies in vNext implementation
        _sendValidationService.ValidateUserCanSaveAsync(userId, send)
            .Returns(Task.FromException(new BadRequestException("Due to an Enterprise Policy, you are only able to delete an existing Send.")));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _nonAnonymousSendCommand.SaveSendAsync(send));

        Assert.Contains("Enterprise Policy", exception.Message);

        // Verify validation service was called
        await _sendValidationService.Received(1).ValidateUserCanSaveAsync(userId, send);

        // Verify repository and notification methods were not called
        await _sendRepository.DidNotReceive().CreateAsync(Arg.Any<Send>());
        await _sendRepository.DidNotReceive().UpsertAsync(Arg.Any<Send>());
        await _pushNotificationService.DidNotReceive().PushSyncSendCreateAsync(Arg.Any<Send>());
        await _pushNotificationService.DidNotReceive().PushSyncSendUpdateAsync(Arg.Any<Send>());
    }

    [Theory]
    [InlineData(true)]  // New Send (Id is default)
    [InlineData(false)] // Existing Send (Id is not default)
    public async Task SaveSendAsync_DisableSend_DoesntApply_Success_vNext(bool isNewSend)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var send = new Send
        {
            Id = isNewSend ? default : Guid.NewGuid(),
            Type = SendType.Text,
            UserId = userId,
            Data = "Text with Notes"
        };

        var initialDate = DateTime.UtcNow.AddMinutes(-5);
        send.RevisionDate = initialDate;

        // Configure validation service to return success for vNext implementation
        _sendValidationService.ValidateUserCanSaveAsync(userId, send).Returns(Task.CompletedTask);

        // Set up context for reference event
        _currentContext.ClientId.Returns("test-client");
        _currentContext.ClientVersion.Returns(Version.Parse("1.0.0"));

        // Enable feature flag for policy requirements (vNext path)
        _featureService.IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(true);

        // Act
        await _nonAnonymousSendCommand.SaveSendAsync(send);

        // Assert
        // Verify validation was checked with vNext path
        await _sendValidationService.Received(1).ValidateUserCanSaveAsync(userId, send);

        if (isNewSend)
        {
            // For new Sends
            await _sendRepository.Received(1).CreateAsync(send);
            await _pushNotificationService.Received(1).PushSyncSendCreateAsync(send);
        }
        else
        {
            // For existing Sends
            await _sendRepository.Received(1).UpsertAsync(send);
            Assert.NotEqual(initialDate, send.RevisionDate);
            await _pushNotificationService.Received(1).PushSyncSendUpdateAsync(send);
        }
    }

    // Send Options Policy - Disable Hide Email check
    [Theory]
    [InlineData(true)]  // New Send (Id is default)
    [InlineData(false)] // Existing Send (Id is not default)
    public async Task SaveSendAsync_DisableHideEmail_Applies_Throws_vNext(bool isNewSend)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var send = new Send
        {
            Id = isNewSend ? default : Guid.NewGuid(),
            Type = SendType.Text,
            UserId = userId,
            HideEmail = true
        };

        // Enable feature flag for policy requirements (vNext path)
        _featureService.IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(true);

        // Configure validation service to throw when DisableHideEmail policy applies in vNext implementation
        _sendValidationService.ValidateUserCanSaveAsync(userId, send)
            .Throws(new BadRequestException("Due to an Enterprise Policy, you are not allowed to hide your email address from recipients when creating or editing a Send."));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _nonAnonymousSendCommand.SaveSendAsync(send));

        Assert.Contains("hide your email address", exception.Message);

        // Verify validation was called
        await _sendValidationService.Received(1).ValidateUserCanSaveAsync(userId, send);

        // Verify repository was not called (exception prevented save)
        await _sendRepository.DidNotReceive().CreateAsync(Arg.Any<Send>());
        await _sendRepository.DidNotReceive().UpsertAsync(Arg.Any<Send>());

        // Verify push notification wasn't sent
        await _pushNotificationService.DidNotReceive().PushSyncSendCreateAsync(Arg.Any<Send>());
        await _pushNotificationService.DidNotReceive().PushSyncSendUpdateAsync(Arg.Any<Send>());
    }

    [Theory]
    [InlineData(true)]  // New Send (Id is default)
    [InlineData(false)] // Existing Send (Id is not default)
    public async Task SaveSendAsync_DisableHideEmail_Applies_ButEmailNotHidden_Success_vNext(bool isNewSend)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var send = new Send
        {
            Id = isNewSend ? default : Guid.NewGuid(),
            Type = SendType.Text,
            UserId = userId,
            HideEmail = false  // Email is not hidden, so policy doesn't block
        };

        var initialDate = DateTime.UtcNow.AddMinutes(-5);
        send.RevisionDate = initialDate;

        // Enable feature flag for policy requirements (vNext path)
        _featureService.IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(true);

        // Configure validation service to allow saves when HideEmail is false
        _sendValidationService.ValidateUserCanSaveAsync(userId, send).Returns(Task.CompletedTask);

        // Set up context for reference event
        _currentContext.ClientId.Returns("test-client");
        _currentContext.ClientVersion.Returns(Version.Parse("1.0.0"));

        // Act
        await _nonAnonymousSendCommand.SaveSendAsync(send);

        // Assert
        // Verify validation was called with vNext path
        await _sendValidationService.Received(1).ValidateUserCanSaveAsync(userId, send);

        if (isNewSend)
        {
            // For new Sends
            await _sendRepository.Received(1).CreateAsync(send);
            await _pushNotificationService.Received(1).PushSyncSendCreateAsync(send);
        }
        else
        {
            // For existing Sends
            await _sendRepository.Received(1).UpsertAsync(send);
            Assert.NotEqual(initialDate, send.RevisionDate);
            await _pushNotificationService.Received(1).PushSyncSendUpdateAsync(send);
        }
    }

    [Fact]
    public async Task SaveSendAsync_ExistingSend_Updates()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sendId = Guid.NewGuid();
        var send = new Send
        {
            Id = sendId,
            Type = SendType.Text,
            UserId = userId,
            Data = "Some text data"
        };

        var initialDate = DateTime.UtcNow.AddMinutes(-5);
        send.RevisionDate = initialDate;

        // Act
        await _nonAnonymousSendCommand.SaveSendAsync(send);

        // Assert
        // Verify validation was called
        await _sendValidationService.Received(1).ValidateUserCanSaveAsync(userId, send);

        // Verify repository was called with updated send
        await _sendRepository.Received(1).UpsertAsync(send);

        // Check that the revision date was updated
        Assert.NotEqual(initialDate, send.RevisionDate);

        // Verify push notification was sent for the update
        await _pushNotificationService.Received(1).PushSyncSendUpdateAsync(send);
    }

    [Fact]
    public async Task SaveFileSendAsync_TextType_ThrowsBadRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var send = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.Text, // Text type instead of File
            UserId = userId
        };
        var fileData = new SendFileData();
        var fileLength = 1024L; // 1KB

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _nonAnonymousSendCommand.SaveFileSendAsync(send, fileData, fileLength));

        Assert.Contains("not of type \"file\"", exception.Message);

        // Verify no further methods were called
        await _sendValidationService.DidNotReceive().StorageRemainingForSendAsync(Arg.Any<Send>());
        await _sendRepository.DidNotReceive().CreateAsync(Arg.Any<Send>());
        await _sendRepository.DidNotReceive().UpsertAsync(Arg.Any<Send>());
        await _sendFileStorageService.DidNotReceive().GetSendFileUploadUrlAsync(Arg.Any<Send>(), Arg.Any<string>());
        await _pushNotificationService.DidNotReceive().PushSyncSendCreateAsync(Arg.Any<Send>());
        await _pushNotificationService.DidNotReceive().PushSyncSendUpdateAsync(Arg.Any<Send>());
    }

    [Fact]
    public async Task SaveFileSendAsync_EmptyFile_ThrowsBadRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var send = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.File,
            UserId = userId
        };
        var fileData = new SendFileData();
        var fileLength = 0L; // Empty file

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _nonAnonymousSendCommand.SaveFileSendAsync(send, fileData, fileLength));

        Assert.Contains("No file data", exception.Message);

        // Verify no methods were called after validation failed
        await _sendValidationService.DidNotReceive().StorageRemainingForSendAsync(Arg.Any<Send>());
        await _sendRepository.DidNotReceive().CreateAsync(Arg.Any<Send>());
        await _sendRepository.DidNotReceive().UpsertAsync(Arg.Any<Send>());
        await _sendFileStorageService.DidNotReceive().GetSendFileUploadUrlAsync(Arg.Any<Send>(), Arg.Any<string>());
        await _pushNotificationService.DidNotReceive().PushSyncSendCreateAsync(Arg.Any<Send>());
        await _pushNotificationService.DidNotReceive().PushSyncSendUpdateAsync(Arg.Any<Send>());
    }

    [Fact]
    public async Task SaveFileSendAsync_UserCannotAccessPremium_ThrowsBadRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var send = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.File,
            UserId = userId
        };
        var fileData = new SendFileData();
        var fileLength = 1024L; // 1KB

        // Configure validation service to throw when checking storage
        _sendValidationService.StorageRemainingForSendAsync(send)
            .Throws(new BadRequestException("You must have premium status to use file Sends."));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _nonAnonymousSendCommand.SaveFileSendAsync(send, fileData, fileLength));

        Assert.Contains("premium status", exception.Message);

        // Verify storage validation was called
        await _sendValidationService.Received(1).StorageRemainingForSendAsync(send);

        // Verify no further methods were called
        await _sendRepository.DidNotReceive().CreateAsync(Arg.Any<Send>());
        await _sendRepository.DidNotReceive().UpsertAsync(Arg.Any<Send>());
        await _sendFileStorageService.DidNotReceive().GetSendFileUploadUrlAsync(Arg.Any<Send>(), Arg.Any<string>());
        await _pushNotificationService.DidNotReceive().PushSyncSendCreateAsync(Arg.Any<Send>());
        await _pushNotificationService.DidNotReceive().PushSyncSendUpdateAsync(Arg.Any<Send>());
    }

    [Fact]
    public async Task SaveFileSendAsync_UserHasUnconfirmedEmail_ThrowsBadRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var send = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.File,
            UserId = userId
        };
        var fileData = new SendFileData();
        var fileLength = 1024L; // 1KB

        // Configure validation service to pass storage check
        _sendValidationService.StorageRemainingForSendAsync(send).Returns(10240L); // 10KB remaining

        // Configure validation service to throw when checking user can save
        _sendValidationService.When(x => x.ValidateUserCanSaveAsync(userId, send))
            .Throw(new BadRequestException("You must confirm your email before creating a Send."));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _nonAnonymousSendCommand.SaveFileSendAsync(send, fileData, fileLength));

        Assert.Contains("confirm your email", exception.Message);

        // Verify storage validation was called
        await _sendValidationService.Received(1).StorageRemainingForSendAsync(send);

        // Verify SaveSendAsync attempted to be called, triggering email validation
        await _sendValidationService.Received(1).ValidateUserCanSaveAsync(userId, send);

        // Verify no repository or notification methods were called after validation failed
        await _sendRepository.DidNotReceive().CreateAsync(Arg.Any<Send>());
        await _sendRepository.DidNotReceive().UpsertAsync(Arg.Any<Send>());
        await _pushNotificationService.DidNotReceive().PushSyncSendCreateAsync(Arg.Any<Send>());
        await _pushNotificationService.DidNotReceive().PushSyncSendUpdateAsync(Arg.Any<Send>());
    }

    [Fact]
    public async Task SaveFileSendAsync_UserCanAccessPremium_HasNoStorage_ThrowsBadRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var send = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.File,
            UserId = userId
        };
        var fileData = new SendFileData();
        var fileLength = 1024L; // 1KB

        // Configure validation service to return 0 storage remaining
        _sendValidationService.StorageRemainingForSendAsync(send).Returns(0L);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _nonAnonymousSendCommand.SaveFileSendAsync(send, fileData, fileLength));

        Assert.Contains("Not enough storage available", exception.Message);

        // Verify storage validation was called
        await _sendValidationService.Received(1).StorageRemainingForSendAsync(send);

        // Verify no further methods were called
        await _sendRepository.DidNotReceive().CreateAsync(Arg.Any<Send>());
        await _sendRepository.DidNotReceive().UpsertAsync(Arg.Any<Send>());
        await _sendFileStorageService.DidNotReceive().GetSendFileUploadUrlAsync(Arg.Any<Send>(), Arg.Any<string>());
        await _pushNotificationService.DidNotReceive().PushSyncSendCreateAsync(Arg.Any<Send>());
        await _pushNotificationService.DidNotReceive().PushSyncSendUpdateAsync(Arg.Any<Send>());
    }

    [Fact]
    public async Task SaveFileSendAsync_UserCanAccessPremium_StorageFull_ThrowsBadRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var send = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.File,
            UserId = userId
        };
        var fileData = new SendFileData();
        var fileLength = 1024L; // 1KB

        // Configure validation service to return less storage remaining than needed
        _sendValidationService.StorageRemainingForSendAsync(send).Returns(512L); // Only 512 bytes available

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _nonAnonymousSendCommand.SaveFileSendAsync(send, fileData, fileLength));

        Assert.Contains("Not enough storage available", exception.Message);

        // Verify storage validation was called
        await _sendValidationService.Received(1).StorageRemainingForSendAsync(send);

        // Verify no further methods were called
        await _sendRepository.DidNotReceive().CreateAsync(Arg.Any<Send>());
        await _sendRepository.DidNotReceive().UpsertAsync(Arg.Any<Send>());
        await _sendFileStorageService.DidNotReceive().GetSendFileUploadUrlAsync(Arg.Any<Send>(), Arg.Any<string>());
        await _pushNotificationService.DidNotReceive().PushSyncSendCreateAsync(Arg.Any<Send>());
        await _pushNotificationService.DidNotReceive().PushSyncSendUpdateAsync(Arg.Any<Send>());
    }

    [Fact]
    public async Task SaveFileSendAsync_UserCanAccessPremium_IsNotPremium_IsSelfHosted_GiantFile_ThrowsBadRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var send = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.File,
            UserId = userId
        };
        var fileData = new SendFileData();
        var fileLength = 15L * 1024L * 1024L; // 15 MB

        // Configure validation service to return insufficient storage
        _sendValidationService.StorageRemainingForSendAsync(send)
            .Returns(10L * 1024L * 1024L); // 10 MB remaining

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _nonAnonymousSendCommand.SaveFileSendAsync(send, fileData, fileLength));

        Assert.Contains("Not enough storage available", exception.Message);

        // Verify storage validation was called
        await _sendValidationService.Received(1).StorageRemainingForSendAsync(send);

        // Verify no further methods were called
        await _sendRepository.DidNotReceive().CreateAsync(Arg.Any<Send>());
        await _sendRepository.DidNotReceive().UpsertAsync(Arg.Any<Send>());
        await _sendFileStorageService.DidNotReceive().GetSendFileUploadUrlAsync(Arg.Any<Send>(), Arg.Any<string>());
        await _pushNotificationService.DidNotReceive().PushSyncSendCreateAsync(Arg.Any<Send>());
        await _pushNotificationService.DidNotReceive().PushSyncSendUpdateAsync(Arg.Any<Send>());
    }

    [Fact]
    public async Task SaveFileSendAsync_UserCanAccessPremium_IsNotPremium_IsNotSelfHosted_TwoGigabyteFile_ThrowsBadRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var send = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.File,
            UserId = userId
        };
        var fileData = new SendFileData();
        var fileLength = 2L * 1024L * 1024L * 1024L; // 2MB

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _nonAnonymousSendCommand.SaveFileSendAsync(send, fileData, fileLength));

        Assert.Contains("Max file size is ", exception.Message);

        // Verify no further methods were called
        await _sendValidationService.DidNotReceive().StorageRemainingForSendAsync(Arg.Any<Send>());
        await _sendRepository.DidNotReceive().CreateAsync(Arg.Any<Send>());
        await _sendRepository.DidNotReceive().UpsertAsync(Arg.Any<Send>());
        await _sendFileStorageService.DidNotReceive().GetSendFileUploadUrlAsync(Arg.Any<Send>(), Arg.Any<string>());
        await _pushNotificationService.DidNotReceive().PushSyncSendCreateAsync(Arg.Any<Send>());
        await _pushNotificationService.DidNotReceive().PushSyncSendUpdateAsync(Arg.Any<Send>());
    }

    [Fact]
    public async Task SaveFileSendAsync_UserCanAccessPremium_IsNotPremium_IsNotSelfHosted_NotEnoughSpace_ThrowsBadRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var send = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.File,
            UserId = userId
        };
        var fileData = new SendFileData();
        var fileLength = 2L * 1024L * 1024L; // 2MB

        // Configure validation service to return 1 MB storage remaining
        _sendValidationService.StorageRemainingForSendAsync(send)
            .Returns(1L * 1024L * 1024L);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _nonAnonymousSendCommand.SaveFileSendAsync(send, fileData, fileLength));

        Assert.Contains("Not enough storage available", exception.Message);

        // Verify storage validation was called
        await _sendValidationService.Received(1).StorageRemainingForSendAsync(send);

        // Verify no further methods were called
        await _sendRepository.DidNotReceive().CreateAsync(Arg.Any<Send>());
        await _sendRepository.DidNotReceive().UpsertAsync(Arg.Any<Send>());
        await _sendFileStorageService.DidNotReceive().GetSendFileUploadUrlAsync(Arg.Any<Send>(), Arg.Any<string>());
        await _pushNotificationService.DidNotReceive().PushSyncSendCreateAsync(Arg.Any<Send>());
        await _pushNotificationService.DidNotReceive().PushSyncSendUpdateAsync(Arg.Any<Send>());
    }

    [Fact]
    public async Task SaveFileSendAsync_ThroughOrg_MaxStorageIsNull_ThrowsBadRequest()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var send = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.File,
            OrganizationId = organizationId
        };

        var fileData = new SendFileData
        {
            FileName = "test.txt"
        };

        const long fileLength = 1000;

        // Set up validation service to return 0 storage remaining
        // This simulates the case when an organization's max storage is null
        _sendValidationService.StorageRemainingForSendAsync(send).Returns(0L);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _nonAnonymousSendCommand.SaveFileSendAsync(send, fileData, fileLength));

        Assert.Equal("Not enough storage available.", exception.Message);

        // Verify the method was called exactly once
        await _sendValidationService.Received(1).StorageRemainingForSendAsync(send);
    }

    [Fact]
    public async Task SaveFileSendAsync_ThroughOrg_MaxStorageIsNull_TwoGBFile_ThrowsBadRequest()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var send = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.File,
            OrganizationId = orgId,
            UserId = null
        };
        var fileData = new SendFileData();
        var fileLength = 2L * 1024L * 1024L; // 2 MB

        // Configure validation service to throw BadRequest when checking storage for org without storage
        _sendValidationService.StorageRemainingForSendAsync(send)
            .Throws(new BadRequestException("This organization cannot use file sends."));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _nonAnonymousSendCommand.SaveFileSendAsync(send, fileData, fileLength));

        Assert.Contains("This organization cannot use file sends", exception.Message);

        // Verify storage validation was called
        await _sendValidationService.Received(1).StorageRemainingForSendAsync(send);

        // Verify no further methods were called
        await _sendRepository.DidNotReceive().CreateAsync(Arg.Any<Send>());
        await _sendRepository.DidNotReceive().UpsertAsync(Arg.Any<Send>());
        await _sendFileStorageService.DidNotReceive().GetSendFileUploadUrlAsync(Arg.Any<Send>(), Arg.Any<string>());
        await _pushNotificationService.DidNotReceive().PushSyncSendCreateAsync(Arg.Any<Send>());
        await _pushNotificationService.DidNotReceive().PushSyncSendUpdateAsync(Arg.Any<Send>());
    }

    [Fact]
    public async Task SaveFileSendAsync_ThroughOrg_MaxStorageIsOneGB_TwoGBFile_ThrowsBadRequest()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var send = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.File,
            OrganizationId = orgId,
            UserId = null
        };
        var fileData = new SendFileData();
        var fileLength = 2L * 1024L * 1024L; // 2 MB

        _sendValidationService.StorageRemainingForSendAsync(send)
            .Returns(1L * 1024L * 1024L); // 1 MB remaining

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _nonAnonymousSendCommand.SaveFileSendAsync(send, fileData, fileLength));

        Assert.Contains("Not enough storage available", exception.Message);

        // Verify storage validation was called
        await _sendValidationService.Received(1).StorageRemainingForSendAsync(send);

        // Verify no further methods were called
        await _sendRepository.DidNotReceive().CreateAsync(Arg.Any<Send>());
        await _sendRepository.DidNotReceive().UpsertAsync(Arg.Any<Send>());
        await _sendFileStorageService.DidNotReceive().GetSendFileUploadUrlAsync(Arg.Any<Send>(), Arg.Any<string>());
        await _pushNotificationService.DidNotReceive().PushSyncSendCreateAsync(Arg.Any<Send>());
        await _pushNotificationService.DidNotReceive().PushSyncSendUpdateAsync(Arg.Any<Send>());
    }

    [Fact]
    public async Task SaveFileSendAsync_HasEnoughStorage_Success()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var send = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.File,
            UserId = userId
        };
        var fileData = new SendFileData();
        var fileLength = 500L * 1024L; // 500KB
        var expectedFileId = "generatedfileid";
        var expectedUploadUrl = "https://upload.example.com/url";

        // Configure storage validation to return more storage than needed
        _sendValidationService.StorageRemainingForSendAsync(send)
            .Returns(1024L * 1024L); // 1MB remaining

        // Configure file storage service to return upload URL
        _sendFileStorageService.GetSendFileUploadUrlAsync(Arg.Any<Send>(), Arg.Any<string>())
            .Returns(expectedUploadUrl);

        // Set up string generator to return predictable file ID
        _sendCoreHelperService.SecureRandomString(32, false, false)
            .Returns(expectedFileId);

        // Act
        var result = await _nonAnonymousSendCommand.SaveFileSendAsync(send, fileData, fileLength);

        // Assert
        Assert.Equal(expectedUploadUrl, result);

        // Verify storage validation was called
        await _sendValidationService.Received(1).StorageRemainingForSendAsync(send);

        // Verify upload URL was requested
        await _sendFileStorageService.Received(1).GetSendFileUploadUrlAsync(send, expectedFileId);
    }

    [Fact]
    public async Task SaveFileSendAsync_HasEnoughStorage_SendFileThrows_CleansUp()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var send = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.File,
            UserId = userId
        };
        var fileData = new SendFileData();
        var fileLength = 500L * 1024L; // 500KB
        var expectedFileId = "generatedfileid";

        // Configure storage validation to return more storage than needed
        _sendValidationService.StorageRemainingForSendAsync(send)
            .Returns(1024L * 1024L); // 1MB remaining

        // Set up string generator to return predictable file ID
        _sendCoreHelperService.SecureRandomString(32, false, false)
            .Returns(expectedFileId);

        // Configure file storage service to throw exception when getting upload URL
        _sendFileStorageService.GetSendFileUploadUrlAsync(Arg.Any<Send>(), Arg.Any<string>())
            .Throws(new Exception("Storage service unavailable"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _nonAnonymousSendCommand.SaveFileSendAsync(send, fileData, fileLength));

        // Verify storage validation was called
        await _sendValidationService.Received(1).StorageRemainingForSendAsync(send);

        // Verify file was cleaned up after failure
        await _sendFileStorageService.Received(1).DeleteFileAsync(send, expectedFileId);
    }

    [Fact]
    public async Task UpdateFileToExistingSendAsync_SendNull_ThrowsBadRequest()
    {
        // Arrange
        Stream stream = new MemoryStream();
        Send send = null;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _nonAnonymousSendCommand.UploadFileToExistingSendAsync(stream, send));

        Assert.Equal("Send does not have file data", exception.Message);

        // Verify no interactions with storage service
        await _sendFileStorageService.DidNotReceiveWithAnyArgs().UploadNewFileAsync(
            Arg.Any<Stream>(), Arg.Any<Send>(), Arg.Any<string>());
    }

    [Fact]
    public async Task UpdateFileToExistingSendAsync_SendDataNull_ThrowsBadRequest()
    {
        // Arrange
        Stream stream = new MemoryStream();
        var send = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.File,
            UserId = Guid.NewGuid(),
            Data = null // Send exists but has null Data property
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _nonAnonymousSendCommand.UploadFileToExistingSendAsync(stream, send));

        Assert.Equal("Send does not have file data", exception.Message);

        // Verify no interactions with storage service
        await _sendFileStorageService.DidNotReceiveWithAnyArgs().UploadNewFileAsync(
            Arg.Any<Stream>(), Arg.Any<Send>(), Arg.Any<string>());
    }

    [Fact]
    public async Task UpdateFileToExistingSendAsync_NotFileType_ThrowsBadRequest()
    {
        // Arrange
        Stream stream = new MemoryStream();
        var send = new Send
        {
            Id = Guid.NewGuid(),
            Type = SendType.Text, // Not a file type
            UserId = Guid.NewGuid(),
            Data = "{\"someData\":\"value\"}" // Has data, but not file data
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _nonAnonymousSendCommand.UploadFileToExistingSendAsync(stream, send));

        Assert.Equal("Not a File Type Send.", exception.Message);

        // Verify no interactions with storage service
        await _sendFileStorageService.DidNotReceiveWithAnyArgs().UploadNewFileAsync(
            Arg.Any<Stream>(), Arg.Any<Send>(), Arg.Any<string>());
    }

    [Fact]
    public async Task UpdateFileToExistingSendAsync_StreamPositionRestToZero_Success()
    {
        // Arrange
        var stream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        stream.Position = 2;
        var sendId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var fileId = "existingfileid123";

        var sendFileData = new SendFileData { Id = fileId, Size = 1000, Validated = false };
        var send = new Send
        {
            Id = sendId,
            UserId = userId,
            Type = SendType.File,
            Data = JsonSerializer.Serialize(sendFileData)
        };

        // Setup validation to succeed
        _sendFileStorageService.ValidateFileAsync(send, sendFileData.Id, Arg.Any<long>(), Arg.Any<long>()).Returns((true, sendFileData.Size));

        // Act
        await _nonAnonymousSendCommand.UploadFileToExistingSendAsync(stream, send);

        // Assert
        // Verify file was uploaded with correct parameters
        await _sendFileStorageService.Received(1).UploadNewFileAsync(
            Arg.Is<Stream>(s => s == stream && s.Position == 0), // Ensure stream position is reset
            Arg.Is<Send>(s => s.Id == sendId && s.UserId == userId),
            Arg.Is<string>(id => id == fileId)
        );
    }


    [Fact]
    public async Task UploadFileToExistingSendAsync_Success()
    {
        // Arrange
        var stream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        stream.Position = 2; // Simulate a non-zero position
        var sendId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var fileId = "existingfileid123";

        var sendFileData = new SendFileData { Id = fileId, Size = 1000, Validated = false };
        var send = new Send
        {
            Id = sendId,
            UserId = userId,
            Type = SendType.File,
            Data = JsonSerializer.Serialize(sendFileData)
        };

        _sendFileStorageService.ValidateFileAsync(send, sendFileData.Id, Arg.Any<long>(), Arg.Any<long>()).Returns((true, sendFileData.Size));

        // Act
        await _nonAnonymousSendCommand.UploadFileToExistingSendAsync(stream, send);

        // Assert
        // Verify file was uploaded with correct parameters
        await _sendFileStorageService.Received(1).UploadNewFileAsync(
            Arg.Is<Stream>(s => s == stream && s.Position == 0), // Ensure stream position is reset
            Arg.Is<Send>(s => s.Id == sendId && s.UserId == userId),
            Arg.Is<string>(id => id == fileId)
        );
    }

    [Fact]
    public async Task UpdateFileToExistingSendAsync_InvalidSize_ThrowsBadRequest()
    {
        // Arrange
        var stream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var sendId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var fileId = "existingfileid123";

        var sendFileData = new SendFileData { Id = fileId, Size = 1000, Validated = false };
        var send = new Send
        {
            Id = sendId,
            UserId = userId,
            Type = SendType.File,
            Data = JsonSerializer.Serialize(sendFileData)
        };

        // Configure storage service to upload successfully
        _sendFileStorageService.UploadNewFileAsync(
                Arg.Any<Stream>(), Arg.Any<Send>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        // Configure validation to fail due to file size mismatch
        _nonAnonymousSendCommand.ConfirmFileSize(send)
            .Returns(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _nonAnonymousSendCommand.UploadFileToExistingSendAsync(stream, send));

        Assert.Equal("File received does not match expected file length.", exception.Message);
    }
}
