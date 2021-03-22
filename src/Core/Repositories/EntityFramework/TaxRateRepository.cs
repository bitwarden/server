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

        public Task ArchiveAsync(TaxRate model)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<TaxRate>> GetAllActiveAsync()
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<TaxRate>> GetByLocationAsync(TaxRate taxRate)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<TaxRate>> SearchAsync(int skip, int count)
        {
            throw new NotImplementedException();
        }
    }
}
