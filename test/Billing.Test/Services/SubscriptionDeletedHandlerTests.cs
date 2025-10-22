using Bit.Billing.Constants;
using Bit.Billing.Services;
using Bit.Billing.Services.Implementations;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Extensions;
using Bit.Core.Services;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Billing.Test.Services;

public class SubscriptionDeletedHandlerTests
{
    private readonly IStripeEventService _stripeEventService;
    private readonly IUserService _userService;
    private readonly IStripeEventUtilityService _stripeEventUtilityService;
    private readonly IOrganizationDisableCommand _organizationDisableCommand;
    private readonly IProviderRepository _providerRepository;
    private readonly IProviderService _providerService;
    private readonly IProviderOrganizationRepository _providerOrganizationRepository;
    private readonly SubscriptionDeletedHandler _sut;

    public SubscriptionDeletedHandlerTests()
    {
        _stripeEventService = Substitute.For<IStripeEventService>();
        _userService = Substitute.For<IUserService>();
        _stripeEventUtilityService = Substitute.For<IStripeEventUtilityService>();
        _organizationDisableCommand = Substitute.For<IOrganizationDisableCommand>();
        _providerRepository = Substitute.For<IProviderRepository>();
        _providerService = Substitute.For<IProviderService>();
        _providerOrganizationRepository = Substitute.For<IProviderOrganizationRepository>();
        _sut = new SubscriptionDeletedHandler(
            _stripeEventService,
            _userService,
            _stripeEventUtilityService,
            _organizationDisableCommand,
            _providerRepository,
            _providerService,
            _providerOrganizationRepository);
    }

    [Fact]
    public async Task HandleAsync_SubscriptionNotCanceled_DoesNothing()
    {
        // Arrange
        var stripeEvent = new Event();
        var subscription = new Subscription
        {
            Status = "active",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }
                ]
            },
            Metadata = new Dictionary<string, string>()
        };

        _stripeEventService.GetSubscription(stripeEvent, true).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, null, null));

        // Act
        await _sut.HandleAsync(stripeEvent);

        // Assert
        await _organizationDisableCommand.DidNotReceiveWithAnyArgs().DisableAsync(default, default);
        await _userService.DidNotReceiveWithAnyArgs().DisablePremiumAsync(default, default);
        await _providerService.DidNotReceiveWithAnyArgs().UpdateAsync(default);
    }

    [Fact]
    public async Task HandleAsync_OrganizationSubscriptionCanceled_DisablesOrganization()
    {
        // Arrange
        var stripeEvent = new Event();
        var organizationId = Guid.NewGuid();
        var subscription = new Subscription
        {
            Status = StripeSubscriptionStatus.Canceled,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } }
        };

        _stripeEventService.GetSubscription(stripeEvent, true).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(organizationId, null, null));

        // Act
        await _sut.HandleAsync(stripeEvent);

        // Assert
        await _organizationDisableCommand.Received(1)
            .DisableAsync(organizationId, subscription.GetCurrentPeriodEnd());
    }

    [Fact]
    public async Task HandleAsync_UserSubscriptionCanceled_DisablesUserPremium()
    {
        // Arrange
        var stripeEvent = new Event();
        var userId = Guid.NewGuid();
        var subscription = new Subscription
        {
            Status = StripeSubscriptionStatus.Canceled,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }
                ]
            },
            Metadata = new Dictionary<string, string> { { "userId", userId.ToString() } }
        };

        _stripeEventService.GetSubscription(stripeEvent, true).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, userId, null));

        // Act
        await _sut.HandleAsync(stripeEvent);

        // Assert
        await _userService.Received(1)
            .DisablePremiumAsync(userId, subscription.GetCurrentPeriodEnd());
    }

    [Fact]
    public async Task HandleAsync_ProviderMigrationCancellation_DoesNotDisableOrganization()
    {
        // Arrange
        var stripeEvent = new Event();
        var organizationId = Guid.NewGuid();
        var subscription = new Subscription
        {
            Status = StripeSubscriptionStatus.Canceled,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            CancellationDetails = new SubscriptionCancellationDetails
            {
                Comment = "Cancelled as part of provider migration to Consolidated Billing"
            }
        };

        _stripeEventService.GetSubscription(stripeEvent, true).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(organizationId, null, null));

        // Act
        await _sut.HandleAsync(stripeEvent);

        // Assert
        await _organizationDisableCommand.DidNotReceiveWithAnyArgs()
            .DisableAsync(default, default);
    }

    [Fact]
    public async Task HandleAsync_AddedToProviderCancellation_DoesNotDisableOrganization()
    {
        // Arrange
        var stripeEvent = new Event();
        var organizationId = Guid.NewGuid();
        var subscription = new Subscription
        {
            Status = StripeSubscriptionStatus.Canceled,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            CancellationDetails = new SubscriptionCancellationDetails
            {
                Comment = "Organization was added to Provider"
            }
        };

        _stripeEventService.GetSubscription(stripeEvent, true).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(organizationId, null, null));

        // Act
        await _sut.HandleAsync(stripeEvent);

        // Assert
        await _organizationDisableCommand.DidNotReceiveWithAnyArgs()
            .DisableAsync(default, default);
    }

    [Fact]
    public async Task HandleAsync_ProviderSubscriptionCanceled_DisablesProvider()
    {
        // Arrange
        var stripeEvent = new Event();
        var providerId = Guid.NewGuid();
        var provider = new Provider
        {
            Id = providerId,
            Enabled = true
        };
        var subscription = new Subscription
        {
            Status = StripeSubscriptionStatus.Canceled,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }
                ]
            },
            Metadata = new Dictionary<string, string> { { "providerId", providerId.ToString() } }
        };

        _stripeEventService.GetSubscription(stripeEvent, true).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, null, providerId));
        _providerRepository.GetByIdAsync(providerId).Returns(provider);

        // Act
        await _sut.HandleAsync(stripeEvent);

        // Assert
        Assert.False(provider.Enabled);
        await _providerService.Received(1).UpdateAsync(provider);
    }

    [Fact]
    public async Task HandleAsync_ProviderSubscriptionCanceled_ProviderNotFound_DoesNotThrow()
    {
        // Arrange
        var stripeEvent = new Event();
        var providerId = Guid.NewGuid();
        var subscription = new Subscription
        {
            Status = StripeSubscriptionStatus.Canceled,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }
                ]
            },
            Metadata = new Dictionary<string, string> { { "providerId", providerId.ToString() } }
        };

        _stripeEventService.GetSubscription(stripeEvent, true).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, null, providerId));
        _providerRepository.GetByIdAsync(providerId).Returns((Provider)null);

        // Act & Assert - Should not throw
        await _sut.HandleAsync(stripeEvent);

        // Assert
        await _providerService.DidNotReceiveWithAnyArgs().UpdateAsync(default);
    }

    [Fact]
    public async Task HandleAsync_ProviderSubscriptionCanceled_DisablesClientOrganizations()
    {
        // Arrange
        var stripeEvent = new Event();
        var providerId = Guid.NewGuid();
        var org1Id = Guid.NewGuid();
        var org2Id = Guid.NewGuid();
        var provider = new Provider
        {
            Id = providerId,
            Enabled = true
        };
        var subscription = new Subscription
        {
            Status = StripeSubscriptionStatus.Canceled,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }
                ]
            },
            Metadata = new Dictionary<string, string> { { "providerId", providerId.ToString() } }
        };

        var providerOrganizations = new List<ProviderOrganizationOrganizationDetails>
        {
            new() { OrganizationId = org1Id },
            new() { OrganizationId = org2Id }
        };

        _stripeEventService.GetSubscription(stripeEvent, true).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, null, providerId));
        _providerRepository.GetByIdAsync(providerId).Returns(provider);
        _providerOrganizationRepository.GetManyDetailsByProviderAsync(providerId).Returns(providerOrganizations);

        // Act
        await _sut.HandleAsync(stripeEvent);

        // Assert
        Assert.False(provider.Enabled);
        await _providerService.Received(1).UpdateAsync(provider);
        await _organizationDisableCommand.Received(1).DisableAsync(org1Id, subscription.GetCurrentPeriodEnd());
        await _organizationDisableCommand.Received(1).DisableAsync(org2Id, subscription.GetCurrentPeriodEnd());
    }
}
