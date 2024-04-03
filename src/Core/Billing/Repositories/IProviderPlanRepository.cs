using Bit.Core.Billing.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Billing.Repositories;

public interface IProviderPlanRepository : IRepository<ProviderPlan, Guid>
{
    Task<ProviderPlan> GetByProviderId(Guid providerId);
}
