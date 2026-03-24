using Bit.Billing.Services;
using Bit.Billing.Services.Implementations;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Models.BitStripe;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.Billing.Mocks;
using Bit.Core.Test.Billing.Mocks.Plans;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Stripe;
using Xunit;
using static Bit.Core.Billing.Constants.StripeConstants;
using Event = Stripe.Event;

namespace Bit.Billing.Test.Services;

public class SubscriptionUpdatedHandlerTests
{
    private readonly IStripeEventService _stripeEventService;
    private readonly IStripeEventUtilityService _stripeEventUtilityService;
    private readonly IOrganizationService _organizationService;
    private readonly IStripeAdapter _stripeAdapter;
    private readonly IOrganizationSponsorshipRenewCommand _organizationSponsorshipRenewCommand;
    private readonly IUserService _userService;
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationEnableCommand _organizationEnableCommand;
    private readonly IOrganizationDisableCommand _organizationDisableCommand;
    private readonly IPricingClient _pricingClient;
    private readonly IProviderRepository _providerRepository;
    private readonly IProviderService _providerService;
    private readonly IPushNotificationAdapter _pushNotificationAdapter;
    private readonly IFeatureService _featureService;
    private readonly SubscriptionUpdatedHandler _sut;

    public SubscriptionUpdatedHandlerTests()
    {
        _stripeEventService = Substitute.For<IStripeEventService>();
        _stripeEventUtilityService = Substitute.For<IStripeEventUtilityService>();
        _organizationService = Substitute.For<IOrganizationService>();
        _stripeAdapter = Substitute.For<IStripeAdapter>();
        _organizationSponsorshipRenewCommand = Substitute.For<IOrganizationSponsorshipRenewCommand>();
        _userService = Substitute.For<IUserService>();
        _userRepository = Substitute.For<IUserRepository>();
        _providerService = Substitute.For<IProviderService>();
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _organizationEnableCommand = Substitute.For<IOrganizationEnableCommand>();
        _organizationDisableCommand = Substitute.For<IOrganizationDisableCommand>();
        _pricingClient = Substitute.For<IPricingClient>();
        _providerRepository = Substitute.For<IProviderRepository>();
        _providerService = Substitute.For<IProviderService>();
        _pushNotificationAdapter = Substitute.For<IPushNotificationAdapter>();
        _featureService = Substitute.For<IFeatureService>();

        _sut = new SubscriptionUpdatedHandler(
            _stripeEventService,
            _stripeEventUtilityService,
            _organizationService,
            _stripeAdapter,
            _organizationSponsorshipRenewCommand,
            _userService,
            _userRepository,
            _organizationRepository,
            _organizationEnableCommand,
            _organizationDisableCommand,
            _pricingClient,
            _providerRepository,
            _providerService,
            _pushNotificationAdapter,
            _featureService,
            Substitute.For<ILogger<SubscriptionUpdatedHandler>>());
    }

    [Fact]
    public async Task HandleAsync_UnpaidOrganizationSubscription_DisablesOrganizationAndSetsCancellation()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var subscriptionId = "sub_123";
        var currentPeriodEnd = DateTime.UtcNow.AddDays(30);

        var previousSubscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Active
        };

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Unpaid,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        CurrentPeriodEnd = currentPeriodEnd,
                        Plan = new Plan { Id = "2023-enterprise-org-seat-annually" }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization { Id = organizationId, PlanType = PlanType.EnterpriseAnnually2023 };

        var parsedEvent = new Event
        {
            Data = new EventData
            {
                Object = subscription,
                PreviousAttributes = JObject.FromObject(previousSubscription)
            }
        };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);

        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);

        var plan = new Enterprise2023Plan(true);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(plan);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _organizationDisableCommand.Received(1)
            .DisableAsync(organizationId, currentPeriodEnd);
        await _pushNotificationAdapter.Received(1)
            .NotifyEnabledChangedAsync(organization);
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            subscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.CancelAt.HasValue &&
                options.CancelAt.Value <= DateTime.UtcNow.AddDays(7).AddMinutes(1) &&
                options.ProrationBehavior == ProrationBehavior.None &&
                options.CancellationDetails != null &&
                options.CancellationDetails.Comment != null));
    }

    [Fact]
    public async Task
        HandleAsync_UnpaidProviderSubscription_WithValidTransition_DisablesProviderAndSetsCancellation()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var subscriptionId = "sub_test123";

        var previousSubscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Active
        };

        var currentSubscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Unpaid,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }
                ]
            },
            Metadata = new Dictionary<string, string> { ["providerId"] = providerId.ToString() },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle },
            TestClock = null
        };

        var parsedEvent = new Event
        {
            Data = new EventData
            {
                Object = currentSubscription,
                PreviousAttributes = JObject.FromObject(previousSubscription)
            }
        };

        var provider = new Provider { Id = providerId, Enabled = true };

        _stripeEventService.GetSubscription(parsedEvent, true, Arg.Any<List<string>>()).Returns(currentSubscription);
        _providerRepository.GetByIdAsync(providerId).Returns(provider);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        Assert.False(provider.Enabled);
        await _providerService.Received(1).UpdateAsync(provider);

        // Verify that UpdateSubscription was called with CancelAt
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            subscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.CancelAt.HasValue &&
                options.CancelAt.Value <= DateTime.UtcNow.AddDays(7).AddMinutes(1) &&
                options.ProrationBehavior == ProrationBehavior.None &&
                options.CancellationDetails != null &&
                options.CancellationDetails.Comment != null));
    }

    [Fact]
    public async Task HandleAsync_UnpaidProviderSubscription_WithoutValidTransition_DoesNotDisableProvider()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        const string subscriptionId = "sub_123";

        var previousSubscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Unpaid // No valid transition (already unpaid)
        };

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }
                ]
            },
            Status = SubscriptionStatus.Unpaid,
            Metadata = new Dictionary<string, string> { { "providerId", providerId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var provider = new Provider { Id = providerId, Name = "Test Provider", Enabled = true };

        var parsedEvent = new Event
        {
            Data = new EventData
            {
                Object = subscription,
                PreviousAttributes = JObject.FromObject(previousSubscription)
            }
        };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);

        _providerRepository.GetByIdAsync(providerId)
            .Returns(provider);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert - No disable or cancellation since there was no valid status transition
        Assert.True(provider.Enabled);
        await _providerService.DidNotReceive().UpdateAsync(Arg.Any<Provider>());
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task HandleAsync_UnpaidProviderSubscription_WithNonMatchingPreviousStatus_DoesNotDisableProvider()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        const string subscriptionId = "sub_123";

        // Previous status is Canceled, which is not a valid transition source (Trialing/Active/PastDue)
        var previousSubscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Canceled
        };

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }
                ]
            },
            Status = SubscriptionStatus.Unpaid,
            Metadata = new Dictionary<string, string> { { "providerId", providerId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var provider = new Provider { Id = providerId, Name = "Test Provider", Enabled = true };

        var parsedEvent = new Event
        {
            Data = new EventData
            {
                Object = subscription,
                PreviousAttributes = JObject.FromObject(previousSubscription)
            }
        };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);

        _providerRepository.GetByIdAsync(providerId)
            .Returns(provider);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert - No disable or cancellation since the previous status (Canceled) is not a valid transition source
        Assert.True(provider.Enabled);
        await _providerService.DidNotReceive().UpdateAsync(Arg.Any<Provider>());
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task HandleAsync_IncompleteToIncompleteExpiredTransition_DisablesProviderAndSetsCancellation()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var subscriptionId = "sub_123";
        var currentPeriodEnd = DateTime.UtcNow.AddDays(30);

        // Previous status was Incomplete - this is the valid transition for IncompleteExpired
        var previousSubscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Incomplete
        };

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.IncompleteExpired,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd }
                ]
            },
            Metadata = new Dictionary<string, string> { { "providerId", providerId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCreate }
        };

        var provider = new Provider { Id = providerId, Name = "Test Provider", Enabled = true };

        var parsedEvent = new Event
        {
            Data = new EventData
            {
                Object = subscription,
                PreviousAttributes = JObject.FromObject(previousSubscription)
            }
        };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);

        _providerRepository.GetByIdAsync(providerId)
            .Returns(provider);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert - Incomplete to IncompleteExpired should trigger disable and cancellation
        Assert.False(provider.Enabled);
        await _providerService.Received(1).UpdateAsync(provider);
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            subscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.CancelAt.HasValue &&
                options.CancelAt.Value <= DateTime.UtcNow.AddDays(7).AddMinutes(1) &&
                options.ProrationBehavior == ProrationBehavior.None &&
                options.CancellationDetails != null &&
                options.CancellationDetails.Comment != null));
    }

    [Fact]
    public async Task HandleAsync_IncompleteToIncompleteExpiredUserSubscription_DisablesPremiumAndSetsCancellation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subscriptionId = "sub_123";
        var currentPeriodEnd = DateTime.UtcNow.AddDays(30);

        var previousSubscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Incomplete
        };

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.IncompleteExpired,
            Metadata = new Dictionary<string, string> { { "userId", userId.ToString() } },
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd }
                ]
            },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCreate }
        };

        var parsedEvent = new Event
        {
            Data = new EventData
            {
                Object = subscription,
                PreviousAttributes = JObject.FromObject(previousSubscription)
            }
        };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _userService.Received(1).DisablePremiumAsync(userId, currentPeriodEnd);
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            subscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.CancelAt.HasValue &&
                options.CancelAt.Value <= DateTime.UtcNow.AddDays(7).AddMinutes(1) &&
                options.ProrationBehavior == ProrationBehavior.None &&
                options.CancellationDetails != null &&
                options.CancellationDetails.Comment != null));
    }

    [Fact]
    public async Task HandleAsync_IncompleteToIncompleteExpiredOrganizationSubscription_DisablesOrganizationAndSetsCancellation()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var subscriptionId = "sub_123";
        var currentPeriodEnd = DateTime.UtcNow.AddDays(30);

        var previousSubscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Incomplete
        };

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.IncompleteExpired,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        CurrentPeriodEnd = currentPeriodEnd,
                        Plan = new Plan { Id = "2023-enterprise-org-seat-annually" }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCreate }
        };

        var organization = new Organization { Id = organizationId, PlanType = PlanType.EnterpriseAnnually2023 };

        var parsedEvent = new Event
        {
            Data = new EventData
            {
                Object = subscription,
                PreviousAttributes = JObject.FromObject(previousSubscription)
            }
        };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);

        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);

        var plan = new Enterprise2023Plan(true);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(plan);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _organizationDisableCommand.Received(1).DisableAsync(organizationId, currentPeriodEnd);
        await _pushNotificationAdapter.Received(1).NotifyEnabledChangedAsync(organization);
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            subscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.CancelAt.HasValue &&
                options.CancelAt.Value <= DateTime.UtcNow.AddDays(7).AddMinutes(1) &&
                options.ProrationBehavior == ProrationBehavior.None &&
                options.CancellationDetails != null &&
                options.CancellationDetails.Comment != null));
    }

    [Fact]
    public async Task HandleAsync_UnpaidProviderSubscription_WhenProviderNotFound_StillSetsCancellation()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var subscriptionId = "sub_123";
        var currentPeriodEnd = DateTime.UtcNow.AddDays(30);

        var previousSubscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Active
        };

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Unpaid,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd }
                ]
            },
            Metadata = new Dictionary<string, string> { { "providerId", providerId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var parsedEvent = new Event
        {
            Data = new EventData
            {
                Object = subscription,
                PreviousAttributes = JObject.FromObject(previousSubscription)
            }
        };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);

        _providerRepository.GetByIdAsync(providerId)
            .Returns((Provider)null);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert - Provider not updated (since not found), but cancellation is still set
        await _providerService.DidNotReceive().UpdateAsync(Arg.Any<Provider>());
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            subscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.CancelAt.HasValue &&
                options.CancelAt.Value <= DateTime.UtcNow.AddDays(7).AddMinutes(1) &&
                options.ProrationBehavior == ProrationBehavior.None &&
                options.CancellationDetails != null &&
                options.CancellationDetails.Comment != null));
    }

    [Fact]
    public async Task HandleAsync_UnpaidUserSubscription_DisablesPremiumAndSetsCancellation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subscriptionId = "sub_123";
        var currentPeriodEnd = DateTime.UtcNow.AddDays(30);

        var previousSubscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Active
        };

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Unpaid,
            Metadata = new Dictionary<string, string> { { "userId", userId.ToString() } },
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd }
                ]
            },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var parsedEvent = new Event
        {
            Data = new EventData
            {
                Object = subscription,
                PreviousAttributes = JObject.FromObject(previousSubscription)
            }
        };

        var user = new User { Id = userId, Premium = false, PremiumExpirationDate = currentPeriodEnd };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);

        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, userId, null));

        _userRepository.GetByIdAsync(userId).Returns(user);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _userService.Received(1)
            .DisablePremiumAsync(userId, currentPeriodEnd);
        await _userRepository.Received(1).GetByIdAsync(userId);
        await _pushNotificationAdapter.Received(1).NotifyPremiumStatusChangedAsync(user);
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            subscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.CancelAt.HasValue &&
                options.CancelAt.Value <= DateTime.UtcNow.AddDays(7).AddMinutes(1) &&
                options.ProrationBehavior == ProrationBehavior.None &&
                options.CancellationDetails != null &&
                options.CancellationDetails.Comment != null));
        await _stripeAdapter.DidNotReceive()
            .CancelSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionCancelOptions>());
        await _stripeAdapter.DidNotReceive()
            .ListInvoicesAsync(Arg.Any<StripeInvoiceListOptions>());
    }

    [Fact]
    public async Task HandleAsync_IncompleteExpiredUserSubscription_OnlyUpdatesExpiration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subscriptionId = "sub_123";
        var currentPeriodEnd = DateTime.UtcNow.AddDays(30);

        // Previous status that doesn't trigger enable/disable logic
        var previousSubscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Incomplete
        };

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.IncompleteExpired,
            Metadata = new Dictionary<string, string> { { "userId", userId.ToString() } },
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd }
                ]
            }
        };

        var parsedEvent = new Event
        {
            Data = new EventData
            {
                Object = subscription,
                PreviousAttributes = JObject.FromObject(previousSubscription)
            }
        };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);

        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, userId, null));

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert - IncompleteExpired is no longer handled specially, only expiration is updated
        await _userService.DidNotReceive().DisablePremiumAsync(Arg.Any<Guid>(), Arg.Any<DateTime?>());
        await _userService.Received(1).UpdatePremiumExpirationAsync(userId, currentPeriodEnd);
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
        await _stripeAdapter.DidNotReceive()
            .CancelSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionCancelOptions>());
        await _stripeAdapter.DidNotReceive()
            .ListInvoicesAsync(Arg.Any<StripeInvoiceListOptions>());
    }

    [Fact]
    public async Task HandleAsync_ActiveOrganizationSubscription_EnablesOrganizationAndUpdatesExpiration()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var subscriptionId = "sub_123";
        var currentPeriodEnd = DateTime.UtcNow.AddDays(30);

        var previousSubscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Unpaid
        };

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Active,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        CurrentPeriodEnd = currentPeriodEnd,
                        Plan = new Plan { Id = "2023-enterprise-org-seat-annually" }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization { Id = organizationId, PlanType = PlanType.EnterpriseAnnually2023 };
        var parsedEvent = new Event
        {
            Data = new EventData
            {
                Object = subscription,
                PreviousAttributes = JObject.FromObject(previousSubscription)
            }
        };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);

        _organizationRepository.GetByIdAsync(organizationId)
            .Returns(organization);

        var plan = new Enterprise2023Plan(true);
        _pricingClient.GetPlanOrThrow(organization.PlanType)
            .Returns(plan);
        _pricingClient.ListPlans()
            .Returns(MockPlans.Plans);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _organizationEnableCommand.Received(1)
            .EnableAsync(organizationId, currentPeriodEnd);
        await _organizationService.Received(1)
            .UpdateExpirationDateAsync(organizationId, currentPeriodEnd);
        await _pushNotificationAdapter.Received(1)
            .NotifyEnabledChangedAsync(organization);
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            subscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.CancelAtPeriodEnd == false &&
                options.ProrationBehavior == ProrationBehavior.None));
    }

    [Fact]
    public async Task HandleAsync_ActiveUserSubscription_EnablesPremiumAndUpdatesExpiration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subscriptionId = "sub_123";
        var currentPeriodEnd = DateTime.UtcNow.AddDays(30);

        var previousSubscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Unpaid
        };

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Active,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd }
                ]
            },
            Metadata = new Dictionary<string, string> { { "userId", userId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var parsedEvent = new Event
        {
            Data = new EventData
            {
                Object = subscription,
                PreviousAttributes = JObject.FromObject(previousSubscription)
            }
        };

        var user = new User { Id = userId, Premium = true, PremiumExpirationDate = currentPeriodEnd };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);
        _userRepository.GetByIdAsync(userId).Returns(user);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _userService.Received(1)
            .EnablePremiumAsync(userId, currentPeriodEnd);
        await _userService.Received(1)
            .UpdatePremiumExpirationAsync(userId, currentPeriodEnd);
        await _userRepository.Received(1).GetByIdAsync(userId);
        await _pushNotificationAdapter.Received(1).NotifyPremiumStatusChangedAsync(user);
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            subscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.CancelAtPeriodEnd == false &&
                options.ProrationBehavior == ProrationBehavior.None));
    }

    [Fact]
    public async Task HandleAsync_SponsoredSubscription_RenewsSponsorship()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var subscriptionId = "sub_123";
        var currentPeriodEnd = DateTime.UtcNow.AddDays(30);

        // Use a previous status that won't trigger enable/disable logic
        var previousSubscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Active
        };

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Active,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } }
        };

        var parsedEvent = new Event
        {
            Data = new EventData
            {
                Object = subscription,
                PreviousAttributes = JObject.FromObject(previousSubscription)
            }
        };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);

        _stripeEventUtilityService.IsSponsoredSubscription(subscription)
            .Returns(true);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _organizationSponsorshipRenewCommand.Received(1)
            .UpdateExpirationDateAsync(organizationId, currentPeriodEnd);
    }

    [Fact]
    public async Task
        HandleAsync_WhenSubscriptionIsActive_AndOrganizationHasSecretsManagerTrial_AndRemovingSecretsManagerTrial_RemovesPasswordManagerCoupon()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var subscription = new Subscription
        {
            Id = "sub_123",
            Status = SubscriptionStatus.Active,
            CustomerId = "cus_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        CurrentPeriodEnd = DateTime.UtcNow.AddDays(10),
                        Plan = new Plan { Id = "2023-enterprise-org-seat-annually" }
                    }
                ]
            },
            Customer = new Customer
            {
                Balance = 0,
                Discount = new Discount { Coupon = new Coupon { Id = "sm-standalone" } }
            },
            Discounts = [new Discount { Coupon = new Coupon { Id = "sm-standalone" } }],
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } }
        };

        var organization = new Organization { Id = organizationId, PlanType = PlanType.EnterpriseAnnually2023 };

        var plan = new Enterprise2023Plan(true);
        _pricingClient.GetPlanOrThrow(organization.PlanType)
            .Returns(plan);
        _pricingClient.ListPlans()
            .Returns(MockPlans.Plans);

        var parsedEvent = new Event
        {
            Data = new EventData
            {
                Object = subscription,
                PreviousAttributes = JObject.FromObject(new
                {
                    items = new
                    {
                        data = new[] { new { plan = new { id = "secrets-manager-enterprise-seat-annually" } } }
                    },
                    Items = new StripeList<SubscriptionItem>
                    {
                        Data =
                        [
                            new SubscriptionItem { Plan = new Plan { Id = "secrets-manager-enterprise-seat-annually" } }
                        ]
                    }
                })
            }
        };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);

        _organizationRepository.GetByIdAsync(organizationId)
            .Returns(organization);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeAdapter.Received(1).DeleteCustomerDiscountAsync(subscription.CustomerId);
        await _stripeAdapter.Received(1).DeleteSubscriptionDiscountAsync(subscription.Id);
    }
    [Fact]
    public async Task
        HandleAsync_WhenUpgradingPlan_AndPreviousPlanHasSecretsManagerTrial_AndCurrentPlanHasSecretsManagerTrial_DoesNotRemovePasswordManagerCoupon()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var subscription = new Subscription
        {
            Id = "sub_123",
            Status = SubscriptionStatus.Active,
            CustomerId = "cus_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        CurrentPeriodEnd = DateTime.UtcNow.AddDays(10),
                        Plan = new Plan { Id = "2023-enterprise-org-seat-annually" }
                    },
                    new SubscriptionItem
                    {
                        CurrentPeriodEnd = DateTime.UtcNow.AddDays(10),
                        Plan = new Plan { Id = "secrets-manager-enterprise-seat-annually" }
                    }
                ]
            },
            Customer = new Customer
            {
                Balance = 0,
                Discount = new Discount { Coupon = new Coupon { Id = "sm-standalone" } }
            },
            Discounts = [new Discount { Coupon = new Coupon { Id = "sm-standalone" } }],
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } }
        };

        // Note: The organization plan is still the previous plan because the subscription is updated before the organization is updated
        var organization = new Organization { Id = organizationId, PlanType = PlanType.TeamsAnnually2023 };

        var plan = new Teams2023Plan(true);
        _pricingClient.GetPlanOrThrow(organization.PlanType)
            .Returns(plan);
        _pricingClient.ListPlans()
            .Returns(MockPlans.Plans);

        var parsedEvent = new Event
        {
            Data = new EventData
            {
                Object = subscription,
                PreviousAttributes = JObject.FromObject(new
                {
                    items = new
                    {
                        data = new[]
                        {
                            new { plan = new { id = "secrets-manager-teams-seat-annually" } },
                        }
                    },
                    Items = new StripeList<SubscriptionItem>
                    {
                        Data =
                        [
                            new SubscriptionItem { Plan = new Stripe.Plan { Id = "secrets-manager-teams-seat-annually" } },
                        ]
                    }
                })
            }
        };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);

        _organizationRepository.GetByIdAsync(organizationId)
            .Returns(organization);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeAdapter.DidNotReceive().DeleteCustomerDiscountAsync(subscription.CustomerId);
        await _stripeAdapter.DidNotReceive().DeleteSubscriptionDiscountAsync(subscription.Id);
    }

    [Theory]
    [MemberData(nameof(GetValidTransitionToActiveSubscriptions))]
    public async Task
        HandleAsync_ActiveProviderSubscriptionEvent_AndPreviousSubscriptionStatusWasIncompleteOrUnpaid_EnableProviderAndUpdateSubscription(
            Subscription previousSubscription)
    {
        // Arrange
        var (providerId, newSubscription, provider, parsedEvent) =
            CreateProviderTestInputsForUpdatedActiveSubscriptionStatus(previousSubscription);

        _stripeEventService
            .GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(newSubscription);

        _providerRepository
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(provider);
        _stripeAdapter
            .UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(newSubscription);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeEventService
            .Received(1)
            .GetSubscription(parsedEvent, true, Arg.Any<List<string>>());
        await _providerRepository
            .Received(1)
            .GetByIdAsync(providerId);
        await _providerService
            .Received(1)
            .UpdateAsync(Arg.Is<Provider>(p => p.Id == providerId && p.Enabled == true));
        await _stripeAdapter
            .Received(1)
            .UpdateSubscriptionAsync(newSubscription.Id,
                Arg.Is<SubscriptionUpdateOptions>(options =>
                    options.CancelAtPeriodEnd == false &&
                    options.ProrationBehavior == ProrationBehavior.None));
    }

    [Fact]
    public async Task
        HandleAsync_ActiveProviderSubscriptionEvent_AndPreviousSubscriptionStatusWasCanceled_DoesNotEnableProvider()
    {
        // Arrange
        var previousSubscription = new Subscription { Id = "sub_123", Status = SubscriptionStatus.Canceled };
        var (providerId, newSubscription, provider, parsedEvent) =
            CreateProviderTestInputsForUpdatedActiveSubscriptionStatus(previousSubscription);

        _stripeEventService
            .GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(newSubscription);
        _providerRepository
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(provider);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert - Canceled is not a valid transition source for SubscriptionBecameActive
        await _stripeEventService
            .Received(1)
            .GetSubscription(parsedEvent, true, Arg.Any<List<string>>());
        await _providerRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
        await _providerService
            .DidNotReceive()
            .UpdateAsync(Arg.Any<Provider>());
        await _stripeAdapter
            .DidNotReceiveWithAnyArgs()
            .UpdateSubscriptionAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task
        HandleAsync_ActiveProviderSubscriptionEvent_AndPreviousSubscriptionStatusWasAlreadyActive_DoesNotEnableProvider()
    {
        // Arrange
        var previousSubscription = new Subscription { Id = "sub_123", Status = SubscriptionStatus.Active };
        var (providerId, newSubscription, provider, parsedEvent) =
            CreateProviderTestInputsForUpdatedActiveSubscriptionStatus(previousSubscription);

        _stripeEventService
            .GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(newSubscription);
        _providerRepository
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(provider);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert - Already Active is not a valid transition for SubscriptionBecameActive
        await _stripeEventService
            .Received(1)
            .GetSubscription(parsedEvent, true, Arg.Any<List<string>>());
        await _providerRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
        await _providerService
            .DidNotReceive()
            .UpdateAsync(Arg.Any<Provider>());
        await _stripeAdapter
            .DidNotReceiveWithAnyArgs()
            .UpdateSubscriptionAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task
        HandleAsync_ActiveProviderSubscriptionEvent_AndPreviousSubscriptionStatusWasTrialing_DoesNotEnableProvider()
    {
        // Arrange
        var previousSubscription = new Subscription { Id = "sub_123", Status = SubscriptionStatus.Trialing };
        var (providerId, newSubscription, provider, parsedEvent) =
            CreateProviderTestInputsForUpdatedActiveSubscriptionStatus(previousSubscription);

        _stripeEventService
            .GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(newSubscription);
        _providerRepository
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(provider);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert - Trialing is not a valid transition source for SubscriptionBecameActive
        await _stripeEventService
            .Received(1)
            .GetSubscription(parsedEvent, true, Arg.Any<List<string>>());
        await _providerRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
        await _providerService
            .DidNotReceive()
            .UpdateAsync(Arg.Any<Provider>());
        await _stripeAdapter
            .DidNotReceiveWithAnyArgs()
            .UpdateSubscriptionAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task
        HandleAsync_ActiveProviderSubscriptionEvent_AndPreviousSubscriptionStatusWasPastDue_DoesNotEnableProvider()
    {
        // Arrange
        var previousSubscription = new Subscription { Id = "sub_123", Status = SubscriptionStatus.PastDue };
        var (providerId, newSubscription, provider, parsedEvent) =
            CreateProviderTestInputsForUpdatedActiveSubscriptionStatus(previousSubscription);

        _stripeEventService
            .GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(newSubscription);
        _providerRepository
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(provider);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert - PastDue is not a valid transition source for SubscriptionBecameActive
        await _stripeEventService
            .Received(1)
            .GetSubscription(parsedEvent, true, Arg.Any<List<string>>());
        await _providerRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
        await _providerService
            .DidNotReceive()
            .UpdateAsync(Arg.Any<Provider>());
        await _stripeAdapter
            .DidNotReceiveWithAnyArgs()
            .UpdateSubscriptionAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task HandleAsync_ActiveProviderSubscriptionEvent_AndProviderDoesNotExist_NoChanges()
    {
        // Arrange
        var previousSubscription = new Subscription { Id = "sub_123", Status = SubscriptionStatus.Unpaid };
        var (providerId, newSubscription, _, parsedEvent) =
            CreateProviderTestInputsForUpdatedActiveSubscriptionStatus(previousSubscription);

        _stripeEventService
            .GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(newSubscription);
        _providerRepository
            .GetByIdAsync(Arg.Any<Guid>())
            .ReturnsNull();

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeEventService
            .Received(1)
            .GetSubscription(parsedEvent, true, Arg.Any<List<string>>());
        await _providerRepository
            .Received(1)
            .GetByIdAsync(providerId);
        await _providerService
            .DidNotReceive()
            .UpdateAsync(Arg.Any<Provider>());
        await _stripeAdapter
            .DidNotReceive()
            .UpdateSubscriptionAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task HandleAsync_ActiveProviderSubscriptionEvent_WithNonMatchingPreviousStatus_DoesNotEnableProvider()
    {
        // Arrange - Using a previous status (Canceled) that doesn't trigger SubscriptionBecameActive
        var previousSubscription = new Subscription { Id = "sub_123", Status = SubscriptionStatus.Canceled };
        var (providerId, newSubscription, provider, parsedEvent) =
            CreateProviderTestInputsForUpdatedActiveSubscriptionStatus(previousSubscription);

        _stripeEventService
            .GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(newSubscription);
        _providerRepository
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(provider);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert - Canceled is not a valid transition source, so no enable logic is triggered
        await _stripeEventService
            .Received(1)
            .GetSubscription(parsedEvent, true, Arg.Any<List<string>>());
        await _providerRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
        await _providerService
            .DidNotReceive()
            .UpdateAsync(Arg.Any<Provider>());
        await _stripeAdapter
            .DidNotReceive()
            .UpdateSubscriptionAsync(Arg.Any<string>());
    }

    private static (Guid providerId, Subscription newSubscription, Provider provider, Event parsedEvent)
        CreateProviderTestInputsForUpdatedActiveSubscriptionStatus(Subscription? previousSubscription)
    {
        var providerId = Guid.NewGuid();
        var newSubscription = new Subscription
        {
            Id = previousSubscription?.Id ?? "sub_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }
                ]
            },
            Status = SubscriptionStatus.Active,
            Metadata = new Dictionary<string, string> { { "providerId", providerId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var provider = new Provider { Id = providerId, Enabled = false };
        var parsedEvent = new Event
        {
            Data = new EventData
            {
                Object = newSubscription,
                PreviousAttributes =
                    previousSubscription == null ? null : JObject.FromObject(previousSubscription)
            }
        };
        return (providerId, newSubscription, provider, parsedEvent);
    }

    [Fact]
    public async Task HandleAsync_IncompleteUserSubscription_OnlyUpdatesExpiration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subscriptionId = "sub_123";
        var currentPeriodEnd = DateTime.UtcNow.AddDays(30);

        // Previous status that doesn't trigger enable/disable logic (already was incomplete)
        var previousSubscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Incomplete
        };

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Incomplete,
            Metadata = new Dictionary<string, string> { { "userId", userId.ToString() } },
            LatestInvoice = new Invoice { Status = "open" },
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd }
                ]
            }
        };

        var parsedEvent = new Event
        {
            Data = new EventData
            {
                Object = subscription,
                PreviousAttributes = JObject.FromObject(previousSubscription)
            }
        };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert - Incomplete status is no longer handled specially, only expiration is updated
        await _userService.DidNotReceive().DisablePremiumAsync(Arg.Any<Guid>(), Arg.Any<DateTime?>());
        await _userService.Received(1).UpdatePremiumExpirationAsync(userId, currentPeriodEnd);
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    public static IEnumerable<object[]> GetValidTransitionToActiveSubscriptions()
    {
        // Only Incomplete and Unpaid are valid previous statuses for SubscriptionBecameActive
        return new List<object[]>
        {
            new object[] { new Subscription { Id = "sub_123", Status = SubscriptionStatus.Unpaid } },
            new object[] { new Subscription { Id = "sub_123", Status = SubscriptionStatus.Incomplete } }
        };
    }

    #region Schedule-triggered Families migration tests

    [Fact]
    public async Task HandleAsync_ScheduleTriggeredFamiliesMigration_FlagOn_UpdatesOrganization()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var subscriptionId = "sub_123";
        var currentPeriodEnd = DateTime.UtcNow.AddDays(365);
        var familiesPriceId = "2020-families-org-annually";

        var previousSubscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Active
        };

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Active,
            ScheduleId = "sub_sched_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        CurrentPeriodEnd = currentPeriodEnd,
                        Price = new Price { Id = familiesPriceId }
                    }
                ]
            },
            Metadata = new Dictionary<string, string>
            {
                { "organizationId", organizationId.ToString() }
            }
        };

        var parsedEvent = new Event
        {
            Data = new EventData
            {
                Object = subscription,
                PreviousAttributes = JObject.FromObject(new
                {
                    items = new
                    {
                        data = new[] { new { price = new { id = "personal-org-annually" } } }
                    }
                })
            }
        };

        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.FamiliesAnnually2019,
            Plan = "Families 2019",
            UsersGetPremium = false,
            Seats = 5
        };

        var familiesPlan = new FamiliesPlan();

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal)
            .Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually)
            .Returns(familiesPlan);
        _organizationRepository.GetByIdAsync(organizationId)
            .Returns(organization);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        Assert.Equal(PlanType.FamiliesAnnually, organization.PlanType);
        Assert.Equal("Families", organization.Plan);
        Assert.True(organization.UsersGetPremium);
        Assert.Equal(6, organization.Seats);
        await _organizationRepository.Received(1).ReplaceAsync(
            Arg.Is<Organization>(o =>
                o.Id == organizationId &&
                o.PlanType == PlanType.FamiliesAnnually &&
                o.UsersGetPremium));
    }

    [Fact]
    public async Task HandleAsync_ScheduleTriggeredMigration_FlagOff_DoesNotUpdateOrganization()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var subscriptionId = "sub_123";

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Active,
            ScheduleId = "sub_sched_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        CurrentPeriodEnd = DateTime.UtcNow.AddDays(365),
                        Price = new Price { Id = "2020-families-org-annually" }
                    }
                ]
            },
            Metadata = new Dictionary<string, string>
            {
                { "organizationId", organizationId.ToString() }
            }
        };

        var parsedEvent = new Event
        {
            Data = new EventData
            {
                Object = subscription,
                PreviousAttributes = JObject.FromObject(new
                {
                    items = new
                    {
                        data = new[] { new { price = new { id = "personal-org-annually" } } }
                    }
                })
            }
        };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal)
            .Returns(false);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
    }

    [Fact]
    public async Task HandleAsync_NoSchedule_FlagOn_DoesNotUpdateOrganization()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var subscriptionId = "sub_123";

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Active,
            ScheduleId = null,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        CurrentPeriodEnd = DateTime.UtcNow.AddDays(365),
                        Price = new Price { Id = "2020-families-org-annually" }
                    }
                ]
            },
            Metadata = new Dictionary<string, string>
            {
                { "organizationId", organizationId.ToString() }
            }
        };

        var parsedEvent = new Event
        {
            Data = new EventData
            {
                Object = subscription,
                PreviousAttributes = JObject.FromObject(new
                {
                    items = new
                    {
                        data = new[] { new { price = new { id = "personal-org-annually" } } }
                    }
                })
            }
        };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal)
            .Returns(true);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
    }

    [Fact]
    public async Task HandleAsync_ScheduleTriggered_NoOrganizationId_DoesNotUpdateOrganization()
    {
        // Arrange — user subscription with schedule, not an org
        var userId = Guid.NewGuid();
        var subscriptionId = "sub_123";

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Active,
            ScheduleId = "sub_sched_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        CurrentPeriodEnd = DateTime.UtcNow.AddDays(365),
                        Price = new Price { Id = "premium-annually-2026" }
                    }
                ]
            },
            Metadata = new Dictionary<string, string>
            {
                { "userId", userId.ToString() }
            }
        };

        var parsedEvent = new Event
        {
            Data = new EventData
            {
                Object = subscription,
                PreviousAttributes = JObject.FromObject(new
                {
                    items = new
                    {
                        data = new[] { new { price = new { id = "premium-annually" } } }
                    }
                })
            }
        };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal)
            .Returns(true);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
    }

    [Fact]
    public async Task HandleAsync_ScheduleTriggered_PriceDoesNotMatchFamilies_DoesNotUpdateOrganization()
    {
        // Arrange — schedule-triggered but new price is not FamiliesAnnually
        var organizationId = Guid.NewGuid();
        var subscriptionId = "sub_123";

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Active,
            ScheduleId = "sub_sched_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        CurrentPeriodEnd = DateTime.UtcNow.AddDays(365),
                        Price = new Price { Id = "some-other-price-id" }
                    }
                ]
            },
            Metadata = new Dictionary<string, string>
            {
                { "organizationId", organizationId.ToString() }
            }
        };

        var parsedEvent = new Event
        {
            Data = new EventData
            {
                Object = subscription,
                PreviousAttributes = JObject.FromObject(new
                {
                    items = new
                    {
                        data = new[] { new { price = new { id = "old-price" } } }
                    }
                })
            }
        };

        var familiesPlan = new FamiliesPlan();

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal)
            .Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually)
            .Returns(familiesPlan);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
    }

    [Fact]
    public async Task HandleAsync_ScheduleTriggered_NoItemChanges_DoesNotUpdateOrganization()
    {
        // Arrange — schedule present but no item changes in PreviousAttributes
        var organizationId = Guid.NewGuid();
        var subscriptionId = "sub_123";

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Active,
            ScheduleId = "sub_sched_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        CurrentPeriodEnd = DateTime.UtcNow.AddDays(365),
                        Price = new Price { Id = "2020-families-org-annually" }
                    }
                ]
            },
            Metadata = new Dictionary<string, string>
            {
                { "organizationId", organizationId.ToString() }
            }
        };

        // PreviousAttributes has status change but NO items change
        var parsedEvent = new Event
        {
            Data = new EventData
            {
                Object = subscription,
                PreviousAttributes = JObject.FromObject(new { status = "active" })
            }
        };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal)
            .Returns(true);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
    }

    #endregion
}
