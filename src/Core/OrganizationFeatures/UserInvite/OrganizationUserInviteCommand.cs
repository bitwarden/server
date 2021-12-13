using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Table;
using Bit.Core.AccessPolicies;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Services.OrganizationServices.UserInvite;
using Bit.Core.OrganizationFeatures.Mail;
using Bit.Core.OrganizationFeatures.Subscription;

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

        public async Task<List<OrganizationUser>> InviteUsersAsync(Guid organizationId, Guid? invitingUserId,
            IEnumerable<(OrganizationUserInvite invite, string externalId)> invites)
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
                    HandlePermissionResultBadRequest(
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
                var userCount = await _organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(organizationId);
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
            await _organizationUserMailer.SendInvitesAsync(invitedUsers, organization);
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

        private static void HandlePermissionResultBadRequest(AccessPolicyResult result)
        {
            if (!result.Permit)
            {
                throw new BadRequestException(result.BlockReason);
            }
        }
    }
}
