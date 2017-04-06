using System.Threading.Tasks;
using Bit.Core.Models.Business;
using Bit.Core.Models.Table;
using System;
using System.Collections.Generic;

namespace Bit.Core.Services
{
    public interface IOrganizationService
    {
        Task<OrganizationBilling> GetBillingAsync(Organization organization);
        Task<Tuple<Organization, OrganizationUser>> SignUpAsync(OrganizationSignup organizationSignup);
        Task<OrganizationUser> InviteUserAsync(Guid organizationId, Guid invitingUserId, string email,
            Enums.OrganizationUserType type, IEnumerable<SubvaultUser> subvaults);
        Task ResendInviteAsync(Guid organizationId, Guid invitingUserId, Guid organizationUserId);
        Task<OrganizationUser> AcceptUserAsync(Guid organizationUserId, User user, string token);
        Task<OrganizationUser> ConfirmUserAsync(Guid organizationId, Guid organizationUserId, string key, Guid confirmingUserId);
        Task SaveUserAsync(OrganizationUser user, Guid savingUserId, IEnumerable<SubvaultUser> subvaults);
        Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid deletingUserId);
    }
}
