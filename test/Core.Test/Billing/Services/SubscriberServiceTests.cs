using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Services.Implementations;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.ReturnsExtensions;
using Stripe;
using Xunit;

using static Bit.Core.Test.Billing.Utilities;

namespace Bit.Core.Test.Billing.Services;

[SutProviderCustomize]
public class SubscriberServiceTests
{
    #region CancelSubscription
    [Theory, BitAutoData]
    public async Task CancelSubscription_SubscriptionInactive_ContactSupport(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        var subscription = new Subscription
        {
            Status = "canceled"
        };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        stripeAdapter
            .SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .Returns(subscription);

        await ThrowsContactSupportAsync(() =>
            sutProvider.Sut.CancelSubscription(organization, new OffboardingSurveyResponse(), false));

        await stripeAdapter
            .DidNotReceiveWithAnyArgs()
            .SubscriptionUpdateAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());

        await stripeAdapter
            .DidNotReceiveWithAnyArgs()
            .SubscriptionCancelAsync(Arg.Any<string>(), Arg.Any<SubscriptionCancelOptions>());
    }

    [Theory, BitAutoData]
    public async Task CancelSubscription_CancelImmediately_BelongsToOrganization_UpdatesSubscription_CancelSubscriptionImmediately(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        var userId = Guid.NewGuid();

        const string subscriptionId = "subscription_id";

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = "active",
            Metadata = new Dictionary<string, string>
            {
                { "organizationId", "organization_id" }
            }
        };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        stripeAdapter
            .SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .Returns(subscription);

        var offboardingSurveyResponse = new OffboardingSurveyResponse
        {
            UserId = userId,
            Reason = "missing_features",
            Feedback = "Lorem ipsum"
        };

        await sutProvider.Sut.CancelSubscription(organization, offboardingSurveyResponse, true);

        await stripeAdapter
            .Received(1)
            .SubscriptionUpdateAsync(subscriptionId, Arg.Is<SubscriptionUpdateOptions>(
                options => options.Metadata["cancellingUserId"] == userId.ToString()));

        await stripeAdapter
            .Received(1)
            .SubscriptionCancelAsync(subscriptionId, Arg.Is<SubscriptionCancelOptions>(options =>
                options.CancellationDetails.Comment == offboardingSurveyResponse.Feedback &&
                options.CancellationDetails.Feedback == offboardingSurveyResponse.Reason));
    }

    [Theory, BitAutoData]
    public async Task CancelSubscription_CancelImmediately_BelongsToUser_CancelSubscriptionImmediately(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        var userId = Guid.NewGuid();

        const string subscriptionId = "subscription_id";

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = "active",
            Metadata = new Dictionary<string, string>
            {
                { "userId", "user_id" }
            }
        };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        stripeAdapter
            .SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .Returns(subscription);

        var offboardingSurveyResponse = new OffboardingSurveyResponse
        {
            UserId = userId,
            Reason = "missing_features",
            Feedback = "Lorem ipsum"
        };

        await sutProvider.Sut.CancelSubscription(organization, offboardingSurveyResponse, true);

        await stripeAdapter
            .DidNotReceiveWithAnyArgs()
            .SubscriptionUpdateAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());

        await stripeAdapter
            .Received(1)
            .SubscriptionCancelAsync(subscriptionId, Arg.Is<SubscriptionCancelOptions>(options =>
                options.CancellationDetails.Comment == offboardingSurveyResponse.Feedback &&
                options.CancellationDetails.Feedback == offboardingSurveyResponse.Reason));
    }

    [Theory, BitAutoData]
    public async Task CancelSubscription_DoNotCancelImmediately_UpdateSubscriptionToCancelAtEndOfPeriod(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        var userId = Guid.NewGuid();

        const string subscriptionId = "subscription_id";

        organization.ExpirationDate = DateTime.UtcNow.AddDays(5);

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = "active"
        };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        stripeAdapter
            .SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .Returns(subscription);

        var offboardingSurveyResponse = new OffboardingSurveyResponse
        {
            UserId = userId,
            Reason = "missing_features",
            Feedback = "Lorem ipsum"
        };

        await sutProvider.Sut.CancelSubscription(organization, offboardingSurveyResponse, false);

        await stripeAdapter
            .Received(1)
            .SubscriptionUpdateAsync(subscriptionId, Arg.Is<SubscriptionUpdateOptions>(options =>
                options.CancelAtPeriodEnd == true &&
                options.CancellationDetails.Comment == offboardingSurveyResponse.Feedback &&
                options.CancellationDetails.Feedback == offboardingSurveyResponse.Reason &&
                options.Metadata["cancellingUserId"] == userId.ToString()));

        await stripeAdapter
            .DidNotReceiveWithAnyArgs()
            .SubscriptionCancelAsync(Arg.Any<string>(), Arg.Any<SubscriptionCancelOptions>());;
    }
    #endregion

    #region GetCustomer
    [Theory, BitAutoData]
    public async Task GetCustomer_NullSubscriber_ThrowsArgumentNullException(
        SutProvider<SubscriberService> sutProvider)
        => await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await sutProvider.Sut.GetCustomer(null));

    [Theory, BitAutoData]
    public async Task GetCustomer_NoGatewayCustomerId_ReturnsNull(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        organization.GatewayCustomerId = null;

        var customer = await sutProvider.Sut.GetCustomer(organization);

        Assert.Null(customer);
    }

    [Theory, BitAutoData]
    public async Task GetCustomer_NoCustomer_ReturnsNull(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(organization.GatewayCustomerId)
            .ReturnsNull();

        var customer = await sutProvider.Sut.GetCustomer(organization);

        Assert.Null(customer);
    }

    [Theory, BitAutoData]
    public async Task GetCustomer_StripeException_ReturnsNull(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(organization.GatewayCustomerId)
            .ThrowsAsync<StripeException>();

        var customer = await sutProvider.Sut.GetCustomer(organization);

        Assert.Null(customer);
    }

    [Theory, BitAutoData]
    public async Task GetCustomer_Succeeds(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        var customer = new Customer();

        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(organization.GatewayCustomerId)
            .Returns(customer);

        var gotCustomer = await sutProvider.Sut.GetCustomer(organization);

        Assert.Equivalent(customer, gotCustomer);
    }
    #endregion

    #region GetCustomerOrThrow
    [Theory, BitAutoData]
    public async Task GetCustomerOrThrow_NullSubscriber_ThrowsArgumentNullException(
        SutProvider<SubscriberService> sutProvider)
        => await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await sutProvider.Sut.GetCustomerOrThrow(null));

    [Theory, BitAutoData]
    public async Task GetCustomerOrThrow_NoGatewayCustomerId_ContactSupport(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        organization.GatewayCustomerId = null;

        await ThrowsContactSupportAsync(async () => await sutProvider.Sut.GetCustomerOrThrow(organization));
    }

    [Theory, BitAutoData]
    public async Task GetCustomerOrThrow_NoCustomer_ContactSupport(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(organization.GatewayCustomerId)
            .ReturnsNull();

        await ThrowsContactSupportAsync(async () => await sutProvider.Sut.GetCustomerOrThrow(organization));
    }

    [Theory, BitAutoData]
    public async Task GetCustomerOrThrow_StripeException_ContactSupport(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        var stripeException = new StripeException();

        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(organization.GatewayCustomerId)
            .ThrowsAsync(stripeException);

        await ThrowsContactSupportAsync(
            async () => await sutProvider.Sut.GetCustomerOrThrow(organization),
            "An error occurred while trying to retrieve a Stripe Customer",
            stripeException);
    }

    [Theory, BitAutoData]
    public async Task GetCustomerOrThrow_Succeeds(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        var customer = new Customer();

        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(organization.GatewayCustomerId)
            .Returns(customer);

        var gotCustomer = await sutProvider.Sut.GetCustomerOrThrow(organization);

        Assert.Equivalent(customer, gotCustomer);
    }
    #endregion

    #region GetSubscription
    [Theory, BitAutoData]
    public async Task GetSubscription_NullSubscriber_ThrowsArgumentNullException(
        SutProvider<SubscriberService> sutProvider)
        => await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await sutProvider.Sut.GetSubscription(null));

    [Theory, BitAutoData]
    public async Task GetSubscription_NoGatewaySubscriptionId_ReturnsNull(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        organization.GatewaySubscriptionId = null;

        var subscription = await sutProvider.Sut.GetSubscription(organization);

        Assert.Null(subscription);
    }

    [Theory, BitAutoData]
    public async Task GetSubscription_NoSubscription_ReturnsNull(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>()
            .SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .ReturnsNull();

        var subscription = await sutProvider.Sut.GetSubscription(organization);

        Assert.Null(subscription);
    }

    [Theory, BitAutoData]
    public async Task GetSubscription_StripeException_ReturnsNull(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>()
            .SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .ThrowsAsync<StripeException>();

        var subscription = await sutProvider.Sut.GetSubscription(organization);

        Assert.Null(subscription);
    }

    [Theory, BitAutoData]
    public async Task GetSubscription_Succeeds(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        var subscription = new Subscription();

        sutProvider.GetDependency<IStripeAdapter>()
            .SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .Returns(subscription);

        var gotSubscription = await sutProvider.Sut.GetSubscription(organization);

        Assert.Equivalent(subscription, gotSubscription);
    }
    #endregion

    #region GetSubscriptionOrThrow
    [Theory, BitAutoData]
    public async Task GetSubscriptionOrThrow_NullSubscriber_ThrowsArgumentNullException(
        SutProvider<SubscriberService> sutProvider)
        => await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await sutProvider.Sut.GetSubscriptionOrThrow(null));

    [Theory, BitAutoData]
    public async Task GetSubscriptionOrThrow_NoGatewaySubscriptionId_ContactSupport(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        organization.GatewaySubscriptionId = null;

        await ThrowsContactSupportAsync(async () => await sutProvider.Sut.GetSubscriptionOrThrow(organization));
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionOrThrow_NoSubscription_ContactSupport(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>()
            .SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .ReturnsNull();

        await ThrowsContactSupportAsync(async () => await sutProvider.Sut.GetSubscriptionOrThrow(organization));
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionOrThrow_StripeException_ContactSupport(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        var stripeException = new StripeException();

        sutProvider.GetDependency<IStripeAdapter>()
            .SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .ThrowsAsync(stripeException);

        await ThrowsContactSupportAsync(
            async () => await sutProvider.Sut.GetSubscriptionOrThrow(organization),
            "An error occurred while trying to retrieve a Stripe Subscription",
            stripeException);
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionOrThrow_Succeeds(
        Organization organization,
        SutProvider<SubscriberService> sutProvider)
    {
        var subscription = new Subscription();

        sutProvider.GetDependency<IStripeAdapter>()
            .SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .Returns(subscription);

        var gotSubscription = await sutProvider.Sut.GetSubscriptionOrThrow(organization);

        Assert.Equivalent(subscription, gotSubscription);
    }
    #endregion
}
