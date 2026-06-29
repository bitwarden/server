using Bit.Billing.Services;
using Bit.Billing.Services.Implementations;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
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
using NSubstitute.ExceptionExtensions;
using NSubstitute.ReturnsExtensions;
using Stripe;
using Stripe.TestHelpers;
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
    private readonly IPriceIncreaseScheduler _priceIncreaseScheduler;
    private readonly IFeatureService _featureService;
    private readonly IOrganizationPlanMigrationCohortRepository _cohortRepository;
    private readonly IOrganizationPlanMigrationCohortAssignmentRepository _cohortAssignmentRepository;
    private readonly ILogger<SubscriptionUpdatedHandler> _logger;
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
        _priceIncreaseScheduler = Substitute.For<IPriceIncreaseScheduler>();
        _featureService = Substitute.For<IFeatureService>();
        _cohortRepository = Substitute.For<IOrganizationPlanMigrationCohortRepository>();
        _cohortAssignmentRepository = Substitute.For<IOrganizationPlanMigrationCohortAssignmentRepository>();
        _logger = Substitute.For<ILogger<SubscriptionUpdatedHandler>>();

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
            _priceIncreaseScheduler,
            _featureService,
            _cohortRepository,
            _cohortAssignmentRepository,
            _logger);
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
        await _organizationRepository.DidNotReceive()
            .ReplaceAsync(Arg.Any<Organization>());
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
    public async Task HandleAsync_IncompleteToIncompleteExpiredTransition_DisablesProvider()
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

        // Assert - Incomplete to IncompleteExpired should disable the subscriber but
        // must NOT call UpdateSubscriptionAsync: the subscription is already terminal
        // and Stripe would reject the update, causing a 500-retry disable loop.
        Assert.False(provider.Enabled);
        await _providerService.Received(1).UpdateAsync(provider);
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Any<string>(),
            Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task HandleAsync_IncompleteToIncompleteExpiredUserSubscription_DisablesPremium()
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

        var user = new User { Id = userId };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);
        _userRepository.GetByIdAsync(userId).Returns(user);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert - disables Premium but must NOT call UpdateSubscriptionAsync
        // on the already-terminal subscription (would 500-retry and re-disable).
        await _userService.Received(1).DisablePremiumAsync(userId, currentPeriodEnd);
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Any<string>(),
            Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task HandleAsync_IncompleteToIncompleteExpiredOrganizationSubscription_DisablesOrganization()
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

        // Assert - disables organization but must NOT call UpdateSubscriptionAsync
        // on the already-terminal subscription (would 500-retry and re-disable).
        await _organizationDisableCommand.Received(1).DisableAsync(organizationId, currentPeriodEnd);
        await _pushNotificationAdapter.Received(1).NotifyEnabledChangedAsync(organization);
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Any<string>(),
            Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task HandleAsync_WhenProviderNotFound_SkipsHandler()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var subscriptionId = "sub_123";

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
                    new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }
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

        // Assert — guard exits early, no side effects
        await _providerService.DidNotReceive().UpdateAsync(Arg.Any<Provider>());
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
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
        await _userRepository.Received(2).GetByIdAsync(userId);
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

        _userRepository.GetByIdAsync(userId).Returns(new User { Id = userId });

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
        await _userRepository.Received(2).GetByIdAsync(userId);
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

        _organizationRepository.GetByIdAsync(organizationId).Returns(new Organization { Id = organizationId, PlanType = PlanType.FamiliesAnnually });
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(new FamiliesPlan());

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
            .Received(2)
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
        await _providerRepository.Received(1).GetByIdAsync(providerId);
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
        await _providerRepository.Received(1).GetByIdAsync(providerId);
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
        await _providerRepository.Received(1).GetByIdAsync(providerId);
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
        await _providerRepository.Received(1).GetByIdAsync(providerId);
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
        await _providerRepository.Received(1).GetByIdAsync(providerId);
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

        _userRepository.GetByIdAsync(userId).Returns(new User { Id = userId });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert - Incomplete status is no longer handled specially, only expiration is updated
        await _userService.DidNotReceive().DisablePremiumAsync(Arg.Any<Guid>(), Arg.Any<DateTime?>());
        await _userService.Received(1).UpdatePremiumExpirationAsync(userId, currentPeriodEnd);
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task HandleAsync_UnpaidSubscription_ReleasesScheduleBeforeCancellation()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var subscriptionId = "sub_123";
        var customerId = "cus_123";
        var currentPeriodEnd = DateTime.UtcNow.AddDays(30);

        var previousSubscription = new Subscription { Id = subscriptionId, Status = SubscriptionStatus.Active };
        var subscription = new Subscription
        {
            Id = subscriptionId,
            CustomerId = customerId,
            Status = SubscriptionStatus.Unpaid,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd, Plan = new Plan { Id = "2023-enterprise-org-seat-annually" } }]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization { Id = organizationId, PlanType = PlanType.EnterpriseAnnually2023 };
        var parsedEvent = new Event
        {
            Data = new EventData { Object = subscription, PreviousAttributes = JObject.FromObject(previousSubscription) }
        };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>()).Returns(subscription);
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(new Enterprise2023Plan(true));
        _pricingClient.ListPlans().Returns(MockPlans.Plans);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _priceIncreaseScheduler.Received(1).Release(customerId, subscriptionId);
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscriptionId, Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task HandleAsync_ActiveSubscription_RemovesCancellationAndAddsSchedules()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var subscriptionId = "sub_123";
        var currentPeriodEnd = DateTime.UtcNow.AddDays(30);

        var previousSubscription = new Subscription { Id = subscriptionId, Status = SubscriptionStatus.Unpaid };
        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Active,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd, Plan = new Plan { Id = "2023-enterprise-org-seat-annually" } }]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization { Id = organizationId, PlanType = PlanType.EnterpriseAnnually2023 };
        var parsedEvent = new Event
        {
            Data = new EventData { Object = subscription, PreviousAttributes = JObject.FromObject(previousSubscription) }
        };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>()).Returns(subscription);
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(new Enterprise2023Plan(true));
        _pricingClient.ListPlans().Returns(MockPlans.Plans);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _priceIncreaseScheduler.Received(1).ScheduleForSubscription(subscription);
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscriptionId, Arg.Is<SubscriptionUpdateOptions>(o => o.CancelAtPeriodEnd == false));
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

    [Fact]
    public async Task HandleAsync_ScheduleTriggeredFamiliesMigration_UpdatesOrganization()
    {
        // Arrange — Families 2019 → FamiliesAnnually migration via schedule
        var organizationId = Guid.NewGuid();
        var subscriptionId = "sub_123";
        var familiesPlan = new FamiliesPlan();
        var families2019Plan = new Families2019Plan();
        var families2025Plan = new Families2025Plan();

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
                        Price = new Price { Id = familiesPlan.PasswordManager.StripePlanId }
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
                        data = new[] { new { price = new { id = families2019Plan.PasswordManager.StripePlanId } } }
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

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesPlan);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019Plan);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2025).Returns(families2025Plan);
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);

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
    public async Task HandleAsync_NoSchedule_DoesNotUpdateOrganization()
    {
        // Arrange — no ScheduleId means this isn't a schedule transition
        var organizationId = Guid.NewGuid();

        var subscription = new Subscription
        {
            Id = "sub_123",
            Status = SubscriptionStatus.Active,
            ScheduleId = null,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(365) }]
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
        var organization = new Organization { Id = organizationId, PlanType = PlanType.FamiliesAnnually };
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(new FamiliesPlan());

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
    }

    [Fact]
    public async Task HandleAsync_ScheduleTriggered_PreviousPriceNotOldFamilies_DoesNotUpdateOrganization()
    {
        // Arrange — schedule-triggered item change, but previous price is not an old Families price
        // (e.g., a storage update on a Families org that happens to have a schedule)
        var organizationId = Guid.NewGuid();
        var familiesPlan = new FamiliesPlan();
        var families2019Plan = new Families2019Plan();
        var families2025Plan = new Families2025Plan();

        var subscription = new Subscription
        {
            Id = "sub_123",
            Status = SubscriptionStatus.Active,
            ScheduleId = "sub_sched_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        CurrentPeriodEnd = DateTime.UtcNow.AddDays(365),
                        Price = new Price { Id = familiesPlan.PasswordManager.StripePlanId }
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
                        data = new[] { new { price = new { id = "personal-storage-gb-annually" } } }
                    }
                })
            }
        };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesPlan);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019Plan);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2025).Returns(families2025Plan);
        _organizationRepository.GetByIdAsync(organizationId).Returns(new Organization { Id = organizationId, PlanType = PlanType.FamiliesAnnually });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
    }

    [Fact]
    public async Task HandleAsync_ScheduleTriggered_CurrentPriceNotNewFamilies_DoesNotUpdateOrganization()
    {
        // Arrange — previous had old Families price but current doesn't have new Families price
        var organizationId = Guid.NewGuid();
        var familiesPlan = new FamiliesPlan();

        var subscription = new Subscription
        {
            Id = "sub_123",
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
                        data = new[] { new { price = new { id = "personal-org-annually" } } }
                    }
                })
            }
        };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesPlan);
        _organizationRepository.GetByIdAsync(organizationId).Returns(new Organization { Id = organizationId, PlanType = PlanType.FamiliesAnnually });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
    }

    [Fact]
    public async Task HandleAsync_ScheduleTriggered_NoItemChanges_DoesNotUpdateOrganization()
    {
        // Arrange — schedule present but PreviousAttributes has no items (e.g., status-only change)
        var organizationId = Guid.NewGuid();

        var subscription = new Subscription
        {
            Id = "sub_123",
            Status = SubscriptionStatus.Active,
            ScheduleId = "sub_sched_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(365) }]
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
                PreviousAttributes = JObject.FromObject(new { status = "active" })
            }
        };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);
        _organizationRepository.GetByIdAsync(organizationId).Returns(new Organization { Id = organizationId });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
    }

    [Fact]
    public async Task HandleAsync_ScheduleTriggeredMigration_WhenOrganizationNotFound_SkipsHandler()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var familiesPlan = new FamiliesPlan();
        var families2019Plan = new Families2019Plan();
        var families2025Plan = new Families2025Plan();

        var subscription = new Subscription
        {
            Id = "sub_123",
            Status = SubscriptionStatus.Active,
            ScheduleId = "sub_sched_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        CurrentPeriodEnd = DateTime.UtcNow.AddDays(365),
                        Price = new Price { Id = familiesPlan.PasswordManager.StripePlanId }
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
                        data = new[] { new { price = new { id = families2019Plan.PasswordManager.StripePlanId } } }
                    }
                })
            }
        };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesPlan);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019Plan);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2025).Returns(families2025Plan);
        _organizationRepository.GetByIdAsync(organizationId).ReturnsNull();

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — guard exits early — logs warning, does not throw, does not update
        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
    }

    [Fact]
    public async Task HandleAsync_ScheduleTriggered_MultipleItems_MatchesFamiliesPrice_UpdatesOrganization()
    {
        // Arrange — subscription has storage add-on alongside the Families price
        var organizationId = Guid.NewGuid();
        var familiesPlan = new FamiliesPlan();
        var families2019Plan = new Families2019Plan();
        var families2025Plan = new Families2025Plan();

        var subscription = new Subscription
        {
            Id = "sub_123",
            Status = SubscriptionStatus.Active,
            ScheduleId = "sub_sched_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        CurrentPeriodEnd = DateTime.UtcNow.AddDays(365),
                        Price = new Price { Id = familiesPlan.PasswordManager.StripePlanId }
                    },
                    new SubscriptionItem
                    {
                        CurrentPeriodEnd = DateTime.UtcNow.AddDays(365),
                        Price = new Price { Id = "personal-storage-gb-annually" },
                        Quantity = 2
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
                        data = new[] { new { price = new { id = families2019Plan.PasswordManager.StripePlanId } } }
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

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesPlan);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019Plan);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2025).Returns(families2025Plan);
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        Assert.Equal(PlanType.FamiliesAnnually, organization.PlanType);
        Assert.True(organization.UsersGetPremium);
        Assert.Equal(6, organization.Seats);
        await _organizationRepository.Received(1).ReplaceAsync(
            Arg.Is<Organization>(o => o.Id == organizationId));
    }

    [Fact]
    public async Task HandleAsync_UnpaidOrganizationSubscription_StampsCancellationOriginMetadata()
    {
        // The metadata stamp is the sole signal SubscriptionDeletedHandler uses to recognize
        // that the eventual customer.subscription.deleted came from the platform-managed
        // unpaid lifecycle and should void open invoices.
        var organizationId = Guid.NewGuid();
        const string subscriptionId = "sub_metadata_stamp";
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
                Data = [new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd, Plan = new Plan { Id = "2023-enterprise-org-seat-annually" } }]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization
        {
            Id = organizationId,
            Enabled = true,
            PlanType = PlanType.EnterpriseAnnually2023
        };

        var parsedEvent = new Event
        {
            Id = "evt_metadata_stamp",
            Data = new EventData
            {
                Object = subscription,
                PreviousAttributes = JObject.FromObject(previousSubscription)
            }
        };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(new Enterprise2023Plan(true));
        _pricingClient.ListPlans().Returns(MockPlans.Plans);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            subscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.Metadata != null &&
                options.Metadata.ContainsKey(MetadataKeys.CancellationOrigin) &&
                options.Metadata[MetadataKeys.CancellationOrigin] == CancellationOrigins.UnpaidSubscription));
    }

    [Fact]
    public async Task HandleAsync_ActiveFromUnpaidSubscription_ClearsCancellationOriginMetadata()
    {
        // When the customer pays and the subscription recovers, the marker must be removed
        // so a future voluntary cancel doesn't trigger an unwanted invoice void.
        // Stripe removes a metadata key when its value is set to empty string.
        var organizationId = Guid.NewGuid();
        const string subscriptionId = "sub_metadata_clear";
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
                Data = [new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd, Plan = new Plan { Id = "2023-enterprise-org-seat-annually" } }]
            },
            Metadata = new Dictionary<string, string>
            {
                { "organizationId", organizationId.ToString() },
                { MetadataKeys.CancellationOrigin, CancellationOrigins.UnpaidSubscription }
            },
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
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(new Enterprise2023Plan(true));
        _pricingClient.ListPlans().Returns(MockPlans.Plans);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            subscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.Metadata != null &&
                options.Metadata.ContainsKey(MetadataKeys.CancellationOrigin) &&
                options.Metadata[MetadataKeys.CancellationOrigin] == string.Empty));
    }

    [Fact]
    public async Task HandleAsync_UnpaidOrganizationSubscription_WithExemptOrganization_DoesNotDisableAndClearsExemption()
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

        var organization = new Organization
        {
            Id = organizationId,
            Enabled = true,
            ExemptFromBillingAutomation = true,
            PlanType = PlanType.EnterpriseAnnually2023
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

        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);

        var plan = new Enterprise2023Plan(true);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(plan);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _organizationDisableCommand.DidNotReceive()
            .DisableAsync(Arg.Any<Guid>(), Arg.Any<DateTime?>());
        await _stripeAdapter.DidNotReceive()
            .UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
        await _organizationRepository.Received(1).ReplaceAsync(
            Arg.Is<Organization>(o => o.Id == organizationId && !o.ExemptFromBillingAutomation));
    }

    [Fact]
    public async Task HandleAsync_UnpaidOrganizationSubscription_WithSubscriptionUpdateBillingReason_DoesNotDisableAndPreservesExemption()
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
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionUpdate }
        };

        var organization = new Organization
        {
            Id = organizationId,
            Enabled = true,
            ExemptFromBillingAutomation = true,
            PlanType = PlanType.EnterpriseAnnually2023
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

        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);

        var plan = new Enterprise2023Plan(true);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(plan);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — subscription_update billing reason does not match SubscriptionWentUnpaid
        // (which filters on subscription_create and subscription_cycle only), so no disable,
        // no cancellation, and the exempt flag is left unchanged.
        await _organizationDisableCommand.DidNotReceive()
            .DisableAsync(Arg.Any<Guid>(), Arg.Any<DateTime?>());
        await _stripeAdapter.DidNotReceive()
            .UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
        await _organizationRepository.DidNotReceive()
            .ReplaceAsync(Arg.Any<Organization>());
    }

    [Fact]
    public async Task HandleAsync_UnpaidOrganizationSubscription_WithAutomaticPendingInvoiceItemInvoiceBillingReason_DoesNotDisableAndPreservesExemption()
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
            LatestInvoice = new Invoice { BillingReason = BillingReasons.AutomaticPendingInvoiceItemInvoice }
        };

        var organization = new Organization
        {
            Id = organizationId,
            Enabled = true,
            ExemptFromBillingAutomation = true,
            PlanType = PlanType.EnterpriseAnnually2023
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

        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);

        var plan = new Enterprise2023Plan(true);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(plan);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — automatic_pending_invoice_item_invoice does not match SubscriptionWentUnpaid
        // (which filters on subscription_create and subscription_cycle only), so no disable,
        // no cancellation, and the exempt flag is left unchanged.
        await _organizationDisableCommand.DidNotReceive()
            .DisableAsync(Arg.Any<Guid>(), Arg.Any<DateTime?>());
        await _stripeAdapter.DidNotReceive()
            .UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
        await _organizationRepository.DidNotReceive()
            .ReplaceAsync(Arg.Any<Organization>());
    }

    [Fact]
    public async Task HandleAsync_UnpaidOrganizationSubscription_WithExemptOrganization_WhenSubsequentWorkFails_DoesNotClearExemption()
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

        var organization = new Organization
        {
            Id = organizationId,
            Enabled = true,
            ExemptFromBillingAutomation = true,
            PlanType = PlanType.EnterpriseAnnually2023
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

        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);

        _organizationService.UpdateExpirationDateAsync(organizationId, Arg.Any<DateTime?>())
            .ThrowsAsync(new Exception("Simulated failure in subsequent work"));

        // Act
        await Assert.ThrowsAsync<Exception>(() => _sut.HandleAsync(parsedEvent));

        // Assert — the flag clear must not have been persisted
        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
    }

    [Fact]
    public async Task HandleAsync_WhenUserNotFound_SkipsHandler()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subscriptionId = "sub_123";

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
                Data = [new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }]
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

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);
        _userRepository.GetByIdAsync(userId).ReturnsNull();

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — guard exits early, no side effects
        await _userService.DidNotReceive().DisablePremiumAsync(Arg.Any<Guid>(), Arg.Any<DateTime?>());
        await _userService.DidNotReceive().UpdatePremiumExpirationAsync(Arg.Any<Guid>(), Arg.Any<DateTime?>());
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task HandleAsync_WhenOrganizationNotFound_SkipsHandler()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var subscriptionId = "sub_123";

        var previousSubscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Active
        };

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Unpaid,
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }]
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

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);
        _organizationRepository.GetByIdAsync(organizationId).ReturnsNull();

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — guard exits early, no side effects
        await _organizationDisableCommand.DidNotReceive().DisableAsync(Arg.Any<Guid>(), Arg.Any<DateTime?>());
        await _organizationService.DidNotReceive().UpdateExpirationDateAsync(Arg.Any<Guid>(), Arg.Any<DateTime?>());
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task HandleAsync_BusinessMigration_NoScheduleId_DoesNothing()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var subscriptionId = "sub_no_schedule";

        var previousSubscription = new Subscription
        {
            Id = subscriptionId,
            Items = new StripeList<SubscriptionItem> { Data = [] }
        };

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Active,
            ScheduleId = null,
            Items = new StripeList<SubscriptionItem> { Data = [] },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization { Id = organizationId, PlanType = PlanType.EnterpriseAnnually2020 };

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
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(new Enterprise2020Plan(true));
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly2020).Returns(new Enterprise2020Plan(false));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually2020).Returns(new Teams2020Plan(true));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(new Teams2020Plan(false));
        _pricingClient.ListPlans().Returns(MockPlans.Plans);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — handler bails on null ScheduleId; no org or assignment writes
        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
        await _cohortAssignmentRepository.DidNotReceive()
            .ReplaceAsync(Arg.Any<Bit.Core.Billing.Organizations.PlanMigration.Entities.OrganizationPlanMigrationCohortAssignment>());
    }

    [Fact]
    public async Task HandleAsync_BusinessMigration_PreviousPriceNotIn2020Allowlist_DoesNothing()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var subscriptionId = "sub_unrelated_item";
        var enterprise2020Annual = new Enterprise2020Plan(true);

        var previousSubscription = new Subscription
        {
            Id = subscriptionId,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = "price_unrelated_storage" },
                        Plan = new Plan { Id = "price_unrelated_storage" }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Active,
            ScheduleId = "sub_sched_abc",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = "2023-enterprise-org-seat-annually" },
                        Plan = new Plan { Id = "2023-enterprise-org-seat-annually" }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization { Id = organizationId, PlanType = PlanType.EnterpriseAnnually2020 };

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
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Annual);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly2020).Returns(new Enterprise2020Plan(false));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually2020).Returns(new Teams2020Plan(true));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(new Teams2020Plan(false));
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(
            new OrganizationPlanMigrationCohortAssignment
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                CohortId = cohortId
            });
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "Enterprise2020Annual",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            IsActive = true
        });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — handler resolved the path, then bailed at the source-price intersection.
        // No target-plan lookup, no org write, no assignment stamp.
        await _pricingClient.DidNotReceive().GetPlanOrThrow(PlanType.EnterpriseAnnually);
        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
        await _cohortAssignmentRepository.DidNotReceive().ReplaceAsync(Arg.Any<OrganizationPlanMigrationCohortAssignment>());
    }

    [Fact]
    public async Task HandleAsync_BusinessMigration_FeatureFlagOff_DoesNothing()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var subscriptionId = "sub_flag_off";

        var previousSubscription = new Subscription
        {
            Id = subscriptionId,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = "2020-enterprise-org-seat-annually" },
                        Plan = new Plan { Id = "2020-enterprise-org-seat-annually" }
                    }
                ]
            }
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
                        Price = new Price { Id = "2023-enterprise-org-seat-annually" },
                        Plan = new Plan { Id = "2023-enterprise-org-seat-annually" }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization { Id = organizationId, PlanType = PlanType.EnterpriseAnnually2020 };

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
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(false);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(new Enterprise2020Plan(true));
        _pricingClient.ListPlans().Returns(MockPlans.Plans);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — dispatch gate (feature flag) is the outermost guard; no cohort lookups happen
        await _cohortRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
        await _cohortAssignmentRepository.DidNotReceive().GetByOrganizationIdAsync(Arg.Any<Guid>());
        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
    }

    [Fact]
    public async Task HandleAsync_BusinessMigration_NoAssignment_SkipsSilently()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var enterprise2020Annual = new Enterprise2020Plan(true);

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_no_assignment",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { Price = new Price { Id = "price_target_current" }, Plan = new Plan { Id = "price_target_current" } }]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization { Id = organizationId, PlanType = PlanType.EnterpriseAnnually2020 };

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
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Annual);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly2020).Returns(new Enterprise2020Plan(false));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually2020).Returns(new Teams2020Plan(true));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(new Teams2020Plan(false));
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).ReturnsNull();

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — no cohort lookup, no DB writes, no warning (most orgs won't be in a cohort
        // so logging per-event would be noisy)
        await _cohortRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
        await _cohortAssignmentRepository.DidNotReceive().ReplaceAsync(Arg.Any<OrganizationPlanMigrationCohortAssignment>());
        _logger.DidNotReceive().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());
    }

    [Fact]
    public async Task HandleAsync_BusinessMigration_AssignmentExistsCohortMissing_LogsWarningAndSkips()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var enterprise2020Annual = new Enterprise2020Plan(true);

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_cohort_missing",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { Price = new Price { Id = "price_target_current" }, Plan = new Plan { Id = "price_target_current" } }]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization { Id = organizationId, PlanType = PlanType.EnterpriseAnnually2020 };

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
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Annual);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly2020).Returns(new Enterprise2020Plan(false));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually2020).Returns(new Teams2020Plan(true));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(new Teams2020Plan(false));
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(
            new OrganizationPlanMigrationCohortAssignment
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                CohortId = cohortId
            });
        _cohortRepository.GetByIdAsync(cohortId).ReturnsNull();

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — cohort missing, no DB writes
        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
        await _cohortAssignmentRepository.DidNotReceive().ReplaceAsync(Arg.Any<OrganizationPlanMigrationCohortAssignment>());
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString().Contains(organizationId.ToString()) && o.ToString().Contains(cohortId.ToString())),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());
    }

    [Fact]
    public async Task HandleAsync_BusinessMigration_AssignmentAlreadyMigrated_LogsInfoAndSkips()
    {
        // Arrange — assignment is already migrated. Idempotency check fires before cohort lookup.
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var enterprise2020Annual = new Enterprise2020Plan(true);

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_idempotent",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { Price = new Price { Id = "price_target_current" }, Plan = new Plan { Id = "price_target_current" } }]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization { Id = organizationId, PlanType = PlanType.EnterpriseAnnually2020 };

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
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Annual);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly2020).Returns(new Enterprise2020Plan(false));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually2020).Returns(new Teams2020Plan(true));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(new Teams2020Plan(false));
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(
            new OrganizationPlanMigrationCohortAssignment
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                CohortId = cohortId,
                MigratedDate = DateTime.UtcNow.AddMinutes(-5)  // already migrated
            });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — idempotency: cohort lookup NEVER happens, no writes
        await _cohortRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
        await _cohortAssignmentRepository.DidNotReceive().ReplaceAsync(Arg.Any<OrganizationPlanMigrationCohortAssignment>());
    }

    [Fact]
    public async Task HandleAsync_BusinessMigration_TargetPriceNotInCurrentItems_LogsWarningAndSkips()
    {
        // Arrange — sanity check fires when current items don't carry the target plan's price.
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var enterprise2020Annual = new Enterprise2020Plan(true);
        var enterpriseAnnual = new EnterprisePlan(true);

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_sanity_fail",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = "price_something_else" },
                        Plan = new Plan { Id = "price_something_else" }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization { Id = organizationId, PlanType = PlanType.EnterpriseAnnually2020 };

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
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Annual);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly2020).Returns(new Enterprise2020Plan(false));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually2020).Returns(new Teams2020Plan(true));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(new Teams2020Plan(false));
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(enterpriseAnnual);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(
            new OrganizationPlanMigrationCohortAssignment
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                CohortId = cohortId
            });
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "Enterprise2020Annual",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            IsActive = true
        });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — sanity check fired, no writes
        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
        await _cohortAssignmentRepository.DidNotReceive().ReplaceAsync(Arg.Any<OrganizationPlanMigrationCohortAssignment>());
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString().Contains(organizationId.ToString())),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());
    }

    [Fact]
    public async Task HandleAsync_BusinessMigration_EnterpriseAnnual2020ToCurrent_AppliesAndMarksMigrated()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var enterprise2020Annual = new Enterprise2020Plan(true);
        var enterpriseAnnual = new EnterprisePlan(true);

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_happy_ea",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterpriseAnnual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterpriseAnnual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.EnterpriseAnnually2020,
            Plan = enterprise2020Annual.Name,
            UseScim = false,
            Seats = 200,
            MaxStorageGb = 50,
        };

        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = assignmentId,
            OrganizationId = organizationId,
            CohortId = cohortId
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
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Annual);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly2020).Returns(new Enterprise2020Plan(false));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually2020).Returns(new Teams2020Plan(true));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(new Teams2020Plan(false));
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(enterpriseAnnual);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "Enterprise2020Annual",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            IsActive = true
        });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — plan shape applied
        Assert.Equal(PlanType.EnterpriseAnnually, organization.PlanType);
        Assert.Equal(enterpriseAnnual.Name, organization.Plan);
        Assert.Equal(enterpriseAnnual.HasScim, organization.UseScim);
        Assert.True(organization.UsePasswordManager);
        Assert.Equal(enterpriseAnnual.UsersGetPremium, organization.UsersGetPremium);
        Assert.Equal(enterpriseAnnual.PasswordManager.MaxCollections, organization.MaxCollections);

        // Allocation preserved
        Assert.Equal((short)200, organization.Seats);
        Assert.Equal((short)50, organization.MaxStorageGb);

        await _organizationRepository.Received(1).ReplaceAsync(Arg.Is<Organization>(o =>
            o.Id == organizationId && o.PlanType == PlanType.EnterpriseAnnually));

        Assert.NotNull(assignment.MigratedDate);
        Assert.NotEqual(default, assignment.RevisionDate);
        await _cohortAssignmentRepository.Received(1).ReplaceAsync(
            Arg.Is<OrganizationPlanMigrationCohortAssignment>(a => a.Id == assignmentId && a.MigratedDate.HasValue));
    }

    // PM-37510 (T2): migrating Enterprise 2020 (base 200) to current (base 50) writes a grace of 150
    // onto the subscription metadata, merged with the existing metadata, with no item/quantity change.
    [Fact]
    public async Task HandleAsync_BusinessMigration_EnterpriseAnnual2020ToCurrent_WritesGraceMetadata()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var enterprise2020Annual = new Enterprise2020Plan(true);
        var enterpriseAnnual = new EnterprisePlan(true);

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_grace_ea",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterpriseAnnual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterpriseAnnual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.EnterpriseAnnually2020,
            Plan = enterprise2020Annual.Name,
            Seats = 200,
            SmServiceAccounts = 200
        };

        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CohortId = cohortId
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
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Annual);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(enterpriseAnnual);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "Enterprise2020Annual",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            IsActive = true
        });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — grace of 150 written, merged with existing metadata, no item change.
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.Metadata[MetadataKeys.MigrationGraceServiceAccounts] == "150" &&
                options.Metadata["organizationId"] == organizationId.ToString() &&
                options.Items == null));

        Assert.NotNull(assignment.MigratedDate);
    }

    // PM-37510 (T3): grace is entitlement-based (200 -> 50 => 150), independent of the org's actual
    // SmServiceAccounts. A 250-account org still gets grace 150, and the handler writes only metadata —
    // it never forces a billed quantity (the scheduler's 1:1 price mapping already carried it forward).
    [Fact]
    public async Task HandleAsync_BusinessMigration_OrgAboveBaseline_GraceIsEntitlementBased_NoQuantityForced()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var enterprise2020Annual = new Enterprise2020Plan(true);
        var enterpriseAnnual = new EnterprisePlan(true);

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_grace_250",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterpriseAnnual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterpriseAnnual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.EnterpriseAnnually2020,
            Plan = enterprise2020Annual.Name,
            Seats = 200,
            SmServiceAccounts = 250
        };

        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CohortId = cohortId
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
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Annual);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(enterpriseAnnual);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "Enterprise2020Annual",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            IsActive = true
        });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — grace is still the entitlement constant (150), not derived from 250; no items forced.
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.Metadata[MetadataKeys.MigrationGraceServiceAccounts] == "150" &&
                options.Items == null));
    }

    // PM-37510 (T10): if the grace metadata write fails, MigratedDate must NOT be stamped so Stripe
    // retries. This also proves the grace write happens before the MigratedDate stamp.
    [Fact]
    public async Task HandleAsync_BusinessMigration_GraceWriteFails_DoesNotStampMigratedDate()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var enterprise2020Annual = new Enterprise2020Plan(true);
        var enterpriseAnnual = new EnterprisePlan(true);

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_grace_fail",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterpriseAnnual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterpriseAnnual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.EnterpriseAnnually2020,
            Plan = enterprise2020Annual.Name,
            Seats = 200,
            SmServiceAccounts = 200
        };

        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CohortId = cohortId
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
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Annual);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(enterpriseAnnual);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "Enterprise2020Annual",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            IsActive = true
        });

        _stripeAdapter.UpdateSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionUpdateOptions>())
            .Throws(new StripeException("boom"));

        // Act — the grace write fails. It is surfaced as a BillingException, which the method's
        // BillingException catch rethrows so the webhook 500s and Stripe retries (rather than the
        // generic catch swallowing it).
        await Assert.ThrowsAsync<BillingException>(() => _sut.HandleAsync(parsedEvent));

        // Assert — the assignment is left unstamped (grace write happens before the MigratedDate stamp),
        // so the Stripe replay re-runs idempotently.
        Assert.Null(assignment.MigratedDate);
        await _cohortAssignmentRepository.DidNotReceiveWithAnyArgs().ReplaceAsync(default);
    }

    // PM-37510 (T4): reduction-aware. When the target SM baseline is not smaller than the source
    // baseline, grace is 0 and no metadata write occurs. No current real migration path is
    // reduction-neutral, so the target plan here is a local double whose SM baseline matches the
    // source (200) while keeping a distinct PasswordManager price for the schedule-item matching.
    [Fact]
    public async Task HandleAsync_BusinessMigration_TargetBaselineNotSmaller_DoesNotWriteGrace()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var enterprise2020Annual = new Enterprise2020Plan(true); // source SM base 200
        var neutralTarget = new NeutralBaselineEnterprisePlan();  // target SM base 200, distinct PM price

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_no_grace",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = neutralTarget.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = neutralTarget.PasswordManager.StripeSeatPlanId }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.EnterpriseAnnually2020,
            Plan = enterprise2020Annual.Name,
            Seats = 200,
            SmServiceAccounts = 200
        };

        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CohortId = cohortId
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
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Annual);
        // Target plan resolves to one whose SM baseline equals the source (no reduction => grace 0).
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(neutralTarget);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "Enterprise2020Annual",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            IsActive = true
        });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — no grace metadata write; migration still completes.
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.Metadata != null &&
                options.Metadata.ContainsKey(MetadataKeys.MigrationGraceServiceAccounts)));

        Assert.NotNull(assignment.MigratedDate);
    }

    // Target-plan double for the reduction-neutral case: SM base 200 (== Enterprise 2020 source) but a
    // distinct PasswordManager seat price so the handler's source/target schedule-item matching works.
    private sealed record NeutralBaselineEnterprisePlan : Bit.Core.Models.StaticStore.Plan
    {
        public NeutralBaselineEnterprisePlan()
        {
            var enterprise = new EnterprisePlan(true);
            Type = enterprise.Type;
            ProductTier = enterprise.ProductTier;
            Name = enterprise.Name;
            IsAnnual = true;
            PasswordManager = new PasswordManagerFeatures();
            SecretsManager = new SecretsManagerFeatures();
        }

        private sealed record PasswordManagerFeatures : PasswordManagerPlanFeatures
        {
            public PasswordManagerFeatures()
            {
                BaseSeats = 0;
                StripeSeatPlanId = "neutral-enterprise-seat-annually";
                StripeStoragePlanId = "storage-gb-annually";
            }
        }

        private sealed record SecretsManagerFeatures : SecretsManagerPlanFeatures
        {
            public SecretsManagerFeatures()
            {
                BaseSeats = 0;
                BaseServiceAccount = 200; // equal to Enterprise 2020 => no reduction => grace 0
                StripeSeatPlanId = "neutral-secrets-manager-seat-annually";
                StripeServiceAccountPlanId = "neutral-secrets-manager-service-account-annually";
            }
        }
    }

    [Fact]
    public async Task HandleAsync_BusinessMigration_EnterpriseMonthly2020ToCurrent_AppliesAndMarksMigrated()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var enterprise2020Monthly = new Enterprise2020Plan(false);
        var enterpriseMonthly = new EnterprisePlan(false);

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterprise2020Monthly.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterprise2020Monthly.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_happy_em",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterpriseMonthly.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterpriseMonthly.PasswordManager.StripeSeatPlanId }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.EnterpriseMonthly2020,
            Plan = enterprise2020Monthly.Name,
            UseScim = false,
            Seats = 200,
            MaxStorageGb = 50,
        };

        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = assignmentId,
            OrganizationId = organizationId,
            CohortId = cohortId
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
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(new Enterprise2020Plan(true));
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly2020).Returns(enterprise2020Monthly);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually2020).Returns(new Teams2020Plan(true));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(new Teams2020Plan(false));
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly).Returns(enterpriseMonthly);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "Enterprise2020Monthly",
            MigrationPathId = MigrationPathId.Enterprise2020MonthlyToCurrent,
            IsActive = true
        });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        Assert.Equal(PlanType.EnterpriseMonthly, organization.PlanType);
        Assert.Equal(enterpriseMonthly.Name, organization.Plan);
        Assert.Equal((short)200, organization.Seats);
        Assert.Equal((short)50, organization.MaxStorageGb);

        await _organizationRepository.Received(1).ReplaceAsync(Arg.Is<Organization>(o =>
            o.Id == organizationId && o.PlanType == PlanType.EnterpriseMonthly));
        Assert.NotNull(assignment.MigratedDate);
        Assert.NotEqual(default, assignment.RevisionDate);
        await _cohortAssignmentRepository.Received(1).ReplaceAsync(
            Arg.Is<OrganizationPlanMigrationCohortAssignment>(a => a.Id == assignmentId && a.MigratedDate.HasValue));
    }

    [Fact]
    public async Task HandleAsync_BusinessMigration_TeamsAnnually2020ToCurrent_AppliesAndMarksMigrated()
    {
        // Arrange — Teams Track A: UseScim flips false -> true. Load-bearing capability gain.
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var teams2020Annual = new Teams2020Plan(true);
        var teamsAnnual = new TeamsPlan(true);

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = teams2020Annual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = teams2020Annual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_happy_ta",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = teamsAnnual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = teamsAnnual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.TeamsAnnually2020,
            Plan = teams2020Annual.Name,
            UseScim = false,  // Will flip to true
            Seats = 50,
            MaxStorageGb = 20,
        };

        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = assignmentId,
            OrganizationId = organizationId,
            CohortId = cohortId
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
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(new Enterprise2020Plan(true));
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly2020).Returns(new Enterprise2020Plan(false));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually2020).Returns(teams2020Annual);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(new Teams2020Plan(false));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(teamsAnnual);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "Teams2020Annual",
            MigrationPathId = MigrationPathId.Teams2020AnnualToCurrent,
            IsActive = true
        });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — UseScim flip is the headline capability gain
        Assert.Equal(PlanType.TeamsAnnually, organization.PlanType);
        Assert.True(teamsAnnual.HasScim);
        Assert.True(organization.UseScim);

        // Allocation preserved
        Assert.Equal((short)50, organization.Seats);
        Assert.Equal((short)20, organization.MaxStorageGb);

        await _organizationRepository.Received(1).ReplaceAsync(Arg.Is<Organization>(o =>
            o.Id == organizationId && o.PlanType == PlanType.TeamsAnnually));
        Assert.NotNull(assignment.MigratedDate);
        Assert.NotEqual(default, assignment.RevisionDate);
        await _cohortAssignmentRepository.Received(1).ReplaceAsync(
            Arg.Is<OrganizationPlanMigrationCohortAssignment>(a => a.Id == assignmentId && a.MigratedDate.HasValue));
    }

    [Fact]
    public async Task HandleAsync_BusinessMigration_TeamsMonthly2020ToCurrent_AppliesAndMarksMigrated()
    {
        // Arrange — Teams Track A: UseScim flips false -> true.
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var teams2020Monthly = new Teams2020Plan(false);
        var teamsMonthly = new TeamsPlan(false);

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = teams2020Monthly.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = teams2020Monthly.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_happy_tm",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = teamsMonthly.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = teamsMonthly.PasswordManager.StripeSeatPlanId }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.TeamsMonthly2020,
            Plan = teams2020Monthly.Name,
            UseScim = false,  // Will flip to true
            Seats = 25,
            MaxStorageGb = 10,
        };

        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = assignmentId,
            OrganizationId = organizationId,
            CohortId = cohortId
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
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(new Enterprise2020Plan(true));
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly2020).Returns(new Enterprise2020Plan(false));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually2020).Returns(new Teams2020Plan(true));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(teams2020Monthly);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(teamsMonthly);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "Teams2020Monthly",
            MigrationPathId = MigrationPathId.Teams2020MonthlyToCurrent,
            IsActive = true
        });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        Assert.Equal(PlanType.TeamsMonthly, organization.PlanType);
        Assert.True(teamsMonthly.HasScim);
        Assert.True(organization.UseScim);

        Assert.Equal((short)25, organization.Seats);
        Assert.Equal((short)10, organization.MaxStorageGb);

        await _organizationRepository.Received(1).ReplaceAsync(Arg.Is<Organization>(o =>
            o.Id == organizationId && o.PlanType == PlanType.TeamsMonthly));
        Assert.NotNull(assignment.MigratedDate);
        Assert.NotEqual(default, assignment.RevisionDate);
        await _cohortAssignmentRepository.Received(1).ReplaceAsync(
            Arg.Is<OrganizationPlanMigrationCohortAssignment>(a => a.Id == assignmentId && a.MigratedDate.HasValue));
    }

    [Fact]
    public async Task HandleAsync_BusinessMigration_TeamsMonthly2020ToCurrent_WritesGrace30Metadata()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var teams2020Monthly = new Teams2020Plan(false);
        var teamsMonthly = new TeamsPlan(false);

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = teams2020Monthly.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = teams2020Monthly.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_grace_tm",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = teamsMonthly.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = teamsMonthly.PasswordManager.StripeSeatPlanId }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.TeamsMonthly2020,
            Plan = teams2020Monthly.Name,
            Seats = 25,
            SmServiceAccounts = 35
        };

        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CohortId = cohortId
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
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(teams2020Monthly);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(teamsMonthly);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "Teams2020Monthly",
            MigrationPathId = MigrationPathId.Teams2020MonthlyToCurrent,
            IsActive = true
        });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — grace of 30 written, merged with existing metadata, no item change.
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.Metadata[MetadataKeys.MigrationGraceServiceAccounts] == "30" &&
                options.Metadata["organizationId"] == organizationId.ToString() &&
                options.Items == null));

        Assert.NotNull(assignment.MigratedDate);
    }

    // PM-37512: Teams Starter 2023's SM baseline (50) exceeds Teams Current's (20), so the generic grace
    // machinery writes grace 30 automatically — no SM-specific production code.
    [Fact]
    public async Task HandleAsync_BusinessMigration_TeamsStarter2023ToCurrent_WritesGrace30Metadata()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var teamsStarter2023 = new TeamsStarterPlan2023(); // source SM base 50
        var teamsMonthly = new TeamsPlan(false);           // target SM base 20

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = teamsStarter2023.PasswordManager.StripePlanId },
                        Plan = new Plan { Id = teamsStarter2023.PasswordManager.StripePlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_grace_ts2023",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = teamsMonthly.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = teamsMonthly.PasswordManager.StripeSeatPlanId }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.TeamsStarter2023,
            Plan = teamsStarter2023.Name,
            Seats = 10,
            SmServiceAccounts = 50
        };

        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CohortId = cohortId
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
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsStarter2023).Returns(teamsStarter2023);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(teamsMonthly);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "TeamsStarter2023",
            MigrationPathId = MigrationPathId.TeamsStarter2023ToCurrent,
            IsActive = true
        });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — grace of 30 (50 - 20) written, merged with existing metadata, no item change.
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.Metadata[MetadataKeys.MigrationGraceServiceAccounts] == "30" &&
                options.Metadata["organizationId"] == organizationId.ToString() &&
                options.Items == null));

        Assert.NotNull(assignment.MigratedDate);
    }

    // PM-39569 / PM-39286: in the test-clock-driven QA migration flow, Stripe rejects writes to a
    // subscription whose test clock is still advancing. The handler must wait for the clock to settle
    // BEFORE the grace write; otherwise the write 500s and the org is left plan-changed but with
    // MigratedDate unstamped (stuck in "Scheduled"). The wait is a no-op in production (TestClock null).
    [Fact]
    public async Task HandleAsync_BusinessMigration_WithTestClock_WaitsForClockBeforeGraceWrite()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var teamsStarter2023 = new TeamsStarterPlan2023(); // source SM base 50
        var teamsMonthly = new TeamsPlan(false);           // target SM base 20

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = teamsStarter2023.PasswordManager.StripePlanId },
                        Plan = new Plan { Id = teamsStarter2023.PasswordManager.StripePlanId }
                    }
                ]
            }
        };

        var testClock = new TestClock { Id = "clock_advancing", Status = TestClockStatus.Ready };

        var subscription = new Subscription
        {
            Id = "sub_grace_testclock",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = teamsMonthly.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = teamsMonthly.PasswordManager.StripeSeatPlanId }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle },
            TestClock = testClock
        };

        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.TeamsStarter2023,
            Plan = teamsStarter2023.Name,
            Seats = 10,
            SmServiceAccounts = 50
        };

        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CohortId = cohortId
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
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsStarter2023).Returns(teamsStarter2023);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(teamsMonthly);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "TeamsStarter2023",
            MigrationPathId = MigrationPathId.TeamsStarter2023ToCurrent,
            IsActive = true
        });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — the handler waited on the subscription's test clock before writing grace metadata.
        // The ordering is the regression guard: writing first is exactly what 500s the webhook.
        await _stripeAdapter.Received(1).WaitForTestClockToAdvanceAsync(testClock);

        Received.InOrder(() =>
        {
            _stripeAdapter.WaitForTestClockToAdvanceAsync(testClock);
            _stripeAdapter.UpdateSubscriptionAsync(
                subscription.Id,
                Arg.Is<SubscriptionUpdateOptions>(options =>
                    options.Metadata[MetadataKeys.MigrationGraceServiceAccounts] == "30"));
        });

        // And the migration completed: grace written + MigratedDate stamped (org -> Migrated).
        Assert.NotNull(assignment.MigratedDate);
    }

    // PM-37512: plain Teams Starter's SM baseline (20) already equals Teams Current's (20), so 20 - 20 = 0:
    // no grace metadata is written, but the migration still completes.
    [Fact]
    public async Task HandleAsync_BusinessMigration_TeamsStarterToCurrent_DoesNotWriteGrace()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var teamsStarter = new TeamsStarterPlan(); // source SM base 20
        var teamsMonthly = new TeamsPlan(false);   // target SM base 20

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = teamsStarter.PasswordManager.StripePlanId },
                        Plan = new Plan { Id = teamsStarter.PasswordManager.StripePlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_no_grace_ts",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = teamsMonthly.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = teamsMonthly.PasswordManager.StripeSeatPlanId }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.TeamsStarter,
            Plan = teamsStarter.Name,
            Seats = 10,
            SmServiceAccounts = 20
        };

        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CohortId = cohortId
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
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsStarter).Returns(teamsStarter);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(teamsMonthly);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "TeamsStarter",
            MigrationPathId = MigrationPathId.TeamsStarterToCurrent,
            IsActive = true
        });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — no grace metadata write; migration still completes.
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.Metadata != null &&
                options.Metadata.ContainsKey(MetadataKeys.MigrationGraceServiceAccounts)));

        Assert.NotNull(assignment.MigratedDate);
    }

    // PM-39562: packaged source's Seats holds a flat bundle cap; reconcile to the billed per-seat quantity.
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task HandleAsync_BusinessMigration_TeamsStarterToCurrent_ReconcilesSeatsToBilledQuantity(long billedSeats)
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var teamsStarter = new TeamsStarterPlan(); // packaged source, flat bundle cap of 10 seats
        var teamsMonthly = new TeamsPlan(false);   // per-seat target

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = teamsStarter.PasswordManager.StripePlanId },
                        Plan = new Plan { Id = teamsStarter.PasswordManager.StripePlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_reconcile_ts",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    // SM seat line shares the subscription; the fix must pick the PM seat line by price id, not this one.
                    new SubscriptionItem
                    {
                        Price = new Price { Id = teamsMonthly.SecretsManager.StripeSeatPlanId },
                        Plan = new Plan { Id = teamsMonthly.SecretsManager.StripeSeatPlanId },
                        Quantity = 99
                    },
                    new SubscriptionItem
                    {
                        Price = new Price { Id = teamsMonthly.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = teamsMonthly.PasswordManager.StripeSeatPlanId },
                        Quantity = billedSeats
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.TeamsStarter,
            Plan = teamsStarter.Name,
            Seats = 10,
            SmServiceAccounts = 20
        };

        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CohortId = cohortId
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
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsStarter).Returns(teamsStarter);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(teamsMonthly);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "TeamsStarter",
            MigrationPathId = MigrationPathId.TeamsStarterToCurrent,
            IsActive = true
        });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — Seats reconciled to the billed seat line item quantity and persisted.
        Assert.Equal((int)billedSeats, organization.Seats);
        await _organizationRepository.Received(1).ReplaceAsync(Arg.Is<Organization>(o =>
            o.Id == organizationId &&
            o.PlanType == PlanType.TeamsMonthly &&
            o.Seats == (int)billedSeats));

        Assert.NotNull(assignment.MigratedDate);
    }

    // PM-39562: gate is false for a per-seat source, so Seats stays put even when it differs from the billed quantity.
    [Fact]
    public async Task HandleAsync_BusinessMigration_PerSeatSource_LeavesSeatsUnchanged()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var teams2020Monthly = new Teams2020Plan(false); // per-seat source, gate false
        var teamsMonthly = new TeamsPlan(false);         // per-seat target

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = teams2020Monthly.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = teams2020Monthly.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_unchanged_seats",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = teamsMonthly.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = teamsMonthly.PasswordManager.StripeSeatPlanId },
                        Quantity = 7
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.TeamsMonthly2020,
            Plan = teams2020Monthly.Name,
            Seats = 25,
            SmServiceAccounts = 80
        };

        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CohortId = cohortId
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
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(teams2020Monthly);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(teamsMonthly);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "Teams2020Monthly",
            MigrationPathId = MigrationPathId.Teams2020MonthlyToCurrent,
            IsActive = true
        });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — per-seat source: Seats left untouched despite the differing line item quantity.
        Assert.Equal(25, organization.Seats);
        await _organizationRepository.Received(1).ReplaceAsync(Arg.Is<Organization>(o =>
            o.Id == organizationId &&
            o.PlanType == PlanType.TeamsMonthly &&
            o.Seats == 25));

        Assert.NotNull(assignment.MigratedDate);
    }

    // Grace is the per-path entitlement constant (50 -> 20 => 30), independent of the org's own account count.
    [Fact]
    public async Task HandleAsync_BusinessMigration_TeamsMonthly_OrgAboveBaseline_GraceIsEntitlementBased_NoQuantityForced()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var teams2020Monthly = new Teams2020Plan(false);
        var teamsMonthly = new TeamsPlan(false);

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = teams2020Monthly.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = teams2020Monthly.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_grace_tm_80",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = teamsMonthly.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = teamsMonthly.PasswordManager.StripeSeatPlanId }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.TeamsMonthly2020,
            Plan = teams2020Monthly.Name,
            Seats = 25,
            SmServiceAccounts = 80
        };

        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CohortId = cohortId
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
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(teams2020Monthly);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(teamsMonthly);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "Teams2020Monthly",
            MigrationPathId = MigrationPathId.Teams2020MonthlyToCurrent,
            IsActive = true
        });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — grace is still the entitlement constant (30), not derived from 80; no items forced.
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.Metadata[MetadataKeys.MigrationGraceServiceAccounts] == "30" &&
                options.Items == null));

        Assert.NotNull(assignment.MigratedDate);
    }

    // Teams Annually is unchanged at SM base 50, so 50 - 50 = 0: no grace is written, but the migration still completes.
    [Fact]
    public async Task HandleAsync_BusinessMigration_TeamsAnnually2020ToCurrent_DoesNotWriteGrace()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var teams2020Annual = new Teams2020Plan(true);
        var teamsAnnual = new TeamsPlan(true);

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = teams2020Annual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = teams2020Annual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_no_grace_ta",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = teamsAnnual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = teamsAnnual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.TeamsAnnually2020,
            Plan = teams2020Annual.Name,
            Seats = 50,
            SmServiceAccounts = 80
        };

        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CohortId = cohortId
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
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually2020).Returns(teams2020Annual);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(teamsAnnual);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "Teams2020Annual",
            MigrationPathId = MigrationPathId.Teams2020AnnualToCurrent,
            IsActive = true
        });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — no grace metadata write (50 - 50 = 0); migration still completes.
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.Metadata != null &&
                options.Metadata.ContainsKey(MetadataKeys.MigrationGraceServiceAccounts)));

        Assert.NotNull(assignment.MigratedDate);
    }

    [Fact]
    public async Task HandleAsync_BusinessMigration_TeamsMonthly_FeatureFlagOff_DoesNotEstablishGrace()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var teams2020Monthly = new Teams2020Plan(false);
        var teamsMonthly = new TeamsPlan(false);

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = teams2020Monthly.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = teams2020Monthly.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_flag_off_tm",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = teamsMonthly.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = teamsMonthly.PasswordManager.StripeSeatPlanId }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization { Id = organizationId, PlanType = PlanType.TeamsMonthly2020 };

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
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(false);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(teams2020Monthly);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — dispatch gate (feature flag) is the outermost guard; no cohort lookups, no grace,
        // no org write, no subscription metadata update.
        await _cohortRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
        await _cohortAssignmentRepository.DidNotReceive().GetByOrganizationIdAsync(Arg.Any<Guid>());
        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task HandleAsync_BusinessMigration_PreviousAttributesHasNoItemsData_LogsWarningAndSkips()
    {
        // Arrange — Stripe ships customer.subscription.updated payloads where
        // PreviousAttributes exists but carries no `items.data` (e.g., metadata-only
        // changes). The business handler must bail before reaching the cohort
        // lookup or the pricing-service allowlist construction.
        var organizationId = Guid.NewGuid();

        // Serialize an empty Subscription (no `items` data) as PreviousAttributes.
        // The handler short-circuits at `previousSubscription?.Items?.Data == null`.
        var previousSubscription = new Subscription { Id = "sub_metadata_change" };

        var subscription = new Subscription
        {
            Id = "sub_metadata_change",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem> { Data = [] },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization { Id = organizationId, PlanType = PlanType.EnterpriseAnnually2020 };

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
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        // Downstream handlers in HandleAsync also consult the pricing client; provide
        // the mocks they need so the assertion below only proves the business
        // handler skipped its own allowlist + cohort work.
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(new Enterprise2020Plan(true));
        _pricingClient.ListPlans().Returns(MockPlans.Plans);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — business handler bailed: no cohort lookup, no migration writes, warning logged
        await _cohortAssignmentRepository.DidNotReceive().GetByOrganizationIdAsync(Arg.Any<Guid>());
        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
        await _cohortAssignmentRepository.DidNotReceive().ReplaceAsync(Arg.Any<OrganizationPlanMigrationCohortAssignment>());
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString().Contains(organizationId.ToString())),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());
    }

    [Fact]
    public async Task HandleAsync_BusinessMigration_CohortMigrationPathIdNull_LogsWarningAndSkips()
    {
        // Arrange — cohort row exists but has no MigrationPathId (admin paused the
        // cohort or it predates the path-assignment workflow). Handler must skip.
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var enterprise2020Annual = new Enterprise2020Plan(true);

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_cohort_no_path",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { Price = new Price { Id = "price_target_current" }, Plan = new Plan { Id = "price_target_current" } }]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization { Id = organizationId, PlanType = PlanType.EnterpriseAnnually2020 };

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
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Annual);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly2020).Returns(new Enterprise2020Plan(false));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually2020).Returns(new Teams2020Plan(true));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(new Teams2020Plan(false));
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(
            new OrganizationPlanMigrationCohortAssignment
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                CohortId = cohortId
            });
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "PausedCohort",
            MigrationPathId = null,
            IsActive = true
        });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — null MigrationPathId is a skip; no target-plan lookup, no writes
        await _pricingClient.DidNotReceive().GetPlanOrThrow(PlanType.EnterpriseAnnually);
        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
        await _cohortAssignmentRepository.DidNotReceive().ReplaceAsync(Arg.Any<OrganizationPlanMigrationCohortAssignment>());
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString().Contains(organizationId.ToString()) && o.ToString().Contains(cohortId.ToString())),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());
    }

    [Fact]
    public async Task HandleAsync_BusinessMigration_CohortReferencesUnregisteredMigrationPathId_LogsWarningAndSkips()
    {
        // Arrange — cohort row cites a MigrationPathId byte value that the in-memory
        // registry no longer recognizes (forward-compat case where a path was added
        // to the enum but MigrationPaths.All was not updated). Handler must skip
        // rather than NRE.
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var enterprise2020Annual = new Enterprise2020Plan(true);

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_unregistered_path",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { Price = new Price { Id = "price_target_current" }, Plan = new Plan { Id = "price_target_current" } }]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization { Id = organizationId, PlanType = PlanType.EnterpriseAnnually2020 };

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
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Annual);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly2020).Returns(new Enterprise2020Plan(false));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually2020).Returns(new Teams2020Plan(true));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(new Teams2020Plan(false));
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(
            new OrganizationPlanMigrationCohortAssignment
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                CohortId = cohortId
            });
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "ForwardCompatCohort",
            // A byte the registry does not know about. Cast around the enum's named
            // members to simulate a persisted row from a future deployment.
            MigrationPathId = (MigrationPathId)99,
            IsActive = true
        });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — unregistered path is a safe skip; no NRE, no target lookup, no writes
        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
        await _cohortAssignmentRepository.DidNotReceive().ReplaceAsync(Arg.Any<OrganizationPlanMigrationCohortAssignment>());
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString().Contains(organizationId.ToString()) && o.ToString().Contains(cohortId.ToString())),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());
    }

    [Fact]
    public async Task HandleAsync_BusinessMigration_PricingServiceThrowsBillingException_RethrowsForStripeRetry()
    {
        // Arrange — a BillingException from the pricing client must bubble out of
        // the handler so the webhook returns 500 and Stripe retries the event.
        // Swallowing it would mark the migration "handled" without applying it.
        var organizationId = Guid.NewGuid();
        var enterprise2020Annual = new Enterprise2020Plan(true);

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_pricing_outage",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { Price = new Price { Id = "price_target_current" }, Plan = new Plan { Id = "price_target_current" } }]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization { Id = organizationId, PlanType = PlanType.EnterpriseAnnually2020 };

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
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        // The first allowlist call throws — simulating pricing-service unavailability
        // partway through allowlist construction.
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020)
            .Throws(new BillingException(message: "pricing service unavailable"));

        // Act + Assert — BillingException must propagate out of HandleAsync
        await Assert.ThrowsAsync<BillingException>(() => _sut.HandleAsync(parsedEvent));

        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
        await _cohortAssignmentRepository.DidNotReceive().ReplaceAsync(Arg.Any<OrganizationPlanMigrationCohortAssignment>());
    }

    [Fact]
    public async Task HandleAsync_BusinessMigration_OrganizationLookupReturnsNull_LogsWarningAndSkips()
    {
        // Arrange — gating passes but the organization row was deleted between the
        // dispatcher's earlier subscriber fetch and this handler's lookup. Handler
        // must skip without writing.
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var enterprise2020Annual = new Enterprise2020Plan(true);
        var enterpriseAnnual = new EnterprisePlan(true);

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_org_missing",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterpriseAnnual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterpriseAnnual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        // Dispatcher's initial subscriber lookup returns a stub so HandleAsync routes
        // to the Organization branch; the handler's own lookup returns null.
        var dispatcherOrg = new Organization { Id = organizationId, PlanType = PlanType.EnterpriseAnnually2020 };
        _organizationRepository.GetByIdAsync(organizationId).Returns(dispatcherOrg, (Organization?)null);

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
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Annual);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly2020).Returns(new Enterprise2020Plan(false));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually2020).Returns(new Teams2020Plan(true));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(new Teams2020Plan(false));
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(enterpriseAnnual);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(
            new OrganizationPlanMigrationCohortAssignment
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                CohortId = cohortId
            });
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "Enterprise2020Annual",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            IsActive = true
        });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — handler reached the org lookup, saw null, and skipped without writing
        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
        await _cohortAssignmentRepository.DidNotReceive().ReplaceAsync(Arg.Any<OrganizationPlanMigrationCohortAssignment>());
    }

    [Fact]
    public async Task HandleAsync_BusinessMigration_OrganizationReplaceThrows_SwallowsAndLogsError()
    {
        // Arrange — a non-BillingException raised during the write phase must be
        // logged and absorbed so the rest of HandleAsync (UpdateExpirationDate,
        // sponsorship renewal, etc.) still runs and the webhook returns 200.
        // The current contract intentionally does not retry on these failures.
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var enterprise2020Annual = new Enterprise2020Plan(true);
        var enterpriseAnnual = new EnterprisePlan(true);

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_replace_throws",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterpriseAnnual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterpriseAnnual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization { Id = organizationId, PlanType = PlanType.EnterpriseAnnually2020 };
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = assignmentId,
            OrganizationId = organizationId,
            CohortId = cohortId
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
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Annual);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly2020).Returns(new Enterprise2020Plan(false));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually2020).Returns(new Teams2020Plan(true));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(new Teams2020Plan(false));
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(enterpriseAnnual);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "Enterprise2020Annual",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            IsActive = true
        });
        _organizationRepository.ReplaceAsync(Arg.Any<Organization>())
            .Throws(new InvalidOperationException("simulated DB failure"));

        // Act — must NOT throw; generic exceptions are absorbed by the catch-all
        await _sut.HandleAsync(parsedEvent);

        // Assert — assignment is NOT marked migrated when the org write fails;
        // the next webhook will re-attempt the migration.
        await _cohortAssignmentRepository.DidNotReceive().ReplaceAsync(Arg.Any<OrganizationPlanMigrationCohortAssignment>());
        Assert.Null(assignment.MigratedDate);
    }

    [Fact]
    public async Task HandleAsync_BusinessMigration_AssignmentReplaceFailsAfterOrgWrite_RethrowsAsBillingExceptionForStripeRetry()
    {
        // Arrange — verifies Fix 4: a failure stamping MigratedDate AFTER the org write
        // succeeded must surface (as BillingException) so the webhook returns 500 and
        // Stripe retries. ChangePlan is idempotent so the retry safely re-applies.
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var enterprise2020Annual = new Enterprise2020Plan(true);
        var enterpriseAnnual = new EnterprisePlan(true);

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterprise2020Annual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_partial_write",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterpriseAnnual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterpriseAnnual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } }
        };

        var organization = new Organization { Id = organizationId, PlanType = PlanType.EnterpriseAnnually2020 };
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = assignmentId,
            OrganizationId = organizationId,
            CohortId = cohortId
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
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Annual);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly2020).Returns(new Enterprise2020Plan(false));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually2020).Returns(new Teams2020Plan(true));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(new Teams2020Plan(false));
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(enterpriseAnnual);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "Enterprise2020Annual",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            IsActive = true
        });
        _cohortAssignmentRepository.ReplaceAsync(Arg.Any<OrganizationPlanMigrationCohortAssignment>())
            .Throws(new InvalidOperationException("assignment DB failure"));

        // Act + Assert — partial-write is surfaced as BillingException so Stripe retries.
        await Assert.ThrowsAsync<BillingException>(() => _sut.HandleAsync(parsedEvent));

        // Org was written before the failure
        await _organizationRepository.Received(1).ReplaceAsync(Arg.Is<Organization>(o =>
            o.Id == organizationId && o.PlanType == PlanType.EnterpriseAnnually));

        // Assignment MigratedDate was set in-memory but the write failed; retry will redo it.
        Assert.NotNull(assignment.MigratedDate);
    }

    [Fact]
    public async Task HandleAsync_BusinessMigration_Integration_TeamsAnnual2020_AppliesShapeAndStampsAssignmentAgainstInMemoryState()
    {
        // Integration-style test (per PM-37092 AC): drive a synthetic customer.subscription.updated
        // event end-to-end through the handler against in-memory-backed repository substitutes, then
        // assert on the resulting Organization shape and the assignment row state — not on
        // substitute-interaction counts. The org and assignment instances captured here are the
        // same references the handler mutates, so post-Act inspection reads the "stored" state.
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var teams2020Annual = new Teams2020Plan(true);
        var teamsAnnual = new TeamsPlan(true);

        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.TeamsAnnually2020,
            Plan = teams2020Annual.Name,
            UseScim = false,
            UsePolicies = teams2020Annual.HasPolicies,
            UseSso = teams2020Annual.HasSso,
            UseGroups = teams2020Annual.HasGroups,
            UseDirectory = teams2020Annual.HasDirectory,
            Seats = 50,
            MaxStorageGb = 20,
            UseSecretsManager = true,
            SmSeats = 10,
            Name = "Acme Inc.",
            Enabled = true,
            MaxAutoscaleSeats = 100,
            MaxAutoscaleSmSeats = 25
        };
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = assignmentId,
            OrganizationId = organizationId,
            CohortId = cohortId,
            ScheduledDate = DateTime.UtcNow.AddDays(-30),
            MigratedDate = null,
            RevisionDate = DateTime.UtcNow.AddDays(-30)
        };
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "Teams2020Annual-Integration",
            MigrationPathId = MigrationPathId.Teams2020AnnualToCurrent,
            IsActive = true
        };

        // In-memory-backed repository behaviors: substitutes return the captured instances by id,
        // and ReplaceAsync just persists by reference (the entity is mutated in place by the handler).
        _organizationRepository.GetByIdAsync(organizationId).Returns(_ => organization);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(_ => assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(_ => cohort);

        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually2020).Returns(teams2020Annual);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(teamsAnnual);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = teams2020Annual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = teams2020Annual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };
        var subscription = new Subscription
        {
            Id = "sub_integration_ta2020",
            ScheduleId = "sub_sched_integration",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = teamsAnnual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = teamsAnnual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
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

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — resulting Organization shape reflects target plan structurally...
        Assert.Equal(PlanType.TeamsAnnually, organization.PlanType);
        Assert.Equal(teamsAnnual.Name, organization.Plan);
        Assert.True(organization.UseScim);
        Assert.Equal(teamsAnnual.HasPolicies, organization.UsePolicies);
        Assert.Equal(teamsAnnual.HasSso, organization.UseSso);
        Assert.Equal(teamsAnnual.HasGroups, organization.UseGroups);
        Assert.Equal(teamsAnnual.HasDirectory, organization.UseDirectory);
        Assert.Equal(teamsAnnual.UsersGetPremium, organization.UsersGetPremium);
        Assert.Equal(teamsAnnual.PasswordManager.MaxCollections, organization.MaxCollections);
        Assert.True(organization.UsePasswordManager);

        // ...customer-purchase columns are preserved (allocation-preserve policy)...
        Assert.Equal((short)50, organization.Seats);
        Assert.Equal((short)20, organization.MaxStorageGb);
        Assert.True(organization.UseSecretsManager);
        Assert.Equal(10, organization.SmSeats);
        Assert.Equal("Acme Inc.", organization.Name);
        Assert.True(organization.Enabled);
        Assert.Equal(100, organization.MaxAutoscaleSeats);
        Assert.Equal(25, organization.MaxAutoscaleSmSeats);

        // ...and the cohort assignment row is stamped as migrated.
        Assert.NotNull(assignment.MigratedDate);
        Assert.NotEqual(default, assignment.RevisionDate);
        Assert.True(assignment.MigratedDate > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task HandleAsync_BusinessMigration_EnterpriseAnnual2019ToCurrent_AppliesAndMarksMigrated()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var enterprise2019Annual = new Enterprise2019Plan(true);
        var enterpriseAnnual = new EnterprisePlan(true);

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterprise2019Annual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterprise2019Annual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_happy_ea19",
            ScheduleId = "sub_sched_x",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterpriseAnnual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterpriseAnnual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.EnterpriseAnnually2019,
            Plan = enterprise2019Annual.Name,
            Seats = 200,
            MaxStorageGb = 50,
        };

        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = assignmentId,
            OrganizationId = organizationId,
            CohortId = cohortId
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
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2019).Returns(enterprise2019Annual);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(enterpriseAnnual);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "Enterprise2019Annual",
            MigrationPathId = MigrationPathId.Enterprise2019AnnualToCurrent,
            IsActive = true
        });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — plan shape applied
        Assert.Equal(PlanType.EnterpriseAnnually, organization.PlanType);
        Assert.Equal(enterpriseAnnual.Name, organization.Plan);
        Assert.Equal(enterpriseAnnual.HasScim, organization.UseScim);
        Assert.True(organization.UsePasswordManager);
        Assert.Equal(enterpriseAnnual.UsersGetPremium, organization.UsersGetPremium);
        Assert.Equal(enterpriseAnnual.PasswordManager.MaxCollections, organization.MaxCollections);

        // Allocation preserved
        Assert.Equal((short)200, organization.Seats);
        Assert.Equal((short)50, organization.MaxStorageGb);

        await _organizationRepository.Received(1).ReplaceAsync(Arg.Is<Organization>(o =>
            o.Id == organizationId && o.PlanType == PlanType.EnterpriseAnnually));

        Assert.NotNull(assignment.MigratedDate);
        Assert.NotEqual(default, assignment.RevisionDate);
        await _cohortAssignmentRepository.Received(1).ReplaceAsync(
            Arg.Is<OrganizationPlanMigrationCohortAssignment>(a => a.Id == assignmentId && a.MigratedDate.HasValue));
    }

    // PM-37513: Enterprise 2019 (SM base 200) -> current (SM base 50) writes grace 150 onto the
    // subscription metadata, merged with the existing metadata, with no item/quantity change. The
    // grace is computed from plan baselines regardless of the org's actual SM usage (matches Ent 2020).
    [Fact]
    public async Task HandleAsync_BusinessMigration_EnterpriseAnnual2019ToCurrent_WritesGraceMetadata()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var enterprise2019Annual = new Enterprise2019Plan(true);
        var enterpriseAnnual = new EnterprisePlan(true);

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterprise2019Annual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterprise2019Annual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_grace_ea19",
            ScheduleId = "sub_sched_g",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterpriseAnnual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterpriseAnnual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.EnterpriseAnnually2019,
            Plan = enterprise2019Annual.Name,
        };

        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CohortId = cohortId
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
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2019).Returns(enterprise2019Annual);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(enterpriseAnnual);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "Enterprise2019Annual",
            MigrationPathId = MigrationPathId.Enterprise2019AnnualToCurrent,
            IsActive = true
        });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — grace metadata written (200 - 50 = 150), merged with the org id key.
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.Metadata[MetadataKeys.MigrationGraceServiceAccounts] == "150" &&
                options.Metadata["organizationId"] == organizationId.ToString()));
    }

    // PM-37513: the monthly path (MigrationPathId = 6) writes the same grace as the annual path — the SM
    // baseline delta (200 -> 50) is cadence-independent, so grace is 150 for both. Exercises the monthly
    // MigrationPathId, which the annual grace test above does not cover.
    [Fact]
    public async Task HandleAsync_BusinessMigration_EnterpriseMonthly2019ToCurrent_WritesGraceMetadata()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var enterprise2019Monthly = new Enterprise2019Plan(false);
        var enterpriseMonthly = new EnterprisePlan(false);

        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterprise2019Monthly.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterprise2019Monthly.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_grace_em19",
            ScheduleId = "sub_sched_gm",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterpriseMonthly.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterpriseMonthly.PasswordManager.StripeSeatPlanId }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.EnterpriseMonthly2019,
            Plan = enterprise2019Monthly.Name,
        };

        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CohortId = cohortId
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
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly2019).Returns(enterprise2019Monthly);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly).Returns(enterpriseMonthly);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "Enterprise2019Monthly",
            MigrationPathId = MigrationPathId.Enterprise2019MonthlyToCurrent,
            IsActive = true
        });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — grace metadata written (200 - 50 = 150), merged with the org id key.
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.Metadata[MetadataKeys.MigrationGraceServiceAccounts] == "150" &&
                options.Metadata["organizationId"] == organizationId.ToString()));
    }

    // PM-37513: an Enterprise 2019 org with no SM line items (PM seat only) still migrates its plan
    // shape and marks migrated. Grace metadata is computed from plan baselines (150) and written
    // regardless of SM usage; this confirms that write does not disrupt the no-SM migration path.
    [Fact]
    public async Task HandleAsync_BusinessMigration_EnterpriseAnnual2019NoSecretsManager_MigratesCleanly()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var enterprise2019Annual = new Enterprise2019Plan(true);
        var enterpriseAnnual = new EnterprisePlan(true);

        // Previous + current subscriptions carry ONLY the PM seat item — no SM service-account item.
        var previousSubscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterprise2019Annual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterprise2019Annual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            }
        };

        var subscription = new Subscription
        {
            Id = "sub_nosm_ea19",
            ScheduleId = "sub_sched_n",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = enterpriseAnnual.PasswordManager.StripeSeatPlanId },
                        Plan = new Plan { Id = enterpriseAnnual.PasswordManager.StripeSeatPlanId }
                    }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = BillingReasons.SubscriptionCycle }
        };

        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.EnterpriseAnnually2019,
            Plan = enterprise2019Annual.Name,
            UseSecretsManager = false,
            Seats = 10,
        };

        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = assignmentId,
            OrganizationId = organizationId,
            CohortId = cohortId
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
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2019).Returns(enterprise2019Annual);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(enterpriseAnnual);
        _pricingClient.ListPlans().Returns(MockPlans.Plans);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "Enterprise2019Annual",
            MigrationPathId = MigrationPathId.Enterprise2019AnnualToCurrent,
            IsActive = true
        });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — plan shape applied and assignment marked, despite no SM line items.
        Assert.Equal(PlanType.EnterpriseAnnually, organization.PlanType);
        Assert.False(organization.UseSecretsManager); // ChangePlan does not touch this column
        await _organizationRepository.Received(1).ReplaceAsync(Arg.Is<Organization>(o =>
            o.Id == organizationId && o.PlanType == PlanType.EnterpriseAnnually));
        Assert.NotNull(assignment.MigratedDate);
        await _cohortAssignmentRepository.Received(1).ReplaceAsync(
            Arg.Is<OrganizationPlanMigrationCohortAssignment>(a => a.Id == assignmentId && a.MigratedDate.HasValue));

        // Grace metadata is still written from the plan baselines (200 - 50 = 150) even though this org
        // carries no SM line items. This is intentional and matches Enterprise 2020 behavior — pinning it
        // here so the no-SM grace write is a deliberate contract, not an accident a future change silences.
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.Metadata[MetadataKeys.MigrationGraceServiceAccounts] == "150"));
    }
}
