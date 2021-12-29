using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Bit.Core.Models.Api;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Context;
using Bit.Core.Models.Table;

namespace Bit.Api.Controllers
{
    [Route("auth-requests")]
    [Authorize("Application")]
    public class AuthRequestsController : Controller
    {
        private readonly IUserRepository _userRepository;
        private readonly IDeviceRepository _deviceRepository;
        private readonly IDeviceService _deviceService;
        private readonly IUserService _userService;
        private readonly IAuthRequestRepository _authRequestRepository;
        private readonly ICurrentContext _currentContext;

        public AuthRequestsController(
            IUserRepository userRepository,
            IDeviceRepository deviceRepository,
            IDeviceService deviceService,
            IUserService userService,
            IAuthRequestRepository authRequestRepository,
            ICurrentContext currentContext)
        {
            _userRepository = userRepository;
            _deviceRepository = deviceRepository;
            _deviceService = deviceService;
            _userService = userService;
            _authRequestRepository = authRequestRepository;
            _currentContext = currentContext;
        }

        [HttpGet("{id}")]
        public async Task<AuthRequestResponseModel> Get(string id)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var authRequest = await _authRequestRepository.GetByIdAsync(new Guid(id));
            if(authRequest == null || authRequest.UserId != userId)
            {
                throw new NotFoundException();
            }

            return new AuthRequestResponseModel(authRequest);
        }

        [HttpPost("")]
        public async Task<AuthRequestResponseModel> Post([FromBody] AuthRequestCreateRequestModel model)
        {
            var user = await _userRepository.GetByEmailAsync(model.Email);
            if(user == null)
            {
                throw new NotFoundException();
            }
            var device = await _deviceRepository.GetByIdentifierAsync(model.DeviceIdentifier);
            if(device == null)
            {
                device = new Device
                {
                    Identifier = model.DeviceIdentifier,
                    Type = model.DeviceType,
                    UserId = user.Id
                };
                await _deviceService.SaveAsync(device);
            }
            var authRequest = new AuthRequest
            {
                RequestDeviceId = device.Id,
                PublicKey = model.PublicKey,
                UserId = user.Id
            };
            await _authRequestRepository.CreateAsync(authRequest);
            return new AuthRequestResponseModel(authRequest);
        }

        [HttpPut("{id}")]
        public async Task<AuthRequestResponseModel> Put(string id, [FromBody] AuthRequestUpdateRequestModel model)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var authRequest = await _authRequestRepository.GetByIdAsync(new Guid(id));
            if(authRequest == null || authRequest.UserId != userId)
            {
                throw new NotFoundException();
            }

            var device = await _deviceRepository.GetByIdentifierAsync(model.DeviceIdentifier);
            if(device == null)
            {
                throw new BadRequestException("Invalid device.");
            }
            authRequest.Key = model.Key;
            authRequest.ResponseDeviceId = device.Id;
            await _authRequestRepository.ReplaceAsync(authRequest);
            return new AuthRequestResponseModel(authRequest);
        }
    }
}
