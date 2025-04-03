using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Shared.Validation;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Commands;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;


namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
#nullable enable

public class DeleteManagedOrganizationUserAccountCommand : IDeleteManagedOrganizationUserAccountCommand
{
    private readonly IUserService _userService;
    private readonly IEventService _eventService;
    private readonly IDeleteManagedOrganizationUserAccountValidator _deleteManagedOrganizationUserAccountValidator;
    private readonly IGetOrganizationUsersManagementStatusQuery _getOrganizationUsersManagementStatusQuery;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentContext _currentContext;
    private readonly IReferenceEventService _referenceEventService;
    private readonly IPushNotificationService _pushService;

    public DeleteManagedOrganizationUserAccountCommand(
        IUserService userService,
        IEventService eventService,
        IDeleteManagedOrganizationUserAccountValidator deleteManagedOrganizationUserAccountValidator,
        IGetOrganizationUsersManagementStatusQuery getOrganizationUsersManagementStatusQuery,
        IOrganizationUserRepository organizationUserRepository,
        IUserRepository userRepository,
        ICurrentContext currentContext,
        IReferenceEventService referenceEventService,
        IPushNotificationService pushService)
    {
        _userService = userService;
        _eventService = eventService;
        _deleteManagedOrganizationUserAccountValidator = deleteManagedOrganizationUserAccountValidator;
        _getOrganizationUsersManagementStatusQuery = getOrganizationUsersManagementStatusQuery;
        _organizationUserRepository = organizationUserRepository;
        _userRepository = userRepository;
        _currentContext = currentContext;
        _referenceEventService = referenceEventService;
        _pushService = pushService;
    }

    public async Task<CommandResult> DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId)
    {
        var result = await InternalDeleteManyUsersAsync(organizationId, new[] { organizationUserId }, deletingUserId);


        if (result.InvalidResults.Count > 0)
        {

            var error = result.InvalidResults.FirstOrDefault()?.Errors.FirstOrDefault();

            return new Failure();
        }

        return new Success();
    }

    public async Task<IEnumerable<(Guid OrganizationUserId, CommandResult result)>> DeleteManyUsersAsync(Guid organizationId, IEnumerable<Guid> orgUserIds, Guid? deletingUserId)
    {
        var results = await InternalDeleteManyUsersAsync(organizationId, orgUserIds, deletingUserId);
    }

    private async Task<PartialValidationResult<DeleteUserValidationRequest>> InternalDeleteManyUsersAsync(Guid organizationId, IEnumerable<Guid> orgUserIds, Guid? deletingUserId)
    {
        var orgUsers = await _organizationUserRepository.GetManyAsync(orgUserIds);
        var users = await GetUsersAsync(orgUsers);
        var managementStatuses = await _getOrganizationUsersManagementStatusQuery.GetUsersOrganizationManagementStatusAsync(organizationId, orgUserIds);

        var requests = CreateRequests(organizationId, deletingUserId, orgUserIds, orgUsers, users, managementStatuses);
        var validationResults = await _deleteManagedOrganizationUserAccountValidator.ValidateAsync(requests);

        await CancelPremiumsAsync(validationResults.ValidResults);
        await HandleUserDeletionsAsync(validationResults.ValidResults);
        await LogDeletedOrganizationUsersAsync(validationResults.ValidResults);

        return validationResults;
    }

    private List<DeleteUserValidationRequest> CreateRequests(
        Guid organizationId,
        Guid? deletingUserId,
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
                OrganizationUser = orgUser,
                IsManaged = isManaged,
                OrganizationId = organizationId,
                DeletingUserId = deletingUserId,
            });
        }

        return requests;
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

        if (users.Any())
        {
            await DeleteManyAsync(users);
        }
    }

    private async Task DeleteManyAsync(IEnumerable<User> users)
    {
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
            catch (GatewayException)
            {

            }
        }
    }

}

