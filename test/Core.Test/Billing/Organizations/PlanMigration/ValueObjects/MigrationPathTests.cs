using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.PlanMigration.ValueObjects;

public class MigrationPathTests
{
    [Fact]
    public void FromId_KnownId_ReturnsTheMatchingPath()
    {
        var result = MigrationPaths.FromId(MigrationPaths.Enterprise2020AnnualToCurrent.Id);

        Assert.Same(MigrationPaths.Enterprise2020AnnualToCurrent, result);
    }

    [Fact]
    public void FromId_TeamsStarterIds_ReturnTheMatchingPaths()
    {
        Assert.Same(MigrationPaths.TeamsStarterToCurrent,
            MigrationPaths.FromId(MigrationPaths.TeamsStarterToCurrent.Id));
        Assert.Same(MigrationPaths.TeamsStarter2023ToCurrent,
            MigrationPaths.FromId(MigrationPaths.TeamsStarter2023ToCurrent.Id));
    }

    [Fact]
    public void FromId_UnknownId_ReturnsNull()
    {
        // 0 is reserved as "not assigned" and 255 is not registered. Both should
        // resolve to null so callers can decide how to handle data referencing a
        // path that has not yet been registered.
        Assert.Null(MigrationPaths.FromId((MigrationPathId)0));
        Assert.Null(MigrationPaths.FromId((MigrationPathId)byte.MaxValue));
    }
}
