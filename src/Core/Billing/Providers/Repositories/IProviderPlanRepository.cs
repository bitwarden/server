using Bit.Core.Billing.Providers.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Billing.Providers.Repositories;

public interface IProviderPlanRepository : IRepository<ProviderPlan, Guid>
{
    Task<ICollection<ProviderPlan>> GetByProviderId(Guid providerId);
}
