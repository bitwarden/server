using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer.RequestValidators;

public class DeviceValidator(
    IDeviceService deviceService,
    IDeviceRepository deviceRepository,
    GlobalSettings globalSettings,
    IMailService mailService,
    ICurrentContext currentContext,
    IUserService userService) : IDeviceValidator
{
    private readonly IDeviceService _deviceService = deviceService;
    private readonly IDeviceRepository _deviceRepository = deviceRepository;
    private readonly GlobalSettings _globalSettings = globalSettings;
    private readonly IMailService _mailService = mailService;
    private readonly ICurrentContext _currentContext = currentContext;
    private readonly IUserService _userService = userService;

    public async Task<Device> SaveRequestingDeviceAsync(User user, ValidatedTokenRequest request)
    {
        // Quick lil' null check.
        var device = GetDeviceFromRequest(request);
        if (device == null || user == null)
        {
            return null;
        }
        // Already known? Great, return the existing device.
        var existingDevice = await _deviceRepository.GetByIdentifierAsync(device.Identifier, user.Id);
        if (existingDevice != null)
        {
            return existingDevice;
        }

        device.UserId = user.Id;
        await _deviceService.SaveAsync(device);

        // This ensures the user doesn't receive a "new device" email on the first login
        var now = DateTime.UtcNow;
        if (now - user.CreationDate > TimeSpan.FromMinutes(10))
        {
            var deviceType = device.Type.GetType().GetMember(device.Type.ToString())
                .FirstOrDefault()?.GetCustomAttribute<DisplayAttribute>()?.GetName();
            if (!_globalSettings.DisableEmailNewDevice)
            {
                await _mailService.SendNewDeviceLoggedInEmail(user.Email, deviceType, now,
                    _currentContext.IpAddress);
            }
        }
        return device;
    }

    public async Task<Device> SaveRequestingDeviceAsync(User user, Device device)
    {
        // Quick lil' null check.
        if (device == null || user == null)
        {
            return null;
        }

        device.UserId = user.Id;
        await _deviceService.SaveAsync(device);
        return device;
    }

    public async Task<Device> GetKnownDeviceAsync(User user, Device device)
    {
        if (user == null || device == null)
        {
            return null;
        }
        return await _deviceRepository.GetByIdentifierAsync(device.Identifier, user.Id);
    }

    public static Device GetDeviceFromRequest(ValidatedRequest request)
    {
        var deviceIdentifier = request.Raw["DeviceIdentifier"]?.ToString();
        var requestDeviceType = request.Raw["DeviceType"]?.ToString();
        var deviceName = request.Raw["DeviceName"]?.ToString();
        var devicePushToken = request.Raw["DevicePushToken"]?.ToString();

        if (string.IsNullOrWhiteSpace(deviceIdentifier) ||
            string.IsNullOrWhiteSpace(requestDeviceType) ||
            string.IsNullOrWhiteSpace(deviceName) ||
            !Enum.TryParse(requestDeviceType, out DeviceType parsedDeviceType))
        {
            return null;
        }

        return new Device
        {
            Identifier = deviceIdentifier,
            Name = deviceName,
            Type = parsedDeviceType,
            PushToken = string.IsNullOrWhiteSpace(devicePushToken) ? null : devicePushToken
        };
    }

    [RequireFeature(FeatureFlagKeys.NewDeviceVerification)]
    public async Task<(bool, string)> HandleNewDeviceVerificationAsync(User user, ValidatedRequest request)
    {
        var device = GetDeviceFromRequest(request);
        if (device == null || user == null)
        {
            return (false, null);
        }

        // associate the device with the user
        device.UserId = user.Id;
        // parse request for NewDeviceOtp to validate
        var newDeviceOtp = request.Raw["NewDeviceOtp"]?.ToString();
        if(!string.IsNullOrEmpty(newDeviceOtp))
        {
            // verify the NewDeviceOtp
            var otpValid = await _userService.VerifyOTPAsync(user, newDeviceOtp);
            if(otpValid)
            {
                await _deviceService.SaveAsync(device);
                return (true, null);
            }
            return (false, "invalid otp");
        }

        // if a user has no devices they are assumed to be newly registered user which does not require new device verification
        var devices = await _deviceRepository.GetManyByUserIdAsync(user.Id);
        if (devices.Count == 0)
        {
            await _deviceService.SaveAsync(device);
            return (true, null);
        }

        // if we get to here then we need to send a new device verification email
        await _userService.SendOTPAsync(user);
        return (false, "new device verification required");
    }
}
