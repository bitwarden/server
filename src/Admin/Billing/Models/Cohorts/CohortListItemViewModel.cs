using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;

namespace Bit.Admin.Billing.Models.Cohorts;

public class CohortListItemViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public CohortType CohortType { get; set; } = new CohortType.ChurnOnly();
    public bool IsChurnOnly => CohortType is CohortType.ChurnOnly;
    public bool IsActive { get; set; }
    public int Pending { get; set; }
    public int Scheduled { get; set; }
    public int Migrated { get; set; }

    public static CohortListItemViewModel From(CohortListItem item) => new()
    {
        Id = item.Cohort.Id,
        Name = item.Cohort.Name,
        CohortType = CohortType.From(item.Cohort.MigrationPathId),
        IsActive = item.Cohort.IsActive,
        Pending = item.Pending,
        Scheduled = item.Scheduled,
        Migrated = item.Migrated,
    };
}
