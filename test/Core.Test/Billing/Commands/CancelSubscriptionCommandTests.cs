using System.Linq.Expressions;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands.Implementations;
using Bit.Core.Billing.Models;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Commands;

[SutProviderCustomize]
public class CancelSubscriptionCommandTests
{
    private const string _subscriptionId = "subscription_id";
    private const string _cancellingUserIdKey = "cancellingUserId";

    [Theory, BitAutoData]
    public async Task CancelSubscription_Organization_NullOrganization_ThrowsArgumentNullException(
        SutProvider<CancelSubscriptionCommand> sutProvider)
        => await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await sutProvider.Sut.CancelSubscription((Organization)null, new OffboardingSurveyResponse()));

    [Theory, BitAutoData]
    public async Task CancelSubscription_Organization_NoGatewaySubscriptionId_ThrowsGatewayException(
        Organization organization,
        SutProvider<CancelSubscriptionCommand> sutProvider)
    {
        organization.GatewaySubscriptionId = null;

        await ThrowsContactSupportAsync(async () =>
            await sutProvider.Sut.CancelSubscription(organization, new OffboardingSurveyResponse()));
    }

    [Theory, BitAutoData]
    public async Task CancelSubscription_Organization_NoSubscription_ThrowsGatewayException(
        Organization organization,
        SutProvider<CancelSubscriptionCommand> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>().SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .ReturnsNull();

        await ThrowsContactSupportAsync(async () =>
            await sutProvider.Sut.CancelSubscription(organization, new OffboardingSurveyResponse()));
    }

    [Theory, BitAutoData]
    public async Task CancelSubscription_Organization_SubscriptionInactive_NoOp(
        Organization organization,
        SutProvider<CancelSubscriptionCommand> sutProvider)
    {
        var subscription = new Subscription
        {
            Status = "canceled"
        };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        stripeAdapter.SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .Returns(subscription);

        await sutProvider.Sut.CancelSubscription(organization, new OffboardingSurveyResponse());

        await DidNotUpdateSubscription(sutProvider);

        await DidNotCancelSubscription(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task CancelSubscription_Organization_Expired_CancelSubscriptionImmediately(
        Organization organization,
        SutProvider<CancelSubscriptionCommand> sutProvider)
    {
        var userId = Guid.NewGuid();

        organization.ExpirationDate = DateTime.UtcNow.AddDays(-5);

        var subscription = new Subscription
        {
            Id = _subscriptionId,
            Status = "active"
        };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        stripeAdapter.SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .Returns(subscription);

        var offboardingSurveyResponse = new OffboardingSurveyResponse
        {
            UserId = userId,
            Reason = "missing_features",
            Feedback = "Lorem ipsum"
        };

        await sutProvider.Sut.CancelSubscription(organization, offboardingSurveyResponse);

        await UpdatedSubscriptionWith(sutProvider, options => options.Metadata[_cancellingUserIdKey] == userId.ToString());

        await CancelledSubscriptionWith(sutProvider, options =>
            options.CancellationDetails.Comment == offboardingSurveyResponse.Feedback &&
            options.CancellationDetails.Feedback == offboardingSurveyResponse.Reason);
    }

    [Theory, BitAutoData]
    public async Task CancelSubscription_Organization_Active_UpdateSubscriptionToCancelAtEndOfPeriod(
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

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        stripeAdapter.SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .Returns(subscription);

        var offboardingSurveyResponse = new OffboardingSurveyResponse
        {
            UserId = userId,
            Reason = "missing_features",
            Feedback = "Lorem ipsum"
        };

        await sutProvider.Sut.CancelSubscription(organization, offboardingSurveyResponse);

        await UpdatedSubscriptionWith(sutProvider, options =>
            options.CancelAtPeriodEnd == true &&
            options.CancellationDetails.Comment == offboardingSurveyResponse.Feedback &&
            options.CancellationDetails.Feedback == offboardingSurveyResponse.Reason &&
            options.Metadata[_cancellingUserIdKey] == userId.ToString());

        await DidNotCancelSubscription(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task CancelSubscription_User_NullUser_ThrowsArgumentNullException(
        SutProvider<CancelSubscriptionCommand> sutProvider)
        => await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await sutProvider.Sut.CancelSubscription((User)null, new OffboardingSurveyResponse()));

    [Theory, BitAutoData]
    public async Task CancelSubscription_User_NoGatewaySubscriptionId_ThrowsGatewayException(
        User user,
        SutProvider<CancelSubscriptionCommand> sutProvider)
    {
        user.GatewaySubscriptionId = null;

        await ThrowsContactSupportAsync(async () =>
            await sutProvider.Sut.CancelSubscription(user, new OffboardingSurveyResponse()));
    }

    [Theory, BitAutoData]
    public async Task CancelSubscription_User_NoSubscription_ThrowsGatewayException(
        User user,
        SutProvider<CancelSubscriptionCommand> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>().SubscriptionGetAsync(user.GatewaySubscriptionId)
            .ReturnsNull();

        await ThrowsContactSupportAsync(async () =>
            await sutProvider.Sut.CancelSubscription(user, new OffboardingSurveyResponse()));
    }

    [Theory, BitAutoData]
    public async Task CancelSubscription_User_SubscriptionInactive_NoOp(
        User user,
        SutProvider<CancelSubscriptionCommand> sutProvider)
    {
        var subscription = new Subscription
        {
            Status = "canceled"
        };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        stripeAdapter.SubscriptionGetAsync(user.GatewaySubscriptionId)
            .Returns(subscription);

        await sutProvider.Sut.CancelSubscription(user, new OffboardingSurveyResponse());

        await DidNotUpdateSubscription(sutProvider);

        await DidNotCancelSubscription(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task CancelSubscription_User_Expired_CancelSubscriptionImmediately(
        User user,
        SutProvider<CancelSubscriptionCommand> sutProvider)
    {
        var userId = Guid.NewGuid();

        user.PremiumExpirationDate = DateTime.UtcNow.AddDays(-5);

        var subscription = new Subscription
        {
            Id = _subscriptionId,
            Status = "active"
        };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        stripeAdapter.SubscriptionGetAsync(user.GatewaySubscriptionId)
            .Returns(subscription);

        var offboardingSurveyResponse = new OffboardingSurveyResponse
        {
            UserId = userId,
            Reason = "missing_features",
            Feedback = "Lorem ipsum"
        };

        await sutProvider.Sut.CancelSubscription(user, offboardingSurveyResponse);

        await CancelledSubscriptionWith(sutProvider, options =>
            options.CancellationDetails.Comment == offboardingSurveyResponse.Feedback &&
            options.CancellationDetails.Feedback == offboardingSurveyResponse.Reason);

        await DidNotUpdateSubscription(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task CancelSubscription_User_Active_UpdateSubscriptionToCancelAtEndOfPeriod(
        User user,
        SutProvider<CancelSubscriptionCommand> sutProvider)
    {
        var userId = Guid.NewGuid();

        user.PremiumExpirationDate = DateTime.UtcNow.AddDays(5);

        var subscription = new Subscription
        {
            Id = _subscriptionId,
            Status = "active"
        };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        stripeAdapter.SubscriptionGetAsync(user.GatewaySubscriptionId)
            .Returns(subscription);

        var offboardingSurveyResponse = new OffboardingSurveyResponse
        {
            UserId = userId,
            Reason = "missing_features",
            Feedback = "Lorem ipsum"
        };

        await sutProvider.Sut.CancelSubscription(user, offboardingSurveyResponse);

        await UpdatedSubscriptionWith(sutProvider, options =>
            options.CancelAtPeriodEnd == true &&
            options.CancellationDetails.Comment == offboardingSurveyResponse.Feedback &&
            options.CancellationDetails.Feedback == offboardingSurveyResponse.Reason);

        await DidNotCancelSubscription(sutProvider);
    }

    private static Task DidNotCancelSubscription(SutProvider<CancelSubscriptionCommand> sutProvider)
        => sutProvider
            .GetDependency<IStripeAdapter>()
            .DidNotReceiveWithAnyArgs()
            .SubscriptionCancelAsync(Arg.Any<string>(), Arg.Any<SubscriptionCancelOptions>());

    private static Task DidNotUpdateSubscription(SutProvider<CancelSubscriptionCommand> sutProvider)
        => sutProvider
            .GetDependency<IStripeAdapter>()
            .DidNotReceiveWithAnyArgs()
            .SubscriptionUpdateAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());

    private static Task CancelledSubscriptionWith(
        SutProvider<CancelSubscriptionCommand> sutProvider,
        Expression<Predicate<SubscriptionCancelOptions>> predicate)
        => sutProvider
            .GetDependency<IStripeAdapter>()
            .Received(1)
            .SubscriptionCancelAsync(_subscriptionId, Arg.Is(predicate));

    private static Task UpdatedSubscriptionWith(
        SutProvider<CancelSubscriptionCommand> sutProvider,
        Expression<Predicate<SubscriptionUpdateOptions>> predicate)
        => sutProvider
            .GetDependency<IStripeAdapter>()
            .Received(1)
            .SubscriptionUpdateAsync(_subscriptionId, Arg.Is(predicate));

    private static async Task ThrowsContactSupportAsync(Func<Task> function)
    {
        const string message = "Something went wrong when trying to cancel your subscription. Please contact support.";

        var exception = await Assert.ThrowsAsync<GatewayException>(function);

        Assert.Equal(message, exception.Message);
    }
}
