using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Billing.Queries.Implementations;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Stripe;
using Xunit;

using static Bit.Core.Test.Billing.Utilities;

namespace Bit.Core.Test.Billing.Queries;

[SutProviderCustomize]
public class SubscriberQueriesTests
{
    #region GetSubscription
    [Theory, BitAutoData]
    public async Task GetSubscription_NullSubscriber_ThrowsArgumentNullException(
        SutProvider<SubscriberQueries> sutProvider)
        => await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await sutProvider.Sut.GetSubscription(null));

    [Theory, BitAutoData]
    public async Task GetSubscription_Organization_NoGatewaySubscriptionId_ReturnsNull(
        Organization organization,
        SutProvider<SubscriberQueries> sutProvider)
    {
        organization.GatewaySubscriptionId = null;

        var gotSubscription = await sutProvider.Sut.GetSubscription(organization);

        Assert.Null(gotSubscription);
    }

    [Theory, BitAutoData]
    public async Task GetSubscription_Organization_NoSubscription_ReturnsNull(
        Organization organization,
        SutProvider<SubscriberQueries> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>().SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .ReturnsNull();

        var gotSubscription = await sutProvider.Sut.GetSubscription(organization);

        Assert.Null(gotSubscription);
    }

    [Theory, BitAutoData]
    public async Task GetSubscription_Organization_Succeeds(
        Organization organization,
        SutProvider<SubscriberQueries> sutProvider)
    {
        var subscription = new Subscription();

        sutProvider.GetDependency<IStripeAdapter>().SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .Returns(subscription);

        var gotSubscription = await sutProvider.Sut.GetSubscription(organization);

        Assert.Equivalent(subscription, gotSubscription);
    }

    [Theory, BitAutoData]
    public async Task GetSubscription_User_NoGatewaySubscriptionId_ReturnsNull(
        User user,
        SutProvider<SubscriberQueries> sutProvider)
    {
        user.GatewaySubscriptionId = null;

        var gotSubscription = await sutProvider.Sut.GetSubscription(user);

        Assert.Null(gotSubscription);
    }

    [Theory, BitAutoData]
    public async Task GetSubscription_User_NoSubscription_ReturnsNull(
        User user,
        SutProvider<SubscriberQueries> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>().SubscriptionGetAsync(user.GatewaySubscriptionId)
            .ReturnsNull();

        var gotSubscription = await sutProvider.Sut.GetSubscription(user);

        Assert.Null(gotSubscription);
    }

    [Theory, BitAutoData]
    public async Task GetSubscription_User_Succeeds(
        User user,
        SutProvider<SubscriberQueries> sutProvider)
    {
        var subscription = new Subscription();

        sutProvider.GetDependency<IStripeAdapter>().SubscriptionGetAsync(user.GatewaySubscriptionId)
            .Returns(subscription);

        var gotSubscription = await sutProvider.Sut.GetSubscription(user);

        Assert.Equivalent(subscription, gotSubscription);
    }

    [Theory, BitAutoData]
    public async Task GetSubscription_Provider_NoGatewaySubscriptionId_ReturnsNull(
        Provider provider,
        SutProvider<SubscriberQueries> sutProvider)
    {
        provider.GatewaySubscriptionId = null;

        var gotSubscription = await sutProvider.Sut.GetSubscription(provider);

        Assert.Null(gotSubscription);
    }

    [Theory, BitAutoData]
    public async Task GetSubscription_Provider_NoSubscription_ReturnsNull(
        Provider provider,
        SutProvider<SubscriberQueries> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>().SubscriptionGetAsync(provider.GatewaySubscriptionId)
            .ReturnsNull();

        var gotSubscription = await sutProvider.Sut.GetSubscription(provider);

        Assert.Null(gotSubscription);
    }

    [Theory, BitAutoData]
    public async Task GetSubscription_Provider_Succeeds(
        Provider provider,
        SutProvider<SubscriberQueries> sutProvider)
    {
        var subscription = new Subscription();

        sutProvider.GetDependency<IStripeAdapter>().SubscriptionGetAsync(provider.GatewaySubscriptionId)
            .Returns(subscription);

        var gotSubscription = await sutProvider.Sut.GetSubscription(provider);

        Assert.Equivalent(subscription, gotSubscription);
    }
    #endregion

    #region GetSubscriptionOrThrow
    [Theory, BitAutoData]
    public async Task GetSubscriptionOrThrow_NullSubscriber_ThrowsArgumentNullException(
        SutProvider<SubscriberQueries> sutProvider)
        => await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await sutProvider.Sut.GetSubscriptionOrThrow(null));

    [Theory, BitAutoData]
    public async Task GetSubscriptionOrThrow_Organization_NoGatewaySubscriptionId_ThrowsGatewayException(
        Organization organization,
        SutProvider<SubscriberQueries> sutProvider)
    {
        organization.GatewaySubscriptionId = null;

        await ThrowsContactSupportAsync(async () => await sutProvider.Sut.GetSubscriptionOrThrow(organization));
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionOrThrow_Organization_NoSubscription_ThrowsGatewayException(
        Organization organization,
        SutProvider<SubscriberQueries> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>().SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .ReturnsNull();

        await ThrowsContactSupportAsync(async () => await sutProvider.Sut.GetSubscriptionOrThrow(organization));
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionOrThrow_Organization_Succeeds(
        Organization organization,
        SutProvider<SubscriberQueries> sutProvider)
    {
        var subscription = new Subscription();

        sutProvider.GetDependency<IStripeAdapter>().SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .Returns(subscription);

        var gotSubscription = await sutProvider.Sut.GetSubscriptionOrThrow(organization);

        Assert.Equivalent(subscription, gotSubscription);
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionOrThrow_User_NoGatewaySubscriptionId_ThrowsGatewayException(
        User user,
        SutProvider<SubscriberQueries> sutProvider)
    {
        user.GatewaySubscriptionId = null;

        await ThrowsContactSupportAsync(async () => await sutProvider.Sut.GetSubscriptionOrThrow(user));
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionOrThrow_User_NoSubscription_ThrowsGatewayException(
        User user,
        SutProvider<SubscriberQueries> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>().SubscriptionGetAsync(user.GatewaySubscriptionId)
            .ReturnsNull();

        await ThrowsContactSupportAsync(async () => await sutProvider.Sut.GetSubscriptionOrThrow(user));
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionOrThrow_User_Succeeds(
        User user,
        SutProvider<SubscriberQueries> sutProvider)
    {
        var subscription = new Subscription();

        sutProvider.GetDependency<IStripeAdapter>().SubscriptionGetAsync(user.GatewaySubscriptionId)
            .Returns(subscription);

        var gotSubscription = await sutProvider.Sut.GetSubscriptionOrThrow(user);

        Assert.Equivalent(subscription, gotSubscription);
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionOrThrow_Provider_NoGatewaySubscriptionId_ThrowsGatewayException(
        Provider provider,
        SutProvider<SubscriberQueries> sutProvider)
    {
        provider.GatewaySubscriptionId = null;

        await ThrowsContactSupportAsync(async () => await sutProvider.Sut.GetSubscriptionOrThrow(provider));
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionOrThrow_Provider_NoSubscription_ThrowsGatewayException(
        Provider provider,
        SutProvider<SubscriberQueries> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>().SubscriptionGetAsync(provider.GatewaySubscriptionId)
            .ReturnsNull();

        await ThrowsContactSupportAsync(async () => await sutProvider.Sut.GetSubscriptionOrThrow(provider));
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionOrThrow_Provider_Succeeds(
        Provider provider,
        SutProvider<SubscriberQueries> sutProvider)
    {
        var subscription = new Subscription();

        sutProvider.GetDependency<IStripeAdapter>().SubscriptionGetAsync(provider.GatewaySubscriptionId)
            .Returns(subscription);

        var gotSubscription = await sutProvider.Sut.GetSubscriptionOrThrow(provider);

        Assert.Equivalent(subscription, gotSubscription);
    }
    #endregion
}
