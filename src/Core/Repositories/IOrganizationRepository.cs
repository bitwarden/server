using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;

namespace Bit.Core.Repositories
{
    public interface IOrganizationRepository : IRepository<Organization, Guid>
    {
        Task<ICollection<Organization>> GetManyByEnabledAsync();
        Task<ICollection<Organization>> GetManyByUserIdAsync(Guid userId);
        Task<ICollection<Organization>> SearchAsync(string name, string userEmail, bool? paid, int skip, int take);
        Task UpdateStorageAsync(Guid id);
        Task<ICollection<OrganizationAbility>> GetManyAbilitiesAsync();
    }
}
