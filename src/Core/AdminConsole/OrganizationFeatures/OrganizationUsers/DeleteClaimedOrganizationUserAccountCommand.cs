using Bit.Core.AdminConsole.Errors;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Shared.Validation;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Commands;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Microsoft.Extensions.Logging;


namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
#nullable enable

public class DeleteClaimedOrganizationUserAccountCommand : IDeleteClaimedOrganizationUserAccountCommand
{
    private readonly IUserService _userService;
    private readonly IEventService _eventService;
    private readonly IDeleteManagedOrganizationUserAccountValidator _deleteManagedOrganizationUserAccountValidator;
    private readonly IGetOrganizationUsersManagementStatusQuery _getOrganizationUsersManagementStatusQuery;
    private readonly IGetOrganizationUsersClaimedStatusQuery _getOrganizationUsersClaimedStatusQuery;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentContext _currentContext;
    private readonly ILogger<DeleteClaimedOrganizationUserAccountCommand> _logger;
    private readonly IReferenceEventService _referenceEventService;
    private readonly IPushNotificationService _pushService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProviderUserRepository _providerUserRepository;
    public DeleteClaimedOrganizationUserAccountCommand(
        IUserService userService,
        IEventService eventService,
        IGetOrganizationUsersClaimedStatusQuery getOrganizationUsersClaimedStatusQuery,
        IOrganizationUserRepository organizationUserRepository,
        IUserRepository userRepository,
        ICurrentContext currentContext,
        ILogger<DeleteClaimedOrganizationUserAccountCommand> logger,
        IReferenceEventService referenceEventService,
        IPushNotificationService pushService)
    {
        _userService = userService;
        _eventService = eventService;
        _deleteManagedOrganizationUserAccountValidator = deleteManagedOrganizationUserAccountValidator;
        _getOrganizationUsersManagementStatusQuery = _getOrganizationUsersManagementStatusQuery;
        _getOrganizationUsersClaimedStatusQuery = getOrganizationUsersClaimedStatusQuery;
        _organizationUserRepository = organizationUserRepository;
        _userRepository = userRepository;
        _currentContext = currentContext;
        _logger = logger;
        _referenceEventService = referenceEventService;
        _pushService = pushService;
    }

    public async Task<CommandResult<DeleteUserResponse>> DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid deletingUserId)
    {
        var result = await InternalDeleteManyUsersAsync(organizationId, [organizationUserId], deletingUserId);

        var error = result.InvalidResults.FirstOrDefault()?.Errors.FirstOrDefault();

        if (error != null)
        {
            return error.ToFailure<DeleteUserValidationRequest, DeleteUserResponse>();
        }

        var valid = result.ValidResults.First();

        return new Success<DeleteUserResponse>(new DeleteUserResponse
        {
            OrganizationUserId = valid!.Value.OrganizationUserId
        });
    }

    public async Task<Partial<DeleteUserResponse>> DeleteManyUsersAsync(Guid organizationId, IEnumerable<Guid> orgUserIds, Guid deletingUserId)
    {
        var result = await InternalDeleteManyUsersAsync(organizationId, orgUserIds, deletingUserId);

        var successes = result.ValidResults.Select(valid => new DeleteUserResponse { OrganizationUserId = valid.Value.OrganizationUser!.Id });
        var errors = result.InvalidResults
            .Select(invalid => invalid.Errors.First())
            .Select(error => error.ToError(new DeleteUserResponse() { OrganizationUserId = error.ErroredValue.OrganizationUserId }));

        return new Partial<DeleteUserResponse>(successes, errors);
    }

    private async Task<PartialValidationResult<DeleteUserValidationRequest>> InternalDeleteManyUsersAsync(Guid organizationId, IEnumerable<Guid> orgUserIds, Guid deletingUserId)
    {
        var orgUsers = await _organizationUserRepository.GetManyAsync(orgUserIds);
        var users = await GetUsersAsync(orgUsers);
        var managementStatuses = await _getOrganizationUsersManagementStatusQuery.GetUsersOrganizationManagementStatusAsync(organizationId, orgUserIds);

        var requests = CreateRequests(organizationId, deletingUserId, orgUserIds, orgUsers, users, managementStatuses);
        var results = await _deleteManagedOrganizationUserAccountValidator.ValidateAsync(requests);

        await CancelPremiumsAsync(results.ValidResults);
        await HandleUserDeletionsAsync(results.ValidResults);
        await LogDeletedOrganizationUsersAsync(results.ValidResults);

        return results;
    }

    private List<DeleteUserValidationRequest> CreateRequests(
        Guid organizationId,
        Guid deletingUserId,
        IEnumerable<Guid> orgUserIds,
        ICollection<OrganizationUser> orgUsers,
        IEnumerable<User> users,
        IDictionary<Guid, bool> managementStatuses)
    {
        var requests = new List<DeleteUserValidationRequest>();
        foreach (var orgUserId in orgUserIds)
        {
            var orgUser = orgUsers.FirstOrDefault(orgUser => orgUser.Id == orgUserId);
            var user = users.FirstOrDefault(user => user.Id == orgUser?.UserId);
            managementStatuses.TryGetValue(orgUserId, out var isManaged);

            requests.Add(new DeleteUserValidationRequest
            {
                User = user,
                OrganizationUserId = orgUserId,
                OrganizationUser = orgUser,
                IsManaged = isManaged,
                OrganizationId = organizationId,
                DeletingUserId = deletingUserId,
            });
        }

        return requests;
        // Jimmy move this to the validator
        // if (orgUser.Type == OrganizationUserType.Admin && await _currentContext.OrganizationCustom(organizationId))
        // {
        //     throw new BadRequestException("Custom users can not delete admins.");
        // }

        // if (!claimedStatus.TryGetValue(orgUser.Id, out var isClaimed) || !isClaimed)
        // {
        //     throw new BadRequestException("Member is not claimed by the organization.");
        // }
    }

    private async Task<IEnumerable<User>> GetUsersAsync(ICollection<OrganizationUser> orgUsers)
    {
        var userIds = orgUsers
         .Where(orgUser => orgUser.UserId.HasValue)
         .Select(orgUser => orgUser.UserId!.Value)
         .ToList();

        return await _userRepository.GetManyAsync(userIds);
    }

    private async Task LogDeletedOrganizationUsersAsync(List<Valid<DeleteUserValidationRequest>> requests)
    {
        var eventDate = DateTime.UtcNow;

        var events = requests
            .Select(request => (request.Value.OrganizationUser!, (EventType)EventType.OrganizationUser_Deleted, (DateTime?)eventDate))
            .ToList();

        if (events.Any())
        {
            await _eventService.LogOrganizationUserEventsAsync(events);
        }
    }


    private async Task HandleUserDeletionsAsync(List<Valid<DeleteUserValidationRequest>> requests)
    {
        var users = requests
            .Select(request => request.Value.User!);

        if (!users.Any())
        {
            return;
        }

        await _userRepository.DeleteManyAsync(users);

        foreach (var user in users)
        {
            await _referenceEventService.RaiseEventAsync(
                new ReferenceEvent(ReferenceEventType.DeleteAccount, user, _currentContext));
            await _pushService.PushLogOutAsync(user.Id);
        }
    }

    private async Task CancelPremiumsAsync(List<Valid<DeleteUserValidationRequest>> requests)
    {
        var users = requests
            .Select(request => request.Value.User!);

        foreach (var user in users)
        {
            try
            {
                await _userService.CancelPremiumAsync(user);
            }
            catch (GatewayException exception)
            {
                _logger.LogWarning(exception, "Failed to cancel the user's premium.");
            }
        }
    }

}

