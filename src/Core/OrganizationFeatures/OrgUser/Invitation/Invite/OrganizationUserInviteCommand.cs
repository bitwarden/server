using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.OrganizationFeatures.OrgUser.Mail;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrgUser.Invitation.Invite
{
    public class OrganizationUserInviteCommand : IOrganizationUserInviteCommand
    {
        private readonly IOrganizationUserInviteAccessPolicies _organizationUserInviteAccessPolicies;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IOrganizationUserInvitationService _organizationUserInviteService;
        private readonly IOrganizationUserMailer _organizationUserMailer;
        private readonly IOrganizationService _organizationService;
        private readonly IEventService _eventService;
        private readonly IReferenceEventService _referenceEventService;

        public OrganizationUserInviteCommand(
            IOrganizationUserInviteAccessPolicies organizationUserInviteAccessPolicies,
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IOrganizationUserInvitationService organizationUserInviteService,
            IOrganizationUserMailer organizationUserMailer,
            IOrganizationService organizationService,
            IEventService eventService,
            IReferenceEventService referenceEventService
        )
        {
            _organizationUserInviteAccessPolicies = organizationUserInviteAccessPolicies;
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _organizationUserInviteService = organizationUserInviteService;
            _organizationUserMailer = organizationUserMailer;
            _organizationService = organizationService;
            _eventService = eventService;
            _referenceEventService = referenceEventService;
        }

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
                await _organizationService.AutoAddSeatsAsync(organization, newSeatsRequired);
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
