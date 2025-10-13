using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Subscriptions.Commands;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Subscriptions;

using static StripeConstants;

public class RestartSubscriptionCommandTests
{
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IProviderRepository _providerRepository = Substitute.For<IProviderRepository>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly ISubscriberService _subscriberService = Substitute.For<ISubscriberService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly RestartSubscriptionCommand _command;

    public RestartSubscriptionCommandTests()
    {
        _command = new RestartSubscriptionCommand(
            _organizationRepository,
            _providerRepository,
            _stripeAdapter,
            _subscriberService,
            _userRepository);
    }

    [Fact]
    public async Task Run_SubscriptionNotCanceled_ReturnsBadRequest()
    {
        var organization = new Organization { Id = Guid.NewGuid() };

        var subscription = new Subscription { Status = SubscriptionStatus.Active };
        _subscriberService.GetSubscription(organization).Returns(subscription);

        var result = await _command.Run(organization);

        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("Cannot restart a subscription that is not canceled.", badRequest.Response);
    }

    [Fact]
    public async Task Run_NoExistingSubscription_ReturnsBadRequest()
    {
        var organization = new Organization { Id = Guid.NewGuid() };

        _subscriberService.GetSubscription(organization).Returns((Subscription)null);

        var result = await _command.Run(organization);

        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("Cannot restart a subscription that is not canceled.", badRequest.Response);
    }

    [Fact]
    public async Task Run_Organization_Success_ReturnsNone()
    {
        var organizationId = Guid.NewGuid();
        var organization = new Organization { Id = organizationId };
        var currentPeriodEnd = DateTime.UtcNow.AddMonths(1);

        var existingSubscription = new Subscription
        {
            Status = SubscriptionStatus.Canceled,
            CustomerId = "cus_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { Price = new Price { Id = "price_1" }, Quantity = 1 },
                    new SubscriptionItem { Price = new Price { Id = "price_2" }, Quantity = 2 }
                ]
            },
            Metadata = new Dictionary<string, string> { ["key"] = "value" }
        };

        var newSubscription = new Subscription
        {
            Id = "sub_new",
            CurrentPeriodEnd = currentPeriodEnd
        };

        _subscriberService.GetSubscription(organization).Returns(existingSubscription);
        _stripeAdapter.SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(newSubscription);

        var result = await _command.Run(organization);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).SubscriptionCreateAsync(Arg.Is((SubscriptionCreateOptions options) =>
            options.AutomaticTax.Enabled == true &&
            options.CollectionMethod == CollectionMethod.ChargeAutomatically &&
            options.Customer == "cus_123" &&
            options.Items.Count == 2 &&
            options.Items[0].Price == "price_1" &&
            options.Items[0].Quantity == 1 &&
            options.Items[1].Price == "price_2" &&
            options.Items[1].Quantity == 2 &&
            options.Metadata["key"] == "value" &&
            options.OffSession == true &&
            options.TrialPeriodDays == 0));

        await _organizationRepository.Received(1).ReplaceAsync(Arg.Is<Organization>(org =>
            org.Id == organizationId &&
            org.GatewaySubscriptionId == "sub_new" &&
            org.Enabled == true &&
            org.ExpirationDate == currentPeriodEnd));
    }

    [Fact]
    public async Task Run_Provider_Success_ReturnsNone()
    {
        var providerId = Guid.NewGuid();
        var provider = new Provider { Id = providerId };

        var existingSubscription = new Subscription
        {
            Status = SubscriptionStatus.Canceled,
            CustomerId = "cus_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { Price = new Price { Id = "price_1" }, Quantity = 1 }]
            },
            Metadata = new Dictionary<string, string>()
        };

        var newSubscription = new Subscription
        {
            Id = "sub_new",
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
        };

        _subscriberService.GetSubscription(provider).Returns(existingSubscription);
        _stripeAdapter.SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(newSubscription);

        var result = await _command.Run(provider);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>());

        await _providerRepository.Received(1).ReplaceAsync(Arg.Is<Provider>(prov =>
            prov.Id == providerId &&
            prov.GatewaySubscriptionId == "sub_new" &&
            prov.Enabled == true));
    }

    [Fact]
    public async Task Run_User_Success_ReturnsNone()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId };
        var currentPeriodEnd = DateTime.UtcNow.AddMonths(1);

        var existingSubscription = new Subscription
        {
            Status = SubscriptionStatus.Canceled,
            CustomerId = "cus_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { Price = new Price { Id = "price_1" }, Quantity = 1 }]
            },
            Metadata = new Dictionary<string, string>()
        };

        var newSubscription = new Subscription
        {
            Id = "sub_new",
            CurrentPeriodEnd = currentPeriodEnd
        };

        _subscriberService.GetSubscription(user).Returns(existingSubscription);
        _stripeAdapter.SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(newSubscription);

        var result = await _command.Run(user);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>());

        await _userRepository.Received(1).ReplaceAsync(Arg.Is<User>(u =>
            u.Id == userId &&
            u.GatewaySubscriptionId == "sub_new" &&
            u.Premium == true &&
            u.PremiumExpirationDate == currentPeriodEnd));
    }
}
