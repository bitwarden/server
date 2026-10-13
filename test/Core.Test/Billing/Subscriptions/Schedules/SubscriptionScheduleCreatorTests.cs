using Bit.Core.Billing.Services;
using Bit.Core.Billing.Subscriptions.Schedules;
using Bit.Core.Billing.Subscriptions.Schedules.Enums;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;
using Xunit;
using static Bit.Core.Billing.Constants.StripeConstants;

namespace Bit.Core.Test.Billing.Subscriptions.Schedules;

public class SubscriptionScheduleCreatorTests
{
    private static readonly DateTime _phase1Start = new(2026, 7, 6, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _phase1End = new(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc);

    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly ILogger<SubscriptionScheduleCreator> _logger =
        Substitute.For<ILogger<SubscriptionScheduleCreator>>();
    private readonly SubscriptionScheduleCreator _sut;

    public SubscriptionScheduleCreatorTests()
    {
        _sut = new SubscriptionScheduleCreator(_stripeAdapter, _logger);
    }

    private static Subscription CreateSubscription(List<Discount>? discounts = null) => new()
    {
        Id = "sub_1",
        Discounts = discounts
    };

    private static SubscriptionSchedule CreatedSchedule(
        List<SubscriptionSchedulePhaseItemDiscount>? itemDiscounts = null,
        DateTime? trialEnd = null) => new()
        {
            Id = "sub_sched_1",
            Phases =
            [
                new SubscriptionSchedulePhase
                {
                    StartDate = _phase1Start,
                    EndDate = _phase1End,
                    TrialEnd = trialEnd,
                    Items =
                    [
                        new SubscriptionSchedulePhaseItem
                        {
                            PriceId = "price_current",
                            Quantity = 3,
                            Discounts = itemDiscounts
                        }
                    ]
                }
            ]
        };

    private static SubscriptionSchedulePhaseOptions Phase2() => new()
    {
        StartDate = _phase1End,
        EndDate = _phase1End.AddYears(1),
        Items = [new SubscriptionSchedulePhaseItemOptions { Price = "price_next", Quantity = 3 }],
        ProrationBehavior = ProrationBehavior.None
    };

    private void StubCreate(SubscriptionSchedule created) =>
        _stripeAdapter
            .CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .Returns(created);

    private async Task<SubscriptionScheduleUpdateOptions> CreateAndCaptureUpdateAsync(
        Subscription subscription,
        SubscriptionSchedule created,
        SubscriptionSchedulePhaseOptions phase2,
        string managingSystem,
        Dictionary<string, string>? phaseMetadata = null)
    {
        StubCreate(created);
        SubscriptionScheduleUpdateOptions? captured = null;
        _stripeAdapter
            .UpdateSubscriptionScheduleAsync(
                Arg.Any<string>(),
                Arg.Do<SubscriptionScheduleUpdateOptions>(options => captured = options))
            .Returns(new SubscriptionSchedule { Id = created.Id });

        await _sut.CreateWithPhasesAsync(subscription, phase2, managingSystem, phaseMetadata);

        Assert.NotNull(captured);
        return captured;
    }

    private void AssertLoggedError(string expectedContent) =>
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains(expectedContent)),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());

    [Fact]
    public async Task CreateWithPhasesAsync_CreatesFromSubscriptionOnly()
    {
        await CreateAndCaptureUpdateAsync(CreateSubscription(), CreatedSchedule(), Phase2(), ManagingSystems.AnnualUpgrade);

        await _stripeAdapter.Received(1).CreateSubscriptionScheduleAsync(
            Arg.Is<SubscriptionScheduleCreateOptions>(options =>
                options.FromSubscription == "sub_1" &&
                options.Metadata == null &&
                options.Phases == null &&
                options.EndBehavior == null));
    }

    [Theory]
    [InlineData(ManagingSystems.AnnualUpgrade)]
    [InlineData(ManagingSystems.BusinessPriceIncrease)]
    [InlineData(ManagingSystems.PersonalPriceIncrease)]
    public async Task CreateWithPhasesAsync_MarksScheduleWithManagingSystem(string managingSystem)
    {
        var update = await CreateAndCaptureUpdateAsync(CreateSubscription(), CreatedSchedule(), Phase2(), managingSystem);

        Assert.Equal(
            new Dictionary<string, string> { [MetadataKeys.ManagingSystem] = managingSystem },
            update.Metadata);
    }

    [Fact]
    public async Task CreateWithPhasesAsync_UpdatesTheCreatedSchedule_ReleasingAfterTwoPhases()
    {
        var update = await CreateAndCaptureUpdateAsync(CreateSubscription(), CreatedSchedule(), Phase2(), ManagingSystems.AnnualUpgrade);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            "sub_sched_1", Arg.Any<SubscriptionScheduleUpdateOptions>());
        Assert.Equal(SubscriptionScheduleEndBehavior.Release, update.EndBehavior);
        Assert.Equal(2, update.Phases.Count);
    }

    [Fact]
    public async Task CreateWithPhasesAsync_Phase1MirrorsTheCreatedSchedule()
    {
        var update = await CreateAndCaptureUpdateAsync(
            CreateSubscription(),
            CreatedSchedule(itemDiscounts: [new SubscriptionSchedulePhaseItemDiscount { CouponId = "item-coupon" }]),
            Phase2(),
            ManagingSystems.AnnualUpgrade);

        var phase1 = update.Phases[0];
        Assert.Equal(_phase1Start, (DateTime?)phase1.StartDate);
        Assert.Equal(_phase1End, (DateTime?)phase1.EndDate);
        Assert.Equal(ProrationBehavior.None, phase1.ProrationBehavior);
        var item = Assert.Single(phase1.Items);
        Assert.Equal("price_current", item.Price);
        Assert.Equal(3L, item.Quantity);
        Assert.Equal("item-coupon", Assert.Single(item.Discounts).Coupon);
    }

    [Fact]
    public async Task CreateWithPhasesAsync_TrialingSubscription_Phase1CarriesTrialEnd()
    {
        var update = await CreateAndCaptureUpdateAsync(
            CreateSubscription(), CreatedSchedule(trialEnd: _phase1End), Phase2(), ManagingSystems.AnnualUpgrade);

        Assert.Equal(_phase1End, (DateTime?)update.Phases[0].TrialEnd);
    }

    [Fact]
    public async Task CreateWithPhasesAsync_NoTrial_LeavesPhase1TrialEndNull()
    {
        var update = await CreateAndCaptureUpdateAsync(
            CreateSubscription(), CreatedSchedule(), Phase2(), ManagingSystems.AnnualUpgrade);

        Assert.Null(update.Phases[0].TrialEnd);
    }

    [Fact]
    public async Task CreateWithPhasesAsync_Phase1CarriesSubscriptionDiscountsById()
    {
        var subscription = CreateSubscription(
            [new Discount { Id = "di_live", Source = new DiscountSource { Coupon = new Coupon { Id = "cpn_live" } } }]);

        var update = await CreateAndCaptureUpdateAsync(subscription, CreatedSchedule(), Phase2(), ManagingSystems.AnnualUpgrade);

        var discount = Assert.Single(update.Phases[0].Discounts);
        Assert.Equal("di_live", discount.Discount);
        Assert.Null(discount.Coupon);
    }

    [Fact]
    public async Task CreateWithPhasesAsync_NoDiscounts_LeavesPhase1DiscountsAndItemDiscountsNull()
    {
        var update = await CreateAndCaptureUpdateAsync(
            CreateSubscription(discounts: []), CreatedSchedule(), Phase2(), ManagingSystems.AnnualUpgrade);

        Assert.Null(update.Phases[0].Discounts);
        Assert.Null(update.Phases[0].Items[0].Discounts);
    }

    [Fact]
    public async Task CreateWithPhasesAsync_Phase2IsTheCallersOptions()
    {
        var phase2 = Phase2();

        var update = await CreateAndCaptureUpdateAsync(CreateSubscription(), CreatedSchedule(), phase2, ManagingSystems.AnnualUpgrade);

        Assert.Same(phase2, update.Phases[1]);
    }

    [Fact]
    public async Task CreateWithPhasesAsync_PhaseMetadataGiven_AppliesToBothPhases()
    {
        var phaseMetadata = new Dictionary<string, string> { [MetadataKeys.MigrationCohortId] = "cohort_1" };

        var update = await CreateAndCaptureUpdateAsync(
            CreateSubscription(), CreatedSchedule(), Phase2(), ManagingSystems.BusinessPriceIncrease, phaseMetadata);

        Assert.Equal(phaseMetadata, update.Phases[0].Metadata);
        Assert.Equal(phaseMetadata, update.Phases[1].Metadata);
    }

    [Fact]
    public async Task CreateWithPhasesAsync_NoPhaseMetadata_LeavesBothPhasesWithoutMetadata()
    {
        var update = await CreateAndCaptureUpdateAsync(
            CreateSubscription(), CreatedSchedule(), Phase2(), ManagingSystems.PersonalPriceIncrease);

        Assert.Null(update.Phases[0].Metadata);
        Assert.Null(update.Phases[1].Metadata);
    }

    [Fact]
    public async Task CreateWithPhasesAsync_ReturnsTheUpdatedSchedule()
    {
        StubCreate(CreatedSchedule());
        var updated = new SubscriptionSchedule { Id = "sub_sched_1", Status = SubscriptionScheduleStatus.Active };
        _stripeAdapter
            .UpdateSubscriptionScheduleAsync("sub_sched_1", Arg.Any<SubscriptionScheduleUpdateOptions>())
            .Returns(updated);

        var result = await _sut.CreateWithPhasesAsync(
            CreateSubscription(), Phase2(), ManagingSystems.AnnualUpgrade);

        Assert.Same(updated, result);
    }

    [Theory]
    [InlineData(ManagingSystems.AnnualUpgrade, SubscriptionScheduleOwnership.AnnualUpgrade)]
    [InlineData(ManagingSystems.BusinessPriceIncrease, SubscriptionScheduleOwnership.BusinessPriceIncrease)]
    [InlineData(ManagingSystems.PersonalPriceIncrease, SubscriptionScheduleOwnership.PersonalPriceIncrease)]
    public async Task CreateWithPhasesAsync_ScheduleItWrites_ClassifiesAsItsManagingSystem(
        string managingSystem, SubscriptionScheduleOwnership expected)
    {
        // Round-trips the marker through the mapper so the write and the read cannot drift apart.
        var update = await CreateAndCaptureUpdateAsync(CreateSubscription(), CreatedSchedule(), Phase2(), managingSystem);

        var written = new SubscriptionSchedule
        {
            Status = SubscriptionScheduleStatus.Active,
            Metadata = update.Metadata,
            Phases = [.. update.Phases.Select(phase => new SubscriptionSchedulePhase { Metadata = phase.Metadata })]
        };

        Assert.Equal(expected, SubscriptionScheduleOwnershipMapper.MapSchedule(written));
    }

    [Fact]
    public async Task CreateWithPhasesAsync_UpdateFails_ReleasesAndRethrows()
    {
        StubCreate(CreatedSchedule());
        var updateFailure = new StripeException("update failed");
        _stripeAdapter
            .UpdateSubscriptionScheduleAsync(Arg.Any<string>(), Arg.Any<SubscriptionScheduleUpdateOptions>())
            .ThrowsAsync(updateFailure);

        var thrown = await Assert.ThrowsAsync<StripeException>(() =>
            _sut.CreateWithPhasesAsync(CreateSubscription(), Phase2(), ManagingSystems.AnnualUpgrade));

        Assert.Same(updateFailure, thrown);
        await _stripeAdapter.Received(1).ReleaseSubscriptionScheduleAsync(
            "sub_sched_1", Arg.Any<SubscriptionScheduleReleaseOptions>());
        AssertLoggedError("attempting to release orphaned schedule");
    }

    [Fact]
    public async Task CreateWithPhasesAsync_UpdateAndReleaseFail_LogsReleaseFailureAndRethrowsUpdateFailure()
    {
        StubCreate(CreatedSchedule());
        var updateFailure = new StripeException("update failed");
        _stripeAdapter
            .UpdateSubscriptionScheduleAsync(Arg.Any<string>(), Arg.Any<SubscriptionScheduleUpdateOptions>())
            .ThrowsAsync(updateFailure);
        _stripeAdapter
            .ReleaseSubscriptionScheduleAsync(Arg.Any<string>(), Arg.Any<SubscriptionScheduleReleaseOptions>())
            .ThrowsAsync(new StripeException("release failed"));

        var thrown = await Assert.ThrowsAsync<StripeException>(() =>
            _sut.CreateWithPhasesAsync(CreateSubscription(), Phase2(), ManagingSystems.AnnualUpgrade));

        Assert.Same(updateFailure, thrown);
        AssertLoggedError("Manual release required");
    }

    [Fact]
    public async Task CreateWithPhasesAsync_CreateFails_RethrowsWithoutUpdatingOrReleasing()
    {
        _stripeAdapter
            .CreateSubscriptionScheduleAsync(Arg.Any<SubscriptionScheduleCreateOptions>())
            .ThrowsAsync(new StripeException("create failed"));

        await Assert.ThrowsAsync<StripeException>(() =>
            _sut.CreateWithPhasesAsync(CreateSubscription(), Phase2(), ManagingSystems.AnnualUpgrade));

        await _stripeAdapter.DidNotReceiveWithAnyArgs().UpdateSubscriptionScheduleAsync(default!, default!);
        await _stripeAdapter.DidNotReceiveWithAnyArgs().ReleaseSubscriptionScheduleAsync(default!, default);
    }
}
