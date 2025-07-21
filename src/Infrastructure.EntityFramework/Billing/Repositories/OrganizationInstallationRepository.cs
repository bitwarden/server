﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;
using Bit.Core.Billing.Organizations.Entities;
using Bit.Core.Billing.Organizations.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EFOrganizationInstallation = Bit.Infrastructure.EntityFramework.Billing.Models.OrganizationInstallation;

namespace Bit.Infrastructure.EntityFramework.Billing.Repositories;

public class OrganizationInstallationRepository(
    IMapper mapper,
    IServiceScopeFactory serviceScopeFactory) : Repository<OrganizationInstallation, EFOrganizationInstallation, Guid>(
        serviceScopeFactory,
        mapper,
        context => context.OrganizationInstallations), IOrganizationInstallationRepository
{
    public async Task<OrganizationInstallation> GetByInstallationIdAsync(Guid installationId)
    {
        using var serviceScope = ServiceScopeFactory.CreateScope();

        var databaseContext = GetDatabaseContext(serviceScope);

        var query =
            from organizationInstallation in databaseContext.OrganizationInstallations
            where organizationInstallation.Id == installationId
            select organizationInstallation;

        return await query.FirstOrDefaultAsync();
    }

    public async Task<ICollection<OrganizationInstallation>> GetByOrganizationIdAsync(Guid organizationId)
    {
        using var serviceScope = ServiceScopeFactory.CreateScope();

        var databaseContext = GetDatabaseContext(serviceScope);

        var query =
            from organizationInstallation in databaseContext.OrganizationInstallations
            where organizationInstallation.OrganizationId == organizationId
            select organizationInstallation;

        return await query.ToArrayAsync();
    }
}
