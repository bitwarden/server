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

    [Fact]
    public void MapOrNull_NoAttachedSchedule_ReturnsNone()
    {
        var result = SubscriptionScheduleOwnershipMapper.MapOrNull(new Subscription { Id = "sub_1" });

        Assert.NotNull(result);
        Assert.Equal(OrganizationSubscriptionScheduleOwnership.None, result.Ownership);
        Assert.Null(result.Schedule);
    }

    [Fact]
    public void MapOrNull_ScheduleIdSetButNotExpanded_ReturnsNull()
    {
        // Not None: a caller told None would release nothing and then create a second schedule,
        // which Stripe rejects because it permits one active schedule per subscription.
        var result = SubscriptionScheduleOwnershipMapper.MapOrNull(
            new Subscription { Id = "sub_1", ScheduleId = "sub_sched_1", Schedule = null });

        Assert.Null(result);
    }

    [Fact]
    public void MapOrNull_ExpandedSchedule_Classifies()
    {
        var schedule = Schedule(phaseMetadata: new Dictionary<string, string>
        {
            [MetadataKeys.AnnualUpgrade] = "TeamsMonthly2020"
        });

        var result = SubscriptionScheduleOwnershipMapper.MapOrNull(new Subscription
        {
            Id = "sub_1",
            ScheduleId = schedule.Id,
            Schedule = schedule
        });

        Assert.NotNull(result);
        Assert.Equal(OrganizationSubscriptionScheduleOwnership.AnnualUpgrade, result.Ownership);
        Assert.Same(schedule, result.Schedule);
    }

    [Fact]
    public void Map_Null_ReturnsNone()
    {
        var result = SubscriptionScheduleOwnershipMapper.Map(null);

        Assert.Equal(OrganizationSubscriptionScheduleOwnership.None, result.Ownership);
        Assert.Null(result.Schedule);
    }

    [Theory]
    [InlineData(SubscriptionScheduleStatus.NotStarted)]
    [InlineData(SubscriptionScheduleStatus.Released)]
    [InlineData(SubscriptionScheduleStatus.Canceled)]
    [InlineData(SubscriptionScheduleStatus.Completed)]
    public void Map_ScheduleNotActive_ReturnsNoneAndDropsTheSchedule(string status)
    {
        var result = SubscriptionScheduleOwnershipMapper.Map(Schedule(status: status));

        Assert.Equal(OrganizationSubscriptionScheduleOwnership.None, result.Ownership);
        Assert.Null(result.Schedule);
    }

    [Fact]
    public void Map_AnnualUpgradeMetadata_ReturnsAnnualUpgrade()
    {
        var result = SubscriptionScheduleOwnershipMapper.Map(Schedule(
            phaseMetadata: new Dictionary<string, string> { [MetadataKeys.AnnualUpgrade] = "TeamsMonthly" }));

        Assert.Equal(OrganizationSubscriptionScheduleOwnership.AnnualUpgrade, result.Ownership);
    }

    [Fact]
    public void Map_MigrationCohortMetadata_ReturnsPriceMigration()
    {
        var result = SubscriptionScheduleOwnershipMapper.Map(Schedule(
            phaseMetadata: new Dictionary<string, string>
            {
                [MetadataKeys.MigrationCohortId] = Guid.NewGuid().ToString(),
                [MetadataKeys.MigrationCohortName] = "cohort-1"
            }));

        Assert.Equal(OrganizationSubscriptionScheduleOwnership.PriceMigration, result.Ownership);
    }

    [Fact]
    public void Map_BothMarkers_PrefersAnnualUpgrade()
    {
        // Precedence is defined and pinned even though redemption creates its schedule with
        // FromSubscription, so Stripe builds phase 1 clean and no marker rides along.
        var result = SubscriptionScheduleOwnershipMapper.Map(Schedule(
            phaseMetadata: new Dictionary<string, string>
            {
                [MetadataKeys.AnnualUpgrade] = "TeamsMonthly",
                [MetadataKeys.MigrationCohortId] = Guid.NewGuid().ToString()
            }));

        Assert.Equal(OrganizationSubscriptionScheduleOwnership.AnnualUpgrade, result.Ownership);
    }

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
            SubscriptionScheduleOwnershipMapper.Map(schedule).Ownership);
    }

    [Fact]
    public void Map_UnrecognizedMetadataOnly_ReturnsForeign()
    {
        var result = SubscriptionScheduleOwnershipMapper.Map(Schedule(
            phaseMetadata: new Dictionary<string, string> { ["negotiated_term"] = "3y" }));

        Assert.Equal(OrganizationSubscriptionScheduleOwnership.Foreign, result.Ownership);
        Assert.NotNull(result.Schedule);
    }

    [Fact]
    public void Map_NullPhaseMetadata_ReturnsForeign()
    {
        Assert.Equal(
            OrganizationSubscriptionScheduleOwnership.Foreign,
            SubscriptionScheduleOwnershipMapper.Map(Schedule(phaseMetadata: null)).Ownership);
    }

    [Fact]
    public void Map_EmptyPhaseMetadata_ReturnsForeign()
    {
        Assert.Equal(
            OrganizationSubscriptionScheduleOwnership.Foreign,
            SubscriptionScheduleOwnershipMapper.Map(
                Schedule(phaseMetadata: new Dictionary<string, string>())).Ownership);
    }

    [Fact]
    public void Map_NoPhases_ReturnsForeign()
    {
        var schedule = new SubscriptionSchedule
        {
            Id = "sub_sched_1",
            Status = SubscriptionScheduleStatus.Active,
            Phases = null
        };

        Assert.Equal(
            OrganizationSubscriptionScheduleOwnership.Foreign,
            SubscriptionScheduleOwnershipMapper.Map(schedule).Ownership);
    }

    [Fact]
    public void Map_AnnualLatestSeatPriceWithoutMetadata_ReturnsForeign()
    {
        // Pins that content matching is gone. Under the previous implementation a phase carrying
        // the annual-latest seat price was classified as ours on that basis alone.
        var result = SubscriptionScheduleOwnershipMapper.Map(
            Schedule(phaseMetadata: null, priceIds: "2023-enterprise-seat-annually"));

        Assert.Equal(OrganizationSubscriptionScheduleOwnership.Foreign, result.Ownership);
    }
}
