using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;
using Stripe.TestHelpers;
using Xunit;
using static Bit.Core.Billing.Constants.StripeConstants;

namespace Bit.Core.Test.Billing.Organizations.Commands;

public class ExtendOrganizationTrialCommandTests
{
    private const string _subscriptionId = "sub_1";

    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly IOrganizationService _organizationService = Substitute.For<IOrganizationService>();
    private readonly ILogger<ExtendOrganizationTrialCommand> _logger = Substitute.For<ILogger<ExtendOrganizationTrialCommand>>();
    private readonly ExtendOrganizationTrialCommand _command;

    public ExtendOrganizationTrialCommandTests()
    {
        _command = new ExtendOrganizationTrialCommand(_logger, _stripeAdapter, _organizationService);
    }

    private static Organization CreateOrganization(string? subscriptionId = _subscriptionId) =>
        new() { Id = Guid.NewGuid(), GatewaySubscriptionId = subscriptionId };

    private static Subscription CreateTrialingSubscription(DateTime now, double remainingDays, string? scheduleId = null) =>
        new()
        {
            Id = _subscriptionId,
            Status = SubscriptionStatus.Trialing,
            TrialEnd = now.AddDays(remainingDays),
            ScheduleId = scheduleId,
            TestClock = new TestClock { FrozenTime = now }
        };

    private void StubSubscription(Subscription subscription) =>
        _stripeAdapter.GetSubscriptionAsync(_subscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

    private async Task AssertNoStripeOrDatabaseWritesAsync()
    {
        await _stripeAdapter.DidNotReceiveWithAnyArgs().UpdateSubscriptionAsync(default!, default);
        await _organizationService.DidNotReceiveWithAnyArgs().UpdateExpirationDateAsync(default, default);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(-1)]
    public async Task Run_DaysOutOfRange_ReturnsBadRequestWithoutCallingStripe(int days)
    {
        var result = await _command.Run(CreateOrganization(), days);

        Assert.True(result.IsT1);
        Assert.Equal(TrialExtensionPolicy.DaysOutOfRangeMessage, result.AsT1.Response);
        await _stripeAdapter.DidNotReceiveWithAnyArgs().GetSubscriptionAsync(default!, default);
        await AssertNoStripeOrDatabaseWritesAsync();
    }

    [Fact]
    public async Task Run_NoGatewaySubscriptionId_ReturnsBadRequest()
    {
        var result = await _command.Run(CreateOrganization(subscriptionId: null), 10);

        Assert.True(result.IsT1);
        Assert.Equal("Organization has no subscription.", result.AsT1.Response);
        await _stripeAdapter.DidNotReceiveWithAnyArgs().GetSubscriptionAsync(default!, default);
    }

    [Fact]
    public async Task Run_SubscriptionNotTrialing_ReturnsBadRequestWithAcceptanceCriteriaMessage()
    {
        var now = DateTime.UtcNow;
        var subscription = CreateTrialingSubscription(now, 10);
        subscription.Status = SubscriptionStatus.Active;
        StubSubscription(subscription);

        var result = await _command.Run(CreateOrganization(), 10);

        Assert.True(result.IsT1);
        Assert.Equal(
            "Trial cannot be extended because the linked subscription is not in a trialing status.",
            result.AsT1.Response);
        await AssertNoStripeOrDatabaseWritesAsync();
    }

    [Theory]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(29.2)]
    public async Task Run_ThirtyOrMoreDaysRemaining_ReturnsBadRequest(double remainingDays)
    {
        StubSubscription(CreateTrialingSubscription(DateTime.UtcNow, remainingDays));

        var result = await _command.Run(CreateOrganization(), 10);

        Assert.True(result.IsT1);
        Assert.Equal("Trial cannot be extended because 30 or more days remain.", result.AsT1.Response);
        await AssertNoStripeOrDatabaseWritesAsync();
    }

    [Fact]
    public async Task Run_TwentyNineDaysRemaining_Succeeds()
    {
        StubSubscription(CreateTrialingSubscription(DateTime.UtcNow, 29));

        var result = await _command.Run(CreateOrganization(), 1);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task Run_ScheduleAttached_ReturnsBadRequest()
    {
        StubSubscription(CreateTrialingSubscription(DateTime.UtcNow, 10, scheduleId: "sub_sched_1"));

        var result = await _command.Run(CreateOrganization(), 10);

        Assert.True(result.IsT1);
        Assert.Equal(TrialExtensionPolicy.ScheduleAttachedMessage, result.AsT1.Response);
        await AssertNoStripeOrDatabaseWritesAsync();
    }

    [Fact]
    public async Task Run_EligibleSubscription_ExtendsFromExistingTrialEndAndSyncsExpiration()
    {
        const int days = 14;
        var organization = CreateOrganization();
        var subscription = CreateTrialingSubscription(DateTime.UtcNow, 5);
        var expectedTrialEnd = subscription.TrialEnd!.Value.AddDays(days);
        StubSubscription(subscription);

        var result = await _command.Run(organization, days);

        Assert.True(result.Success);
        Assert.Equal(expectedTrialEnd, result.AsT0);
        await _stripeAdapter.Received(1).GetSubscriptionAsync(_subscriptionId,
            Arg.Is<SubscriptionGetOptions>(o => o.Expand.Contains("test_clock")));
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(_subscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(o =>
                Equals(o.TrialEnd.Value, expectedTrialEnd) &&
                o.ProrationBehavior == "none"));
        await _organizationService.Received(1).UpdateExpirationDateAsync(organization.Id, expectedTrialEnd);
    }

    [Fact]
    public async Task Run_TestClockAttached_UsesFrozenTimeForRemainingDays()
    {
        // Against wall-clock time this trial ended long ago (well under 30 days remain), but the test clock is
        // frozen 40 days before trial end, so the command must treat it as having too many days remaining.
        var frozenTime = DateTime.UtcNow.AddYears(-1);
        StubSubscription(CreateTrialingSubscription(frozenTime, 40));

        var result = await _command.Run(CreateOrganization(), 10);

        Assert.True(result.IsT1);
        Assert.Equal(TrialExtensionPolicy.TooManyDaysRemainingMessage, result.AsT1.Response);
    }

    [Fact]
    public async Task Run_StripeUpdateThrows_ReturnsUnhandledAndDoesNotSyncExpiration()
    {
        StubSubscription(CreateTrialingSubscription(DateTime.UtcNow, 5));
        _stripeAdapter.UpdateSubscriptionAsync(_subscriptionId, Arg.Any<SubscriptionUpdateOptions>())
            .ThrowsAsync(new StripeException { StripeError = new StripeError { Code = "api_error" } });

        var result = await _command.Run(CreateOrganization(), 5);

        Assert.True(result.IsT3);
        Assert.IsType<Unhandled>(result.AsT3);
        await _organizationService.DidNotReceiveWithAnyArgs().UpdateExpirationDateAsync(default, default);
    }

    [Fact]
    public async Task Run_ExpirationSyncThrows_ReturnsSuccessAndLogsSyncFailure()
    {
        // Stripe has already moved the trial end by the time the database write runs. Reporting that write's
        // failure as a failed extension would invite a retry that extends the trial a second time, so the command
        // must report success, keep the audit record, and leave the expiration date to the subscription webhook.
        const int days = 5;
        var organization = CreateOrganization();
        var subscription = CreateTrialingSubscription(DateTime.UtcNow, 5);
        var expectedTrialEnd = subscription.TrialEnd!.Value.AddDays(days);
        StubSubscription(subscription);
        _organizationService.UpdateExpirationDateAsync(organization.Id, Arg.Any<DateTime?>())
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var result = await _command.Run(organization, days);

        Assert.True(result.Success);
        Assert.Equal(expectedTrialEnd, result.AsT0);
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(_subscriptionId, Arg.Any<SubscriptionUpdateOptions>());
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("Extended trial")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("failed to sync the expiration date")),
            Arg.Any<InvalidOperationException>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
