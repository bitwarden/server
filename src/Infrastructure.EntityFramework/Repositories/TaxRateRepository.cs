using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Infrastructure.EntityFramework.Repositories
{
    public class TaxRateRepository : Repository<TableModel.TaxRate, TaxRate, string>, ITaxRateRepository
    {
        public TaxRateRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.TaxRates)
        { }

        public async Task ArchiveAsync(TableModel.TaxRate model)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var entity = await dbContext.FindAsync<TableModel.TaxRate>(model);
                entity.Active = false;
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task<ICollection<TableModel.TaxRate>> GetAllActiveAsync()
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var results = await dbContext.TaxRates
                    .Where(t => t.Active)
                    .ToListAsync();
                return Mapper.Map<List<TableModel.TaxRate>>(results);
            }
        }

        public async Task<ICollection<TableModel.TaxRate>> GetByLocationAsync(TableModel.TaxRate taxRate)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var results = await dbContext.TaxRates
                    .Where(t => t.Active &&
                        t.Country == taxRate.Country &&
                        t.PostalCode == taxRate.PostalCode)
                    .ToListAsync();
                return Mapper.Map<List<TableModel.TaxRate>>(results);
            }
        }

        public async Task<ICollection<TableModel.TaxRate>> SearchAsync(int skip, int count)
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
                return Mapper.Map<List<TableModel.TaxRate>>(results);
            }
        }
    }
}
