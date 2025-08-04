// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bit.Core;
using Bit.Core.Auth.Services;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Identity.IdentityServer.Enums;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Caching.Distributed;

namespace Bit.Identity.IdentityServer.RequestValidators;

public class DeviceValidator(
    IDeviceService deviceService,
    IDeviceRepository deviceRepository,
    GlobalSettings globalSettings,
    IMailService mailService,
    ICurrentContext currentContext,
    IUserService userService,
    IDistributedCache distributedCache,
    ITwoFactorEmailService twoFactorEmailService,
    ILogger<DeviceValidator> logger) : IDeviceValidator
{
    private readonly IDeviceService _deviceService = deviceService;
    private readonly IDeviceRepository _deviceRepository = deviceRepository;
    private readonly GlobalSettings _globalSettings = globalSettings;
    private readonly IMailService _mailService = mailService;
    private readonly ICurrentContext _currentContext = currentContext;
    private readonly IUserService _userService = userService;
    private readonly IDistributedCache distributedCache = distributedCache;
    private readonly ILogger<DeviceValidator> _logger = logger;
    private readonly ITwoFactorEmailService _twoFactorEmailService = twoFactorEmailService;

    private const string PasswordGrantType = "password";

    public async Task<bool> ValidateRequestDeviceAsync(ValidatedTokenRequest request, CustomValidatorRequestContext context)
    {
        // Parse device from request and return early if no device information is provided
        var requestDevice = context.Device ?? GetDeviceFromRequest(request);
        // If context.Device and request device information are null then return error
        // backwards compatibility -- check if user is null
        // PM-13340: Null user check happens in the HandleNewDeviceVerificationAsync method and can be removed from here
        if (requestDevice == null || context.User == null)
        {
            (context.ValidationErrorResult, context.CustomResponse) =
                BuildDeviceErrorResult(DeviceValidationResultType.NoDeviceInformationProvided);
            return false;
        }

        // if not a new device request then check if the device is known
        if (!NewDeviceOtpRequest(request))
        {
            var knownDevice = await GetKnownDeviceAsync(context.User, requestDevice);
            // if the device is know then we return the device fetched from the database
            // returning the database device is important for TDE
            if (knownDevice != null)
            {
                context.KnownDevice = true;
                context.Device = knownDevice;
                return true;
            }
        }

        // We have established that the device is unknown at this point; begin new device verification
        // for standard password grant type requests
        // Note: the auth request flow re-uses the resource owner password flow but new device verification
        // is not required for auth requests
        var rawAuthRequestId = request.Raw["AuthRequest"]?.ToLowerInvariant();
        var isAuthRequest = !string.IsNullOrEmpty(rawAuthRequestId);
        if (request.GrantType == PasswordGrantType &&
            !isAuthRequest &&
            context is { TwoFactorRequired: false, SsoRequired: false } &&
            _globalSettings.EnableNewDeviceVerification)
        {
            var validationResult = await HandleNewDeviceVerificationAsync(context.User, request);
            if (validationResult != DeviceValidationResultType.Success)
            {
                (context.ValidationErrorResult, context.CustomResponse) =
                    BuildDeviceErrorResult(validationResult);
                if (validationResult == DeviceValidationResultType.NewDeviceVerificationRequired)
                {
                    await _twoFactorEmailService.SendNewDeviceVerificationEmailAsync(context.User);
                }
                return false;
            }
        }

        // Device still unknown, but if we are in an auth request flow, this is not valid
        // as we only support auth request authN requests on known devices
        if (request.GrantType == PasswordGrantType && isAuthRequest)
        {
            (context.ValidationErrorResult, context.CustomResponse) =
                BuildDeviceErrorResult(DeviceValidationResultType.AuthRequestFlowUnknownDevice);
            return false;
        }

        // At this point we have established either new device verification is not required or the NewDeviceOtp is valid,
        // so we save the device to the database and proceed with authentication
        requestDevice.UserId = context.User.Id;
        await _deviceService.SaveAsync(requestDevice);
        context.Device = requestDevice;

        if (!_globalSettings.DisableEmailNewDevice)
        {
            await SendNewDeviceLoginEmail(context.User, requestDevice);
        }

        return true;
    }

    /// <summary>
    /// Checks the if the requesting deice requires new device verification otherwise saves the device to the database
    /// </summary>
    /// <param name="user">user attempting to authenticate</param>
    /// <param name="ValidatedRequest">The Request is used to check for the NewDeviceOtp and for the raw device data</param>
    /// <returns>returns deviceValidationResultType</returns>
    private async Task<DeviceValidationResultType> HandleNewDeviceVerificationAsync(User user, ValidatedRequest request)
    {
        // currently unreachable due to backward compatibility
        // PM-13340: will address this
        if (user == null)
        {
            return DeviceValidationResultType.InvalidUser;
        }

        // Has the User opted out of new device verification
        if (!user.VerifyDevices)
        {
            return DeviceValidationResultType.Success;
        }

        // User is newly registered, so don't require new device verification
        var createdSpan = DateTime.UtcNow - user.CreationDate;
        if (createdSpan < TimeSpan.FromHours(24))
        {
            return DeviceValidationResultType.Success;
        }

        // CS exception flow
        // Check cache for user information
        var cacheKey = string.Format(AuthConstants.NewDeviceVerificationExceptionCacheKeyFormat, user.Id.ToString());
        var cacheValue = await distributedCache.GetAsync(cacheKey);
        if (cacheValue != null)
        {
            // if found in cache return success result and remove from cache
            await distributedCache.RemoveAsync(cacheKey);
            _logger.LogInformation("New device verification exception for user {UserId} found in cache", user.Id);
            return DeviceValidationResultType.Success;
        }

        // parse request for NewDeviceOtp to validate
        var newDeviceOtp = request.Raw["NewDeviceOtp"]?.ToString();
        // we only check null here since an empty OTP will be considered an incorrect OTP
        if (newDeviceOtp != null)
        {
            // verify the NewDeviceOtp
            var otpValid = await _userService.VerifyOTPAsync(user, newDeviceOtp);
            if (otpValid)
            {
                // In order to get here they would have to have access to their email so we verify it if it's not already
                if (!user.EmailVerified)
                {
                    user.EmailVerified = true;
                    await _userService.SaveUserAsync(user);
                }
                return DeviceValidationResultType.Success;
            }
            return DeviceValidationResultType.InvalidNewDeviceOtp;
        }

        // if a user has no devices they are assumed to be newly registered user which does not require new device verification
        var devices = await _deviceRepository.GetManyByUserIdAsync(user.Id);
        if (devices.Count == 0)
        {
            return DeviceValidationResultType.Success;
        }

        // if we get to here then we need to send a new device verification email
        return DeviceValidationResultType.NewDeviceVerificationRequired;
    }

    /// <summary>
    /// Sends an email whenever the user logs in from a new device. Will not send to a user who's account
    /// is less than 10 minutes old. We assume an account that is less than 10 minutes old is new and does
    /// not need an email stating they just logged in.
    /// </summary>
    /// <param name="user">user logging in</param>
    /// <param name="requestDevice">current device being approved to login</param>
    /// <returns>void</returns>
    private async Task SendNewDeviceLoginEmail(User user, Device requestDevice)
    {
        // Ensure that the user doesn't receive a "new device" email on the first login
        var now = DateTime.UtcNow;
        if (now - user.CreationDate > TimeSpan.FromMinutes(10))
        {
            var deviceType = requestDevice.Type.GetType().GetMember(requestDevice.Type.ToString())
                .FirstOrDefault()?.GetCustomAttribute<DisplayAttribute>()?.GetName();
            await _mailService.SendNewDeviceLoggedInEmail(user.Email, deviceType, now,
                _currentContext.IpAddress);
        }
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

    /// <summary>
    /// This builds builds the error result for the various grant and token validators. The Success type is not used here.
    /// </summary>
    /// <param name="errorType">DeviceValidationResultType that is an error, success type is not used.</param>
    /// <returns>validation result used by grant and token validators, and the custom response for either Grant or Token response objects.</returns>
    private static (Duende.IdentityServer.Validation.ValidationResult, Dictionary<string, object>) BuildDeviceErrorResult(DeviceValidationResultType errorType)
    {
        var result = new Duende.IdentityServer.Validation.ValidationResult
        {
            IsError = true,
            Error = "device_error",
        };
        var customResponse = new Dictionary<string, object>();
        switch (errorType)
        {
            /*
             * The ErrorMessage is brittle and is used to control the flow in the clients. Do not change them without updating the client as well.
             * There is a backwards compatibility issue as well: if you make a change on the clients then ensure that they are backwards
             * compatible.
             */
            case DeviceValidationResultType.InvalidUser:
                result.ErrorDescription = "Invalid user";
                customResponse.Add("ErrorModel", new ErrorResponseModel("invalid user"));
                break;
            case DeviceValidationResultType.InvalidNewDeviceOtp:
                result.ErrorDescription = "Invalid New Device OTP";
                customResponse.Add("ErrorModel", new ErrorResponseModel("invalid new device otp"));
                break;
            case DeviceValidationResultType.NewDeviceVerificationRequired:
                result.ErrorDescription = "New device verification required";
                customResponse.Add("ErrorModel", new ErrorResponseModel("new device verification required"));
                break;
            case DeviceValidationResultType.NoDeviceInformationProvided:
                result.ErrorDescription = "No device information provided";
                customResponse.Add("ErrorModel", new ErrorResponseModel("no device information provided"));
                break;
            case DeviceValidationResultType.AuthRequestFlowUnknownDevice:
                result.ErrorDescription = "Auth requests are not supported on unknown devices";
                customResponse.Add("ErrorModel", new ErrorResponseModel("auth request flow unsupported on unknown device"));
                break;
        }
        return (result, customResponse);
    }
}
