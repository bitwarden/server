using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Tax.Services;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Subscriptions.Organization.Commands;
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

    [Fact]
    public void AddOrganizationSubscriptions_RegistersTheAutoscalePrivilegedControlsSeatsCommandAsScoped()
    {
        var services = new ServiceCollection();

        services.AddOrganizationSubscriptions();

        var descriptor = Assert.Single(
            services, d => d.ServiceType == typeof(IAutoscalePrivilegedControlsSeatsCommand));
        Assert.Equal(typeof(AutoscalePrivilegedControlsSeatsCommand), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddOrganizationSubscriptions_RegistersPurchasePrivilegedControlsHandler()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IStripeAdapter>());
        services.AddSingleton(Substitute.For<IPricingClient>());
        services.AddSingleton(Substitute.For<ISubscriptionDiscountService>());
        services.AddSingleton(Substitute.For<ITaxService>());
        services.AddSingleton(Substitute.For<IUserService>());
        services.AddSingleton(Substitute.For<IBitwardenEnvironment>());
        services.AddSingleton(Substitute.For<IOrganizationRepository>());
        services.AddSingleton(Substitute.For<IOrganizationUserRepository>());
        services.AddSingleton(Substitute.For<IStripePaymentService>());
        services.AddSingleton(Substitute.For<IOrganizationService>());
        services.AddSingleton(Substitute.For<IUpdateOrganizationSubscriptionCommand>());
        services.AddLogging();

        services.AddOrganizationSubscriptions();
        using var scope = services.BuildServiceProvider().CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<PurchasePrivilegedControlsHandler>());
    }
}
