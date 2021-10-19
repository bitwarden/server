using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table.Provider;

namespace Bit.Core.Repositories
{
    public interface IProviderRepository : IRepository<Provider, Guid>
    {
        Task<ICollection<Provider>> SearchAsync(string name, string userEmail, int skip, int take);
        Task<ICollection<ProviderAbility>> GetManyAbilitiesAsync();
    }
}
