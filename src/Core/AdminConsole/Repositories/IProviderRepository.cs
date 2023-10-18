using Bit.Core.Entities.Provider;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.Repositories;

public interface IProviderRepository : IRepository<Provider, Guid>
{
    Task<Provider> GetByOrganizationIdAsync(Guid organizationId);
    Task<ICollection<Provider>> SearchAsync(string name, string userEmail, int skip, int take);
    Task<ICollection<ProviderAbility>> GetManyAbilitiesAsync();
}
