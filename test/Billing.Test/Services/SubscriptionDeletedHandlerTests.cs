using Bit.Billing.Constants;
using Bit.Billing.Services;
using Bit.Billing.Services.Implementations;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
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
    private readonly SubscriptionDeletedHandler _sut;

    public SubscriptionDeletedHandlerTests()
    {
        _stripeEventService = Substitute.For<IStripeEventService>();
        _userService = Substitute.For<IUserService>();
        _stripeEventUtilityService = Substitute.For<IStripeEventUtilityService>();
        _organizationDisableCommand = Substitute.For<IOrganizationDisableCommand>();
        _sut = new SubscriptionDeletedHandler(
            _stripeEventService,
            _userService,
            _stripeEventUtilityService,
            _organizationDisableCommand);
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
}
