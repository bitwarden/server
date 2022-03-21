using System.Threading.Tasks;
using Bit.Core.Entities;

namespace Bit.Core.Services
{
    public interface IOrganizationConnectionValidator
    {
        Task<OrganizationConnection> ValidateAsync(OrganizationConnection organizationConnection);
    }
}
