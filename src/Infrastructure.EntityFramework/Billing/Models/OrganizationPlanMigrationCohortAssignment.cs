using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

namespace Bit.Infrastructure.EntityFramework.Billing.Models;

public class OrganizationPlanMigrationCohortAssignment
    : Core.Billing.Organizations.PlanMigration.Entities.OrganizationPlanMigrationCohortAssignment
{
    public virtual Organization Organization { get; set; } = null!;
    public virtual OrganizationPlanMigrationCohort Cohort { get; set; } = null!;
}

public class OrganizationPlanMigrationCohortAssignmentMapperProfile : Profile
{
    public OrganizationPlanMigrationCohortAssignmentMapperProfile()
    {
        CreateMap<Core.Billing.Organizations.PlanMigration.Entities.OrganizationPlanMigrationCohortAssignment,
                OrganizationPlanMigrationCohortAssignment>()
            .ReverseMap();
    }
}
