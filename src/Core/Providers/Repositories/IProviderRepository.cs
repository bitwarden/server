using Bit.Core.Providers.Entities;
using Bit.Core.Providers.Models.Data;
using Bit.Core.Repositories;

#nullable enable

namespace Bit.Core.Providers.Repositories;

public interface IProviderRepository : IRepository<Provider, Guid>
{
    Task<Provider?> GetByGatewayCustomerIdAsync(string gatewayCustomerId);
    Task<Provider?> GetByGatewaySubscriptionIdAsync(string gatewaySubscriptionId);
    Task<Provider?> GetByOrganizationIdAsync(Guid organizationId);
    Task<ICollection<Provider>> SearchAsync(string name, string userEmail, int skip, int take);
    Task<ProviderAbility?> GetAbilityAsync(Guid id);
}
