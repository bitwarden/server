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
    public class GrantRepository : BaseEntityFrameworkRepository, IGrantRepository
    {
        public GrantRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper)
        { }

        public Task DeleteByKeyAsync(string key)
        {
            throw new NotImplementedException();
        }

        public Task DeleteManyAsync(string subjectId, string sessionId, string clientId, string type)
        {
            throw new NotImplementedException();
        }

        public Task<Grant> GetByKeyAsync(string key)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<Grant>> GetManyAsync(string subjectId, string sessionId, string clientId, string type)
        {
            throw new NotImplementedException();
        }

        public Task SaveAsync(Grant obj)
        {
            throw new NotImplementedException();
        }
    }
}

