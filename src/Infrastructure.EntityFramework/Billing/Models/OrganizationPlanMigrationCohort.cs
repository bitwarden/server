using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Billing.Models;

public class OrganizationPlanMigrationCohort
    : Core.Billing.Organizations.PlanMigration.Entities.OrganizationPlanMigrationCohort
{
}

public class OrganizationPlanMigrationCohortMapperProfile : Profile
{
    public OrganizationPlanMigrationCohortMapperProfile()
    {
        CreateMap<Core.Billing.Organizations.PlanMigration.Entities.OrganizationPlanMigrationCohort,
                OrganizationPlanMigrationCohort>()
            .ReverseMap();
    }
}
