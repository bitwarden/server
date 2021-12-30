using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Bit.Core.OrganizationFeatures.OrgUser.Mail;
using Bit.Core.Repositories;

namespace Bit.Core.OrganizationFeatures.OrgUser.Invitation.ResendInvite
{
    public class OrganizationUserResendInviteCommand : IOrganizationUserResendInviteCommand
    {
        private readonly IOrganizationUserResendInviteAccessPolicies _organizationUserInviteAccessPolicies;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IOrganizationUserInvitationService _organizationUserInviteService;
        private readonly IOrganizationUserMailer _organizationUserMailer;

        public OrganizationUserResendInviteCommand(
            IOrganizationUserResendInviteAccessPolicies organizationUserInviteAccessPolicies,
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IOrganizationUserInvitationService organizationUserInviteService,
            IOrganizationUserMailer organizationUserMailer
        )
        {
            _organizationUserInviteAccessPolicies = organizationUserInviteAccessPolicies;
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _organizationUserInviteService = organizationUserInviteService;
            _organizationUserMailer = organizationUserMailer;
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
    }
}
