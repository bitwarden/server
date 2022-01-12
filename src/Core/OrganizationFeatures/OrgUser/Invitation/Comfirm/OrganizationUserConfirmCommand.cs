using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Table;
using Bit.Core.OrganizationFeatures.OrgUser.Mail;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.OrgUser.Invitation.Confirm
{
    public class OrganizationUserConfirmCommand : IOrganizationUserConfirmCommand
    {
        private readonly IOrganizationUserConfirmAccessPolicies _organizationUserConfirmAccessPolicies;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IUserRepository _userRepository;
        private readonly IOrganizationService _organizationService;
        private readonly IOrganizationUserMailer _organizationUserMailer;
        private readonly IEventService _eventService;

        public OrganizationUserConfirmCommand(
            IOrganizationUserConfirmAccessPolicies organizationUserInviteAccessPolicies,
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IUserRepository userRepository,
            IOrganizationService organizationService,
            IOrganizationUserMailer organizationUserMailer,
            IEventService eventService
        )
        {
            _organizationUserConfirmAccessPolicies = organizationUserInviteAccessPolicies;
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _userRepository = userRepository;
            _organizationService = organizationService;
            _organizationUserMailer = organizationUserMailer;
            _eventService = eventService;
        }

        public async Task<List<(OrganizationUser orgUser, string error)>> ConfirmUsersAsync(Guid organizationId, Dictionary<Guid, string> orgUserKeys)
        {
            var organizationUsers = await _organizationUserRepository.GetManyAsync(orgUserKeys.Keys);
            var keyedUserOrgUser = organizationUsers
                .Where(u => u.Status == OrganizationUserStatusType.Accepted && u.OrganizationId == organizationId && u.UserId != null)
                .ToDictionary(u => u.UserId.Value, u => u);

            if (!keyedUserOrgUser.Any())
            {
                return new();
            }

            var organization = await _organizationRepository.GetByIdAsync(organizationId);
            var keyedUserAllOrgUsers = (await _organizationUserRepository.GetManyByManyUsersAsync(keyedUserOrgUser.Keys))
                .GroupBy(u => u.UserId.Value).ToDictionary(u => u.Key, u => u.ToList());
            var users = await _userRepository.GetManyAsync(keyedUserOrgUser.Keys);

            var succeededUsers = new List<OrganizationUser>();
            var result = new List<(OrganizationUser orguser, string failureReason)>();

            foreach (var user in users)
            {
                if (!keyedUserOrgUser.ContainsKey(user.Id))
                {
                    continue;
                }
                var orgUser = keyedUserOrgUser[user.Id];
                var allOrgUsers = keyedUserAllOrgUsers.GetValueOrDefault(user.Id, new List<OrganizationUser>());

                var accessPolicy = await _organizationUserConfirmAccessPolicies.CanConfirmUserAsync(organization, user, orgUser, allOrgUsers);

                if (!accessPolicy.Permit)
                {
                    result.Add((orgUser,
                        string.IsNullOrWhiteSpace(accessPolicy.BlockReason)
                            ? "User Invalid."
                            : accessPolicy.BlockReason));
                    continue;
                }

                orgUser = orgUser.ConfirmUser(orgUserKeys[orgUser.Id]);

                await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Confirmed);
                await _organizationUserMailer.SendOrganizationConfirmedEmail(organization, user);
                await _organizationService.DeleteAndPushUserRegistrationAsync(organizationId, user.Id);
                succeededUsers.Add(orgUser);
                result.Add((orgUser, ""));
            }

            await _organizationUserRepository.ReplaceManyAsync(succeededUsers);

            return result;
        }
    }
}
