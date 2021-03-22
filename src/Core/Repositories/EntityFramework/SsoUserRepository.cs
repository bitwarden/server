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
    public class SsoUserRepository : Repository<TableModel.SsoUser, EfModel.SsoUser, long>, ISsoUserRepository
    {
        public SsoUserRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.SsoUsers)
        { }

        public Task DeleteAsync(Guid userId, Guid? organizationId)
        {
            throw new NotImplementedException();
        }
    }
}
