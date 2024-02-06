using System.Linq.Expressions;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands.Implementations;
using Bit.Core.Billing.Models;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Stripe;
using Xunit;

using static Bit.Core.Test.Billing.Utilities;

namespace Bit.Core.Test.Billing.Commands;

[SutProviderCustomize]
public class CancelSubscriptionCommandTests
{
    private const string _subscriptionId = "subscription_id";
    private const string _cancellingUserIdKey = "cancellingUserId";

    [Theory, BitAutoData]
    public async Task CancelSubscription_SubscriptionInactive_ThrowsGatewayException(
        SutProvider<CancelSubscriptionCommand> sutProvider)
    {
        var subscription = new Subscription
        {
            Status = "canceled"
        };

        await ThrowsContactSupportAsync(() =>
            sutProvider.Sut.CancelSubscription(subscription, new OffboardingSurveyResponse(), false));

        await DidNotUpdateSubscription(sutProvider);

        await DidNotCancelSubscription(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task CancelSubscription_CancelImmediately_BelongsToOrganization_UpdatesSubscription_CancelSubscriptionImmediately(
        SutProvider<CancelSubscriptionCommand> sutProvider)
    {
        var userId = Guid.NewGuid();

        var subscription = new Subscription
        {
            Id = _subscriptionId,
            Status = "active",
            Metadata = new Dictionary<string, string>
            {
                { "organizationId", "organization_id" }
            }
        };

        var offboardingSurveyResponse = new OffboardingSurveyResponse
        {
            UserId = userId,
            Reason = "missing_features",
            Feedback = "Lorem ipsum"
        };

        await sutProvider.Sut.CancelSubscription(subscription, offboardingSurveyResponse, true);

        await UpdatedSubscriptionWith(sutProvider, options => options.Metadata[_cancellingUserIdKey] == userId.ToString());

        await CancelledSubscriptionWith(sutProvider, options =>
            options.CancellationDetails.Comment == offboardingSurveyResponse.Feedback &&
            options.CancellationDetails.Feedback == offboardingSurveyResponse.Reason);
    }

    [Theory, BitAutoData]
    public async Task CancelSubscription_CancelImmediately_BelongsToUser_CancelSubscriptionImmediately(
        SutProvider<CancelSubscriptionCommand> sutProvider)
    {
        var userId = Guid.NewGuid();

        var subscription = new Subscription
        {
            Id = _subscriptionId,
            Status = "active",
            Metadata = new Dictionary<string, string>
            {
                { "userId", "user_id" }
            }
        };

        var offboardingSurveyResponse = new OffboardingSurveyResponse
        {
            UserId = userId,
            Reason = "missing_features",
            Feedback = "Lorem ipsum"
        };

        await sutProvider.Sut.CancelSubscription(subscription, offboardingSurveyResponse, true);

        await DidNotUpdateSubscription(sutProvider);

        await CancelledSubscriptionWith(sutProvider, options =>
            options.CancellationDetails.Comment == offboardingSurveyResponse.Feedback &&
            options.CancellationDetails.Feedback == offboardingSurveyResponse.Reason);
    }

    [Theory, BitAutoData]
    public async Task CancelSubscription_DoNotCancelImmediately_UpdateSubscriptionToCancelAtEndOfPeriod(
        Organization organization,
        SutProvider<CancelSubscriptionCommand> sutProvider)
    {
        var userId = Guid.NewGuid();

        organization.ExpirationDate = DateTime.UtcNow.AddDays(5);

        var subscription = new Subscription
        {
            Id = _subscriptionId,
            Status = "active"
        };

        var offboardingSurveyResponse = new OffboardingSurveyResponse
        {
            UserId = userId,
            Reason = "missing_features",
            Feedback = "Lorem ipsum"
        };

        await sutProvider.Sut.CancelSubscription(subscription, offboardingSurveyResponse, false);

        await UpdatedSubscriptionWith(sutProvider, options =>
            options.CancelAtPeriodEnd == true &&
            options.CancellationDetails.Comment == offboardingSurveyResponse.Feedback &&
            options.CancellationDetails.Feedback == offboardingSurveyResponse.Reason &&
            options.Metadata[_cancellingUserIdKey] == userId.ToString());

        await DidNotCancelSubscription(sutProvider);
    }

    private static Task<Subscription> DidNotCancelSubscription(SutProvider<CancelSubscriptionCommand> sutProvider)
        => sutProvider
            .GetDependency<IStripeAdapter>()
            .DidNotReceiveWithAnyArgs()
            .SubscriptionCancelAsync(Arg.Any<string>(), Arg.Any<SubscriptionCancelOptions>());

    private static Task<Subscription> DidNotUpdateSubscription(SutProvider<CancelSubscriptionCommand> sutProvider)
        => sutProvider
            .GetDependency<IStripeAdapter>()
            .DidNotReceiveWithAnyArgs()
            .SubscriptionUpdateAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());

    private static Task<Subscription> CancelledSubscriptionWith(
        SutProvider<CancelSubscriptionCommand> sutProvider,
        Expression<Predicate<SubscriptionCancelOptions>> predicate)
        => sutProvider
            .GetDependency<IStripeAdapter>()
            .Received(1)
            .SubscriptionCancelAsync(_subscriptionId, Arg.Is(predicate));

    private static Task<Subscription> UpdatedSubscriptionWith(
        SutProvider<CancelSubscriptionCommand> sutProvider,
        Expression<Predicate<SubscriptionUpdateOptions>> predicate)
        => sutProvider
            .GetDependency<IStripeAdapter>()
            .Received(1)
            .SubscriptionUpdateAsync(_subscriptionId, Arg.Is(predicate));
}
