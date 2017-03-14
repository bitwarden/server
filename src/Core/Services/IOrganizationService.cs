using System.Threading.Tasks;
using Bit.Core.Models.Business;
using Bit.Core.Models.Table;
using System;
using System.Collections.Generic;

namespace Bit.Core.Services
{
    public interface IOrganizationService
    {
        Task<Tuple<Organization, OrganizationUser>> SignUpAsync(OrganizationSignup organizationSignup);
        Task<OrganizationUser> InviteUserAsync(Guid organizationId, string email, Enums.OrganizationUserType type,
            IEnumerable<SubvaultUser> subvaults);
        Task<OrganizationUser> AcceptUserAsync(Guid organizationUserId, User user, string token);
        Task<OrganizationUser> ConfirmUserAsync(Guid organizationUserId, string key);
        Task SaveUserAsync(OrganizationUser user, IEnumerable<SubvaultUser> subvaults);
    }
}
