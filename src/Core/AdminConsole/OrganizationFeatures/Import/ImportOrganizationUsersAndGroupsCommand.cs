using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Import;

public class ImportOrganizationUsersAndGroupsCommand : IImportOrganizationUsersAndGroupsCommand
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IPaymentService _paymentService;
    private readonly IGroupRepository _groupRepository;
    private readonly IEventService _eventService;
    private readonly IOrganizationService _organizationService;
    private readonly IFeatureService _featureService;

    private readonly EventSystemUser _EventSystemUser = EventSystemUser.PublicApi;

    public ImportOrganizationUsersAndGroupsCommand(IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IPaymentService paymentService,
            IGroupRepository groupRepository,
            IEventService eventService,
            IOrganizationService organizationService,
            IFeatureService featureService)
    {
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _paymentService = paymentService;
        _groupRepository = groupRepository;
        _eventService = eventService;
        _organizationService = organizationService;
        _featureService = featureService;
    }

    /// <summary>
    /// Imports and synchronizes organization users and groups.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization.</param>
    /// <param name="importedGroups">List of groups to import.</param>
    /// <param name="importedUsers">List of users to import.</param>
    /// <param name="removeUserExternalIds">A collection of ExternalUserIds to be removed from the organization.</param>
    /// <param name="overwriteExisting">Indicates whether to delete existing external users from the organization
    /// who are not included in the current import.</param>
    /// <exception cref="NotFoundException">Thrown if the organization does not exist.</exception>
    /// <exception cref="BadRequestException">Thrown if the organization is not configured to use directory syncing.</exception>
    public async Task ImportAsync(Guid organizationId,
        IEnumerable<ImportedGroup> importedGroups,
        IEnumerable<ImportedOrganizationUser> importedUsers,
        IEnumerable<string> removeUserExternalIds,
        bool overwriteExisting)
    {
        var organization = await GetOrgById(organizationId);
        if (organization is null)
        {
            throw new NotFoundException();
        }

        if (!organization.UseDirectory)
        {
            throw new BadRequestException("Organization cannot use directory syncing.");
        }

        var existingUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(organizationId);
        var importUserData = new OrganizationUserImportData(existingUsers, importedUsers);
        var events = new List<(OrganizationUserUserDetails ou, EventType e, DateTime? d)>();

        await RemoveExistingExternalUsers(removeUserExternalIds, events, importUserData);

        if (overwriteExisting)
        {
            await OverwriteExisting(events, importUserData);
        }

        await UpdateExistingUsers(importedUsers, importUserData);

        await AddNewUsers(organization, importedUsers, importUserData);

        await ImportGroups(organization, importedGroups, importUserData);

        await _eventService.LogOrganizationUserEventsAsync(events.Select(e => (e.ou, e.e, _EventSystemUser, e.d)));
    }

    /// <summary>
    /// Deletes external users based on provided set of ExternalIds.
    /// </summary>
    /// <param name="removeUserExternalIds">A collection of external user IDs to be deleted.</param>
    /// <param name="events">A list to which user removal events will be added.</param>
    /// <param name="importUserData">Data containing imported and existing external users.</param>

    private async Task RemoveExistingExternalUsers(IEnumerable<string> removeUserExternalIds,
            List<(OrganizationUserUserDetails ou, EventType e, DateTime? d)> events,
            OrganizationUserImportData importUserData)
    {
        if (!removeUserExternalIds.Any())
        {
            return;
        }

        var existingUsersDict = importUserData.ExistingExternalUsers.ToDictionary(u => u.ExternalId);
        // Determine which ids in removeUserExternalIds to delete based on:
        // They are not in ImportedExternalIds, they are in existingUsersDict, and they are not an owner.
        var removeUsersSet = new HashSet<string>(removeUserExternalIds)
            .Except(importUserData.ImportedExternalIds)
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

    /// <summary>
    /// Updates existing organization users by assigning each an ExternalId from the imported user data
    /// where a match is found by email and the existing user lacks an ExternalId. Saves the updated
    /// users and updates the ExistingExternalUsersIdDict mapping.
    /// </summary>
    /// <param name="importedUsers">List of imported organization users.</param>
    /// <param name="importUserData">Data containing existing and imported users, along with mapping dictionaries.</param>
    private async Task UpdateExistingUsers(IEnumerable<ImportedOrganizationUser> importedUsers, OrganizationUserImportData importUserData)
    {
        if (!importedUsers.Any())
        {
            return;
        }

        var updateUsers = new List<OrganizationUser>();

        // Map existing and imported users to dicts keyed by Email
        var existingUsersEmailsDict = importUserData.ExistingUsers
            .Where(u => string.IsNullOrWhiteSpace(u.ExternalId))
            .ToDictionary(u => u.Email);
        var importedUsersEmailsDict = importedUsers.ToDictionary(u => u.Email);

        // Determine which users to update.
        var userEmailsToUpdate = existingUsersEmailsDict.Keys.Intersect(importedUsersEmailsDict.Keys).ToList();
        var userIdsToUpdate = userEmailsToUpdate.Select(e => existingUsersEmailsDict[e].Id).ToList();

        var organizationUsers = (await _organizationUserRepository.GetManyAsync(userIdsToUpdate)).ToDictionary(u => u.Id);

        foreach (var userEmail in userEmailsToUpdate)
        {
            // verify userEmail has an associated OrganizationUser
            existingUsersEmailsDict.TryGetValue(userEmail, out var existingUser);
            organizationUsers.TryGetValue(existingUser!.Id, out var organizationUser);
            importedUsersEmailsDict.TryGetValue(userEmail, out var importedUser);

            if (organizationUser is null || importedUser is null)
            {
                continue;
            }

            organizationUser.ExternalId = importedUser.ExternalId;
            updateUsers.Add(organizationUser);
            importUserData.ExistingExternalUsersIdDict.Add(organizationUser.ExternalId, organizationUser.Id);
        }
        await _organizationUserRepository.UpsertManyAsync(updateUsers);
    }

    /// <summary>
    /// Adds new external users to the organization by inviting users who are present in the imported data
    /// but not already part of the organization. Sends invitations, updates the user Id mapping on success,
    /// and throws exceptions on failure.
    /// </summary>
    /// <param name="organization">The target organization to which users are being added.</param>
    /// <param name="importedUsers">A collection of imported users to consider for addition.</param>
    /// <param name="importUserData">Data containing imported user info and existing user mappings.</param>
    private async Task AddNewUsers(Organization organization,
            IEnumerable<ImportedOrganizationUser> importedUsers,
            OrganizationUserImportData importUserData)
    {
        // Determine which users are already in the organization
        var existingUsersSet = new HashSet<string>(importUserData.ExistingExternalUsersIdDict.Keys);
        var usersToAdd = importUserData.ImportedExternalIds.Except(existingUsersSet).ToList();
        var userInvites = new List<(OrganizationUserInvite, string)>();
        var hasStandaloneSecretsManager = await _paymentService.HasSecretsManagerStandalone(organization);

        foreach (var user in importedUsers)
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

        var invitedUsers = await _organizationService.InviteUsersAsync(organization.Id, Guid.Empty, _EventSystemUser, userInvites);
        foreach (var invitedUser in invitedUsers)
        {
            importUserData.ExistingExternalUsersIdDict.TryAdd(invitedUser.ExternalId!, invitedUser.Id);
        }
    }

    /// <summary>
    /// Deletes existing external users from the organization who are not included in the current import and are not owners.
    /// Records corresponding removal events and updates the internal mapping by removing deleted users.
    /// </summary>
    /// <param name="events">A list to which user removal events will be added.</param>
    /// <param name="importUserData">Data containing existing and imported external users along with their Id mappings.</param>
    private async Task OverwriteExisting(
            List<(OrganizationUserUserDetails ou, EventType e, DateTime? d)> events,
            OrganizationUserImportData importUserData)
    {
        var usersToDelete = importUserData.ExistingExternalUsers
            .Where(u =>
                u.Type != OrganizationUserType.Owner &&
                !importUserData.ImportedExternalIds.Contains(u.ExternalId) &&
                importUserData.ExistingExternalUsersIdDict.ContainsKey(u.ExternalId))
            .ToList();

        if (_featureService.IsEnabled(FeatureFlagKeys.DirectoryConnectorPreventUserRemoval) &&
            usersToDelete.Any(u => !u.HasMasterPassword))
        {
            // Removing users without an MP will put their account in an unrecoverable state.
            // We allow this during normal syncs for offboarding, but overwriteExisting risks bricking every user in
            // the organization, so you don't get to do it here.
            throw new BadRequestException(
                "Sync failed. To proceed, disable the 'Remove and re-add users during next sync' setting and try again.");
        }

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

    /// <summary>
    /// Imports group data into the organization by saving new groups and updating existing ones.
    /// </summary>
    /// <param name="organization">The organization into which groups are being imported.</param>
    /// <param name="importedGroups">A collection of groups to be imported.</param>
    /// <param name="importUserData">Data containing information about existing and imported users.</param>
    private async Task ImportGroups(Organization organization, IEnumerable<ImportedGroup> importedGroups, OrganizationUserImportData importUserData)
    {
        if (!importedGroups.Any())
        {
            return;
        }

        if (!organization.UseGroups)
        {
            throw new BadRequestException("Organization cannot use groups.");
        }

        var existingGroups = await _groupRepository.GetManyByOrganizationIdAsync(organization.Id);
        var importGroupData = new OrganizationGroupImportData(importedGroups, existingGroups);

        await SaveNewGroups(importGroupData, importUserData);
        await UpdateExistingGroups(importGroupData, importUserData, organization);
    }

    /// <summary>
    /// Saves newly imported groups that do not already exist in the organization.
    /// Sets their creation and revision dates, associates users with each group.
    /// </summary>
    /// <param name="importGroupData">Data containing both imported and existing groups.</param>
    /// <param name="importUserData">Data containing information about existing and imported users.</param>
    private async Task SaveNewGroups(OrganizationGroupImportData importGroupData, OrganizationUserImportData importUserData)
    {
        var existingExternalGroupsDict = importGroupData.ExistingExternalGroups.ToDictionary(g => g.ExternalId!);
        var newGroups = importGroupData.Groups
            .Where(g => !existingExternalGroupsDict.ContainsKey(g.Group.ExternalId!))
            .Select(g => g.Group)
            .ToList()!;

        var savedGroups = new List<Group>();
        foreach (var group in newGroups)
        {
            group.CreationDate = group.RevisionDate = DateTime.UtcNow;

            savedGroups.Add(await _groupRepository.CreateAsync(group));
            await UpdateUsersAsync(group, importGroupData.GroupsDict[group.ExternalId!].ExternalUserIds,
                importUserData.ExistingExternalUsersIdDict);
        }

        await _eventService.LogGroupEventsAsync(
            savedGroups.Select(g => (g, EventType.Group_Created, (EventSystemUser?)_EventSystemUser, (DateTime?)DateTime.UtcNow)));
    }

    /// <summary>
    /// Updates existing groups in the organization based on imported group data.
    /// If a group's name has changed, it updates the name and revision date in the repository.
    /// Also updates group-user associations.
    /// </summary>
    /// <param name="importGroupData">Data containing imported groups and their user associations.</param>
    /// <param name="importUserData">Data containing imported and existing organization users.</param>
    /// <param name="organization">The organization to which the groups belong.</param>
    private async Task UpdateExistingGroups(OrganizationGroupImportData importGroupData,
            OrganizationUserImportData importUserData,
            Organization organization)
    {
        var updateGroups = importGroupData.ExistingExternalGroups
            .Where(g => importGroupData.GroupsDict.ContainsKey(g.ExternalId!))
            .ToList();

        if (updateGroups.Any())
        {
            // get existing group users
            var groupUsers = await _groupRepository.GetManyGroupUsersByOrganizationIdAsync(organization.Id);
            var existingGroupUsers = groupUsers
                .GroupBy(gu => gu.GroupId)
                .ToDictionary(g => g.Key, g => new HashSet<Guid>(g.Select(gr => gr.OrganizationUserId)));

            foreach (var group in updateGroups)
            {
                // Check for changes to the group, update if changed.
                var updatedGroup = importGroupData.GroupsDict[group.ExternalId!].Group;
                if (group.Name != updatedGroup.Name)
                {
                    group.RevisionDate = DateTime.UtcNow;
                    group.Name = updatedGroup.Name;

                    await _groupRepository.ReplaceAsync(group);
                }

                // compare and update user group associations
                await UpdateUsersAsync(group, importGroupData.GroupsDict[group.ExternalId!].ExternalUserIds,
                    importUserData.ExistingExternalUsersIdDict,
                    existingGroupUsers.ContainsKey(group.Id) ? existingGroupUsers[group.Id] : null);

            }

            await _eventService.LogGroupEventsAsync(
                updateGroups.Select(g => (g, EventType.Group_Updated, (EventSystemUser?)_EventSystemUser, (DateTime?)DateTime.UtcNow)));
        }

    }

    /// <summary>
    /// Updates the user associations for a given group.
    /// Only updates if the set of associated users differs from the current group membership.
    /// Filters users based on those present in the existing user Id dictionary.
    /// </summary>
    /// <param name="group">The group whose user associations are being updated.</param>
    /// <param name="groupUsers">A set of ExternalUserIds to be associated with the group.</param>
    /// <param name="existingUsersIdDict">A dictionary mapping ExternalUserIds to internal user Ids.</param>
    /// <param name="existingUsers">Optional set of currently associated user Ids for comparison.</param>
    private async Task UpdateUsersAsync(Group group, HashSet<string> groupUsers,
        Dictionary<string, Guid> existingUsersIdDict, HashSet<Guid>? existingUsers = null)
    {
        var availableUsers = groupUsers.Intersect(existingUsersIdDict.Keys);
        var users = new HashSet<Guid>(availableUsers.Select(u => existingUsersIdDict[u]));
        if (existingUsers is not null && existingUsers.Count == users.Count && users.SetEquals(existingUsers))
        {
            return;
        }

        await _groupRepository.UpdateUsersAsync(group.Id, users);
    }

    private async Task<Organization?> GetOrgById(Guid id)
    {
        return await _organizationRepository.GetByIdAsync(id);
    }
}
