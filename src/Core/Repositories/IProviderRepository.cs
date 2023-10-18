using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories;

public interface IProviderRepository : IRepository<Provider, Guid>
{
    Task<Provider> GetByOrganizationIdAsync(Guid organizationId);
    Task<ICollection<Provider>> SearchAsync(string name, string userEmail, int skip, int take);
    Task<ICollection<ProviderAbility>> GetManyAbilitiesAsync();
}
