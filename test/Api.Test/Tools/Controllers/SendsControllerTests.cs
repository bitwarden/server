using System.Security.Claims;
using System.Text.Json;
using AutoFixture.Xunit2;
using Bit.Api.Models.Response;
using Bit.Api.Tools.Controllers;
using Bit.Api.Tools.Models.Request;
using Bit.Api.Tools.Models.Response;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Settings;
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
    private readonly GlobalSettings _globalSettings;
    private readonly IUserService _userService;
    private readonly ISendRepository _sendRepository;
    private readonly INonAnonymousSendCommand _nonAnonymousSendCommand;
    private readonly IAnonymousSendCommand _anonymousSendCommand;
    private readonly ISendOwnerQuery _sendOwnerQuery;
    private readonly ISendAuthorizationService _sendAuthorizationService;
    private readonly ISendFileStorageService _sendFileStorageService;
    private readonly ILogger<SendsController> _logger;

    public SendsControllerTests()
    {
        _userService = Substitute.For<IUserService>();
        _sendRepository = Substitute.For<ISendRepository>();
        _nonAnonymousSendCommand = Substitute.For<INonAnonymousSendCommand>();
        _anonymousSendCommand = Substitute.For<IAnonymousSendCommand>();
        _sendOwnerQuery = Substitute.For<ISendOwnerQuery>();
        _sendAuthorizationService = Substitute.For<ISendAuthorizationService>();
        _sendFileStorageService = Substitute.For<ISendFileStorageService>();
        _globalSettings = new GlobalSettings();
        _logger = Substitute.For<ILogger<SendsController>>();

        _sut = new SendsController(
            _sendRepository,
            _userService,
            _sendAuthorizationService,
            _anonymousSendCommand,
            _nonAnonymousSendCommand,
            _sendOwnerQuery,
            _sendFileStorageService,
            _logger,
            _globalSettings
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
        var request = new SendRequestModel() { Type = SendType.File, FileLength = 1024L, DeletionDate = now.AddDays(32) };

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

        var result = await _sut.Get();

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

        var result = await _sut.Get();

        Assert.NotNull(result);
        Assert.IsType<ListResponseModel<SendResponseModel>>(result);
        Assert.Empty(result.Data);
        await _sendOwnerQuery.Received(1).GetOwned(Arg.Any<ClaimsPrincipal>());
    }
}
