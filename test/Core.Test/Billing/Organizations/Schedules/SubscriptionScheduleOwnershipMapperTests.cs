using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.Schedules;
using Bit.Core.Billing.Organizations.Schedules.Enums;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.Schedules;

using static StripeConstants;

public class SubscriptionScheduleOwnershipMapperTests
{
    private static SubscriptionSchedule Schedule(
        string status = SubscriptionScheduleStatus.Active,
        Dictionary<string, string>? phaseMetadata = null,
        params string[] priceIds) => new()
        {
            Id = "sub_sched_1",
            Status = status,
            Phases =
            [
                new SubscriptionSchedulePhase
                {
                    Metadata = phaseMetadata,
                    Items = [.. priceIds.Select(id => new SubscriptionSchedulePhaseItem { PriceId = id })]
                }
            ]
        };

    private static Subscription WithSchedule(SubscriptionSchedule? schedule) => new()
    {
        Id = "sub_1",
        ScheduleId = schedule?.Id,
        Schedule = schedule
    };

    [Fact]
    public void Map_NoAttachedSchedule_ReturnsNone() =>
        Assert.Equal(
            OrganizationSubscriptionScheduleOwnership.None,
            SubscriptionScheduleOwnershipMapper.Map(new Subscription { Id = "sub_1" }));

    [Fact]
    public void Map_ScheduleIdSetButNotExpanded_ReturnsUnexpanded() =>
        Assert.Equal(
            OrganizationSubscriptionScheduleOwnership.Unexpanded,
            SubscriptionScheduleOwnershipMapper.Map(
                new Subscription { Id = "sub_1", ScheduleId = "sub_sched_1", Schedule = null }));

    [Theory]
    [InlineData(SubscriptionScheduleStatus.NotStarted)]
    [InlineData(SubscriptionScheduleStatus.Released)]
    [InlineData(SubscriptionScheduleStatus.Canceled)]
    [InlineData(SubscriptionScheduleStatus.Completed)]
    public void Map_ScheduleNotActive_ReturnsNone(string status) =>
        Assert.Equal(
            OrganizationSubscriptionScheduleOwnership.None,
            SubscriptionScheduleOwnershipMapper.Map(WithSchedule(Schedule(status: status))));

    [Fact]
    public void Map_AnnualUpgradeMetadata_ReturnsAnnualUpgrade() =>
        Assert.Equal(
            OrganizationSubscriptionScheduleOwnership.AnnualUpgrade,
            SubscriptionScheduleOwnershipMapper.Map(WithSchedule(Schedule(
                phaseMetadata: new Dictionary<string, string> { [MetadataKeys.AnnualUpgrade] = "TeamsMonthly" }))));

    [Fact]
    public void Map_MigrationCohortMetadata_ReturnsPriceMigration() =>
        Assert.Equal(
            OrganizationSubscriptionScheduleOwnership.PriceMigration,
            SubscriptionScheduleOwnershipMapper.Map(WithSchedule(Schedule(
                phaseMetadata: new Dictionary<string, string>
                {
                    [MetadataKeys.MigrationCohortId] = Guid.NewGuid().ToString(),
                    [MetadataKeys.MigrationCohortName] = "cohort-1"
                }))));

    [Fact]
    public void Map_BothMarkers_PrefersAnnualUpgrade() =>
        // Precedence is defined and pinned even though redemption creates its schedule with
        // FromSubscription, so Stripe builds phase 1 clean and no marker rides along.
        Assert.Equal(
            OrganizationSubscriptionScheduleOwnership.AnnualUpgrade,
            SubscriptionScheduleOwnershipMapper.Map(WithSchedule(Schedule(
                phaseMetadata: new Dictionary<string, string>
                {
                    [MetadataKeys.AnnualUpgrade] = "TeamsMonthly",
                    [MetadataKeys.MigrationCohortId] = Guid.NewGuid().ToString()
                }))));

    [Fact]
    public void Map_MarkerOnAnyPhase_IsEnough()
    {
        var schedule = new SubscriptionSchedule
        {
            Id = "sub_sched_1",
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase { Metadata = null },
                new SubscriptionSchedulePhase
                {
                    Metadata = new Dictionary<string, string> { [MetadataKeys.AnnualUpgrade] = "TeamsMonthly" }
                }
            ]
        };

        Assert.Equal(
            OrganizationSubscriptionScheduleOwnership.AnnualUpgrade,
            SubscriptionScheduleOwnershipMapper.Map(WithSchedule(schedule)));
    }

    [Fact]
    public void Map_UnrecognizedMetadataOnly_ReturnsForeign() =>
        Assert.Equal(
            OrganizationSubscriptionScheduleOwnership.Foreign,
            SubscriptionScheduleOwnershipMapper.Map(WithSchedule(Schedule(
                phaseMetadata: new Dictionary<string, string> { ["negotiated_term"] = "3y" }))));

    [Fact]
    public void Map_NullPhaseMetadata_ReturnsForeign() =>
        Assert.Equal(
            OrganizationSubscriptionScheduleOwnership.Foreign,
            SubscriptionScheduleOwnershipMapper.Map(WithSchedule(Schedule(phaseMetadata: null))));

    [Fact]
    public void Map_EmptyPhaseMetadata_ReturnsForeign() =>
        Assert.Equal(
            OrganizationSubscriptionScheduleOwnership.Foreign,
            SubscriptionScheduleOwnershipMapper.Map(WithSchedule(
                Schedule(phaseMetadata: new Dictionary<string, string>()))));

    [Fact]
    public void Map_NoPhases_ReturnsForeign() =>
        Assert.Equal(
            OrganizationSubscriptionScheduleOwnership.Foreign,
            SubscriptionScheduleOwnershipMapper.Map(WithSchedule(new SubscriptionSchedule
            {
                Id = "sub_sched_1",
                Status = SubscriptionScheduleStatus.Active,
                Phases = null
            })));

    [Fact]
    public void Map_AnnualLatestSeatPriceWithoutMetadata_ReturnsForeign() =>
        // Pins that content matching is gone. Under the previous implementation a phase carrying
        // the annual-latest seat price was classified as ours on that basis alone.
        Assert.Equal(
            OrganizationSubscriptionScheduleOwnership.Foreign,
            SubscriptionScheduleOwnershipMapper.Map(WithSchedule(
                Schedule(phaseMetadata: null, priceIds: "2023-enterprise-seat-annually"))));

    [Fact]
    public void MapSchedule_AnnualUpgradeMetadata_ReturnsAnnualUpgrade()
    {
        var schedule = new SubscriptionSchedule
        {
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase
                {
                    Metadata = new Dictionary<string, string> { [MetadataKeys.AnnualUpgrade] = "TeamsMonthly2020" }
                }
            ]
        };

        Assert.Equal(
            OrganizationSubscriptionScheduleOwnership.AnnualUpgrade,
            SubscriptionScheduleOwnershipMapper.MapSchedule(schedule));
    }

    [Fact]
    public void MapSchedule_MigrationCohortMetadata_ReturnsPriceMigration()
    {
        var schedule = new SubscriptionSchedule
        {
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase
                {
                    Metadata = new Dictionary<string, string> { [MetadataKeys.MigrationCohortId] = "cohort_1" }
                }
            ]
        };

        Assert.Equal(
            OrganizationSubscriptionScheduleOwnership.PriceMigration,
            SubscriptionScheduleOwnershipMapper.MapSchedule(schedule));
    }
}
