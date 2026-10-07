using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Tax.Services;
using Bit.Core.Services;
using Bit.Subscriptions.Organization.Handlers;
using Bit.Subscriptions.Organization.Queries;
using Bitwarden.Server.Sdk.Environment;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Bit.Subscriptions.Organization.Test;

public class SubscriptionsOrganizationServiceCollectionExtensionsTests
{
    [Fact]
    public void AddOrganizationSubscriptions_RegistersThePurchasePreviewQueryAndHandler()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IStripeAdapter>());
        services.AddSingleton(Substitute.For<IPricingClient>());
        services.AddSingleton(Substitute.For<ISubscriptionDiscountService>());
        services.AddSingleton(Substitute.For<ITaxService>());
        services.AddSingleton(Substitute.For<IUserService>());
        services.AddSingleton(Substitute.For<IBitwardenEnvironment>());
        services.AddLogging();

        services.AddOrganizationSubscriptions();
        using var scope = services.BuildServiceProvider().CreateScope();

        Assert.IsType<PreviewOrganizationSubscriptionPurchaseQuery>(
            scope.ServiceProvider.GetService<IPreviewOrganizationSubscriptionPurchaseQuery>());
        Assert.NotNull(scope.ServiceProvider.GetService<PreviewOrganizationSubscriptionPurchaseHandler>());
    }
}
