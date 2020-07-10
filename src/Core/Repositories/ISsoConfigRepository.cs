using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Table;

namespace Bit.Core.Repositories
{
    public interface ISsoConfigRepository : IRepository<SsoConfig, long>
    {
        Task<SsoConfig> GetBySsoConfigIdAsync(long? id);
        Task<ICollection<SsoConfig>> GetManyByOrganizationIdAsync(Guid organizationId);
        Task<ICollection<SsoConfig>> GetManyByIdentifierAsync(string identifier);
    }
}
