using Bit.Core.Billing.Services.Implementations;
using Bit.Core.Billing.Subscriptions.Schedules;
using Bit.Core.Billing.Subscriptions.Schedules.Enums;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;
using Xunit;
using static Bit.Core.Billing.Constants.StripeConstants;

namespace Bit.Core.Test.Billing.Services;

public class StripeSubscriptionScheduleAdapterTests
{
    private static readonly DateTime _phase1Start = new(2026, 7, 6, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _phase1End = new(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc);

    private readonly SubscriptionScheduleService _subscriptionScheduleService =
        Substitute.For<SubscriptionScheduleService>();
    private readonly ILogger<StripeSubscriptionScheduleAdapter> _logger =
        Substitute.For<ILogger<StripeSubscriptionScheduleAdapter>>();
    private readonly StripeSubscriptionScheduleAdapter _sut;

    public StripeSubscriptionScheduleAdapterTests()
    {
        _sut = new StripeSubscriptionScheduleAdapter(_subscriptionScheduleService, _logger);
    }

    private static Subscription CreateSubscription(List<Discount>? discounts = null) => new()
    {
        Id = "sub_1",
        Discounts = discounts
    };

    private static SubscriptionSchedule CreatedSchedule(
        List<SubscriptionSchedulePhaseItemDiscount>? itemDiscounts = null) => new()
        {
            Id = "sub_sched_1",
            Phases =
            [
                new SubscriptionSchedulePhase
                {
                    StartDate = _phase1Start,
                    EndDate = _phase1End,
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
        _subscriptionScheduleService
            .CreateAsync(Arg.Any<SubscriptionScheduleCreateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>())
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
        _subscriptionScheduleService
            .UpdateAsync(
                Arg.Any<string>(),
                Arg.Do<SubscriptionScheduleUpdateOptions>(options => captured = options),
                Arg.Any<RequestOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(new SubscriptionSchedule { Id = created.Id });

        await _sut.CreateSubscriptionScheduleWithPhasesAsync(subscription, phase2, managingSystem, phaseMetadata);

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
    public async Task CreateSubscriptionScheduleWithPhasesAsync_CreatesFromSubscriptionOnly()
    {
        await CreateAndCaptureUpdateAsync(CreateSubscription(), CreatedSchedule(), Phase2(), ManagingSystems.AnnualUpgrade);

        await _subscriptionScheduleService.Received(1).CreateAsync(
            Arg.Is<SubscriptionScheduleCreateOptions>(options =>
                options.FromSubscription == "sub_1" &&
                options.Metadata == null &&
                options.Phases == null &&
                options.EndBehavior == null),
            Arg.Any<RequestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(ManagingSystems.AnnualUpgrade)]
    [InlineData(ManagingSystems.BusinessPriceIncrease)]
    [InlineData(ManagingSystems.PersonalPriceIncrease)]
    public async Task CreateSubscriptionScheduleWithPhasesAsync_MarksScheduleWithManagingSystem(string managingSystem)
    {
        var update = await CreateAndCaptureUpdateAsync(CreateSubscription(), CreatedSchedule(), Phase2(), managingSystem);

        Assert.Equal(
            new Dictionary<string, string> { [MetadataKeys.ManagingSystem] = managingSystem },
            update.Metadata);
    }

    [Fact]
    public async Task CreateSubscriptionScheduleWithPhasesAsync_UpdatesTheCreatedSchedule_ReleasingAfterTwoPhases()
    {
        var update = await CreateAndCaptureUpdateAsync(CreateSubscription(), CreatedSchedule(), Phase2(), ManagingSystems.AnnualUpgrade);

        await _subscriptionScheduleService.Received(1).UpdateAsync(
            "sub_sched_1", Arg.Any<SubscriptionScheduleUpdateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>());
        Assert.Equal(SubscriptionScheduleEndBehavior.Release, update.EndBehavior);
        Assert.Equal(2, update.Phases.Count);
    }

    [Fact]
    public async Task CreateSubscriptionScheduleWithPhasesAsync_Phase1MirrorsTheCreatedSchedule()
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
    public async Task CreateSubscriptionScheduleWithPhasesAsync_Phase1CarriesSubscriptionDiscountsById()
    {
        var subscription = CreateSubscription(
            [new Discount { Id = "di_live", Source = new DiscountSource { Coupon = new Coupon { Id = "cpn_live" } } }]);

        var update = await CreateAndCaptureUpdateAsync(subscription, CreatedSchedule(), Phase2(), ManagingSystems.AnnualUpgrade);

        var discount = Assert.Single(update.Phases[0].Discounts);
        Assert.Equal("di_live", discount.Discount);
        Assert.Null(discount.Coupon);
    }

    [Fact]
    public async Task CreateSubscriptionScheduleWithPhasesAsync_NoDiscounts_LeavesPhase1DiscountsAndItemDiscountsNull()
    {
        var update = await CreateAndCaptureUpdateAsync(
            CreateSubscription(discounts: []), CreatedSchedule(), Phase2(), ManagingSystems.AnnualUpgrade);

        Assert.Null(update.Phases[0].Discounts);
        Assert.Null(update.Phases[0].Items[0].Discounts);
    }

    [Fact]
    public async Task CreateSubscriptionScheduleWithPhasesAsync_Phase2IsTheCallersOptions()
    {
        var phase2 = Phase2();

        var update = await CreateAndCaptureUpdateAsync(CreateSubscription(), CreatedSchedule(), phase2, ManagingSystems.AnnualUpgrade);

        Assert.Same(phase2, update.Phases[1]);
    }

    [Fact]
    public async Task CreateSubscriptionScheduleWithPhasesAsync_PhaseMetadataGiven_AppliesToBothPhases()
    {
        var phaseMetadata = new Dictionary<string, string> { [MetadataKeys.MigrationCohortId] = "cohort_1" };

        var update = await CreateAndCaptureUpdateAsync(
            CreateSubscription(), CreatedSchedule(), Phase2(), ManagingSystems.BusinessPriceIncrease, phaseMetadata);

        Assert.Equal(phaseMetadata, update.Phases[0].Metadata);
        Assert.Equal(phaseMetadata, update.Phases[1].Metadata);
    }

    [Fact]
    public async Task CreateSubscriptionScheduleWithPhasesAsync_NoPhaseMetadata_LeavesBothPhasesWithoutMetadata()
    {
        var update = await CreateAndCaptureUpdateAsync(
            CreateSubscription(), CreatedSchedule(), Phase2(), ManagingSystems.PersonalPriceIncrease);

        Assert.Null(update.Phases[0].Metadata);
        Assert.Null(update.Phases[1].Metadata);
    }

    [Fact]
    public async Task CreateSubscriptionScheduleWithPhasesAsync_ReturnsTheUpdatedSchedule()
    {
        StubCreate(CreatedSchedule());
        var updated = new SubscriptionSchedule { Id = "sub_sched_1", Status = SubscriptionScheduleStatus.Active };
        _subscriptionScheduleService
            .UpdateAsync("sub_sched_1", Arg.Any<SubscriptionScheduleUpdateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>())
            .Returns(updated);

        var result = await _sut.CreateSubscriptionScheduleWithPhasesAsync(
            CreateSubscription(), Phase2(), ManagingSystems.AnnualUpgrade);

        Assert.Same(updated, result);
    }

    [Theory]
    [InlineData(ManagingSystems.AnnualUpgrade, SubscriptionScheduleOwnership.AnnualUpgrade)]
    [InlineData(ManagingSystems.BusinessPriceIncrease, SubscriptionScheduleOwnership.BusinessPriceIncrease)]
    [InlineData(ManagingSystems.PersonalPriceIncrease, SubscriptionScheduleOwnership.PersonalPriceIncrease)]
    public async Task CreateSubscriptionScheduleWithPhasesAsync_ScheduleItWrites_ClassifiesAsItsManagingSystem(
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
    public async Task CreateSubscriptionScheduleWithPhasesAsync_UpdateFails_ReleasesAndRethrows()
    {
        StubCreate(CreatedSchedule());
        var updateFailure = new StripeException("update failed");
        _subscriptionScheduleService
            .UpdateAsync(Arg.Any<string>(), Arg.Any<SubscriptionScheduleUpdateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(updateFailure);

        var thrown = await Assert.ThrowsAsync<StripeException>(() =>
            _sut.CreateSubscriptionScheduleWithPhasesAsync(CreateSubscription(), Phase2(), ManagingSystems.AnnualUpgrade));

        Assert.Same(updateFailure, thrown);
        await _subscriptionScheduleService.Received(1).ReleaseAsync(
            "sub_sched_1", Arg.Any<SubscriptionScheduleReleaseOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>());
        AssertLoggedError("attempting to release orphaned schedule");
    }

    [Fact]
    public async Task CreateSubscriptionScheduleWithPhasesAsync_UpdateAndReleaseFail_LogsReleaseFailureAndRethrowsUpdateFailure()
    {
        StubCreate(CreatedSchedule());
        var updateFailure = new StripeException("update failed");
        _subscriptionScheduleService
            .UpdateAsync(Arg.Any<string>(), Arg.Any<SubscriptionScheduleUpdateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(updateFailure);
        _subscriptionScheduleService
            .ReleaseAsync(Arg.Any<string>(), Arg.Any<SubscriptionScheduleReleaseOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new StripeException("release failed"));

        var thrown = await Assert.ThrowsAsync<StripeException>(() =>
            _sut.CreateSubscriptionScheduleWithPhasesAsync(CreateSubscription(), Phase2(), ManagingSystems.AnnualUpgrade));

        Assert.Same(updateFailure, thrown);
        AssertLoggedError("Manual release required");
    }

    [Fact]
    public async Task CreateSubscriptionScheduleWithPhasesAsync_CreateFails_RethrowsWithoutUpdatingOrReleasing()
    {
        _subscriptionScheduleService
            .CreateAsync(Arg.Any<SubscriptionScheduleCreateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new StripeException("create failed"));

        await Assert.ThrowsAsync<StripeException>(() =>
            _sut.CreateSubscriptionScheduleWithPhasesAsync(CreateSubscription(), Phase2(), ManagingSystems.AnnualUpgrade));

        await _subscriptionScheduleService.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default!, default, default);
        await _subscriptionScheduleService.DidNotReceiveWithAnyArgs().ReleaseAsync(default!, default, default, default);
    }

    [Fact]
    public async Task GetSubscriptionScheduleAsync_ForwardsToStripe()
    {
        var options = new SubscriptionScheduleGetOptions();
        var expected = new SubscriptionSchedule { Id = "sub_sched_1" };
        _subscriptionScheduleService.GetAsync("sub_sched_1", options, null, default).Returns(expected);

        Assert.Same(expected, await _sut.GetSubscriptionScheduleAsync("sub_sched_1", options));
    }

    [Fact]
    public async Task ListSubscriptionSchedulesAsync_ForwardsToStripe()
    {
        var options = new SubscriptionScheduleListOptions { Customer = "cus_1" };
        var expected = new StripeList<SubscriptionSchedule> { Data = [] };
        _subscriptionScheduleService.ListAsync(options, null, default).Returns(expected);

        Assert.Same(expected, await _sut.ListSubscriptionSchedulesAsync(options));
    }

    [Fact]
    public async Task UpdateSubscriptionScheduleAsync_ForwardsToStripe()
    {
        var options = new SubscriptionScheduleUpdateOptions();
        var expected = new SubscriptionSchedule { Id = "sub_sched_1" };
        _subscriptionScheduleService.UpdateAsync("sub_sched_1", options, null, default).Returns(expected);

        Assert.Same(expected, await _sut.UpdateSubscriptionScheduleAsync("sub_sched_1", options));
    }

    [Fact]
    public async Task ReleaseSubscriptionScheduleAsync_ForwardsToStripe()
    {
        var expected = new SubscriptionSchedule { Id = "sub_sched_1" };
        _subscriptionScheduleService.ReleaseAsync("sub_sched_1", null, null, default).Returns(expected);

        Assert.Same(expected, await _sut.ReleaseSubscriptionScheduleAsync("sub_sched_1"));
    }
}
