using System.Security.Claims;
using System.Text.Json;
using AutoFixture.Xunit2;
using Bit.Api.Models.Response;
using Bit.Api.Tools.Controllers;
using Bit.Api.Tools.Models;
using Bit.Api.Tools.Models.Request;
using Bit.Api.Tools.Models.Response;
using Bit.Core;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Services;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.SendFeatures.Commands.Interfaces;
using Bit.Core.Tools.SendFeatures.Queries.Interfaces;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Tools.Controllers;

public class SendsControllerTests : IDisposable
{
    private readonly SendsController _sut;
    private readonly IUserService _userService;
    private readonly ISendRepository _sendRepository;
    private readonly INonAnonymousSendCommand _nonAnonymousSendCommand;
    private readonly IAnonymousSendCommand _anonymousSendCommand;
    private readonly ISendOwnerQuery _sendOwnerQuery;
    private readonly ISendAuthorizationService _sendAuthorizationService;
    private readonly ISendFileStorageService _sendFileStorageService;
    private readonly ILogger<SendsController> _logger;
    private readonly IFeatureService _featureService;
    private readonly IPushNotificationService _pushNotificationService;

    public SendsControllerTests()
    {
        _userService = Substitute.For<IUserService>();
        _sendRepository = Substitute.For<ISendRepository>();
        _nonAnonymousSendCommand = Substitute.For<INonAnonymousSendCommand>();
        _anonymousSendCommand = Substitute.For<IAnonymousSendCommand>();
        _sendOwnerQuery = Substitute.For<ISendOwnerQuery>();
        _sendAuthorizationService = Substitute.For<ISendAuthorizationService>();
        _sendFileStorageService = Substitute.For<ISendFileStorageService>();
        _logger = Substitute.For<ILogger<SendsController>>();
        _featureService = Substitute.For<IFeatureService>();
        _pushNotificationService = Substitute.For<IPushNotificationService>();

        _sut = new SendsController(
            _sendRepository,
            _userService,
            _sendAuthorizationService,
            _anonymousSendCommand,
            _nonAnonymousSendCommand,
            _sendOwnerQuery,
            _sendFileStorageService,
            _logger,
            _featureService,
            _pushNotificationService
        );
    }

    public void Dispose()
    {
        _sut?.Dispose();
    }

    [Theory, AutoData]
    public async Task SendsController_WhenSendHidesEmail_CreatorIdentifierShouldBeNull(
        Guid id, Send send, User user)
    {
        var accessId = CoreHelpers.Base64UrlEncode(id.ToByteArray());

        send.Id = default;
        send.Type = SendType.Text;
        send.Data = JsonSerializer.Serialize(new Dictionary<string, string>());
        send.AuthType = AuthType.None;
        send.Emails = null;
        send.HideEmail = true;

        _sendRepository.GetByIdAsync(Arg.Any<Guid>()).Returns(send);
        _sendAuthorizationService.AccessAsync(send, null).Returns(SendAccessResult.Granted);
        _userService.GetUserByIdAsync(Arg.Any<Guid>()).Returns(user);

        var request = new SendAccessRequestModel();
        var actionResult = await _sut.Access(accessId, request);
        var response = (actionResult as ObjectResult)?.Value as SendAccessResponseModel;

        Assert.NotNull(response);
        Assert.Null(response.CreatorIdentifier);
    }

    [Fact]
    public async Task Post_DeletionDateIsMoreThan31DaysFromNow_ThrowsBadRequest()
    {
        var now = DateTime.UtcNow;
        var expected = "You cannot have a Send with a deletion date that far " +
                       "into the future. Adjust the Deletion Date to a value less than 31 days from now " +
                       "and try again.";
        var request = new SendRequestModel() { DeletionDate = now.AddDays(32) };

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Post(request));
        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public async Task PostFile_DeletionDateIsMoreThan31DaysFromNow_ThrowsBadRequest()
    {
        var now = DateTime.UtcNow;
        var expected = "You cannot have a Send with a deletion date that far " +
                       "into the future. Adjust the Deletion Date to a value less than 31 days from now " +
                       "and try again.";
        var request =
            new SendRequestModel() { Type = SendType.File, FileLength = 1024L, DeletionDate = now.AddDays(32) };

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.PostFile(request));
        Assert.Equal(expected, exception.Message);
    }

    [Theory, AutoData]
    public async Task Get_WithValidId_ReturnsSendResponseModel(Guid sendId, Send send)
    {
        send.Type = SendType.Text;
        var textData = new SendTextData("Test Send", "Notes", "Sample text", false);
        send.Data = JsonSerializer.Serialize(textData);
        _sendOwnerQuery.Get(sendId, Arg.Any<ClaimsPrincipal>()).Returns(send);

        var result = await _sut.Get(sendId.ToString());

        Assert.NotNull(result);
        Assert.IsType<SendResponseModel>(result);
        Assert.Equal(send.Id, result.Id);
        await _sendOwnerQuery.Received(1).Get(sendId, Arg.Any<ClaimsPrincipal>());
    }

    [Theory, AutoData]
    public async Task Get_WithInvalidGuid_ThrowsException(string invalidId)
    {
        await Assert.ThrowsAsync<FormatException>(() => _sut.Get(invalidId));
    }

    [Fact]
    public async Task GetAllOwned_ReturnsListResponseModelWithSendResponseModels()
    {
        var textSendData = new SendTextData("Test Send 1", "Notes 1", "Sample text", false);
        var fileSendData = new SendFileData("Test Send 2", "Notes 2", "test.txt") { Id = "file-123", Size = 1024 };
        var sends = new List<Send>
        {
            new Send { Id = Guid.NewGuid(), Type = SendType.Text, Data = JsonSerializer.Serialize(textSendData) },
            new Send { Id = Guid.NewGuid(), Type = SendType.File, Data = JsonSerializer.Serialize(fileSendData) }
        };
        _sendOwnerQuery.GetOwned(Arg.Any<ClaimsPrincipal>()).Returns(sends);

        var result = await _sut.GetAll();

        Assert.NotNull(result);
        Assert.IsType<ListResponseModel<SendResponseModel>>(result);
        Assert.Equal(2, result.Data.Count());
        var sendResponseModels = result.Data.ToList();
        Assert.Equal(sends[0].Id, sendResponseModels[0].Id);
        Assert.Equal(sends[1].Id, sendResponseModels[1].Id);
        await _sendOwnerQuery.Received(1).GetOwned(Arg.Any<ClaimsPrincipal>());
    }

    [Fact]
    public async Task GetAllOwned_WhenNoSends_ReturnsEmptyListResponseModel()
    {
        _sendOwnerQuery.GetOwned(Arg.Any<ClaimsPrincipal>()).Returns(new List<Send>());

        var result = await _sut.GetAll();

        Assert.NotNull(result);
        Assert.IsType<ListResponseModel<SendResponseModel>>(result);
        Assert.Empty(result.Data);
        await _sendOwnerQuery.Received(1).GetOwned(Arg.Any<ClaimsPrincipal>());
    }

    [Theory, AutoData]
    public async Task Post_WithPassword_InfersAuthTypePassword(Guid userId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        var request = new SendRequestModel
        {
            Type = SendType.Text,
            Key = "key",
            Text = new SendTextModel { Text = "text" },
            Password = "password",
            DeletionDate = DateTime.UtcNow.AddDays(7)
        };

        var result = await _sut.Post(request);

        Assert.NotNull(result);
        Assert.Equal(AuthType.Password, result.AuthType);
        await _nonAnonymousSendCommand.Received(1).SaveSendAsync(Arg.Is<Send>(s =>
            s.AuthType == AuthType.Password &&
            s.Password != null &&
            s.Emails == null &&
            s.UserId == userId &&
            s.Type == SendType.Text));
        _userService.Received(1).GetProperUserId(Arg.Any<ClaimsPrincipal>());
    }

    [Theory, AutoData]
    public async Task Post_WithEmails_InfersAuthTypeEmail(Guid userId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        var request = new SendRequestModel
        {
            Type = SendType.Text,
            Key = "key",
            Text = new SendTextModel { Text = "text" },
            Emails = "test@example.com",
            DeletionDate = DateTime.UtcNow.AddDays(7)
        };

        var result = await _sut.Post(request);

        Assert.NotNull(result);
        Assert.Equal(AuthType.Email, result.AuthType);
        await _nonAnonymousSendCommand.Received(1).SaveSendAsync(Arg.Is<Send>(s =>
            s.AuthType == AuthType.Email &&
            s.Emails != null &&
            s.Password == null &&
            s.UserId == userId &&
            s.Type == SendType.Text));
        _userService.Received(1).GetProperUserId(Arg.Any<ClaimsPrincipal>());
    }

    [Theory, AutoData]
    public async Task Post_WithoutPasswordOrEmails_InfersAuthTypeNone(Guid userId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        var request = new SendRequestModel
        {
            Type = SendType.Text,
            Key = "key",
            Text = new SendTextModel { Text = "text" },
            DeletionDate = DateTime.UtcNow.AddDays(7)
        };

        var result = await _sut.Post(request);

        Assert.NotNull(result);
        Assert.Equal(AuthType.None, result.AuthType);
        await _nonAnonymousSendCommand.Received(1).SaveSendAsync(Arg.Is<Send>(s =>
            s.AuthType == AuthType.None &&
            s.Password == null &&
            s.Emails == null &&
            s.UserId == userId &&
            s.Type == SendType.Text));
        _userService.Received(1).GetProperUserId(Arg.Any<ClaimsPrincipal>());
    }

    [Theory]
    [InlineData(AuthType.Password)]
    [InlineData(AuthType.Email)]
    [InlineData(AuthType.None)]
    public async Task Access_ReturnsCorrectAuthType(AuthType authType)
    {
        var sendId = Guid.NewGuid();
        var accessId = CoreHelpers.Base64UrlEncode(sendId.ToByteArray());
        var send = new Send
        {
            Id = sendId,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new Dictionary<string, string>()),
            AuthType = authType
        };

        _sendRepository.GetByIdAsync(sendId).Returns(send);
        _sendAuthorizationService.AccessAsync(send, "pwd123").Returns(SendAccessResult.Granted);

        var request = new SendAccessRequestModel();
        var actionResult = await _sut.Access(accessId, request);
        var response = (actionResult as ObjectResult)?.Value as SendAccessResponseModel;

        Assert.NotNull(response);
        Assert.Equal(authType, response.AuthType);
    }

    [Theory]
    [InlineData(AuthType.Password)]
    [InlineData(AuthType.Email)]
    [InlineData(AuthType.None)]
    public async Task Get_ReturnsCorrectAuthType(AuthType authType)
    {
        var sendId = Guid.NewGuid();
        var send = new Send
        {
            Id = sendId,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("a", "b", "c", false)),
            AuthType = authType
        };

        _sendOwnerQuery.Get(sendId, Arg.Any<ClaimsPrincipal>()).Returns(send);

        var result = await _sut.Get(sendId.ToString());

        Assert.NotNull(result);
        Assert.Equal(authType, result.AuthType);
    }

    [Theory, AutoData]
    public async Task Put_WithValidSend_UpdatesSuccessfully(Guid userId, Guid sendId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        var existingSend = new Send
        {
            Id = sendId,
            UserId = userId,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("Old", "Old notes", "Old text", false)),
            AuthType = AuthType.None
        };
        _sendRepository.GetByIdAsync(sendId).Returns(existingSend);

        var request = new SendRequestModel
        {
            Type = SendType.Text,
            Key = "updated-key",
            Text = new SendTextModel { Text = "updated text" },
            Password = "new-password",
            DeletionDate = DateTime.UtcNow.AddDays(7)
        };

        var result = await _sut.Put(sendId.ToString(), request);

        Assert.NotNull(result);
        Assert.Equal(sendId, result.Id);
        await _nonAnonymousSendCommand.Received(1).SaveSendAsync(Arg.Is<Send>(s => s.Id == sendId));
    }

    [Theory, AutoData]
    public async Task Put_WithNonExistentSend_ThrowsNotFoundException(Guid userId, Guid sendId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        _sendRepository.GetByIdAsync(sendId).Returns((Send)null);

        var request = new SendRequestModel
        {
            Type = SendType.Text,
            Key = "key",
            Text = new SendTextModel { Text = "text" },
            DeletionDate = DateTime.UtcNow.AddDays(7)
        };

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.Put(sendId.ToString(), request));
    }

    [Theory, AutoData]
    public async Task Put_WithWrongUser_ThrowsNotFoundException(Guid userId, Guid otherUserId, Guid sendId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        var existingSend = new Send
        {
            Id = sendId,
            UserId = otherUserId,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("Old", "Old notes", "Old text", false))
        };
        _sendRepository.GetByIdAsync(sendId).Returns(existingSend);

        var request = new SendRequestModel
        {
            Type = SendType.Text,
            Key = "key",
            Text = new SendTextModel { Text = "text" },
            DeletionDate = DateTime.UtcNow.AddDays(7)
        };

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.Put(sendId.ToString(), request));
    }

    [Theory, AutoData]
    public async Task PutRemovePassword_WithValidSend_RemovesPasswordAndSetsAuthTypeNone(Guid userId, Guid sendId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        var existingSend = new Send
        {
            Id = sendId,
            UserId = userId,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("Test", "Notes", "Text", false)),
            Password = "hashed-password",
            AuthType = AuthType.Password
        };
        _sendRepository.GetByIdAsync(sendId).Returns(existingSend);

        var result = await _sut.PutRemovePassword(sendId.ToString());

        Assert.NotNull(result);
        Assert.Equal(sendId, result.Id);
        Assert.Equal(AuthType.None, result.AuthType);
        await _nonAnonymousSendCommand.Received(1).SaveSendAsync(Arg.Is<Send>(s =>
            s.Id == sendId &&
            s.Password == null &&
            s.AuthType == AuthType.None));
    }

    [Theory, AutoData]
    public async Task PutRemovePassword_WithNonExistentSend_ThrowsNotFoundException(Guid userId, Guid sendId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        _sendRepository.GetByIdAsync(sendId).Returns((Send)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.PutRemovePassword(sendId.ToString()));
    }

    [Theory, AutoData]
    public async Task PutRemovePassword_WithWrongUser_ThrowsNotFoundException(Guid userId, Guid otherUserId,
        Guid sendId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        var existingSend = new Send
        {
            Id = sendId,
            UserId = otherUserId,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("Test", "Notes", "Text", false)),
            Password = "hashed-password"
        };
        _sendRepository.GetByIdAsync(sendId).Returns(existingSend);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.PutRemovePassword(sendId.ToString()));
    }

    [Theory, AutoData]
    public async Task Delete_WithValidSend_DeletesSuccessfully(Guid userId, Guid sendId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        var existingSend = new Send
        {
            Id = sendId,
            UserId = userId,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("Test", "Notes", "Text", false))
        };
        _sendRepository.GetByIdAsync(sendId).Returns(existingSend);

        await _sut.Delete(sendId.ToString());

        await _nonAnonymousSendCommand.Received(1).DeleteSendAsync(Arg.Is<Send>(s => s.Id == sendId));
    }

    [Theory, AutoData]
    public async Task Delete_WithNonExistentSend_ThrowsNotFoundException(Guid userId, Guid sendId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        _sendRepository.GetByIdAsync(sendId).Returns((Send)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.Delete(sendId.ToString()));
    }

    [Theory, AutoData]
    public async Task Delete_WithWrongUser_ThrowsNotFoundException(Guid userId, Guid otherUserId, Guid sendId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        var existingSend = new Send
        {
            Id = sendId,
            UserId = otherUserId,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("Test", "Notes", "Text", false))
        };
        _sendRepository.GetByIdAsync(sendId).Returns(existingSend);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.Delete(sendId.ToString()));
    }

    [Theory, AutoData]
    public async Task PostFile_WithPassword_InfersAuthTypePassword(Guid userId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        _nonAnonymousSendCommand.SaveFileSendAsync(Arg.Any<Send>(), Arg.Any<SendFileData>(), Arg.Any<long>())
            .Returns("https://example.com/upload")
            .AndDoes(callInfo =>
            {
                var send = callInfo.ArgAt<Send>(0);
                var data = callInfo.ArgAt<SendFileData>(1);
                send.Data = JsonSerializer.Serialize(data);
            });

        var request = new SendRequestModel
        {
            Type = SendType.File,
            Key = "key",
            File = new SendFileModel { FileName = "test.txt" },
            FileLength = 1024L,
            Password = "password",
            DeletionDate = DateTime.UtcNow.AddDays(7)
        };

        var result = await _sut.PostFile(request);

        Assert.NotNull(result);
        Assert.NotNull(result.SendResponse);
        Assert.Equal(AuthType.Password, result.SendResponse.AuthType);
        await _nonAnonymousSendCommand.Received(1).SaveFileSendAsync(
            Arg.Is<Send>(s =>
                s.AuthType == AuthType.Password &&
                s.Password != null &&
                s.Emails == null &&
                s.UserId == userId),
            Arg.Any<SendFileData>(),
            1024L);
    }

    [Theory, AutoData]
    public async Task PostFile_WithEmails_InfersAuthTypeEmail(Guid userId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        _nonAnonymousSendCommand.SaveFileSendAsync(Arg.Any<Send>(), Arg.Any<SendFileData>(), Arg.Any<long>())
            .Returns("https://example.com/upload")
            .AndDoes(callInfo =>
            {
                var send = callInfo.ArgAt<Send>(0);
                var data = callInfo.ArgAt<SendFileData>(1);
                send.Data = JsonSerializer.Serialize(data);
            });

        var request = new SendRequestModel
        {
            Type = SendType.File,
            Key = "key",
            File = new SendFileModel { FileName = "test.txt" },
            FileLength = 1024L,
            Emails = "test@example.com",
            DeletionDate = DateTime.UtcNow.AddDays(7)
        };

        var result = await _sut.PostFile(request);

        Assert.NotNull(result);
        Assert.NotNull(result.SendResponse);
        Assert.Equal(AuthType.Email, result.SendResponse.AuthType);
        await _nonAnonymousSendCommand.Received(1).SaveFileSendAsync(
            Arg.Is<Send>(s =>
                s.AuthType == AuthType.Email &&
                s.Emails != null &&
                s.Password == null &&
                s.UserId == userId),
            Arg.Any<SendFileData>(),
            1024L);
    }

    [Theory, AutoData]
    public async Task PostFile_WithoutPasswordOrEmails_InfersAuthTypeNone(Guid userId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        _nonAnonymousSendCommand.SaveFileSendAsync(Arg.Any<Send>(), Arg.Any<SendFileData>(), Arg.Any<long>())
            .Returns("https://example.com/upload")
            .AndDoes(callInfo =>
            {
                var send = callInfo.ArgAt<Send>(0);
                var data = callInfo.ArgAt<SendFileData>(1);
                send.Data = JsonSerializer.Serialize(data);
            });

        var request = new SendRequestModel
        {
            Type = SendType.File,
            Key = "key",
            File = new SendFileModel { FileName = "test.txt" },
            FileLength = 1024L,
            DeletionDate = DateTime.UtcNow.AddDays(7)
        };

        var result = await _sut.PostFile(request);

        Assert.NotNull(result);
        Assert.NotNull(result.SendResponse);
        Assert.Equal(AuthType.None, result.SendResponse.AuthType);
        await _nonAnonymousSendCommand.Received(1).SaveFileSendAsync(
            Arg.Is<Send>(s =>
                s.AuthType == AuthType.None &&
                s.Password == null &&
                s.Emails == null &&
                s.UserId == userId),
            Arg.Any<SendFileData>(),
            1024L);
    }

    [Theory, AutoData]
    public async Task Put_ChangingFromPasswordToEmails_UpdatesAuthTypeToEmail(Guid userId, Guid sendId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        var existingSend = new Send
        {
            Id = sendId,
            UserId = userId,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("Old", "Old notes", "Old text", false)),
            Password = "hashed-password",
            AuthType = AuthType.Password
        };
        _sendRepository.GetByIdAsync(sendId).Returns(existingSend);

        var request = new SendRequestModel
        {
            Type = SendType.Text,
            Key = "updated-key",
            Text = new SendTextModel { Text = "updated text" },
            Emails = "new@example.com",
            DeletionDate = DateTime.UtcNow.AddDays(7)
        };

        var result = await _sut.Put(sendId.ToString(), request);

        Assert.NotNull(result);
        Assert.Equal(sendId, result.Id);
        await _nonAnonymousSendCommand.Received(1).SaveSendAsync(Arg.Is<Send>(s =>
            s.Id == sendId &&
            s.AuthType == AuthType.Email &&
            s.Emails != null &&
            s.Password == null));
    }

    [Theory, AutoData]
    public async Task Put_ChangingFromEmailToPassword_UpdatesAuthTypeToPassword(Guid userId, Guid sendId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        var existingSend = new Send
        {
            Id = sendId,
            UserId = userId,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("Old", "Old notes", "Old text", false)),
            Emails = "old@example.com",
            AuthType = AuthType.Email
        };
        _sendRepository.GetByIdAsync(sendId).Returns(existingSend);

        var request = new SendRequestModel
        {
            Type = SendType.Text,
            Key = "updated-key",
            Text = new SendTextModel { Text = "updated text" },
            Password = "new-password",
            DeletionDate = DateTime.UtcNow.AddDays(7)
        };

        var result = await _sut.Put(sendId.ToString(), request);

        Assert.NotNull(result);
        Assert.Equal(sendId, result.Id);
        await _nonAnonymousSendCommand.Received(1).SaveSendAsync(Arg.Is<Send>(s =>
            s.Id == sendId &&
            s.AuthType == AuthType.Password &&
            s.Password != null &&
            s.Emails == null));
    }

    [Theory, AutoData]
    public async Task Put_WithoutPasswordOrEmails_PreservesExistingPassword(Guid userId, Guid sendId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        var existingSend = new Send
        {
            Id = sendId,
            UserId = userId,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("Old", "Old notes", "Old text", false)),
            Password = "hashed-password",
            AuthType = AuthType.Password
        };
        _sendRepository.GetByIdAsync(sendId).Returns(existingSend);

        var request = new SendRequestModel
        {
            Type = SendType.Text,
            Key = "updated-key",
            Text = new SendTextModel { Text = "updated text" },
            DeletionDate = DateTime.UtcNow.AddDays(7)
        };

        var result = await _sut.Put(sendId.ToString(), request);

        Assert.NotNull(result);
        Assert.Equal(sendId, result.Id);
        await _nonAnonymousSendCommand.Received(1).SaveSendAsync(Arg.Is<Send>(s =>
            s.Id == sendId &&
            s.AuthType == AuthType.Password &&
            s.Password == "hashed-password" &&
            s.Emails == null));
    }

    [Theory, AutoData]
    public async Task Put_WithoutPasswordOrEmails_PreservesExistingEmails(Guid userId, Guid sendId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        var existingSend = new Send
        {
            Id = sendId,
            UserId = userId,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("Old", "Old notes", "Old text", false)),
            Emails = "test@example.com",
            AuthType = AuthType.Email
        };
        _sendRepository.GetByIdAsync(sendId).Returns(existingSend);

        var request = new SendRequestModel
        {
            Type = SendType.Text,
            Key = "updated-key",
            Text = new SendTextModel { Text = "updated text" },
            DeletionDate = DateTime.UtcNow.AddDays(7)
        };

        var result = await _sut.Put(sendId.ToString(), request);

        Assert.NotNull(result);
        Assert.Equal(sendId, result.Id);
        await _nonAnonymousSendCommand.Received(1).SaveSendAsync(Arg.Is<Send>(s =>
            s.Id == sendId &&
            s.AuthType == AuthType.Email &&
            s.Emails == "test@example.com" &&
            s.Password == null));
    }

    [Theory, AutoData]
    public async Task Put_WithoutPasswordOrEmails_PreservesNoneAuthType(Guid userId, Guid sendId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        var existingSend = new Send
        {
            Id = sendId,
            UserId = userId,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("Old", "Old notes", "Old text", false)),
            Password = null,
            Emails = null,
            AuthType = AuthType.None
        };
        _sendRepository.GetByIdAsync(sendId).Returns(existingSend);

        var request = new SendRequestModel
        {
            Type = SendType.Text,
            Key = "updated-key",
            Text = new SendTextModel { Text = "updated text" },
            DeletionDate = DateTime.UtcNow.AddDays(7)
        };

        var result = await _sut.Put(sendId.ToString(), request);

        Assert.NotNull(result);
        Assert.Equal(sendId, result.Id);
        await _nonAnonymousSendCommand.Received(1).SaveSendAsync(Arg.Is<Send>(s =>
            s.Id == sendId &&
            s.AuthType == AuthType.None &&
            s.Password == null &&
            s.Emails == null));
    }

    #region Authenticated Access Endpoints

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
        _sendRepository.GetByIdAsync(sendId).Returns(send);
        _userService.GetUserByIdAsync(creator.Id).Returns(creator);

        var result = await _sut.AccessUsingAuth();

        Assert.NotNull(result);
        var objectResult = Assert.IsType<ObjectResult>(result);
        var response = Assert.IsType<SendAccessResponseModel>(objectResult.Value);
        Assert.Equal(CoreHelpers.Base64UrlEncode(sendId.ToByteArray()), response.Id);
        Assert.Equal(creator.Email, response.CreatorIdentifier);
        await _sendRepository.Received(1).GetByIdAsync(sendId);
        await _userService.Received(1).GetUserByIdAsync(creator.Id);
    }

    [Theory, AutoData]
    public async Task AccessUsingAuth_WithEmailProtectedSend_WithFfDisabled_ReturnsUnauthorizedResult(Guid sendId, User creator)
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
            AuthType = AuthType.Email,
            Emails = "test@example.com",
            MaxAccessCount = null
        };
        var user = CreateUserWithSendIdClaim(sendId);
        _sut.ControllerContext = CreateControllerContextWithUser(user);
        _sendRepository.GetByIdAsync(sendId).Returns(send);
        _userService.GetUserByIdAsync(creator.Id).Returns(creator);
        _featureService.IsEnabled(FeatureFlagKeys.SendEmailOTP).Returns(false);

        var result = await _sut.AccessUsingAuth();

        Assert.NotNull(result);
        Assert.IsType<UnauthorizedResult>(result);
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
        _sendRepository.GetByIdAsync(sendId).Returns(send);

        var result = await _sut.AccessUsingAuth();

        Assert.NotNull(result);
        var objectResult = Assert.IsType<ObjectResult>(result);
        var response = Assert.IsType<SendAccessResponseModel>(objectResult.Value);
        Assert.Equal(CoreHelpers.Base64UrlEncode(sendId.ToByteArray()), response.Id);
        Assert.Null(response.CreatorIdentifier);
        await _sendRepository.Received(1).GetByIdAsync(sendId);
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
        _sendRepository.GetByIdAsync(sendId).Returns(send);

        var result = await _sut.AccessUsingAuth();

        Assert.NotNull(result);
        var objectResult = Assert.IsType<ObjectResult>(result);
        var response = Assert.IsType<SendAccessResponseModel>(objectResult.Value);
        Assert.Equal(CoreHelpers.Base64UrlEncode(sendId.ToByteArray()), response.Id);
        Assert.Null(response.CreatorIdentifier);
        await _sendRepository.Received(1).GetByIdAsync(sendId);
        await _userService.DidNotReceive().GetUserByIdAsync(Arg.Any<Guid>());
    }

    [Theory, AutoData]
    public async Task AccessUsingAuth_WithNonExistentSend_ThrowsBadRequestException(Guid sendId)
    {
        var user = CreateUserWithSendIdClaim(sendId);
        _sut.ControllerContext = CreateControllerContextWithUser(user);
        _sendRepository.GetByIdAsync(sendId).Returns((Send)null);

        var exception =
            await Assert.ThrowsAsync<BadRequestException>(() => _sut.AccessUsingAuth());

        Assert.Equal("Could not locate send", exception.Message);
        await _sendRepository.Received(1).GetByIdAsync(sendId);
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
        _sendRepository.GetByIdAsync(sendId).Returns(send);
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
        _sendRepository.GetByIdAsync(sendId).Returns(send);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.AccessUsingAuth());

        await _sendRepository.Received(1).GetByIdAsync(sendId);
        await _userService.DidNotReceive().GetUserByIdAsync(Arg.Any<Guid>());
        await _sendRepository.DidNotReceive().ReplaceAsync(Arg.Any<Send>());
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
        _sendRepository.GetByIdAsync(sendId).Returns(send);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.AccessUsingAuth());

        await _sendRepository.Received(1).GetByIdAsync(sendId);
        await _userService.DidNotReceive().GetUserByIdAsync(Arg.Any<Guid>());
        await _sendRepository.DidNotReceive().ReplaceAsync(Arg.Any<Send>());
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
        _sendRepository.GetByIdAsync(sendId).Returns(send);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.AccessUsingAuth());

        await _sendRepository.Received(1).GetByIdAsync(sendId);
        await _userService.DidNotReceive().GetUserByIdAsync(Arg.Any<Guid>());
        await _sendRepository.DidNotReceive().ReplaceAsync(Arg.Any<Send>());
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
        _sendRepository.GetByIdAsync(sendId).Returns(send);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.AccessUsingAuth());

        await _sendRepository.Received(1).GetByIdAsync(sendId);
        await _userService.DidNotReceive().GetUserByIdAsync(Arg.Any<Guid>());
        await _sendRepository.DidNotReceive().ReplaceAsync(Arg.Any<Send>());
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
        _sendRepository.GetByIdAsync(sendId).Returns(send);
        _nonAnonymousSendCommand.GetSendFileDownloadUrlAsync(send, fileId)
            .Returns((expectedUrl, SendAccessResult.Granted));

        var result = await _sut.GetSendFileDownloadDataUsingAuth(fileId);

        Assert.NotNull(result);
        var objectResult = Assert.IsType<ObjectResult>(result);
        var response = Assert.IsType<SendFileDownloadDataResponseModel>(objectResult.Value);
        Assert.Equal(fileId, response.Id);
        Assert.Equal(expectedUrl, response.Url);
        await _sendRepository.Received(1).GetByIdAsync(sendId);
        await _nonAnonymousSendCommand.Received(1).GetSendFileDownloadUrlAsync(send, fileId);
    }

    [Theory, AutoData]
    public async Task GetSendFileDownloadDataUsingAuth_WithEmailProtectedSend_WithFfDisabled_ReturnsUnauthorizedResult(
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
            AuthType = AuthType.Email,
            Emails = "test@example.com",
            MaxAccessCount = null
        };
        var user = CreateUserWithSendIdClaim(sendId);
        _sut.ControllerContext = CreateControllerContextWithUser(user);
        _sendRepository.GetByIdAsync(sendId).Returns(send);
        _sendFileStorageService.GetSendFileDownloadUrlAsync(send, fileId).Returns(expectedUrl);
        _featureService.IsEnabled(FeatureFlagKeys.SendEmailOTP).Returns(false);

        var result = await _sut.GetSendFileDownloadDataUsingAuth(fileId);

        Assert.NotNull(result);
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Theory, AutoData]
    public async Task GetSendFileDownloadDataUsingAuth_WithNonExistentSend_ThrowsBadRequestException(
        Guid sendId, string fileId)
    {
        var user = CreateUserWithSendIdClaim(sendId);
        _sut.ControllerContext = CreateControllerContextWithUser(user);
        _sendRepository.GetByIdAsync(sendId).Returns((Send)null);

        var exception =
            await Assert.ThrowsAsync<BadRequestException>(() => _sut.GetSendFileDownloadDataUsingAuth(fileId));

        Assert.Equal("Could not locate send", exception.Message);
        await _sendRepository.Received(1).GetByIdAsync(sendId);
        await _nonAnonymousSendCommand.DidNotReceive()
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
        _sendRepository.GetByIdAsync(sendId).Returns(send);
        _nonAnonymousSendCommand
            .When(x => x.GetSendFileDownloadUrlAsync(send, fileId))
            .Do(x => throw new BadRequestException("Can only get a download URL for a file type of Send"));

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _sut.GetSendFileDownloadDataUsingAuth(fileId));

        Assert.Equal("Can only get a download URL for a file type of Send", exception.Message);
        await _sendRepository.Received(1).GetByIdAsync(sendId);
        await _nonAnonymousSendCommand.Received(1).GetSendFileDownloadUrlAsync(send, fileId);
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
        _sendRepository.GetByIdAsync(sendId).Returns(send);
        _nonAnonymousSendCommand.GetSendFileDownloadUrlAsync(send, fileId)
            .Returns((null, SendAccessResult.Denied));

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetSendFileDownloadDataUsingAuth(fileId));

        await _sendRepository.Received(1).GetByIdAsync(sendId);
        await _nonAnonymousSendCommand.Received(1).GetSendFileDownloadUrlAsync(send, fileId);
    }


    #endregion

    #region PutRemoveAuth Tests

    [Theory, AutoData]
    public async Task PutRemoveAuth_WithPasswordProtectedSend_RemovesPasswordAndSetsAuthTypeNone(Guid userId,
        Guid sendId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        var existingSend = new Send
        {
            Id = sendId,
            UserId = userId,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("Test", "Notes", "Text", false)),
            Password = "hashed-password",
            Emails = null,
            AuthType = AuthType.Password
        };
        _sendRepository.GetByIdAsync(sendId).Returns(existingSend);

        var result = await _sut.PutRemoveAuth(sendId.ToString());

        Assert.NotNull(result);
        Assert.Equal(sendId, result.Id);
        Assert.Equal(AuthType.None, result.AuthType);
        Assert.Null(result.Password);
        Assert.Null(result.Emails);
        await _nonAnonymousSendCommand.Received(1).SaveSendAsync(Arg.Is<Send>(s =>
            s.Id == sendId &&
            s.Password == null &&
            s.Emails == null &&
            s.AuthType == AuthType.None));
    }

    [Theory, AutoData]
    public async Task PutRemoveAuth_WithEmailProtectedSend_RemovesEmailsAndSetsAuthTypeNone(Guid userId, Guid sendId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        var existingSend = new Send
        {
            Id = sendId,
            UserId = userId,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("Test", "Notes", "Text", false)),
            Password = null,
            Emails = "test@example.com,user@example.com",
            AuthType = AuthType.Email
        };
        _sendRepository.GetByIdAsync(sendId).Returns(existingSend);

        var result = await _sut.PutRemoveAuth(sendId.ToString());

        Assert.NotNull(result);
        Assert.Equal(sendId, result.Id);
        Assert.Equal(AuthType.None, result.AuthType);
        Assert.Null(result.Password);
        Assert.Null(result.Emails);
        await _nonAnonymousSendCommand.Received(1).SaveSendAsync(Arg.Is<Send>(s =>
            s.Id == sendId &&
            s.Password == null &&
            s.Emails == null &&
            s.AuthType == AuthType.None));
    }

    [Theory, AutoData]
    public async Task PutRemoveAuth_WithSendAlreadyHavingNoAuth_StillSucceeds(Guid userId, Guid sendId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        var existingSend = new Send
        {
            Id = sendId,
            UserId = userId,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("Test", "Notes", "Text", false)),
            Password = null,
            Emails = null,
            AuthType = AuthType.None
        };
        _sendRepository.GetByIdAsync(sendId).Returns(existingSend);

        var result = await _sut.PutRemoveAuth(sendId.ToString());

        Assert.NotNull(result);
        Assert.Equal(sendId, result.Id);
        Assert.Equal(AuthType.None, result.AuthType);
        Assert.Null(result.Password);
        Assert.Null(result.Emails);
        await _nonAnonymousSendCommand.Received(1).SaveSendAsync(Arg.Is<Send>(s =>
            s.Id == sendId &&
            s.Password == null &&
            s.Emails == null &&
            s.AuthType == AuthType.None));
    }

    [Theory, AutoData]
    public async Task PutRemoveAuth_WithFileSend_RemovesAuthAndPreservesFileData(Guid userId, Guid sendId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        var fileData = new SendFileData("Test File", "Notes", "document.pdf") { Id = "file-123", Size = 2048 };
        var existingSend = new Send
        {
            Id = sendId,
            UserId = userId,
            Type = SendType.File,
            Data = JsonSerializer.Serialize(fileData),
            Password = "hashed-password",
            Emails = null,
            AuthType = AuthType.Password
        };
        _sendRepository.GetByIdAsync(sendId).Returns(existingSend);

        var result = await _sut.PutRemoveAuth(sendId.ToString());

        Assert.NotNull(result);
        Assert.Equal(sendId, result.Id);
        Assert.Equal(AuthType.None, result.AuthType);
        Assert.Equal(SendType.File, result.Type);
        Assert.NotNull(result.File);
        Assert.Equal("file-123", result.File.Id);
        Assert.Null(result.Password);
        Assert.Null(result.Emails);
    }

    [Theory, AutoData]
    public async Task PutRemoveAuth_WithNonExistentSend_ThrowsNotFoundException(Guid userId, Guid sendId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        _sendRepository.GetByIdAsync(sendId).Returns((Send)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.PutRemoveAuth(sendId.ToString()));

        await _sendRepository.Received(1).GetByIdAsync(sendId);
        await _nonAnonymousSendCommand.DidNotReceive().SaveSendAsync(Arg.Any<Send>());
    }

    [Theory, AutoData]
    public async Task PutRemoveAuth_WithWrongUser_ThrowsNotFoundException(Guid userId, Guid otherUserId, Guid sendId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        var existingSend = new Send
        {
            Id = sendId,
            UserId = otherUserId,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("Test", "Notes", "Text", false)),
            Password = "hashed-password",
            AuthType = AuthType.Password
        };
        _sendRepository.GetByIdAsync(sendId).Returns(existingSend);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.PutRemoveAuth(sendId.ToString()));

        await _sendRepository.Received(1).GetByIdAsync(sendId);
        await _nonAnonymousSendCommand.DidNotReceive().SaveSendAsync(Arg.Any<Send>());
    }

    [Theory, AutoData]
    public async Task PutRemoveAuth_WithNullUserId_ThrowsInvalidOperationException(Guid sendId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns((Guid?)null);

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.PutRemoveAuth(sendId.ToString()));

        Assert.Equal("User ID not found", exception.Message);
        await _sendRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
        await _nonAnonymousSendCommand.DidNotReceive().SaveSendAsync(Arg.Any<Send>());
    }

    [Theory, AutoData]
    public async Task PutRemoveAuth_WithSendHavingBothPasswordAndEmails_RemovesBoth(Guid userId, Guid sendId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        var existingSend = new Send
        {
            Id = sendId,
            UserId = userId,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("Test", "Notes", "Text", false)),
            Password = "hashed-password",
            Emails = "test@example.com",
            AuthType = AuthType.Password
        };
        _sendRepository.GetByIdAsync(sendId).Returns(existingSend);

        var result = await _sut.PutRemoveAuth(sendId.ToString());

        Assert.NotNull(result);
        Assert.Equal(sendId, result.Id);
        Assert.Equal(AuthType.None, result.AuthType);
        Assert.Null(result.Password);
        Assert.Null(result.Emails);
        await _nonAnonymousSendCommand.Received(1).SaveSendAsync(Arg.Is<Send>(s =>
            s.Id == sendId &&
            s.Password == null &&
            s.Emails == null &&
            s.AuthType == AuthType.None));
    }

    [Theory, AutoData]
    public async Task PutRemoveAuth_PreservesOtherSendProperties(Guid userId, Guid sendId)
    {
        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        var deletionDate = DateTime.UtcNow.AddDays(7);
        var expirationDate = DateTime.UtcNow.AddDays(3);
        var existingSend = new Send
        {
            Id = sendId,
            UserId = userId,
            Type = SendType.Text,
            Data = JsonSerializer.Serialize(new SendTextData("Test", "Notes", "Text", false)),
            Password = "hashed-password",
            AuthType = AuthType.Password,
            Key = "encryption-key",
            MaxAccessCount = 10,
            AccessCount = 3,
            DeletionDate = deletionDate,
            ExpirationDate = expirationDate,
            Disabled = false,
            HideEmail = true
        };
        _sendRepository.GetByIdAsync(sendId).Returns(existingSend);

        var result = await _sut.PutRemoveAuth(sendId.ToString());

        Assert.NotNull(result);
        Assert.Equal(sendId, result.Id);
        Assert.Equal(AuthType.None, result.AuthType);
        // Verify other properties are preserved
        Assert.Equal("encryption-key", result.Key);
        Assert.Equal(10, result.MaxAccessCount);
        Assert.Equal(3, result.AccessCount);
        Assert.Equal(deletionDate, result.DeletionDate);
        Assert.Equal(expirationDate, result.ExpirationDate);
        Assert.False(result.Disabled);
        Assert.True(result.HideEmail);
    }

    #endregion

    #region Test Helpers

    private static ClaimsPrincipal CreateUserWithSendIdClaim(Guid sendId)
    {
        var claims = new List<Claim> { new Claim("send_id", sendId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    private static ControllerContext CreateControllerContextWithUser(ClaimsPrincipal user)
    {
        return new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = user } };
    }

    #endregion
}
