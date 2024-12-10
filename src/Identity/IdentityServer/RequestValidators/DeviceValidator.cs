using System.Reflection;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Duende.IdentityServer.Validation;
using Bit.Identity.IdentityServer.Enums;
using Bit.Core.Models.Api;
using System.ComponentModel.DataAnnotations;

namespace Bit.Identity.IdentityServer.RequestValidators;

public class DeviceValidator(
    IDeviceService deviceService,
    IDeviceRepository deviceRepository,
    GlobalSettings globalSettings,
    IMailService mailService,
    ICurrentContext currentContext,
    IUserService userService,
    IFeatureService featureService) : IDeviceValidator
{
    private readonly IDeviceService _deviceService = deviceService;
    private readonly IDeviceRepository _deviceRepository = deviceRepository;
    private readonly GlobalSettings _globalSettings = globalSettings;
    private readonly IMailService _mailService = mailService;
    private readonly ICurrentContext _currentContext = currentContext;
    private readonly IUserService _userService = userService;
    private readonly IFeatureService _featureService = featureService;

    public async Task<Device> SaveRequestingDeviceAsync(User user, ValidatedRequest request)
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

    /// <summary>
    /// Checks request for the NewDeviceOtp field to determine if a new device verification is required.
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public static bool NewDeviceOtpRequest(ValidatedRequest request)
    {
        return !string.IsNullOrEmpty(request.Raw["NewDeviceOtp"]?.ToString());
    }

    public async Task<(bool, DeviceValidationErrorType)> HandleNewDeviceVerificationAsync(User user, ValidatedRequest request)
    {
        var device = GetDeviceFromRequest(request);
        if (device == null || user == null)
        {
            return (false, DeviceValidationErrorType.InvalidUserOrDevice);
        }

        // associate the device with the user
        device.UserId = user.Id;
        // parse request for NewDeviceOtp to validate
        var newDeviceOtp = request.Raw["NewDeviceOtp"]?.ToString();
        if (!string.IsNullOrEmpty(newDeviceOtp))
        {
            // verify the NewDeviceOtp
            var otpValid = await _userService.VerifyOTPAsync(user, newDeviceOtp);
            if (otpValid)
            {
                await _deviceService.SaveAsync(device);
                return (true, DeviceValidationErrorType.None);
            }
            return (false, DeviceValidationErrorType.InvalidNewDeviceOtp);
        }
        // if a user has no devices they are assumed to be newly registered user which does not require new device verification
        var devices = await _deviceRepository.GetManyByUserIdAsync(user.Id);
        if (devices.Count == 0)
        {
            await _deviceService.SaveAsync(device);
            return (true, DeviceValidationErrorType.None);
        }

        // if we get to here then we need to send a new device verification email
        return (false, DeviceValidationErrorType.NewDeviceVerificationRequired);
    }

    public async Task<bool> DeviceValid(ValidatedTokenRequest request, CustomValidatorRequestContext context)
    {
        // if the device is known then return early. Login with Passkey (Webauthn grantType) sets knownDevice
        // to true and we want to honor that.
        if (context.KnownDevice)
        {
            return true;
        }

        // Parse device from request and return early if no device information is provided
        var requestDevice = context.Device ?? GetDeviceFromRequest(request);
        // If both are null then return error
        if (requestDevice == null)
        {
            (context.ValidationErrorResult, context.CustomResponse) =
                BuildErrorResult(DeviceValidationErrorType.NoDeviceInformationProvided);
            return false;
        }

        // if not a new device request then check if the device is known
        if (!NewDeviceOtpRequest(request))
        {
            var knownDevice = await GetKnownDeviceAsync(context.User, requestDevice);
            // if the device is know then the device is valid and we return
            if (knownDevice != null)
            {
                context.Device = knownDevice;
                return true;
            }
        }

        // We have established that the device is unknown at this point; begin new device verification
        if (_featureService.IsEnabled(FeatureFlagKeys.NewDeviceVerification))
        {
            if (request.GrantType == "password" &&
                request.Raw["AuthRequest"] == null &&
                !context.TwoFactorRequired &&
                !context.SsoRequired &&
                _globalSettings.EnableNewDeviceVerification)
            {
                // We only want to return early if the device is invalid or there is an error
                var (deviceValidated, errorType) = await HandleNewDeviceVerificationAsync(context.User, request);
                if (!deviceValidated)
                {
                    (context.ValidationErrorResult, context.CustomResponse) =
                        BuildErrorResult(errorType);
                    await _userService.SendOTPAsync(context.User);
                    return false;
                }
            }
            else
            {
                // if new EnableNewDeviceVerification is not enabled then we save the device
                requestDevice.UserId = context.User.Id;
                await _deviceService.SaveAsync(requestDevice);
                context.Device = requestDevice;
            }
            return true;
        }
        else
        {
            // backwards compatibility
            context.Device = await SaveRequestingDeviceAsync(context.User, request);
            return true;
        }
    }

    private (Duende.IdentityServer.Validation.ValidationResult, Dictionary<string, object>) BuildErrorResult(DeviceValidationErrorType errorType)
    {
        var result = new Duende.IdentityServer.Validation.ValidationResult
        {
            IsError = true,
            Error = "device_error",
        };
        var customResponse = new Dictionary<string, object>();
        switch (errorType)
        {
            case DeviceValidationErrorType.InvalidUserOrDevice:
                result.ErrorDescription = "Invalid user or device";
                customResponse.Add("ErrorModel", new ErrorResponseModel("invalid user or device"));
                break;
            case DeviceValidationErrorType.InvalidNewDeviceOtp:
                result.ErrorDescription = "Invalid New Device OTP";
                customResponse.Add("ErrorModel", new ErrorResponseModel("invalid new device otp"));
                break;
            case DeviceValidationErrorType.NewDeviceVerificationRequired:
                result.ErrorDescription = "New device verification required";
                customResponse.Add("ErrorModel", new ErrorResponseModel("new device verification required"));
                break;
            case DeviceValidationErrorType.NoDeviceInformationProvided:
                result.ErrorDescription = "No device information provided";
                customResponse.Add("ErrorModel", new ErrorResponseModel("no device information provided"));
                break;
        }
        return (result, customResponse);
    }
}
