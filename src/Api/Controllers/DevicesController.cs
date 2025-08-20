// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Api.Auth.Models.Request;
using Bit.Api.Models.Request;
using Bit.Api.Models.Response;
using Bit.Core.Auth.Models.Api.Request;
using Bit.Core.Auth.Models.Api.Response;
using Bit.Core.Auth.UserFeatures.DeviceTrust;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.SharedWeb.Swagger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

[Route("devices")]
[Authorize("Application")]
public class DevicesController : Controller
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceService _deviceService;
    private readonly IUserService _userService;
    private readonly IUntrustDevicesCommand _untrustDevicesCommand;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentContext _currentContext;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(
        IDeviceRepository deviceRepository,
        IDeviceService deviceService,
        IUserService userService,
        IUntrustDevicesCommand untrustDevicesCommand,
        IUserRepository userRepository,
        ICurrentContext currentContext,
        ILogger<DevicesController> logger)
    {
        _deviceRepository = deviceRepository;
        _deviceService = deviceService;
        _userService = userService;
        _untrustDevicesCommand = untrustDevicesCommand;
        _userRepository = userRepository;
        _currentContext = currentContext;
        _logger = logger;
    }

    [HttpGet("{id}")]
    public async Task<DeviceResponseModel> Get(string id)
    {
        var device = await _deviceRepository.GetByIdAsync(new Guid(id), _userService.GetProperUserId(User).Value);
        if (device == null)
        {
            throw new NotFoundException();
        }

        var response = new DeviceResponseModel(device);
        return response;
    }

    [HttpGet("identifier/{identifier}")]
    public async Task<DeviceResponseModel> GetByIdentifier(string identifier)
    {
        var device = await _deviceRepository.GetByIdentifierAsync(identifier, _userService.GetProperUserId(User).Value);
        if (device == null)
        {
            throw new NotFoundException();
        }

        var response = new DeviceResponseModel(device);
        return response;
    }

    [HttpGet("")]
    public async Task<ListResponseModel<DeviceAuthRequestResponseModel>> GetAll()
    {
        var devicesWithPendingAuthData = await _deviceRepository.GetManyByUserIdWithDeviceAuth(_userService.GetProperUserId(User).Value);

        // Convert from DeviceAuthDetails to DeviceAuthRequestResponseModel
        var deviceAuthRequestResponseList = devicesWithPendingAuthData
            .Select(DeviceAuthRequestResponseModel.From)
            .ToList();

        var response = new ListResponseModel<DeviceAuthRequestResponseModel>(deviceAuthRequestResponseList);
        return response;
    }

    [HttpPost("")]
    public async Task<DeviceResponseModel> Post([FromBody] DeviceRequestModel model)
    {
        var device = model.ToDevice(_userService.GetProperUserId(User));
        await _deviceService.SaveAsync(device);

        var response = new DeviceResponseModel(device);
        return response;
    }

    [HttpPut("{id}")]
    [HttpPost("{id}")]
    [SwaggerExclude("POST")]
    public async Task<DeviceResponseModel> Put(string id, [FromBody] DeviceRequestModel model)
    {
        var device = await _deviceRepository.GetByIdAsync(new Guid(id), _userService.GetProperUserId(User).Value);
        if (device == null)
        {
            throw new NotFoundException();
        }

        await _deviceService.SaveAsync(model.ToDevice(device));

        var response = new DeviceResponseModel(device);
        return response;
    }

    [HttpPut("{identifier}/keys")]
    [HttpPost("{identifier}/keys")]
    [SwaggerExclude("POST")]
    public async Task<DeviceResponseModel> PutKeys(string identifier, [FromBody] DeviceKeysRequestModel model)
    {
        var device = await _deviceRepository.GetByIdentifierAsync(identifier, _userService.GetProperUserId(User).Value);
        if (device == null)
        {
            throw new NotFoundException();
        }

        await _deviceService.SaveAsync(model.ToDevice(device));

        var response = new DeviceResponseModel(device);
        return response;
    }

    [HttpPost("{identifier}/retrieve-keys")]
    [Obsolete("This endpoint is deprecated. The keys are on the regular device GET endpoints now.")]
    public async Task<ProtectedDeviceResponseModel> GetDeviceKeys(string identifier)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);

        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var device = await _deviceRepository.GetByIdentifierAsync(identifier, user.Id);
        if (device == null)
        {
            throw new NotFoundException();
        }

        return new ProtectedDeviceResponseModel(device);
    }

    [HttpPost("update-trust")]
    public async Task PostUpdateTrust([FromBody] UpdateDevicesTrustRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);

        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        if (!await _userService.VerifySecretAsync(user, model.Secret))
        {
            await Task.Delay(2000);
            throw new BadRequestException(string.Empty, "User verification failed.");
        }

        await _deviceService.UpdateDevicesTrustAsync(
            _currentContext.DeviceIdentifier,
            user.Id,
            model.CurrentDevice,
            model.OtherDevices ?? Enumerable.Empty<OtherDeviceKeysUpdateRequestModel>());
    }

    [HttpPost("untrust")]
    public async Task PostUntrust([FromBody] UntrustDevicesRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);

        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        await _untrustDevicesCommand.UntrustDevices(user, model.Devices);
    }

    [HttpPut("identifier/{identifier}/token")]
    [HttpPost("identifier/{identifier}/token")]
    [SwaggerExclude("POST")]
    public async Task PutToken(string identifier, [FromBody] DeviceTokenRequestModel model)
    {
        var device = await _deviceRepository.GetByIdentifierAsync(identifier, _userService.GetProperUserId(User).Value);
        if (device == null)
        {
            throw new NotFoundException();
        }

        await _deviceService.SaveAsync(model.ToDevice(device));
    }

    [HttpPut("identifier/{identifier}/web-push-auth")]
    [HttpPost("identifier/{identifier}/web-push-auth")]
    [SwaggerExclude("POST")]
    public async Task PutWebPushAuth(string identifier, [FromBody] WebPushAuthRequestModel model)
    {
        var device = await _deviceRepository.GetByIdentifierAsync(identifier, _userService.GetProperUserId(User).Value);
        if (device == null)
        {
            throw new NotFoundException();
        }

        await _deviceService.SaveAsync(
            model.ToData(),
            device,
            _currentContext.Organizations.Select(org => org.Id.ToString())
        );
    }

    [AllowAnonymous]
    [HttpPut("identifier/{identifier}/clear-token")]
    [HttpPost("identifier/{identifier}/clear-token")]
    [SwaggerExclude("POST")]
    public async Task PutClearToken(string identifier)
    {
        var device = await _deviceRepository.GetByIdentifierAsync(identifier);
        if (device == null)
        {
            throw new NotFoundException();
        }

        await _deviceService.ClearTokenAsync(device);
    }

    [HttpDelete("{id}")]
    [HttpPost("{id}/deactivate")]
    [SwaggerExclude("POST")]
    public async Task Deactivate(string id)
    {
        var device = await _deviceRepository.GetByIdAsync(new Guid(id), _userService.GetProperUserId(User).Value);
        if (device == null)
        {
            throw new NotFoundException();
        }

        await _deviceService.DeactivateAsync(device);
    }

    [AllowAnonymous]
    [HttpGet("knowndevice")]
    public async Task<bool> GetByIdentifierQuery(
            [Required][FromHeader(Name = "X-Request-Email")] string Email,
            [Required][FromHeader(Name = "X-Device-Identifier")] string DeviceIdentifier)
        => await GetByEmailAndIdentifier(CoreHelpers.Base64UrlDecodeString(Email), DeviceIdentifier);

    [Obsolete("Path is deprecated due to encoding issues, use /knowndevice instead.")]
    [AllowAnonymous]
    [HttpGet("knowndevice/{email}/{identifier}")]
    public async Task<bool> GetByEmailAndIdentifier(string email, string identifier)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(identifier))
        {
            throw new BadRequestException("Please provide an email and device identifier");
        }

        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null)
        {
            return false;
        }

        var device = await _deviceRepository.GetByIdentifierAsync(identifier, user.Id);
        return device != null;
    }

    [HttpPost("lost-trust")]
    public void PostLostTrust()
    {
        var userId = _currentContext.UserId.GetValueOrDefault();
        if (userId == default)
        {
            throw new UnauthorizedAccessException();
        }

        var deviceId = _currentContext.DeviceIdentifier;
        if (deviceId == null)
        {
            throw new BadRequestException("Please provide a device identifier");
        }

        var deviceType = _currentContext.DeviceType;
        if (deviceType == null)
        {
            throw new BadRequestException("Please provide a device type");
        }

        _logger.LogError("User {id} has a device key, but didn't receive decryption keys for device {device} of type {deviceType}", userId,
            deviceId, deviceType);
    }

}
