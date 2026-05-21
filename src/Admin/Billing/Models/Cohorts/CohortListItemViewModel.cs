using Bit.Core.Billing.Organizations.PlanMigration.Models;

namespace Bit.Admin.Billing.Models.Cohorts;

public class CohortListItemViewModel
{
    public string Name { get; set; } = string.Empty;
    public int Pending { get; set; }
    public int Scheduled { get; set; }
    public int Migrated { get; set; }

    public static CohortListItemViewModel From(CohortListItem item) => new()
    {
        Name = item.Cohort.Name,
        Pending = item.Pending,
        Scheduled = item.Scheduled,
        Migrated = item.Migrated,
    };
}
