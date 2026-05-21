using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.PlanMigration.ValueObjects;

public class CohortTypeTests
{
    [Fact]
    public void From_NullId_ReturnsChurnOnly()
    {
        var result = CohortType.From(null);

        Assert.IsType<CohortType.ChurnOnly>(result);
    }

    [Fact]
    public void From_RegisteredId_ReturnsMigrationWithMatchingPath()
    {
        var result = CohortType.From(MigrationPathId.Enterprise2020AnnualToCurrent);

        var migration = Assert.IsType<CohortType.Migration>(result);
        Assert.Equal(MigrationPaths.Enterprise2020AnnualToCurrent, migration.Path);
    }

    [Fact]
    public void From_UnknownId_ReturnsUnresolvedMigrationCarryingByte()
    {
        const byte unknownId = 99;

        var result = CohortType.From((MigrationPathId)unknownId);

        var unresolved = Assert.IsType<CohortType.UnresolvedMigration>(result);
        Assert.Equal(unknownId, unresolved.Id);
    }
}
