﻿using AutoMapper;
using Bit.Core.Auth.Repositories;
using Bit.Infrastructure.EntityFramework.Auth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class SsoUserRepository
    : Repository<Core.Auth.Entities.SsoUser, SsoUser, long>,
        ISsoUserRepository
{
    public SsoUserRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.SsoUsers) { }

    public async Task DeleteAsync(Guid userId, Guid? organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            await dbContext
                .SsoUsers.Where(su => su.UserId == userId && su.OrganizationId == organizationId)
                .ExecuteDeleteAsync();
        }
    }

    public async Task<Core.Auth.Entities.SsoUser?> GetByUserIdOrganizationIdAsync(
        Guid organizationId,
        Guid userId
    )
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var entity = await GetDbSet(dbContext)
                .FirstOrDefaultAsync(e => e.OrganizationId == organizationId && e.UserId == userId);
            return entity;
        }
    }
}
