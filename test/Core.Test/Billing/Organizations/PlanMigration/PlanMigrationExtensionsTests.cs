using Bit.Core.Billing.Organizations.PlanMigration;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Test.Billing.Mocks.Plans;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.PlanMigration;

public class PlanMigrationExtensionsTests
{
    [Fact]
    public void IsPackagedMigrationSource_FlatBundle_TrueRegardlessOfPolicy()
    {
        var plan = new TeamsStarterPlan();

        Assert.True(plan.IsPackagedMigrationSource(SeatCountPolicy.Preserve));
        Assert.True(plan.IsPackagedMigrationSource(SeatCountPolicy.ActualUsage));
    }

    [Fact]
    public void IsPackagedMigrationSource_SeatOverage_FollowsPolicy()
    {
        var plan = new Teams2019Plan(isAnnual: true);

        Assert.True(plan.IsPackagedMigrationSource(SeatCountPolicy.ActualUsage));
        Assert.False(plan.IsPackagedMigrationSource(SeatCountPolicy.Preserve));
    }

    [Fact]
    public void IsPackagedMigrationSource_Scalable_FollowsPolicy()
    {
        // Scalable + ActualUsage is a misconfiguration; the gate reports true here, and
        // ResolveMigratedSeatCount throws downstream. Scalable + Preserve is the correct pairing.
        var plan = new TeamsPlan(isAnnual: true);

        Assert.False(plan.IsPackagedMigrationSource(SeatCountPolicy.Preserve));
        Assert.True(plan.IsPackagedMigrationSource(SeatCountPolicy.ActualUsage));
    }

    [Theory]
    [InlineData(0, 10, 1)]   // floored at 1
    [InlineData(7, 10, 7)]   // occupied below the bundle, purchased ignored
    [InlineData(10, 10, 10)] // full bundle
    public void ResolveMigratedSeatCount_FlatBundle_BillsOccupiedFlooredAtOne(
        int occupiedSeats, int? purchasedSeats, int expected)
    {
        var plan = new TeamsStarterPlan();

        var result = plan.ResolveMigratedSeatCount(occupiedSeats, purchasedSeats);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(3, 20, 3)]   // below base -> occupied
    [InlineData(5, 20, 20)]  // at base -> purchased
    [InlineData(8, 20, 20)]  // above base -> purchased
    [InlineData(8, null, 8)] // above base, no purchased -> occupied
    public void ResolveMigratedSeatCount_SeatOverage_BillsOccupiedBelowBaseOtherwisePurchased(
        int occupiedSeats, int? purchasedSeats, int expected)
    {
        var plan = new Teams2019Plan(isAnnual: true);

        var result = plan.ResolveMigratedSeatCount(occupiedSeats, purchasedSeats);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveMigratedSeatCount_ScalableSource_Throws()
    {
        // Scalable: a per-seat line with no packaged base (BaseSeats == 0). It must preserve its
        // line-item quantity, so resolving from usage is a misuse.
        var plan = new TeamsPlan(isAnnual: true);

        Assert.Throws<ArgumentException>(() => plan.ResolveMigratedSeatCount(occupiedSeats: 5, purchasedSeats: 5));
    }

    [Fact]
    public void ResolveMigratedSeatCount_NegativeOccupiedSeats_Throws()
    {
        var plan = new Teams2019Plan(isAnnual: true);

        Assert.Throws<ArgumentOutOfRangeException>(() => plan.ResolveMigratedSeatCount(occupiedSeats: -1, purchasedSeats: 20));
    }
}
