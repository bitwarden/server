﻿using AutoMapper;
using Bit.Core.Auth.Repositories;
using Bit.Infrastructure.EntityFramework.Auth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class SsoConfigRepository
    : Repository<Core.Auth.Entities.SsoConfig, SsoConfig, long>,
        ISsoConfigRepository
{
    public SsoConfigRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.SsoConfigs) { }

    public async Task<Core.Auth.Entities.SsoConfig?> GetByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var ssoConfig = await GetDbSet(dbContext)
                .SingleOrDefaultAsync(sc => sc.OrganizationId == organizationId);
            return Mapper.Map<Core.Auth.Entities.SsoConfig>(ssoConfig);
        }
    }

    public async Task<Core.Auth.Entities.SsoConfig?> GetByIdentifierAsync(string identifier)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var ssoConfig = await GetDbSet(dbContext)
                .SingleOrDefaultAsync(sc => sc.Organization.Identifier == identifier);
            return Mapper.Map<Core.Auth.Entities.SsoConfig>(ssoConfig);
        }
    }

    public async Task<ICollection<Core.Auth.Entities.SsoConfig>> GetManyByRevisionNotBeforeDate(
        DateTime? notBefore
    )
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var ssoConfigs = await GetDbSet(dbContext)
                .Where(sc => sc.Enabled && sc.RevisionDate >= notBefore)
                .ToListAsync();
            return Mapper.Map<List<Core.Auth.Entities.SsoConfig>>(ssoConfigs);
        }
    }
}
