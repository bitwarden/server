using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Exceptions;
using Bit.Core.Auth.Models.Api.Request.AuthRequest;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;

#nullable enable

namespace Bit.Core.Auth.Services.Implementations;

public class AuthRequestService : IAuthRequestService
{
    private readonly IAuthRequestRepository _authRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly IGlobalSettings _globalSettings;
    private readonly IDeviceRepository _deviceRepository;
    private readonly ICurrentContext _currentContext;
    private readonly IPushNotificationService _pushNotificationService;

    public AuthRequestService(
        IAuthRequestRepository authRequestRepository,
        IUserRepository userRepository,
        IGlobalSettings globalSettings,
        IDeviceRepository deviceRepository,
        ICurrentContext currentContext,
        IPushNotificationService pushNotificationService)
    {
        _authRequestRepository = authRequestRepository;
        _userRepository = userRepository;
        _globalSettings = globalSettings;
        _deviceRepository = deviceRepository;
        _currentContext = currentContext;
        _pushNotificationService = pushNotificationService;
    }

    public async Task<AuthRequest?> GetAuthRequestAsync(Guid id, Guid userId)
    {
        var authRequest = await _authRequestRepository.GetByIdAsync(id);
        if (authRequest == null || authRequest.UserId != userId)
        {
            return null;
        }

        return authRequest;
    }

    public async Task<AuthRequest?> GetValidatedAuthRequestAsync(Guid id, string code)
    {
        var authRequest = await _authRequestRepository.GetByIdAsync(id);
        if (authRequest == null || 
            !CoreHelpers.FixedTimeEquals(authRequest.AccessCode, code) || 
            authRequest.GetExpirationDate() < DateTime.UtcNow)
        {
            return null;
        }

        return authRequest;
    }

    /// <summary>
    /// Validates and Creates an <see cref="AuthRequest" /> in the database, as well as pushes it through notifications services
    /// </summary>
    /// <remarks>
    /// This method can only be called inside of an HTTP call because of it's reliance on <see cref="ICurrentContext" />
    /// </remarks>
    public async Task<AuthRequest> CreateAuthRequestAsync(AuthRequestCreateRequestModel model)
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
                throw new BadRequestException(
                    "Login with device is only available on devices that have been previously logged in.");
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
            Type = model.Type.GetValueOrDefault(),
        };

        authRequest = await _authRequestRepository.CreateAsync(authRequest);
        await _pushNotificationService.PushAuthRequestAsync(authRequest);
        return authRequest;
    }

    public async Task<AuthRequest> UpdateAuthRequestAsync(Guid authRequestId, Guid userId, AuthRequestUpdateRequestModel model)
    {
        var authRequest = await _authRequestRepository.GetByIdAsync(authRequestId);
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

        authRequest.ResponseDeviceId = device.Id;
        authRequest.ResponseDate = DateTime.UtcNow;
        authRequest.Approved = model.RequestApproved;

        if (model.RequestApproved)
        {
            authRequest.Key = model.Key;
            authRequest.MasterPasswordHash = model.MasterPasswordHash;
        }

        await _authRequestRepository.ReplaceAsync(authRequest);

        // We only want to send an approval notification if the request is approved (or null), 
        // to not leak that it was denied to the originating client if it was originated by a malicious actor.
        if (authRequest.Approved ?? true)
        {
            await _pushNotificationService.PushAuthRequestResponseAsync(authRequest);
        }

        return authRequest;
    }
}
