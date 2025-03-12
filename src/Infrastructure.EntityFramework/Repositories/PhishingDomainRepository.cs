using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class PhishingDomainRepository : IPhishingDomainRepository
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public PhishingDomainRepository(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task<ICollection<string>> GetActivePhishingDomainsAsync()
    {
        using (var scope = _serviceScopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var domains = await dbContext.PhishingDomains
                .Select(d => d.Domain)
                .ToListAsync();
            return domains;
        }
    }

    public async Task UpdatePhishingDomainsAsync(IEnumerable<string> domains)
    {
        using (var scope = _serviceScopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            // Clear existing domains
            await dbContext.PhishingDomains.ExecuteDeleteAsync();

            // Add new domains
            var phishingDomains = domains.Select(d => new PhishingDomain
            {
                Id = Guid.NewGuid(),
                Domain = d,
                CreationDate = DateTime.UtcNow,
                RevisionDate = DateTime.UtcNow
            });
            await dbContext.PhishingDomains.AddRangeAsync(phishingDomains);
            await dbContext.SaveChangesAsync();
        }
    }
}
