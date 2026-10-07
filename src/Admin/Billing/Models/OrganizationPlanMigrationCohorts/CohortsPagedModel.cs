using Bit.Admin.Models;

namespace Bit.Admin.Billing.Models.OrganizationPlanMigrationCohorts;

public class CohortsPagedModel : PagedModel<CohortListItemViewModel>
{
    public string? NameSearch { get; set; }
}
