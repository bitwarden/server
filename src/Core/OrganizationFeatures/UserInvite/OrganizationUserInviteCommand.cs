using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Services.OrganizationServices.UserInvite;
using Bit.Core.OrganizationFeatures.Mail;
using Bit.Core.OrganizationFeatures.Subscription;
using Bit.Core.Utilities;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrgUser;

namespace Bit.Core.OrganizationFeatures.UserInvite
{
    public class OrganizationUserInviteCommand : IOrganizationUserInviteCommand
    {
        private readonly IOrganizationUserInviteAccessPolicies _organizationUserInviteAccessPolicies;
        private readonly IOrganizationUserAccessPolicies _organizationUserAccessPolicies;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IUserRepository _userRepository;
        private readonly IOrganizationService _organizationService;
        private readonly IOrganizationUserService _organizationUserService;
        private readonly IOrganizationUserInviteService _organizationUserInviteService;
        private readonly IOrganizationUserMailer _organizationUserMailer;
        private readonly IOrganizationSubscriptionService _organizationSubscriptionService;
        private readonly IEventService _eventService;
        private readonly IReferenceEventService _referenceEventService;

        public OrganizationUserInviteCommand(
            IOrganizationUserInviteAccessPolicies organizationUserInviteAccessPolicies,
            IOrganizationUserAccessPolicies organizationUserAccessPolicies,
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IUserRepository userRepository,
            IOrganizationService organizationService,
            IOrganizationUserService organizationUserService,
            IOrganizationUserInviteService organizationUserInviteService,
            IOrganizationUserMailer organizationUserMailer,
            IOrganizationSubscriptionService organizationSubscriptionService,
            IEventService eventService,
            IReferenceEventService referenceEventService
        )
        {
            _organizationUserInviteAccessPolicies = organizationUserInviteAccessPolicies;
            _organizationUserAccessPolicies = organizationUserAccessPolicies;
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _userRepository = userRepository;
            _organizationService = organizationService;
            _organizationUserService = organizationUserService;
            _organizationUserInviteService = organizationUserInviteService;
            _organizationUserMailer = organizationUserMailer;
            _organizationSubscriptionService = organizationSubscriptionService;
            _eventService = eventService;
            _referenceEventService = referenceEventService;
        }

        public async Task<OrganizationUser> InviteUserAsync(Guid organizationId, Guid? invitingUserId, OrganizationUserInviteData invite, string externalId) =>
            (await InviteUsersAsync(organizationId, invitingUserId, new[] { (invite, externalId) })).FirstOrDefault();

        public async Task<List<OrganizationUser>> InviteUsersAsync(Guid organizationId, Guid? invitingUserId,
            IEnumerable<(OrganizationUserInviteData invite, string externalId)> invites)
        {
            var organization = await _organizationRepository.GetByIdAsync(organizationId);
            var initialSeatCount = organization.Seats;
            CoreHelpers.HandlePermissionResult(
                await _organizationUserInviteAccessPolicies
                .CanInviteAsync(organization, invites.Select(i => i.invite), invitingUserId)
            );

            // Validate organization subscription size
            var newSeatsRequired = 0;
            var existingEmails = new HashSet<string>(await _organizationUserRepository.SelectKnownEmailsAsync(
                organizationId, invites.SelectMany(i => i.invite.Emails), false), StringComparer.InvariantCultureIgnoreCase);
            if (organization.Seats.HasValue)
            {
                var userCount = await _organizationUserRepository.GetCountByOrganizationIdAsync(organizationId);
                var availableSeats = organization.Seats.Value - userCount;
                newSeatsRequired = invites.Sum(i => i.invite.Emails.Count()) - existingEmails.Count - availableSeats;
            }

            var invitedUsers = await _organizationUserInviteService.InviteUsersAsync(organization, invites, existingEmails);
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

        public async Task ResendInviteAsync(Guid organizationId, Guid organizationUserId)
        {
            var (_, FailureReason) = (await ResendInvitesAsync(organizationId, new[] { organizationUserId })).First();
            if (!string.IsNullOrEmpty(FailureReason))
            {
                throw new BadRequestException(FailureReason);
            }
        }

        public async Task<IEnumerable<(OrganizationUser orgUser, string failureReason)>> ResendInvitesAsync(Guid organizationId,
            IEnumerable<Guid> organizationUsersId)
        {
            var orgUsers = await _organizationUserRepository.GetManyAsync(organizationUsersId);
            var org = await _organizationRepository.GetByIdAsync(organizationId);

            var result = new List<(OrganizationUser orgUser, string failureReason)>();
            foreach (var orgUser in orgUsers)
            {
                var accessPolicy = _organizationUserInviteAccessPolicies.CanResendInvite(orgUser, org);
                if (!accessPolicy.Permit)
                {
                    result.Add((orgUser, accessPolicy.BlockReason ?? "User Invalid."));
                    continue;
                }

                await _organizationUserMailer.SendInvitesAsync(new[] { (orgUser, _organizationUserInviteService.MakeToken(orgUser)) }, org);
                result.Add((orgUser, ""));
            }

            return result;
        }

        public async Task<OrganizationUser> AcceptUserAsync(Guid organizationUserId, User user, string token)
        {
            var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
            var org = await _organizationRepository.GetByIdAsync(orgUser.OrganizationId);

            CoreHelpers.HandlePermissionResult(
                await _organizationUserInviteAccessPolicies.CanAcceptInviteAsync(org, user, orgUser, _organizationUserInviteService.TokenIsValid(token, user, orgUser))
            );

            orgUser.Status = OrganizationUserStatusType.Accepted;
            orgUser.UserId = user.Id;
            orgUser.Email = null;

            await _organizationUserRepository.ReplaceAsync(orgUser);

            await _organizationUserMailer.SendOrganizationAcceptedEmailAsync(org, user);
            return orgUser;
        }

        public async Task<OrganizationUser> ConfirmUserAsync(Guid organizationId, Guid organizationUserId, string key)
        {
            var result = await ConfirmUsersAsync(organizationId, new Dictionary<Guid, string> { { organizationUserId, key } });

            if (!result.Any())
            {
                throw new BadRequestException("User not valid.");
            }

            var (orgUser, error) = result[0];
            if (!string.IsNullOrWhiteSpace(error))
            {
                throw new BadRequestException(error);
            }
            return orgUser;
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

                var accessPolicy = await _organizationUserInviteAccessPolicies.CanConfirmUserAsync(organization, user, orgUser, allOrgUsers);

                if (!accessPolicy.Permit)
                {
                    result.Add((orgUser,
                        string.IsNullOrWhiteSpace(accessPolicy.BlockReason)
                            ? "User Invalid."
                            : accessPolicy.BlockReason));
                    continue;
                }


                orgUser.Status = OrganizationUserStatusType.Confirmed;
                orgUser.Key = orgUserKeys[orgUser.Id];
                orgUser.Email = null;

                await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Confirmed);
                await _organizationUserMailer.SendOrganizationConfirmedEmail(organization, user);
                await _organizationUserService.DeleteAndPushUserRegistrationAsync(organizationId, user.Id);
                succeededUsers.Add(orgUser);
                result.Add((orgUser, ""));
            }

            await _organizationUserRepository.ReplaceManyAsync(succeededUsers);

            return result;
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
