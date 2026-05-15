using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;

namespace Bit.Infrastructure.Dapper.Billing.Repositories;

public class OrganizationPlanMigrationCohortRepository(
    GlobalSettings globalSettings)
    : Repository<OrganizationPlanMigrationCohort, Guid>(
        globalSettings.SqlServer.ConnectionString,
        globalSettings.SqlServer.ReadOnlyConnectionString),
        IOrganizationPlanMigrationCohortRepository;
