using Bit.Api.Models.Request;
using Bit.Api.Models.Response;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

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
    private readonly IGlobalSettings _globalSettings;

    public AuthRequestsController(
        IUserRepository userRepository,
        IDeviceRepository deviceRepository,
        IUserService userService,
        IAuthRequestRepository authRequestRepository,
        ICurrentContext currentContext,
        IPushNotificationService pushNotificationService,
        IGlobalSettings globalSettings)
    {
        _userRepository = userRepository;
        _deviceRepository = deviceRepository;
        _userService = userService;
        _authRequestRepository = authRequestRepository;
        _currentContext = currentContext;
        _pushNotificationService = pushNotificationService;
        _globalSettings = globalSettings;
    }

    [HttpGet("")]
    public async Task<ListResponseModel<AuthRequestResponseModel>> Get()
    {
        var userId = _userService.GetProperUserId(User).Value;
        var authRequests = await _authRequestRepository.GetManyByUserIdAsync(userId);
        var responses = authRequests.Select(a => new AuthRequestResponseModel(a, _globalSettings.BaseServiceUri.Vault)).ToList();
        return new ListResponseModel<AuthRequestResponseModel>(responses);
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

        return new AuthRequestResponseModel(authRequest, _globalSettings.BaseServiceUri.Vault);
    }

    [HttpGet("{id}/response")]
    [AllowAnonymous]
    public async Task<AuthRequestResponseModel> GetResponse(string id, [FromQuery] string code)
    {
        var authRequest = await _authRequestRepository.GetByIdAsync(new Guid(id));
        if (authRequest == null || !CoreHelpers.FixedTimeEquals(authRequest.AccessCode, code) || authRequest.GetExpirationDate() < DateTime.UtcNow)
        {
            throw new NotFoundException();
        }

        return new AuthRequestResponseModel(authRequest, _globalSettings.BaseServiceUri.Vault);
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
        if (_globalSettings.PasswordlessAuth.KnownDevicesOnly)
        {
            var devices = await _deviceRepository.GetManyByUserIdAsync(user.Id);
            if (devices == null || !devices.Any(d => d.Identifier == model.DeviceIdentifier))
            {
                throw new BadRequestException("Login with device is only available on devices that have been previously logged in.");
            }
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
            RequestFingerprint = model.FingerprintPhrase
        };
        authRequest = await _authRequestRepository.CreateAsync(authRequest);
        await _pushNotificationService.PushAuthRequestAsync(authRequest);
        var r = new AuthRequestResponseModel(authRequest, _globalSettings.BaseServiceUri.Vault);
        return r;
    }

    [HttpPut("{id}")]
    public async Task<AuthRequestResponseModel> Put(string id, [FromBody] AuthRequestUpdateRequestModel model)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var authRequest = await _authRequestRepository.GetByIdAsync(new Guid(id));
        if (authRequest == null || authRequest.UserId != userId || authRequest.GetExpirationDate() < DateTime.UtcNow)
        {
            throw new NotFoundException();
        }

        if (authRequest.Approved is not null)
        {
            throw new DuplicateAuthRequestException();
        }

        var device = await _deviceRepository.GetByIdentifierAsync(model.DeviceIdentifier, userId);
        if (device == null)
        {
            throw new BadRequestException("Invalid device.");
        }

        authRequest.Key = model.Key;
        authRequest.MasterPasswordHash = model.MasterPasswordHash;
        authRequest.ResponseDeviceId = device.Id;
        authRequest.ResponseDate = DateTime.UtcNow;
        authRequest.Approved = model.RequestApproved;
        await _authRequestRepository.ReplaceAsync(authRequest);

        // We only want to send an approval notification if the request is approved (or null), 
        // to not leak that it was denied to the originating client if it was originated by a malicious actor.
        if (authRequest.Approved ?? true)
        {
            await _pushNotificationService.PushAuthRequestResponseAsync(authRequest);
        }

        return new AuthRequestResponseModel(authRequest, _globalSettings.BaseServiceUri.Vault);
    }
}
