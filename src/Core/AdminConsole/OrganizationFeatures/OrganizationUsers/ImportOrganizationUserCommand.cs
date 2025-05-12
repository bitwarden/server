using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.Commands;
using Bit.Core.Billing.Pricing;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class ImportOrganizationUserCommand : IImportOrganizationUserCommand
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IPaymentService _paymentService;
    private readonly IGroupRepository _groupRepository;
    private readonly IEventService _eventService;
    private readonly IReferenceEventService _referenceEventService;
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationService _organizationService;
    private readonly IInviteOrganizationUsersCommand _inviteOrganizationUsersCommand;
    private readonly IPricingClient _pricingClient;

    public ImportOrganizationUserCommand(IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IPaymentService paymentService,
            IGroupRepository groupRepository,
            IEventService eventService,
            IReferenceEventService referenceEventService,
            ICurrentContext currentContext,
            IOrganizationService organizationService,
            IInviteOrganizationUsersCommand inviteOrganizationUsersCommand,
            IPricingClient pricingClient
            )
    {
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _paymentService = paymentService;
        _groupRepository = groupRepository;
        _eventService = eventService;
        _referenceEventService = referenceEventService;
        _currentContext = currentContext;
        _organizationService = organizationService;
        _inviteOrganizationUsersCommand = inviteOrganizationUsersCommand;
        _pricingClient = pricingClient;
    }

    public async Task ImportAsync(Guid organizationId,
        IEnumerable<ImportedGroup> groups,
        IEnumerable<ImportedOrganizationUser> newUsers,
        IEnumerable<string> removeUserExternalIds,
        bool overwriteExisting,
        EventSystemUser eventSystemUser)
    {
        var organization = await GetOrgById(organizationId);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        if (!organization.UseDirectory)
        {
            throw new BadRequestException("Organization cannot use directory syncing.");
        }

        var existingUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(organizationId);

        var importUserData = new OrganizationUserImportData
        {
            NewUsersSet = new HashSet<string>(newUsers?.Select(u => u.ExternalId) ?? new List<string>()),
            ExistingUsers = existingUsers,
            ExistingExternalUsers = GetExistingExternalUsers(existingUsers),
            ExistingExternalUsersIdDict = GetExistingExternalUsers(existingUsers).ToDictionary(u => u.ExternalId, u => u.Id)
        };

        var events = new List<(OrganizationUserUserDetails ou, EventType e, DateTime? d)>();

        // Users
        await RemoveExistingExternalUsers(removeUserExternalIds, events, importUserData);
        if (overwriteExisting)
        {
            await OverwriteExisting(events, importUserData);
        }
        await RemoveExistingUsers(existingUsers, newUsers, organization, importUserData);
        await AddNewUsers(organization, newUsers, eventSystemUser, importUserData);

        // Groups
        await ImportGroups(organization, groups, eventSystemUser, importUserData);

        await _eventService.LogOrganizationUserEventsAsync(events.Select(e => (e.ou, e.e, eventSystemUser, e.d)));
        await _referenceEventService.RaiseEventAsync(
            new ReferenceEvent(ReferenceEventType.DirectorySynced, organization, _currentContext));
    }

    private async Task UpdateUsersAsync(Group group, HashSet<string> groupUsers,
        Dictionary<string, Guid> existingUsersIdDict, HashSet<Guid> existingUsers = null)
    {
        var availableUsers = groupUsers.Intersect(existingUsersIdDict.Keys);
        var users = new HashSet<Guid>(availableUsers.Select(u => existingUsersIdDict[u]));
        if (existingUsers != null && existingUsers.Count == users.Count && users.SetEquals(existingUsers))
        {
            return;
        }

        await _groupRepository.UpdateUsersAsync(group.Id, users);
    }

    private async Task RemoveExistingExternalUsers(IEnumerable<string> removeUserExternalIds,
            List<(OrganizationUserUserDetails ou, EventType e, DateTime? d)> events,
            OrganizationUserImportData importUserData)
    {
        if (!removeUserExternalIds.Any())
        {
            return;
        }

        var existingUsersDict = importUserData.ExistingExternalUsers.ToDictionary(u => u.ExternalId);
        var removeUsersSet = new HashSet<string>(removeUserExternalIds)
            .Except(importUserData.NewUsersSet)
            .Where(u => existingUsersDict.ContainsKey(u) && existingUsersDict[u].Type != OrganizationUserType.Owner)
            .Select(u => existingUsersDict[u]);

        await _organizationUserRepository.DeleteManyAsync(removeUsersSet.Select(u => u.Id));
        events.AddRange(removeUsersSet.Select(u => (
          u,
          EventType.OrganizationUser_Removed,
          (DateTime?)DateTime.UtcNow
          ))
        );
    }

    private async Task RemoveExistingUsers(IEnumerable<OrganizationUserUserDetails> existingUsers,
            IEnumerable<ImportedOrganizationUser> newUsers,
            Organization organization,
            OrganizationUserImportData importUserData)
    {
        if (!newUsers.Any())
        {
            return;
        }

        // Marry existing users
        var existingUsersEmailsDict = existingUsers
            .Where(u => string.IsNullOrWhiteSpace(u.ExternalId))
            .ToDictionary(u => u.Email);
        var newUsersEmailsDict = newUsers.ToDictionary(u => u.Email);
        var usersToAttach = existingUsersEmailsDict.Keys.Intersect(newUsersEmailsDict.Keys).ToList();
        var usersToUpsert = new List<OrganizationUser>();
        foreach (var user in usersToAttach)
        {
            var orgUserDetails = existingUsersEmailsDict[user];
            var orgUser = await _organizationUserRepository.GetByIdAsync(orgUserDetails.Id);
            if (orgUser != null)
            {
                orgUser.ExternalId = newUsersEmailsDict[user].ExternalId;
                usersToUpsert.Add(orgUser);
                importUserData.ExistingExternalUsersIdDict.Add(orgUser.ExternalId, orgUser.Id);
            }
        }
        await _organizationUserRepository.UpsertManyAsync(usersToUpsert);
    }

    private async Task AddNewUsers(Organization organization,
            IEnumerable<ImportedOrganizationUser> newUsers,
            EventSystemUser eventSystemUser,
            OrganizationUserImportData importUserData)
    {

        var existingUsersSet = new HashSet<string>(importUserData.ExistingExternalUsersIdDict.Keys);
        var usersToAdd = importUserData.NewUsersSet.Except(existingUsersSet).ToList();

        var seatsAvailable = int.MaxValue;
        var enoughSeatsAvailable = true;
        if (organization.Seats.HasValue)
        {
            var occupiedSeats = await _organizationUserRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id);
            seatsAvailable = organization.Seats.Value - occupiedSeats;
            enoughSeatsAvailable = seatsAvailable >= usersToAdd.Count;
        }

        var hasStandaloneSecretsManager = await _paymentService.HasSecretsManagerStandalone(organization);

        var userInvites = new List<OrganizationUserInviteCommandModel>();
        foreach (var user in newUsers)
        {
            if (!usersToAdd.Contains(user.ExternalId) || string.IsNullOrWhiteSpace(user.Email))
            {
                continue;
            }

            try
            {
                var invite = new OrganizationUserInviteCommandModel(user.Email, user.ExternalId);
                userInvites.Add(new OrganizationUserInviteCommandModel(invite, hasStandaloneSecretsManager));
            }
            catch (BadRequestException)
            {
                // Thrown when the user is already invited to the organization
                continue;
            }
        }

        var commandResult = await InviteUsersAsync(userInvites, organization);

        switch (commandResult)
        {
            case Success<InviteOrganizationUsersResponse> result:
                foreach (var u in result.Value.InvitedUsers)
                {
                    importUserData.ExistingExternalUsersIdDict.Add(u.ExternalId, u.Id);
                }
                break;
            case Failure<InviteOrganizationUsersResponse> failure:
                throw new BadRequestException(failure.Error.Message);
            default:
                throw new InvalidOperationException($"Unhandled commandResult type: {commandResult.GetType().Name}");
        }
    }

    private async Task<CommandResult<InviteOrganizationUsersResponse>> InviteUsersAsync(List<OrganizationUserInviteCommandModel> invites, Organization organization)
    {
        var plan = await _pricingClient.GetPlanOrThrow(organization.PlanType);
        var inviteOrganization = new InviteOrganization(organization, plan);
        var request = new InviteOrganizationUsersRequest(invites.ToArray(), inviteOrganization, Guid.Empty, DateTimeOffset.UtcNow);

        return await _inviteOrganizationUsersCommand.InviteImportedOrganizationUsersAsync(request, organization.Id);
    }

    private async Task OverwriteExisting(
            List<(OrganizationUserUserDetails ou, EventType e, DateTime? d)> events,
            OrganizationUserImportData importUserData)
    {
        // Remove existing external users that are not in new user set
        var usersToDelete = importUserData.ExistingExternalUsers.Where(u =>
            u.Type != OrganizationUserType.Owner &&
            !importUserData.NewUsersSet.Contains(u.ExternalId) &&
            importUserData.ExistingExternalUsersIdDict.ContainsKey(u.ExternalId));
        await _organizationUserRepository.DeleteManyAsync(usersToDelete.Select(u => u.Id));
        events.AddRange(usersToDelete.Select(u => (
          u,
          EventType.OrganizationUser_Removed,
          (DateTime?)DateTime.UtcNow
          ))
        );
        foreach (var deletedUser in usersToDelete)
        {
            importUserData.ExistingExternalUsersIdDict.Remove(deletedUser.ExternalId);
        }
    }

    private async Task ImportGroups(Organization organization,
            IEnumerable<ImportedGroup> groups,
            EventSystemUser eventSystemUser,
            OrganizationUserImportData importUserData)
    {

        if (!groups.Any())
        {
            return;
        }

        if (!organization.UseGroups)
        {
            throw new BadRequestException("Organization cannot use groups.");
        }

        var existingGroups = await _groupRepository.GetManyByOrganizationIdAsync(organization.Id);
        var importGroupData = new OrganizationGroupImportData
        {
            Groups = groups,
            GroupsDict = groups.ToDictionary(g => g.Group.ExternalId),
            ExistingGroups = existingGroups,
            ExistingExternalGroups = GetExistingExternalGroups(existingGroups)
        };

        await SaveNewGroups(importGroupData, importUserData, eventSystemUser);
        await UpdateExistingGroups(importGroupData, importUserData, organization, eventSystemUser);
    }

    private async Task SaveNewGroups(OrganizationGroupImportData importGroupData,
            OrganizationUserImportData importUserData,
            EventSystemUser eventSystemUser)
    {
        var newGroups = importGroupData.Groups
            .Where(g => !importGroupData.ExistingExternalGroups.ToDictionary(g => g.ExternalId).ContainsKey(g.Group.ExternalId))
            .Select(g => g.Group).ToList();

        var savedGroups = new List<Group>();
        foreach (var group in newGroups)
        {
            group.CreationDate = group.RevisionDate = DateTime.UtcNow;

            savedGroups.Add(await _groupRepository.CreateAsync(group));
            await UpdateUsersAsync(group, importGroupData.GroupsDict[group.ExternalId].ExternalUserIds,
                importUserData.ExistingExternalUsersIdDict);
        }

        await _eventService.LogGroupEventsAsync(
            savedGroups.Select(g => (g, EventType.Group_Created, (EventSystemUser?)eventSystemUser, (DateTime?)DateTime.UtcNow)));
    }

    private async Task UpdateExistingGroups(OrganizationGroupImportData importGroupData,
            OrganizationUserImportData importUserData,
            Organization organization,
            EventSystemUser eventSystemUser)
    {
        var updateGroups = importGroupData.ExistingExternalGroups
            .Where(g => importGroupData.GroupsDict.ContainsKey(g.ExternalId))
            .ToList();

        if (updateGroups.Any())
        {
            var groupUsers = await _groupRepository.GetManyGroupUsersByOrganizationIdAsync(organization.Id);
            var existingGroupUsers = groupUsers
                .GroupBy(gu => gu.GroupId)
                .ToDictionary(g => g.Key, g => new HashSet<Guid>(g.Select(gr => gr.OrganizationUserId)));

            foreach (var group in updateGroups)
            {
                var updatedGroup = importGroupData.GroupsDict[group.ExternalId].Group;
                if (group.Name != updatedGroup.Name)
                {
                    group.RevisionDate = DateTime.UtcNow;
                    group.Name = updatedGroup.Name;

                    await _groupRepository.ReplaceAsync(group);
                }

                await UpdateUsersAsync(group, importGroupData.GroupsDict[group.ExternalId].ExternalUserIds,
                    importUserData.ExistingExternalUsersIdDict,
                    existingGroupUsers.ContainsKey(group.Id) ? existingGroupUsers[group.Id] : null);

            }

            await _eventService.LogGroupEventsAsync(
                updateGroups.Select(g => (g, EventType.Group_Updated, (EventSystemUser?)eventSystemUser, (DateTime?)DateTime.UtcNow)));
        }

    }

    private IEnumerable<OrganizationUserUserDetails> GetExistingExternalUsers(ICollection<OrganizationUserUserDetails> existingUsers)
    {
        return existingUsers
            .Where(u => !string.IsNullOrWhiteSpace(u.ExternalId))
            .ToList();
    }

    private IEnumerable<Group> GetExistingExternalGroups(ICollection<Group> existingGroups)
    {
        return existingGroups
            .Where(u => !string.IsNullOrWhiteSpace(u.ExternalId))
            .ToList();
    }

    private async Task<Organization> GetOrgById(Guid id)
    {
        return await _organizationRepository.GetByIdAsync(id);
    }
}
