using Bit.Core.AdminConsole.Entities;
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
    #endregion
}
