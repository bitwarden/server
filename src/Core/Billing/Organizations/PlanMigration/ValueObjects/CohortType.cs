using Bit.Core.Billing.Organizations.PlanMigration.Enums;

namespace Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;

public abstract record CohortType
{
    public sealed record ChurnOnly : CohortType;
    public sealed record Migration(MigrationPath Path) : CohortType;
    public sealed record UnresolvedMigration(byte Id) : CohortType;

    public static CohortType From(MigrationPathId? id)
    {
        if (id is null)
        {
            return new ChurnOnly();
        }

        var path = MigrationPaths.FromId(id.Value);
        return path is null
            ? new UnresolvedMigration((byte)id.Value)
            : new Migration(path);
    }
}
