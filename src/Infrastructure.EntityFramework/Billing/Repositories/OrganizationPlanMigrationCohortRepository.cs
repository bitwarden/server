using AutoMapper;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.Extensions.DependencyInjection;
using CoreEntities = Bit.Core.Billing.Organizations.PlanMigration.Entities;
using EFOrganizationPlanMigrationCohort =
    Bit.Infrastructure.EntityFramework.Billing.Models.OrganizationPlanMigrationCohort;

namespace Bit.Infrastructure.EntityFramework.Billing.Repositories;

public class OrganizationPlanMigrationCohortRepository(
    IMapper mapper,
    IServiceScopeFactory serviceScopeFactory)
    : Repository<CoreEntities.OrganizationPlanMigrationCohort, EFOrganizationPlanMigrationCohort, Guid>(
        serviceScopeFactory,
        mapper,
        context => context.OrganizationPlanMigrationCohorts),
        IOrganizationPlanMigrationCohortRepository;
