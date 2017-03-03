using System.Threading.Tasks;
using Bit.Core.Models.Business;
using Bit.Core.Domains;
using System;

namespace Bit.Core.Services
{
    public interface IOrganizationService
    {
        Task<Tuple<Organization, OrganizationUser>> SignUpAsync(OrganizationSignup organizationSignup);
    }
}
