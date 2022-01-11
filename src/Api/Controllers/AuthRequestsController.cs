using System;
using System.Threading.Tasks;
using Bit.Api.Models.Request;
using Bit.Api.Models.Response;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers
{
    [Route("auth-requests")]
    [Authorize("Application")]
    public class AuthRequestsController : Controller
    {
        private readonly IUserRepository _userRepository;
        private readonly IDeviceRepository _deviceRepository;
        private readonly IUserService _userService;
        private readonly IAuthRequestRepository _authRequestRepository;
        private readonly ICurrentContext _currentContext;
        private readonly IPushNotificationService _pushNotificationService;

        public AuthRequestsController(
            IUserRepository userRepository,
            IDeviceRepository deviceRepository,
            IUserService userService,
            IAuthRequestRepository authRequestRepository,
            ICurrentContext currentContext,
            IPushNotificationService pushNotificationService)
        {
            _userRepository = userRepository;
            _deviceRepository = deviceRepository;
            _userService = userService;
            _authRequestRepository = authRequestRepository;
            _currentContext = currentContext;
            _pushNotificationService = pushNotificationService;
        }

        [HttpGet("{id}")]
        public async Task<AuthRequestResponseModel> Get(string id)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var authRequest = await _authRequestRepository.GetByIdAsync(new Guid(id));
            if (authRequest == null || authRequest.UserId != userId)
            {
                throw new NotFoundException();
            }

            return new AuthRequestResponseModel(authRequest);
        }

        [HttpGet("{id}/response")]
        [AllowAnonymous]
        public async Task<AuthRequestResponseModel> GetResponse(string id, [FromQuery] string code)
        {
            var authRequest = await _authRequestRepository.GetByIdAsync(new Guid(id));
            if (authRequest == null || code != authRequest.AccessCode)
            {
                throw new NotFoundException();
            }

            // Make sure the request is not too old.
            if (!authRequest.ResponseDate.HasValue || authRequest.AuthenticationDate.HasValue ||
                DateTime.UtcNow - authRequest.CreationDate > TimeSpan.FromHours(1))
            {
                throw new NotFoundException();
            }

            return new AuthRequestResponseModel(authRequest);
        }

        [HttpPost("")]
        [AllowAnonymous]
        public async Task<AuthRequestResponseModel> Post([FromBody] AuthRequestCreateRequestModel model)
        {
            var user = await _userRepository.GetByEmailAsync(model.Email);
            if (user == null)
            {
                throw new NotFoundException();
            }
            if (!_currentContext.DeviceType.HasValue)
            {
                throw new BadRequestException("Device type not provided.");
            }
            var authRequest = new AuthRequest
            {
                RequestDeviceIdentifier = model.DeviceIdentifier,
                RequestDeviceType = _currentContext.DeviceType.Value,
                RequestIpAddress = _currentContext.IpAddress,
                AccessCode = model.AccessCode,
                PublicKey = model.PublicKey,
                UserId = user.Id,
                Type = model.Type.Value,
            };
            await _authRequestRepository.CreateAsync(authRequest);
            await _pushNotificationService.PushAuthRequestAsync(authRequest);
            return new AuthRequestResponseModel(authRequest);
        }

        [HttpPut("{id}")]
        public async Task<AuthRequestResponseModel> Put(string id, [FromBody] AuthRequestUpdateRequestModel model)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var authRequest = await _authRequestRepository.GetByIdAsync(new Guid(id));
            if (authRequest == null || authRequest.UserId != userId)
            {
                throw new NotFoundException();
            }

            // Make sure the request is not too old.
            if (DateTime.UtcNow - authRequest.CreationDate > TimeSpan.FromHours(1))
            {
                throw new NotFoundException();
            }

            var device = await _deviceRepository.GetByIdentifierAsync(model.DeviceIdentifier);
            if (device == null)
            {
                throw new BadRequestException("Invalid device.");
            }
            authRequest.Key = model.Key;
            authRequest.MasterPasswordHash = model.MasterPasswordHash;
            authRequest.ResponseDeviceId = device.Id;
            authRequest.ResponseDate = DateTime.UtcNow;
            await _authRequestRepository.ReplaceAsync(authRequest);
            await _pushNotificationService.PushAuthRequestResponseAsync(authRequest);
            return new AuthRequestResponseModel(authRequest);
        }
    }
}
