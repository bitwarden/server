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

        public async Task DeleteByKeyAsync(string key)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = from g in dbContext.Grants
                            where g.Key == key
                            select g;
                dbContext.Remove(query);
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task DeleteManyAsync(string subjectId, string sessionId, string clientId, string type)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = from g in dbContext.Grants
                            where g.SubjectId == subjectId &&
                                g.ClientId == clientId && 
                                g.SessionId == sessionId &&
                                g.Type == type
                            select g;
                dbContext.Remove(query);
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task<Grant> GetByKeyAsync(string key)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = from g in dbContext.Grants
                            where g.Key == key
                            select g;
                var grant = await query.FirstOrDefaultAsync();
                return grant;
            }
        }

        public async Task<ICollection<Grant>> GetManyAsync(string subjectId, string sessionId, string clientId, string type)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = from g in dbContext.Grants
                            where g.SubjectId == subjectId &&
                                g.ClientId == clientId && 
                                g.SessionId == sessionId &&
                                g.Type == type
                            select g;
                var grants = await query.ToListAsync();
                return (ICollection<Grant>)grants;
            }
        }

        public Task SaveAsync(Grant obj)
        {
            // TODO
            throw new NotImplementedException();
        }
    }
}

