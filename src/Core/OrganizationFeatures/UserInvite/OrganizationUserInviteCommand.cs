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

namespace Bit.Core.OrganizationFeatures.UserInvite
{
    public class OrganizationUserInviteCommand : IOrganizationUserInviteCommand
    {
        private readonly IOrganizationUserInviteAccessPolicies _organizationUserInviteAccessPolicies;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IOrganizationService _organizationService;
        private readonly IOrganizationUserInviteService _organizationUserInviteService;
        private readonly IOrganizationUserMailer _organizationUserMailer;
        private readonly IOrganizationSubscriptionService _organizationSubscriptionService;
        private readonly IEventService _eventService;
        private readonly IReferenceEventService _referenceEventService;

        public OrganizationUserInviteCommand(
            IOrganizationUserInviteAccessPolicies organizationPermissions,
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IOrganizationService organizationService,
            IOrganizationUserInviteService organizationUserInviteService,
            IOrganizationUserMailer organizationUserMailer,
            IOrganizationSubscriptionService organizationSubscriptionService,
            IEventService eventService,
            IReferenceEventService referenceEventService
        )
        {
            _organizationUserInviteAccessPolicies = organizationPermissions;
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _organizationService = organizationService;
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
            // Validate inputs
            var organization = await _organizationRepository.GetByIdAsync(organizationId);
            var initialSeatCount = organization.Seats;
            if (organization == null || invites.Any(i => i.invite.Emails == null))
            {
                throw new NotFoundException();
            }

            // Validate permission to create invite
            var inviteTypes = new HashSet<OrganizationUserType>(invites.Where(i => i.invite.Type.HasValue).Select(i => i.invite.Type.Value));
            if (invitingUserId.HasValue && inviteTypes.Count > 0)
            {
                foreach (var type in inviteTypes)
                {
                    CoreHelpers.HandlePermissionResult(
                        await _organizationUserInviteAccessPolicies.UserCanEditUserType(organizationId, type)
                    );
                }
            }

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

            // validate org has owners
            var invitedAreAllOwners = invites.All(i => i.invite.Type == OrganizationUserType.Owner);
            if (!invitedAreAllOwners && !await _organizationService.HasConfirmedOwnersExceptAsync(organizationId, Array.Empty<Guid>()))
            {
                throw new BadRequestException("Organization must have at least one confirmed owner.");
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
                    result.Add((orgUser, "User Invalid."));
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
                await _organizationUserInviteAccessPolicies.CanAcceptInvite(org, user, orgUser, _organizationUserInviteService.TokenIsValid(token, user, orgUser))
            );

            orgUser.Status = OrganizationUserStatusType.Accepted;
            orgUser.UserId = user.Id;
            orgUser.Email = null;

            await _organizationUserRepository.ReplaceAsync(orgUser);

            await _organizationUserMailer.SendOrganizationAcceptedEmailAsync(org, user);
            return orgUser;
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
