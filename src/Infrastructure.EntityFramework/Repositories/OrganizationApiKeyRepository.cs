using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories
{
    public class OrganizationApiKeyRepository : IOrganizationApiKeyRepository
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IMapper _mapper;

        public OrganizationApiKeyRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _mapper = mapper;
        }

        public async Task CreateAsync(OrganizationApiKey organizationApiKey)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                dbContext.OrganizationApiKeys.Include(a => a.Organization);
                dbContext.OrganizationApiKeys.Add(_mapper.Map<Models.OrganizationApiKey>(organizationApiKey));
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task<ICollection<OrganizationApiKey>> GetByOrganizationIdAsync(Guid organizationId)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var apiKeys = await dbContext.OrganizationApiKeys
                    .Where(o => o.OrganizationId == organizationId)
                    .ToListAsync();
                return _mapper.Map<List<OrganizationApiKey>>(apiKeys);
            }
        }

        public async Task<OrganizationApiKey> GetByOrganizationIdTypeAsync(Guid organizationId, OrganizationApiKeyType type)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var apiKey = await dbContext.OrganizationApiKeys
                    .FirstOrDefaultAsync(o => o.OrganizationId == organizationId && o.Type == type);
                return _mapper.Map<OrganizationApiKey>(apiKey);
            }
        }

        public async Task<bool> GetCanUseByApiKeyAsync(Guid organizationId, string apiKey, OrganizationApiKeyType type)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                return await dbContext.OrganizationApiKeys
                    .AnyAsync(o => o.OrganizationId == organizationId && o.ApiKey == apiKey && o.Type == type);
            }
        }

        public async Task UpdateAsync(OrganizationApiKey organizationApiKey)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var orgApiKey = await dbContext.OrganizationApiKeys
                    .FirstOrDefaultAsync(o => o.OrganizationId == organizationApiKey.OrganizationId
                        && o.Type == organizationApiKey.Type);

                if (orgApiKey != null)
                {
                    orgApiKey.ApiKey = organizationApiKey.ApiKey;
                    orgApiKey.RevisionDate = organizationApiKey.RevisionDate;
                    await dbContext.SaveChangesAsync();
                }
            }
        }

        private DatabaseContext GetDatabaseContext(IServiceScope scope)
        {
            return scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        }
    }
}
