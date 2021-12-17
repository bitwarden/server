using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrgUser
{
    public class OrganizationUserService : IOrganizationUserService
    {
        private readonly IOrganizationUserAccessPolicies _organizationUserAccessPolicies;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IDeviceRepository _deviceRepository;
        private readonly IEventService _eventService;
        private readonly IPushRegistrationService _pushRegistrationService;
        private readonly IPushNotificationService _pushNotificationService;

        public OrganizationUserService(
            IOrganizationUserAccessPolicies organizationUserAccessPolicies,
            IOrganizationUserRepository organizationUserRepository,
            IDeviceRepository deviceRepository,
            IEventService eventService,
            IPushRegistrationService pushRegistrationService,
            IPushNotificationService pushNotificationService
        )
        {
            _organizationUserAccessPolicies = organizationUserAccessPolicies;
            _organizationUserRepository = organizationUserRepository;
            _deviceRepository = deviceRepository;

            _eventService = eventService;
            _pushRegistrationService = pushRegistrationService;
            _pushNotificationService = pushNotificationService;
        }

        public async Task SaveUserAsync(OrganizationUser orgUser, Guid? savingUserId,
            IEnumerable<SelectionReadOnly> collections)
        {
            CoreHelpers.HandlePermissionResult(
                await _organizationUserAccessPolicies.CanSaveAsync(orgUser, savingUserId)
            );

            if (orgUser.AccessAll)
            {
                // We don't need any collections if we're flagged to have all access.
                collections = new List<SelectionReadOnly>();
            }
            await _organizationUserRepository.ReplaceAsync(orgUser, collections);
            await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Updated);
        }

        public async Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId)
        {
            var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
            CoreHelpers.HandlePermissionResult(
                await _organizationUserAccessPolicies.CanDeleteUserAsync(organizationId, orgUser, deletingUserId)
            );

            await _organizationUserRepository.DeleteAsync(orgUser);
            await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Removed);

            if (orgUser.UserId.HasValue)
            {
                await DeleteAndPushUserRegistrationAsync(organizationId, orgUser.UserId.Value);
            }
        }

        public async Task<List<(OrganizationUser orgUser, string error)>> DeleteUsersAsync(Guid organizationId,
            IEnumerable<Guid> organizationUsersId, Guid? deletingUserId)
        {
            var orgUsers = await _organizationUserRepository.GetManyAsync(organizationUsersId);
            var filteredUsers = orgUsers.Where(u => u.OrganizationId == organizationId)
                .ToList();

            CoreHelpers.HandlePermissionResult(
                await _organizationUserAccessPolicies.CanDeleteManyUsersAsync(organizationId, filteredUsers, deletingUserId)
            );

            var result = new List<(OrganizationUser orgUser, string error)>();
            var deletedUsers = new List<OrganizationUser>();
            foreach (var orgUser in filteredUsers)
            {
                var accessResult = await _organizationUserAccessPolicies
                    .CanDeleteUserAsync(organizationId, orgUser, deletingUserId);

                if (!accessResult.Permit)
                {
                    result.Add((orgUser, accessResult.BlockReason));
                    continue;
                }

                if (orgUser.UserId.HasValue)
                {
                    await DeleteAndPushUserRegistrationAsync(organizationId, orgUser.UserId.Value);
                }
                result.Add((orgUser, ""));
                deletedUsers.Add(orgUser);
            }

            await _eventService.LogOrganizationUserEventsAsync(
                deletedUsers.Select(u => (u, EventType.OrganizationUser_Removed, (DateTime?)null))
            );
            await _organizationUserRepository.DeleteManyAsync(deletedUsers.Select(u => u.Id));

            return result;
        }

        // TODO: Remove or rename one of these delete functions
        public async Task DeleteUserAsync(Guid organizationId, Guid userId)
        {
            var orgUser = await _organizationUserRepository.GetByOrganizationAsync(organizationId, userId);
            CoreHelpers.HandlePermissionResult(
                await _organizationUserAccessPolicies.CanSelfDeleteUserAsync(orgUser)
            );

            await _organizationUserRepository.DeleteAsync(orgUser);
            await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Removed);

            if (orgUser.UserId.HasValue)
            {
                await DeleteAndPushUserRegistrationAsync(organizationId, orgUser.UserId.Value);
            }
        }

        public async Task DeleteAndPushUserRegistrationAsync(Guid organizationId, Guid userId)
        {
            var deviceIds = await GetUserDeviceIdsAsync(userId);
            await _pushRegistrationService.DeleteUserRegistrationOrganizationAsync(deviceIds,
                organizationId.ToString());
            await _pushNotificationService.PushSyncOrgKeysAsync(userId);
        }

        private async Task<IEnumerable<string>> GetUserDeviceIdsAsync(Guid userId)
        {
            var devices = await _deviceRepository.GetManyByUserIdAsync(userId);
            return devices.Where(d => !string.IsNullOrWhiteSpace(d.PushToken)).Select(d => d.Id.ToString());
        }

        public async Task UpdateUserGroupsAsync(OrganizationUser organizationUser, IEnumerable<Guid> groupIds, Guid? loggedInUserId)
        {
            if (loggedInUserId.HasValue)
            {
                CoreHelpers.HandlePermissionResult(
                    await _organizationUserAccessPolicies.UserCanEditUserTypeAsync(organizationUser.OrganizationId, organizationUser.Type, null)
                );
            }

            await _organizationUserRepository.UpdateGroupsAsync(organizationUser.Id, groupIds);
            await _eventService.LogOrganizationUserEventAsync(organizationUser,
                EventType.OrganizationUser_UpdatedGroups);
        }
    }
}
