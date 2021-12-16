using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Models.Table;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DataModel = Bit.Core.Models.Data;
using EfModel = Bit.Core.Models.EntityFramework;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Core.Repositories.EntityFramework
{
    public class TaxRateRepository : Repository<TableModel.TaxRate, EfModel.TaxRate, string>, ITaxRateRepository
    {
        public TaxRateRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.TaxRates)
        { }

        public async Task ArchiveAsync(TaxRate model)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var entity = await dbContext.FindAsync<TaxRate>(model);
                entity.Active = false;
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task<ICollection<TaxRate>> GetAllActiveAsync()
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

        public async Task<ICollection<TaxRate>> GetByLocationAsync(TaxRate taxRate)
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

        public async Task<ICollection<TaxRate>> SearchAsync(int skip, int count)
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
