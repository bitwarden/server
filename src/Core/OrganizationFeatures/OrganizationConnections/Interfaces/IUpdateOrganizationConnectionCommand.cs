using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations.OrganizationConnections;

namespace Bit.Core.OrganizationFeatures.OrganizationConnections.Interfaces
{
    public interface IUpdateOrganizationConnectionCommand
    {
        Task<OrganizationConnection> UpdateAsync(OrganizationConnectionData connectionData);
    }
}
