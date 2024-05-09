using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bit.Core.AdminConsole.OrganizationAuth.Interfaces;
using Bit.Core.AdminConsole.OrganizationAuth.Models;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Api.Request.AuthRequest;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.AdminConsole.OrganizationAuth;

public class UpdateOrganizationAuthRequestCommand : IUpdateOrganizationAuthRequestCommand
{
    private readonly IAuthRequestService _authRequestService;
    private readonly IMailService _mailService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UpdateOrganizationAuthRequestCommand> _logger;
    private readonly IAuthRequestRepository _authRequestRepository;
    private readonly IGlobalSettings _globalSettings;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IEventService _eventService;

    public UpdateOrganizationAuthRequestCommand(
        IAuthRequestService authRequestService,
        IMailService mailService,
        IUserRepository userRepository,
        ILogger<UpdateOrganizationAuthRequestCommand> logger,
        IAuthRequestRepository authRequestRepository,
        IGlobalSettings globalSettings,
        IPushNotificationService pushNotificationService,
        IOrganizationUserRepository organizationUserRepository,
        IEventService eventService)
    {
        _authRequestService = authRequestService;
        _mailService = mailService;
        _userRepository = userRepository;
        _logger = logger;
        _authRequestRepository = authRequestRepository;
        _globalSettings = globalSettings;
        _pushNotificationService = pushNotificationService;
        _organizationUserRepository = organizationUserRepository;
        _eventService = eventService;
    }

    public async Task UpdateAsync(Guid requestId, Guid userId, bool requestApproved, string encryptedUserKey)
    {
        var updatedAuthRequest = await _authRequestService.UpdateAuthRequestAsync(requestId, userId,
            new AuthRequestUpdateRequestModel { RequestApproved = requestApproved, Key = encryptedUserKey });

        if (updatedAuthRequest.Approved is true)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                _logger.LogError("User ({id}) not found. Trusted device admin approval email not sent.", userId);
                return;
            }
            var approvalDateTime = updatedAuthRequest.ResponseDate ?? DateTime.UtcNow;
            var deviceTypeDisplayName = updatedAuthRequest.RequestDeviceType.GetType()
                .GetMember(updatedAuthRequest.RequestDeviceType.ToString())
                .FirstOrDefault()?
                .GetCustomAttribute<DisplayAttribute>()?.Name ?? "Unknown";
            var deviceTypeAndIdentifier = $"{deviceTypeDisplayName} - {updatedAuthRequest.RequestDeviceIdentifier}";
            await _mailService.SendTrustedDeviceAdminApprovalEmailAsync(user.Email, approvalDateTime,
                updatedAuthRequest.RequestIpAddress, deviceTypeAndIdentifier);
        }
    }

    public async Task UpdateManyAsync(Guid organizationId, IEnumerable<OrganizationAuthRequestUpdateCommandModel> authRequestUpdates)
    {
        var databaseRecords = await FetchManyOrganizationAuthRequestsFromTheDatabase(organizationId, authRequestUpdates.Select(aru => aru.Id));
        var processedAuthRequests = ProcessManyAuthRequests(databaseRecords, authRequestUpdates, organizationId);
        await UpdateManyOrganizationAuthRequestsInTheDatabase(processedAuthRequests);
        await PushManyAuthRequestNotifications(processedAuthRequests);
        await PushManyTrustedDeviceEmails(processedAuthRequests);
        await PushManyAuthRequestEventLogs(processedAuthRequests);
    }

    public async Task UpdateManyOrganizationAuthRequestsInTheDatabase(IEnumerable<OrganizationAdminAuthRequest> authRequests)
    {
        if (authRequests != null && authRequests.Any())
        {
            await _authRequestRepository.UpdateManyAsync(authRequests);
        }
    }

    public async Task<ICollection<OrganizationAdminAuthRequest>> FetchManyOrganizationAuthRequestsFromTheDatabase(Guid organizationId, IEnumerable<Guid> authRequestIds)
    {
        return authRequestIds != null && authRequestIds.Any() ?
            await _authRequestRepository
            .GetManyAdminApprovalRequestsByManyIdsAsync(
                organizationId,
                authRequestIds
            ) :
            new List<OrganizationAdminAuthRequest>();
    }

    public IEnumerable<T> ProcessManyAuthRequests<T>(
            IEnumerable<T> authRequestsToProcess,
            IEnumerable<OrganizationAuthRequestUpdateCommandModel> updates,
            Guid organizationId) where T : AuthRequest
    {
        var processedAuthRequests = new List<T>();
        authRequestsToProcess = FilterOutSpentAuthRequests(authRequestsToProcess);
        authRequestsToProcess = FilterOutExpiredAuthRequests(authRequestsToProcess);
        authRequestsToProcess = FilterOutAuthRequestsWithNoUpdates(authRequestsToProcess, updates);
        authRequestsToProcess = FilterOutAuthRequestsThatDoNotMatchOrganizationId(authRequestsToProcess, organizationId);
        foreach (var authRequestToProcess in authRequestsToProcess ?? new List<T>())
        {
            var updatesForThisRequest = updates.SingleOrDefault(u => u.Id == authRequestToProcess.Id);
            var processedAuthRequest = updatesForThisRequest.Approved ?
                ApproveAuthRequest(authRequestToProcess, updatesForThisRequest.Key) :
                DenyAuthRequest(authRequestToProcess);
            processedAuthRequests.Add(processedAuthRequest);
        }
        return processedAuthRequests;
    }

    public T ApproveAuthRequest<T>(T authRequestToApprove, string Key) where T : AuthRequest
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            _logger.LogError($"An auth request with id {authRequestToApprove.Id} was approved, but no key was provided. This auth request can not be approved.");
            return authRequestToApprove;
        }
        authRequestToApprove.Key = Key;
        authRequestToApprove.Approved = true;
        authRequestToApprove.ResponseDate = DateTime.UtcNow;
        return authRequestToApprove;
    }

    public T DenyAuthRequest<T>(T authRequestToDeny) where T : AuthRequest
    {
        authRequestToDeny.Approved = false;
        authRequestToDeny.ResponseDate = DateTime.UtcNow;
        return authRequestToDeny;
    }

    public IEnumerable<T> FilterOutSpentAuthRequests<T>(IEnumerable<T> authRequests) where T : AuthRequest
    {
        return authRequests?.Where(au => au.Approved == null && !au.ResponseDate.HasValue && !au.AuthenticationDate.HasValue).ToList() ?? new List<T>();
    }

    public IEnumerable<T> FilterOutExpiredAuthRequests<T>(IEnumerable<T> authRequests) where T : AuthRequest
    {
        return authRequests?.Where(au => DateTime.UtcNow < au.CreationDate.Add(FetchRequestExpirationTimespan())).ToList() ?? new List<T>();
    }

    public IEnumerable<T> FilterOutAuthRequestsWithNoUpdates<T>(
        IEnumerable<T> authRequests,
        IEnumerable<OrganizationAuthRequestUpdateCommandModel> authRequestUpdates
    ) where T : AuthRequest
    {
        return authRequests?.Where(ar => authRequestUpdates.FirstOrDefault(aru => ar.Id == aru.Id) != null).ToList() ?? new List<T>();
    }

    public IEnumerable<T> FilterOutAuthRequestsThatDoNotMatchOrganizationId<T>(
        IEnumerable<T> authRequests,
        Guid organizationId
    ) where T : AuthRequest
    {
        return authRequests?.Where(ar => ar.OrganizationId == organizationId).ToList() ?? new List<T>();
    }

    public TimeSpan FetchRequestExpirationTimespan()
    {
        return _globalSettings.PasswordlessAuth.AdminRequestExpiration;
    }

    public async Task<bool> PushManyAuthRequestNotifications<T>(IEnumerable<T> authRequests) where T : AuthRequest
    {
        var pushedNotifications = false;
        foreach (var authRequest in authRequests ?? new List<T>())
        {
            await PushAuthRequestNotification(authRequest);
            pushedNotifications = true;
        }
        return pushedNotifications;
    }

    public async Task PushAuthRequestNotification<T>(T authRequest) where T : AuthRequest
    {
        if (!authRequest?.Approved ?? true)
        {
            return;
        }
        await _pushNotificationService.PushAuthRequestResponseAsync(authRequest);
    }

    public async Task<User> FetchUserFromTheDatabase(Guid userId)
    {
        return await _userRepository.GetByIdAsync(userId);
    }

    public async Task<bool> PushManyTrustedDeviceEmails<T>(IEnumerable<T> authRequests) where T : AuthRequest
    {
        var pushedEmails = false;
        foreach (var authRequest in authRequests ?? new List<T>())
        {
            await PushTrustedDeviceEmail(authRequest);
            pushedEmails = true;
        }
        return pushedEmails;
    }

    public string InferDeviceTypeDisplayName<T>(T authRequest) where T : AuthRequest
    {
        return authRequest.RequestDeviceType.GetType()
            .GetMember(authRequest.RequestDeviceType.ToString())
            .FirstOrDefault()?
            // This unknown case can't be unit tested without adding an enum
            // with no display attribute. Faith and trust are required!
            .GetCustomAttribute<DisplayAttribute>()?.Name ?? "Unknown Device Type";
    }

    public string BuildDeviceTypeAndIdentifierDisplayString<T>(T authRequest) where T : AuthRequest
    {
        if (authRequest == null)
        {
            return "Unknown Device";
        }
        var deviceTypeAndIdentifierString = InferDeviceTypeDisplayName(authRequest);
        deviceTypeAndIdentifierString += string.IsNullOrWhiteSpace(authRequest.RequestDeviceIdentifier) ?
            "" :
            " - " + authRequest.RequestDeviceIdentifier;
        return deviceTypeAndIdentifierString;
    }

    public async Task PushTrustedDeviceEmail<T>(T authRequest) where T : AuthRequest
    {
        if (!authRequest?.Approved ?? true)
        {
            return;
        }

        var user = await FetchUserFromTheDatabase(authRequest.UserId);

        // This should be impossible
        if (user == null)
        {
            _logger.LogError($"User {authRequest.UserId} not found. Trusted device admin approval email not sent.");
            return;
        }

        await _mailService.SendTrustedDeviceAdminApprovalEmailAsync(user.Email, authRequest.ResponseDate ?? DateTime.UtcNow,
            authRequest.RequestIpAddress, BuildDeviceTypeAndIdentifierDisplayString(authRequest));
    }

    public async Task<bool> PushManyAuthRequestEventLogs<T>(IEnumerable<T> authRequests) where T : AuthRequest
    {
        var pushedLogs = false;
        foreach (var authRequest in authRequests ?? new List<T>())
        {
            await PushAuthRequestEventLog(authRequest);
            pushedLogs = true;
        }
        return pushedLogs;
    }

    public async Task<OrganizationUser> FetchOrganizationUserFromTheDatabase<T>(T authRequest) where T : AuthRequest
    {
        if (!authRequest.OrganizationId.HasValue)
        {
            return null;
        }
        return await _organizationUserRepository.GetByOrganizationAsync(authRequest.OrganizationId.Value, authRequest.UserId);
    }

    public EventType CalculateOrganizationAuthRequestProcessingEventLogType<T>(T authRequest) where T : AuthRequest
    {
        return authRequest.Approved ?? false ?
            EventType.OrganizationUser_ApprovedAuthRequest :
            EventType.OrganizationUser_RejectedAuthRequest;
    }

    public async Task PushAuthRequestEventLog<T>(T authRequest) where T : AuthRequest
    {
        var organizationUser = await FetchOrganizationUserFromTheDatabase(authRequest);

        // This should be impossible
        if (organizationUser == null)
        {
            _logger.LogError($"An organization user was not found while processing auth request {authRequest.Id}. Event logs can not be posted for this request.");
            return;
        }
        await _eventService.LogOrganizationUserEventAsync(organizationUser, CalculateOrganizationAuthRequestProcessingEventLogType(authRequest));
    }
}
