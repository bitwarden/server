using AutoFixture.Xunit2;
using Bit.Api.Controllers;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NSubstitute;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Bit.Api.Models.Request;
using Bit.Api.Models.Response;
using Xunit;

namespace Bit.Api.Test.Controllers
{
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
            Guid id, Send send, User user)
        {
            var accessId = CoreHelpers.Base64UrlEncode(id.ToByteArray());

            send.Id = default;
            send.Type = SendType.Text;
            send.Data = JsonConvert.SerializeObject(new Dictionary<string, string>());
            send.HideEmail = true;

            _sendService.AccessAsync(id, null).Returns((send, false, false));
            _userService.GetUserByIdAsync(Arg.Any<Guid>()).Returns(user);

            var request = new SendAccessRequestModel();
            var actionResult = await _sut.Access(accessId, request);
            var response = (actionResult as ObjectResult)?.Value as SendAccessResponseModel;

            Assert.NotNull(response);
            Assert.Null(response.CreatorIdentifier);
        }
    }
}

