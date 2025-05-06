using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using OrganizationUserInvite = Bit.Core.Models.Business.OrganizationUserInvite;

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

    public ImportOrganizationUserCommand(IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IPaymentService paymentService,
            IGroupRepository groupRepository,
            IEventService eventService,
            IReferenceEventService referenceEventService,
            ICurrentContext currentContext,
            IOrganizationService organizationService
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
    }

    public async Task ImportAsync(Guid organizationId,
        IEnumerable<ImportedGroup> groups,
        IEnumerable<ImportedOrganizationUser> newUsers,
        IEnumerable<string> removeUserExternalIds,
        bool overwriteExisting,
        EventSystemUser eventSystemUser
    )
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

        var newUsersSet = new HashSet<string>(newUsers?.Select(u => u.ExternalId) ?? new List<string>());
        var existingUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(organizationId);
        var existingExternalUsers = existingUsers.Where(u => !string.IsNullOrWhiteSpace(u.ExternalId)).ToList();
        var existingExternalUsersIdDict = existingExternalUsers.ToDictionary(u => u.ExternalId, u => u.Id);

        // Users

        var events = new List<(OrganizationUserUserDetails ou, EventType e, DateTime? d)>();

        if (removeUserExternalIds?.Any() ?? false)
        {
            await RemoveExistingExternalUsers(removeUserExternalIds, events, existingExternalUsers, newUsersSet);
        }

        if (overwriteExisting)
        {
            // Remove existing external users that are not in new user set
            var usersToDelete = existingExternalUsers.Where(u =>
                u.Type != OrganizationUserType.Owner &&
                !newUsersSet.Contains(u.ExternalId) &&
                existingExternalUsersIdDict.ContainsKey(u.ExternalId));
            await _organizationUserRepository.DeleteManyAsync(usersToDelete.Select(u => u.Id));
            events.AddRange(usersToDelete.Select(u => (
              u,
              EventType.OrganizationUser_Removed,
              (DateTime?)DateTime.UtcNow
              ))
            );
            foreach (var deletedUser in usersToDelete)
            {
                existingExternalUsersIdDict.Remove(deletedUser.ExternalId);
            }
        }

        if (newUsers?.Any() ?? false)
        {
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
                    existingExternalUsersIdDict.Add(orgUser.ExternalId, orgUser.Id);
                }
            }
            await _organizationUserRepository.UpsertManyAsync(usersToUpsert);

            // Add new users
            var existingUsersSet = new HashSet<string>(existingExternalUsersIdDict.Keys);
            var usersToAdd = newUsersSet.Except(existingUsersSet).ToList();

            var seatsAvailable = int.MaxValue;
            var enoughSeatsAvailable = true;
            if (organization.Seats.HasValue)
            {
                var occupiedSeats = await _organizationUserRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id);
                seatsAvailable = organization.Seats.Value - occupiedSeats;
                enoughSeatsAvailable = seatsAvailable >= usersToAdd.Count;
            }

            var hasStandaloneSecretsManager = await _paymentService.HasSecretsManagerStandalone(organization);

            var userInvites = new List<(OrganizationUserInvite, string)>();
            foreach (var user in newUsers)
            {
                if (!usersToAdd.Contains(user.ExternalId) || string.IsNullOrWhiteSpace(user.Email))
                {
                    continue;
                }

                try
                {
                    var invite = new OrganizationUserInvite
                    {
                        Emails = new List<string> { user.Email },
                        Type = OrganizationUserType.User,
                        Collections = new List<CollectionAccessSelection>(),
                        AccessSecretsManager = hasStandaloneSecretsManager
                    };
                    userInvites.Add((invite, user.ExternalId));
                }
                catch (BadRequestException)
                {
                    // Thrown when the user is already invited to the organization
                    continue;
                }
            }

            //@TODO: replace with command
            var invitedUsers = await _organizationService.InviteUsersAsync(organizationId, invitingUserId: null, systemUser: eventSystemUser, userInvites);
            foreach (var invitedUser in invitedUsers)
            {
                existingExternalUsersIdDict.Add(invitedUser.ExternalId, invitedUser.Id);
            }
        }


        // Groups
        if (groups?.Any() ?? false)
        {
            if (!organization.UseGroups)
            {
                throw new BadRequestException("Organization cannot use groups.");
            }

            var groupsDict = groups.ToDictionary(g => g.Group.ExternalId);
            var existingGroups = await _groupRepository.GetManyByOrganizationIdAsync(organizationId);
            var existingExternalGroups = existingGroups
                .Where(u => !string.IsNullOrWhiteSpace(u.ExternalId)).ToList();
            var existingExternalGroupsDict = existingExternalGroups.ToDictionary(g => g.ExternalId);

            var newGroups = groups
                .Where(g => !existingExternalGroupsDict.ContainsKey(g.Group.ExternalId))
                .Select(g => g.Group).ToList();

            var savedGroups = new List<Group>();
            foreach (var group in newGroups)
            {
                group.CreationDate = group.RevisionDate = DateTime.UtcNow;

                savedGroups.Add(await _groupRepository.CreateAsync(group));
                await UpdateUsersAsync(group, groupsDict[group.ExternalId].ExternalUserIds,
                    existingExternalUsersIdDict);
            }

            await _eventService.LogGroupEventsAsync(
                savedGroups.Select(g => (g, EventType.Group_Created, (EventSystemUser?)eventSystemUser, (DateTime?)DateTime.UtcNow)));

            var updateGroups = existingExternalGroups
                .Where(g => groupsDict.ContainsKey(g.ExternalId))
                .ToList();

            if (updateGroups.Any())
            {
                var groupUsers = await _groupRepository.GetManyGroupUsersByOrganizationIdAsync(organizationId);
                var existingGroupUsers = groupUsers
                    .GroupBy(gu => gu.GroupId)
                    .ToDictionary(g => g.Key, g => new HashSet<Guid>(g.Select(gr => gr.OrganizationUserId)));

                foreach (var group in updateGroups)
                {
                    var updatedGroup = groupsDict[group.ExternalId].Group;
                    if (group.Name != updatedGroup.Name)
                    {
                        group.RevisionDate = DateTime.UtcNow;
                        group.Name = updatedGroup.Name;

                        await _groupRepository.ReplaceAsync(group);
                    }

                    await UpdateUsersAsync(group, groupsDict[group.ExternalId].ExternalUserIds,
                        existingExternalUsersIdDict,
                        existingGroupUsers.ContainsKey(group.Id) ? existingGroupUsers[group.Id] : null);

                }

                await _eventService.LogGroupEventsAsync(
                    updateGroups.Select(g => (g, EventType.Group_Updated, (EventSystemUser?)eventSystemUser, (DateTime?)DateTime.UtcNow)));
            }
        }

        await _eventService.LogOrganizationUserEventsAsync(events.Select(e => (e.ou, e.e, eventSystemUser, e.d)));
        await _referenceEventService.RaiseEventAsync(
            new ReferenceEvent(ReferenceEventType.DirectorySynced, organization, _currentContext));
    }

    private async Task<Organization> GetOrgById(Guid id)
    {
        return await _organizationRepository.GetByIdAsync(id);
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

    private async Task RemoveExistingExternalUsers(
            IEnumerable<string> removeUserExternalIds,
            List<(OrganizationUserUserDetails ou, EventType e, DateTime? d)> events,
            IEnumerable<OrganizationUserUserDetails> existingExternalUsers,
            HashSet<string> newUsersSet
    )
    {
        var existingUsersDict = existingExternalUsers.ToDictionary(u => u.ExternalId);
        var removeUsersSet = new HashSet<string>(removeUserExternalIds)
            .Except(newUsersSet)
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
}
