using Bit.Core.Billing.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Billing.Repositories;

public interface IProviderPlanRepository : IRepository<ProviderPlan, Guid>
{
    Task<ICollection<ProviderPlan>> GetByProviderId(Guid providerId);
}
