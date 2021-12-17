using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.OrganizationFeatures.Mail;
using Bit.Core.OrganizationFeatures.Subscription;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Services.OrganizationServices.UserInvite;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrgUser
{
    public class OrganizationUserImportCommand : IOrganizationUserImportCommand
    {
        readonly IOrganizationUserImportAccessPolicies _organizationUserImportAccessPolicies;
        readonly IOrganizationUserInviteService _organizationUserInviteService;
        readonly IOrganizationSubscriptionService _organizationSubscriptionService;
        readonly IOrganizationUserMailer _organizationUserMailer;
        readonly IOrganizationRepository _organizationRepository;
        readonly IOrganizationUserRepository _organizationUserRepository;
        readonly IGroupRepository _groupRepository;
        readonly IEventService _eventService;
        readonly IReferenceEventService _referenceEventService;


        public OrganizationUserImportCommand(
            IOrganizationUserImportAccessPolicies organizationUserImportAccessPolicies,
            IOrganizationUserInviteService organizationUserInviteService,
            IOrganizationSubscriptionService organizationSubscriptionService,
            IOrganizationUserMailer organizationUserMailer,
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IGroupRepository groupRepository,
            IEventService eventService,
            IReferenceEventService referenceEventService

        )
        {
            _organizationUserImportAccessPolicies = organizationUserImportAccessPolicies;
            _organizationUserInviteService = organizationUserInviteService;
            _organizationSubscriptionService = organizationSubscriptionService;
            _organizationUserMailer = organizationUserMailer;
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _groupRepository = groupRepository;
            _eventService = eventService;
            _referenceEventService = referenceEventService;
        }

        // TODO MDG: split this up
        // TODO MDG: resuse OrganizationUserInviteCommand
        public async Task ImportAsync(Guid organizationId, Guid? importingUserId, IEnumerable<ImportedGroup> groups,
            IEnumerable<ImportedOrganizationUser> newUsers, IEnumerable<string> removeUserExternalIds,
            bool overwriteExisting)
        {
            var organization = await _organizationRepository.GetByIdAsync(organizationId);
            CoreHelpers.HandlePermissionResult(
                _organizationUserImportAccessPolicies.CanImport(organization)
            );

            var newUsersSet = new HashSet<string>(newUsers?.Select(u => u.ExternalId) ?? new List<string>());
            var existingUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(organizationId);
            var existingExternalUsers = existingUsers.Where(u => !string.IsNullOrWhiteSpace(u.ExternalId)).ToList();
            var existingExternalUsersIdDict = existingExternalUsers.ToDictionary(u => u.ExternalId, u => u.Id);

            // Users

            // Remove Users
            if (removeUserExternalIds?.Any() ?? false)
            {
                var removeUsersSet = new HashSet<string>(removeUserExternalIds);
                var existingUsersDict = existingExternalUsers.ToDictionary(u => u.ExternalId);

                await _organizationUserRepository.DeleteManyAsync(removeUsersSet
                    .Except(newUsersSet)
                    .Where(u => existingUsersDict.ContainsKey(u) && existingUsersDict[u].Type != OrganizationUserType.Owner)
                    .Select(u => existingUsersDict[u].Id));
            }

            if (overwriteExisting)
            {
                // Remove existing external users that are not in new user set
                var usersToDelete = existingExternalUsers.Where(u =>
                    u.Type != OrganizationUserType.Owner &&
                    !newUsersSet.Contains(u.ExternalId) &&
                    existingExternalUsersIdDict.ContainsKey(u.ExternalId));
                await _organizationUserRepository.DeleteManyAsync(usersToDelete.Select(u => u.Id));
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
                var newSeatsRequired = 0;
                if (organization.Seats.HasValue)
                {
                    var userCount = await _organizationUserRepository.GetCountByOrganizationIdAsync(organizationId);
                    seatsAvailable = organization.Seats.Value - userCount;
                    newSeatsRequired = seatsAvailable - usersToAdd.Count;
                }

                var userInvites = new List<(OrganizationUserInviteData, string)>();
                foreach (var user in newUsers)
                {
                    if (!usersToAdd.Contains(user.ExternalId) || string.IsNullOrWhiteSpace(user.Email))
                    {
                        continue;
                    }

                    var invite = new OrganizationUserInviteData
                    {
                        Emails = new List<string> { user.Email },
                        Type = OrganizationUserType.User,
                        AccessAll = false,
                        Collections = new List<SelectionReadOnly>(),
                    };
                    userInvites.Add((invite, user.ExternalId));
                }

                var invitedUsers = await InviteUsersAsync(organization, newSeatsRequired, userInvites);
                foreach (var invitedUser in invitedUsers)
                {
                    existingExternalUsersIdDict.Add(invitedUser.ExternalId, invitedUser.Id);
                }
            }


            // Groups
            if (groups?.Any() ?? false)
            {
                CoreHelpers.HandlePermissionResult(
                    _organizationUserImportAccessPolicies.CanUseGroups(organization)
                );

                var groupsDict = groups.ToDictionary(g => g.Group.ExternalId);
                var existingGroups = await _groupRepository.GetManyByOrganizationIdAsync(organizationId);
                var existingExternalGroups = existingGroups
                    .Where(u => !string.IsNullOrWhiteSpace(u.ExternalId)).ToList();
                var existingExternalGroupsDict = existingExternalGroups.ToDictionary(g => g.ExternalId);

                var newGroups = groups
                    .Where(g => !existingExternalGroupsDict.ContainsKey(g.Group.ExternalId))
                    .Select(g => g.Group);

                foreach (var group in newGroups)
                {
                    group.CreationDate = group.RevisionDate = DateTime.UtcNow;

                    await _groupRepository.CreateAsync(group);
                    await UpdateUsersAsync(group, groupsDict[group.ExternalId].ExternalUserIds,
                        existingExternalUsersIdDict);
                }

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
                }
            }

            await _referenceEventService.RaiseEventAsync(
                new ReferenceEvent(ReferenceEventType.DirectorySynced, organization));
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

        private async Task<List<OrganizationUser>> InviteUsersAsync(Organization organization, int newSeatsRequired,
            IEnumerable<(OrganizationUserInviteData invite, string externalId)> invites)
        {
            var invitedUsers = await _organizationUserInviteService.InviteUsersAsync(organization, invites);
            try
            {
                await _organizationSubscriptionService.AutoAddSeatsAsync(organization, newSeatsRequired);
            }
            catch
            {
                await _organizationUserRepository.DeleteManyAsync(invitedUsers.Select(u => u.Id));
                throw;
            }
            await _organizationUserMailer.SendInvitesAsync(invitedUsers.Select(u => (u, _organizationUserInviteService.MakeToken(u))), organization);
            await CreateInviteEventsForOrganizationUsersAsync(invitedUsers, organization);

            return invitedUsers;
        }

        private async Task CreateInviteEventsForOrganizationUsersAsync(List<OrganizationUser> organizationUsers, Organization organization)
        {
            await _eventService.LogOrganizationUserEventsAsync(organizationUsers.Select(u => (u, EventType.OrganizationUser_Invited, (DateTime?)DateTime.UtcNow)));
            await _referenceEventService.RaiseEventAsync(
                new ReferenceEvent(ReferenceEventType.InvitedUsers, organization)
                {
                    Users = organizationUsers.Count
                });
        }
    }
}
