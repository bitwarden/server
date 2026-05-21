using Bit.Core.Billing.Organizations.PlanMigration.Models;

namespace Bit.Admin.Billing.Models.Cohorts;

public class CohortListItemViewModel
{
    public string Name { get; set; } = string.Empty;

    public static CohortListItemViewModel From(CohortListItem item) => new()
    {
        Name = item.Cohort.Name,
    };
}
