using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bit.Core.AdminConsole.OrganizationAuth.Interfaces;
using Bit.Core.AdminConsole.OrganizationAuth.Models;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Api.Request.AuthRequest;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Services;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;
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

    // TODO: When refactoring this method as a part of Bulk Device Approval
    // post-release cleanup we should be able to construct a single
    // AuthRequestProcessor and run its Process() Save() methods, and the
    // various calls to send notifications.
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

    public async Task UpdateAsync(Guid organizationId, IEnumerable<OrganizationAuthRequestUpdate> authRequestUpdates)
    {
        var authRequestEntities = await FetchManyOrganizationAuthRequestsFromTheDatabase(organizationId, authRequestUpdates.Select(aru => aru.Id));
        var processor = new BatchAuthRequestUpdateProcessor(
            authRequestEntities,
            authRequestUpdates,
            new AuthRequestUpdateProcessorConfiguration()
            {
                OrganizationId = organizationId,
                AuthRequestExpiresAfter = _globalSettings.PasswordlessAuth.AdminRequestExpiration
            }
        );
        processor.Process((Exception e) => _logger.LogError(e.Message));
        await processor.Save((IEnumerable<OrganizationAdminAuthRequest> authRequests) => _authRequestRepository.UpdateManyAsync(authRequests));
        await processor.SendPushNotifications((ar) => _pushNotificationService.PushAuthRequestResponseAsync(ar));
        await processor.SendApprovalEmailsForProcessedRequests(SendApprovalEmail);
        await processor.LogOrganizationEventsForProcessedRequests(LogOrganizationEvents);
    }

    async Task<ICollection<OrganizationAdminAuthRequest>> FetchManyOrganizationAuthRequestsFromTheDatabase(Guid organizationId, IEnumerable<Guid> authRequestIds)
    {
        return authRequestIds != null && authRequestIds.Any()
            ? await _authRequestRepository
            .GetManyAdminApprovalRequestsByManyIdsAsync(
                organizationId,
                authRequestIds
            )
            : new List<OrganizationAdminAuthRequest>();
    }

    async Task SendApprovalEmail<T>(T authRequest, string identifier) where T : AuthRequest
    {
        var user = await _userRepository.GetByIdAsync(authRequest.UserId);

        // This should be impossible
        if (user == null)
        {
            _logger.LogError($"User {authRequest.UserId} not found. Trusted device admin approval email not sent.");
            return;
        }

        await _mailService.SendTrustedDeviceAdminApprovalEmailAsync(
            user.Email,
            authRequest.ResponseDate ?? DateTime.UtcNow,
            authRequest.RequestIpAddress,
            identifier
        );
    }

    async Task LogOrganizationEvents(IEnumerable<(OrganizationAdminAuthRequest AuthRequest, EventType EventType)> events)
    {
        var organizationUsers = await _organizationUserRepository.GetManyAsync(events.Select(e => e.AuthRequest.OrganizationUserId));
        await _eventService.LogOrganizationUserEventsAsync(
            organizationUsers.Select(ou =>
            {
                var e = events.FirstOrDefault(e => e.AuthRequest.OrganizationUserId == ou.Id);
                return (ou, e.EventType, e.AuthRequest.ResponseDate);
            })
        );
    }
}
