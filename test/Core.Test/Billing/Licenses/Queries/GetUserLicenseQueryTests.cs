using Bit.Core.Billing.Licenses.Queries;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Licenses.Queries;

[SubscriptionInfoCustomize]
[SutProviderCustomize]
public class GetUserLicenseQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task RunAsync_CanceledSubscription_Throws(
        SutProvider<GetUserLicenseQuery> sutProvider,
        User user, SubscriptionInfo subInfo)
    {
        subInfo.Subscription = new SubscriptionInfo.BillingSubscription(new Subscription { Status = "canceled" });

        sutProvider.GetDependency<IStripePaymentService>().GetSubscriptionAsync(user).Returns(subInfo);

        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.Run(user));
        Assert.Contains("Unable to generate license due to a payment issue", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_IncompleteSubscription_Throws(
        SutProvider<GetUserLicenseQuery> sutProvider,
        User user, SubscriptionInfo subInfo)
    {
        subInfo.Subscription = new SubscriptionInfo.BillingSubscription(new Subscription { Status = "incomplete" });

        sutProvider.GetDependency<IStripePaymentService>().GetSubscriptionAsync(user).Returns(subInfo);

        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.Run(user));
        Assert.Contains("Unable to generate license due to a payment issue", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_IncompleteExpiredSubscription_Throws(
        SutProvider<GetUserLicenseQuery> sutProvider,
        User user, SubscriptionInfo subInfo)
    {
        subInfo.Subscription = new SubscriptionInfo.BillingSubscription(new Subscription { Status = "incomplete_expired" });

        sutProvider.GetDependency<IStripePaymentService>().GetSubscriptionAsync(user).Returns(subInfo);

        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.Run(user));
        Assert.Contains("Unable to generate license due to a payment issue", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_NullSubscription_Throws(
        SutProvider<GetUserLicenseQuery> sutProvider,
        User user, SubscriptionInfo subInfo)
    {
        subInfo.Subscription = null;

        sutProvider.GetDependency<IStripePaymentService>().GetSubscriptionAsync(user).Returns(subInfo);

        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.Run(user));
        Assert.Contains("No active subscription found", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_ActiveSubscription_Succeeds(
        SutProvider<GetUserLicenseQuery> sutProvider,
        User user, SubscriptionInfo subInfo, UserLicense userLicense)
    {
        subInfo.Subscription = new SubscriptionInfo.BillingSubscription(new Subscription { Status = "active" });

        sutProvider.GetDependency<IStripePaymentService>().GetSubscriptionAsync(user).Returns(subInfo);
        sutProvider.GetDependency<IUserService>().GenerateLicenseAsync(user).Returns(userLicense);

        var result = await sutProvider.Sut.Run(user);

        Assert.NotNull(result);
        Assert.Equal(userLicense, result);
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_TrialingSubscription_Succeeds(
        SutProvider<GetUserLicenseQuery> sutProvider,
        User user, SubscriptionInfo subInfo, UserLicense userLicense)
    {
        subInfo.Subscription = new SubscriptionInfo.BillingSubscription(new Subscription { Status = "trialing" });

        sutProvider.GetDependency<IStripePaymentService>().GetSubscriptionAsync(user).Returns(subInfo);
        sutProvider.GetDependency<IUserService>().GenerateLicenseAsync(user).Returns(userLicense);

        var result = await sutProvider.Sut.Run(user);

        Assert.NotNull(result);
        Assert.Equal(userLicense, result);
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_PastDueSubscription_Succeeds(
        SutProvider<GetUserLicenseQuery> sutProvider,
        User user, SubscriptionInfo subInfo, UserLicense userLicense)
    {
        subInfo.Subscription = new SubscriptionInfo.BillingSubscription(new Subscription { Status = "past_due" });

        sutProvider.GetDependency<IStripePaymentService>().GetSubscriptionAsync(user).Returns(subInfo);
        sutProvider.GetDependency<IUserService>().GenerateLicenseAsync(user).Returns(userLicense);

        var result = await sutProvider.Sut.Run(user);

        Assert.NotNull(result);
        Assert.Equal(userLicense, result);
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_NullSubscriptionInfo_Throws(
        SutProvider<GetUserLicenseQuery> sutProvider,
        User user)
    {
        sutProvider.GetDependency<IStripePaymentService>().GetSubscriptionAsync(user)
            .Returns((SubscriptionInfo)null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.Run(user));
        Assert.Contains("No active subscription found", exception.Message);
    }
}
