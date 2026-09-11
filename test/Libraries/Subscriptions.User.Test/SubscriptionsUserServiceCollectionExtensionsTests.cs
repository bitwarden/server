using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Services;
using Bit.Subscriptions.User.Commands;
using Bit.Subscriptions.User.Handlers;
using Bitwarden.Server.Sdk.Environment;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Bit.Subscriptions.User.Test;

public class SubscriptionsUserServiceCollectionExtensionsTests
{
    [Fact]
    public void AddUserSubscriptions_RegistersTheCommandAndHandler()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IStripeAdapter>());
        services.AddSingleton(Substitute.For<IPricingClient>());
        services.AddSingleton(Substitute.For<IUserService>());
        services.AddSingleton(Substitute.For<IBitwardenEnvironment>());
        services.AddLogging();

        services.AddUserSubscriptions();
        using var scope = services.BuildServiceProvider().CreateScope();

        Assert.IsType<PreviewPremiumUpgradeCommand>(scope.ServiceProvider.GetService<IPreviewPremiumUpgradeCommand>());
        Assert.NotNull(scope.ServiceProvider.GetService<UserSubscriptionEndpointsHandler>());
    }
}
