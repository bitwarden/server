using System.Diagnostics;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Exceptions;
using Bit.Core.Auth.Models.Api.Request.AuthRequest;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
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
    private readonly IEventService _eventService;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public AuthRequestService(
        IAuthRequestRepository authRequestRepository,
        IUserRepository userRepository,
        IGlobalSettings globalSettings,
        IDeviceRepository deviceRepository,
        ICurrentContext currentContext,
        IPushNotificationService pushNotificationService,
        IEventService eventService,
        IOrganizationUserRepository organizationRepository)
    {
        _authRequestRepository = authRequestRepository;
        _userRepository = userRepository;
        _globalSettings = globalSettings;
        _deviceRepository = deviceRepository;
        _currentContext = currentContext;
        _pushNotificationService = pushNotificationService;
        _eventService = eventService;
        _organizationUserRepository = organizationRepository;
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
        if (authRequest == null || !CoreHelpers.FixedTimeEquals(authRequest.AccessCode, code))
        {
            return null;
        }

        if (!IsAuthRequestValid(authRequest))
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
        if (!_currentContext.DeviceType.HasValue)
        {
            throw new BadRequestException("Device type not provided.");
        }

        var userNotFound = false;
        var user = await _userRepository.GetByEmailAsync(model.Email);
        if (user == null)
        {
            userNotFound = true;
        }
        else if (_globalSettings.PasswordlessAuth.KnownDevicesOnly)
        {
            var devices = await _deviceRepository.GetManyByUserIdAsync(user.Id);
            if (devices == null || !devices.Any(d => d.Identifier == model.DeviceIdentifier))
            {
                userNotFound = true;
            }
        }

        // Anonymous endpoints must not leak that a user exists or not
        if (userNotFound)
        {
            throw new BadRequestException("User or known device not found.");
        }

        // AdminApproval requests require correlating the user and their organization
        if (model.Type == AuthRequestType.AdminApproval)
        {
            // TODO: When single org policy is turned on we should query for only a single organization from the current user
            // and create only an AuthRequest for that organization and return only that one

            // This will send out the request to all organizations this user belongs to
            var organizationUsers = await _organizationUserRepository.GetManyByUserAsync(_currentContext.UserId!.Value);

            if (organizationUsers.Count == 0)
            {
                throw new BadRequestException("User does not belong to any organizations.");
            }

            Debug.Assert(user is not null, "user should have been validated to be non-null and thrown if it's not.");
            // A user event will automatically create logs for each organization/provider this user belongs to.
            await _eventService.LogUserEventAsync(user.Id, EventType.User_RequestedDeviceApproval);

            AuthRequest? firstAuthRequest = null;
            foreach (var organizationUser in organizationUsers)
            {
                var createdAuthRequest = await CreateAuthRequestAsync(model, user, organizationUser.OrganizationId);
                firstAuthRequest ??= createdAuthRequest;
            }

            // I know this won't be null because I have already validated that at least one organization exists
            return firstAuthRequest!;
        }

        Debug.Assert(user is not null, "user should have been validated to be non-null and thrown if it's not.");
        var authRequest = await CreateAuthRequestAsync(model, user, organizationId: null);
        await _pushNotificationService.PushAuthRequestAsync(authRequest);
        return authRequest;
    }

    private async Task<AuthRequest> CreateAuthRequestAsync(AuthRequestCreateRequestModel model, User user, Guid? organizationId)
    {
        Debug.Assert(_currentContext.DeviceType.HasValue, "DeviceType should have already been validated to have a value.");
        var authRequest = new AuthRequest
        {
            RequestDeviceIdentifier = model.DeviceIdentifier,
            RequestDeviceType = _currentContext.DeviceType.Value,
            RequestIpAddress = _currentContext.IpAddress,
            AccessCode = model.AccessCode,
            PublicKey = model.PublicKey,
            UserId = user.Id,
            Type = model.Type.GetValueOrDefault(),
            OrganizationId = organizationId,
        };
        authRequest = await _authRequestRepository.CreateAsync(authRequest);
        return authRequest;
    }

    public async Task<AuthRequest> UpdateAuthRequestAsync(Guid authRequestId, Guid currentUserId, AuthRequestUpdateRequestModel model)
    {
        var authRequest = await _authRequestRepository.GetByIdAsync(authRequestId);

        if (authRequest == null)
        {
            throw new NotFoundException();
        }

        // Once Approval/Disapproval has been set, this AuthRequest should not be updated again.
        if (authRequest.Approved is not null)
        {
            throw new DuplicateAuthRequestException();
        }

        // Do type specific validation
        switch (authRequest.Type)
        {
            case AuthRequestType.AdminApproval:
                // AdminApproval has a different expiration time, by default is 7 days compared to
                // non-AdminApproval ones having a default of 15 minutes.
                if (IsDateExpired(authRequest.CreationDate, _globalSettings.PasswordlessAuth.AdminRequestExpiration))
                {
                    throw new NotFoundException();
                }
                break;
            case AuthRequestType.AuthenticateAndUnlock:
            case AuthRequestType.Unlock:
                if (IsDateExpired(authRequest.CreationDate, _globalSettings.PasswordlessAuth.UserRequestExpiration))
                {
                    throw new NotFoundException();
                }

                if (authRequest.UserId != currentUserId)
                {
                    throw new NotFoundException();
                }

                // Admin approval responses are not tied to a specific device, but these types are so we need to validate them
                var device = await _deviceRepository.GetByIdentifierAsync(model.DeviceIdentifier, currentUserId);
                if (device == null)
                {
                    throw new BadRequestException("Invalid device.");
                }
                authRequest.ResponseDeviceId = device.Id;
                break;
        }

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
            if (authRequest.OrganizationId.HasValue)
            {
                var organizationUser = await _organizationUserRepository
                    .GetByOrganizationAsync(authRequest.OrganizationId.Value, authRequest.UserId);
                await _eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_ApprovedAuthRequest);
            }

            // No matter what we want to push out the success notification
            await _pushNotificationService.PushAuthRequestResponseAsync(authRequest);
        }
        // If the request is rejected by an organization admin then we want to log an event of that action
        else if (authRequest.Approved.HasValue && !authRequest.Approved.Value && authRequest.OrganizationId.HasValue)
        {
            var organizationUser = await _organizationUserRepository
                    .GetByOrganizationAsync(authRequest.OrganizationId.Value, authRequest.UserId);
            await _eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_RejectedAuthRequest);
        }

        return authRequest;
    }

    private bool IsAuthRequestValid(AuthRequest authRequest)
    {
        return authRequest.Type switch
        {
            AuthRequestType.AuthenticateAndUnlock or AuthRequestType.Unlock
                => !IsDateExpired(authRequest.CreationDate, _globalSettings.PasswordlessAuth.UserRequestExpiration),
            AuthRequestType.AdminApproval => IsAdminApprovalAuthRequestValid(authRequest),
            _ => false,
        };
    }

    private bool IsAdminApprovalAuthRequestValid(AuthRequest authRequest)
    {
        Debug.Assert(authRequest.Type == AuthRequestType.AdminApproval, "This method should only be called on AdminApproval type");
        // If an AdminApproval type has been approved it's expiration time is based on how long it's been since approved.
        if (authRequest.Approved is true)
        {
            Debug.Assert(authRequest.ResponseDate.HasValue, "The response date should have been set when the request was updated.");
            return !IsDateExpired(authRequest.ResponseDate.Value, _globalSettings.PasswordlessAuth.AfterAdminApprovalExpiration);
        }
        else
        {
            return !IsDateExpired(authRequest.CreationDate, _globalSettings.PasswordlessAuth.AdminRequestExpiration);
        }
    }

    private static bool IsDateExpired(DateTime savedDate, TimeSpan allowedLifetime)
    {
        return DateTime.UtcNow > savedDate.Add(allowedLifetime);
    }
}
