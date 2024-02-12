using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Queries.Implementations;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Queries;

[SutProviderCustomize]
public class GetSubscriptionQueryTests
{
    [Theory, BitAutoData]
    public async Task GetSubscription_NullSubscriber_ThrowsArgumentNullException(
        SutProvider<GetSubscriptionQuery> sutProvider)
        => await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await sutProvider.Sut.GetSubscription(null));

    [Theory, BitAutoData]
    public async Task GetSubscription_Organization_NoGatewaySubscriptionId_ThrowsGatewayException(
        Organization organization,
        SutProvider<GetSubscriptionQuery> sutProvider)
    {
        organization.GatewaySubscriptionId = null;

        await ThrowsContactSupportAsync(async () => await sutProvider.Sut.GetSubscription(organization));
    }

    [Theory, BitAutoData]
    public async Task GetSubscription_Organization_NoSubscription_ThrowsGatewayException(
        Organization organization,
        SutProvider<GetSubscriptionQuery> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>().SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .ReturnsNull();

        await ThrowsContactSupportAsync(async () => await sutProvider.Sut.GetSubscription(organization));
    }

    [Theory, BitAutoData]
    public async Task GetSubscription_Organization_Succeeds(
        Organization organization,
        SutProvider<GetSubscriptionQuery> sutProvider)
    {
        var subscription = new Subscription();

        sutProvider.GetDependency<IStripeAdapter>().SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .Returns(subscription);

        var gotSubscription = await sutProvider.Sut.GetSubscription(organization);

        Assert.Equivalent(subscription, gotSubscription);
    }

    [Theory, BitAutoData]
    public async Task GetSubscription_User_NoGatewaySubscriptionId_ThrowsGatewayException(
        User user,
        SutProvider<GetSubscriptionQuery> sutProvider)
    {
        user.GatewaySubscriptionId = null;

        await ThrowsContactSupportAsync(async () => await sutProvider.Sut.GetSubscription(user));
    }

    [Theory, BitAutoData]
    public async Task GetSubscription_User_NoSubscription_ThrowsGatewayException(
        User user,
        SutProvider<GetSubscriptionQuery> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>().SubscriptionGetAsync(user.GatewaySubscriptionId)
            .ReturnsNull();

        await ThrowsContactSupportAsync(async () => await sutProvider.Sut.GetSubscription(user));
    }

    [Theory, BitAutoData]
    public async Task GetSubscription_User_Succeeds(
        User user,
        SutProvider<GetSubscriptionQuery> sutProvider)
    {
        var subscription = new Subscription();

        sutProvider.GetDependency<IStripeAdapter>().SubscriptionGetAsync(user.GatewaySubscriptionId)
            .Returns(subscription);

        var gotSubscription = await sutProvider.Sut.GetSubscription(user);

        Assert.Equivalent(subscription, gotSubscription);
    }

    private static async Task ThrowsContactSupportAsync(Func<Task> function)
    {
        const string message = "Something went wrong with your request. Please contact support.";

        var exception = await Assert.ThrowsAsync<GatewayException>(function);

        Assert.Equal(message, exception.Message);
    }
}
