using System.Threading.Tasks;
using Bit.Core.Models.Business;
using Bit.Core.Domains;
using System;

namespace Bit.Core.Services
{
    public interface IOrganizationService
    {
        Task<Tuple<Organization, OrganizationUser>> SignUpAsync(OrganizationSignup organizationSignup);
        Task<OrganizationUser> InviteUserAsync(Guid organizationId, string email);
        Task<OrganizationUser> AcceptUserAsync(Guid organizationUserId, User user, string token);
        Task<OrganizationUser> ConfirmUserAsync(Guid organizationUserId, string key);
    }
}
