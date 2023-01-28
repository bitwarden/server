using Bit.Core.Entities.Provider;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories;

public interface IProviderRepository : IRepository<Provider, Guid>
{
    Task<ICollection<Provider>> SearchAsync(string name, string userEmail, int skip, int take);
    Task<ICollection<ProviderAbility>> GetManyAbilitiesAsync();
}
