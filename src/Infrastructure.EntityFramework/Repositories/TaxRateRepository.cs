using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class TaxRateRepository : Repository<Core.Entities.TaxRate, TaxRate, string>, ITaxRateRepository
{
    public TaxRateRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.TaxRates)
    { }

    public async Task ArchiveAsync(Core.Entities.TaxRate model)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var entity = await dbContext.FindAsync<Core.Entities.TaxRate>(model);
            entity.Active = false;
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<ICollection<Core.Entities.TaxRate>> GetAllActiveAsync()
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.TaxRates
                .Where(t => t.Active)
                .ToListAsync();
            return Mapper.Map<List<Core.Entities.TaxRate>>(results);
        }
    }

    public async Task<ICollection<Core.Entities.TaxRate>> GetByLocationAsync(Core.Entities.TaxRate taxRate)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.TaxRates
                .Where(t => t.Active &&
                    t.Country == taxRate.Country &&
                    t.PostalCode == taxRate.PostalCode)
                .ToListAsync();
            return Mapper.Map<List<Core.Entities.TaxRate>>(results);
        }
    }

    public async Task<ICollection<Core.Entities.TaxRate>> SearchAsync(int skip, int count)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.TaxRates
                .Skip(skip)
                .Take(count)
                .Where(t => t.Active)
                .OrderBy(t => t.Country).ThenByDescending(t => t.PostalCode)
                .ToListAsync();
            return Mapper.Map<List<Core.Entities.TaxRate>>(results);
        }
    }
}
