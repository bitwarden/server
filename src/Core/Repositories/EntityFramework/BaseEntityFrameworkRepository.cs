using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Repositories.EntityFramework.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Repositories.EntityFramework
{
    public abstract class BaseEntityFrameworkRepository
    {
        public BaseEntityFrameworkRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        {
            ServiceScopeFactory = serviceScopeFactory;
            Mapper = mapper;
        }

        protected IServiceScopeFactory ServiceScopeFactory { get; private set; }
        protected IMapper Mapper { get; private set; }

        public DatabaseContext GetDatabaseContext(IServiceScope serviceScope)
        {
            return serviceScope.ServiceProvider.GetRequiredService<DatabaseContext>();
        }

        public void ClearChangeTracking()
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                dbContext.ChangeTracker.Clear();
            }
        }

        public async Task<int> GetCountFromQuery<T>(IQuery<T> query)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                return await query.Run(GetDatabaseContext(scope)).CountAsync();
            }
        }
    }
}
