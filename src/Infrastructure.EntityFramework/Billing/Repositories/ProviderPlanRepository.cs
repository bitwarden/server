using AutoMapper;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EFProviderPlan = Bit.Infrastructure.EntityFramework.Billing.Models.ProviderPlan;

namespace Bit.Infrastructure.EntityFramework.Billing.Repositories;

public class ProviderPlanRepository(IMapper mapper, IServiceScopeFactory serviceScopeFactory)
    : Repository<ProviderPlan, EFProviderPlan, Guid>(
        serviceScopeFactory,
        mapper,
        context => context.ProviderPlans
    ),
        IProviderPlanRepository
{
    public async Task<ICollection<ProviderPlan>> GetByProviderId(Guid providerId)
    {
        using var serviceScope = ServiceScopeFactory.CreateScope();

        var databaseContext = GetDatabaseContext(serviceScope);

        var query =
            from providerPlan in databaseContext.ProviderPlans
            where providerPlan.ProviderId == providerId
            select providerPlan;

        return await query.ToArrayAsync();
    }
}
