using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Repositories
{
    public interface IOrganizationConnectionRepository : IRepository<OrganizationConnection, Guid>
    {
        Task<ICollection<OrganizationConnection>> GetEnabledByOrganizationIdTypeAsync(Guid organizationId, OrganizationConnectionType type);
    }
}
