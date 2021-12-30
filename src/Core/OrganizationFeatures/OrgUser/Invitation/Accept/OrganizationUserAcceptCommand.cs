using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Bit.Core.OrganizationFeatures.OrgUser.Mail;
using Bit.Core.Repositories;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrgUser.Invitation.Accept
{
    public class OrganizationUserAcceptCommand : IOrganizationUserAcceptCommand
    {
        private readonly IOrganizationUserAcceptAccessPolicies _organizationUserAcceptAccessPolicies;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IOrganizationUserInvitationService _organizationUserInvitationService;
        private readonly IOrganizationUserMailer _organizationUserMailer;

        public OrganizationUserAcceptCommand(
            IOrganizationUserAcceptAccessPolicies organizationUserAcceptAccessPolicies,
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IOrganizationUserInvitationService organizationUserInviteService,
            IOrganizationUserMailer organizationUserMailer
        )
        {
            _organizationUserAcceptAccessPolicies = organizationUserAcceptAccessPolicies;
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _organizationUserInvitationService = organizationUserInviteService;
            _organizationUserMailer = organizationUserMailer;
        }

        public async Task<OrganizationUser> AcceptUserAsync(Guid organizationUserId, User user, string token)
        {
            var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
            var org = await _organizationRepository.GetByIdAsync(orgUser.OrganizationId);

            CoreHelpers.HandlePermissionResult(
                await _organizationUserAcceptAccessPolicies.CanAcceptInviteAsync(org, user, orgUser, _organizationUserInvitationService.TokenIsValid(token, user, orgUser))
            );

            orgUser = orgUser.AcceptUser(user.Id);

            await _organizationUserRepository.ReplaceAsync(orgUser);

            await _organizationUserMailer.SendOrganizationAcceptedEmailAsync(org, user);
            return orgUser;
        }
    }
}
