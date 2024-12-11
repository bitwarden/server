using System.Text.Json;
using AutoFixture.Xunit2;
using Bit.Api.Tools.Controllers;
using Bit.Api.Tools.Models.Request;
using Bit.Api.Tools.Models.Response;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Repositories;
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
    private readonly ISendService _sendService;
    private readonly ISendFileStorageService _sendFileStorageService;
    private readonly ILogger<SendsController> _logger;
    private readonly ICurrentContext _currentContext;

    public SendsControllerTests()
    {
        _userService = Substitute.For<IUserService>();
        _sendRepository = Substitute.For<ISendRepository>();
        _sendService = Substitute.For<ISendService>();
        _sendFileStorageService = Substitute.For<ISendFileStorageService>();
        _globalSettings = new GlobalSettings();
        _logger = Substitute.For<ILogger<SendsController>>();
        _currentContext = Substitute.For<ICurrentContext>();

        _sut = new SendsController(
            _sendRepository,
            _userService,
            _sendService,
            _sendFileStorageService,
            _logger,
            _globalSettings,
            _currentContext
        );
    }

    public void Dispose()
    {
        _sut?.Dispose();
    }

    [Theory, AutoData]
    public async Task SendsController_WhenSendHidesEmail_CreatorIdentifierShouldBeNull(
        Guid id,
        Send send,
        User user
    )
    {
        var accessId = CoreHelpers.Base64UrlEncode(id.ToByteArray());

        send.Id = default;
        send.Type = SendType.Text;
        send.Data = JsonSerializer.Serialize(new Dictionary<string, string>());
        send.HideEmail = true;

        _sendService.AccessAsync(id, null).Returns((send, false, false));
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
        var expected =
            "You cannot have a Send with a deletion date that far "
            + "into the future. Adjust the Deletion Date to a value less than 31 days from now "
            + "and try again.";
        var request = new SendRequestModel() { DeletionDate = now.AddDays(32) };

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Post(request));
        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public async Task PostFile_DeletionDateIsMoreThan31DaysFromNow_ThrowsBadRequest()
    {
        var now = DateTime.UtcNow;
        var expected =
            "You cannot have a Send with a deletion date that far "
            + "into the future. Adjust the Deletion Date to a value less than 31 days from now "
            + "and try again.";
        var request = new SendRequestModel()
        {
            Type = SendType.File,
            FileLength = 1024L,
            DeletionDate = now.AddDays(32),
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.PostFile(request));
        Assert.Equal(expected, exception.Message);
    }
}
