using System.Threading.Tasks;
using Bit.Core.Entities;

namespace Bit.Core.Services
{
    public interface IOrganizationConnectionService
    {
        Task SaveAsync(OrganizationConnection organizationConnection);
    }
}
