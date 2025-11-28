using Bit.Billing.Constants;
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
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.Billing.Mocks.Plans;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Quartz;
using Stripe;
using Xunit;
using Event = Stripe.Event;

namespace Bit.Billing.Test.Services;

public class SubscriptionUpdatedHandlerTests
{
    private readonly IStripeEventService _stripeEventService;
    private readonly IStripeEventUtilityService _stripeEventUtilityService;
    private readonly IOrganizationService _organizationService;
    private readonly IStripeFacade _stripeFacade;
    private readonly IOrganizationSponsorshipRenewCommand _organizationSponsorshipRenewCommand;
    private readonly IUserService _userService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationEnableCommand _organizationEnableCommand;
    private readonly IOrganizationDisableCommand _organizationDisableCommand;
    private readonly IPricingClient _pricingClient;
    private readonly IFeatureService _featureService;
    private readonly IProviderRepository _providerRepository;
    private readonly IProviderService _providerService;
    private readonly IScheduler _scheduler;
    private readonly IPushNotificationAdapter _pushNotificationAdapter;
    private readonly SubscriptionUpdatedHandler _sut;

    public SubscriptionUpdatedHandlerTests()
    {
        _stripeEventService = Substitute.For<IStripeEventService>();
        _stripeEventUtilityService = Substitute.For<IStripeEventUtilityService>();
        _organizationService = Substitute.For<IOrganizationService>();
        _stripeFacade = Substitute.For<IStripeFacade>();
        _organizationSponsorshipRenewCommand = Substitute.For<IOrganizationSponsorshipRenewCommand>();
        _userService = Substitute.For<IUserService>();
        _providerService = Substitute.For<IProviderService>();
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        var schedulerFactory = Substitute.For<ISchedulerFactory>();
        _organizationEnableCommand = Substitute.For<IOrganizationEnableCommand>();
        _organizationDisableCommand = Substitute.For<IOrganizationDisableCommand>();
        _pricingClient = Substitute.For<IPricingClient>();
        _featureService = Substitute.For<IFeatureService>();
        _providerRepository = Substitute.For<IProviderRepository>();
        _providerService = Substitute.For<IProviderService>();
        var logger = Substitute.For<ILogger<SubscriptionUpdatedHandler>>();
        _scheduler = Substitute.For<IScheduler>();
        _pushNotificationAdapter = Substitute.For<IPushNotificationAdapter>();

        schedulerFactory.GetScheduler().Returns(_scheduler);

        _sut = new SubscriptionUpdatedHandler(
            _stripeEventService,
            _stripeEventUtilityService,
            _organizationService,
            _stripeFacade,
            _organizationSponsorshipRenewCommand,
            _userService,
            _organizationRepository,
            schedulerFactory,
            _organizationEnableCommand,
            _organizationDisableCommand,
            _pricingClient,
            _featureService,
            _providerRepository,
            _providerService,
            logger,
            _pushNotificationAdapter);
    }

    [Fact]
    public async Task HandleAsync_UnpaidOrganizationSubscription_DisablesOrganizationAndSchedulesCancellation()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var subscriptionId = "sub_123";
        var currentPeriodEnd = DateTime.UtcNow.AddDays(30);
        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = StripeSubscriptionStatus.Unpaid,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = "subscription_cycle" }
        };

        var parsedEvent = new Event { Data = new EventData() };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);

        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(organizationId, null, null));

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _organizationDisableCommand.Received(1)
            .DisableAsync(organizationId, currentPeriodEnd);
        await _scheduler.Received(1).ScheduleJob(
            Arg.Is<IJobDetail>(j => j.Key.Name == $"cancel-sub-{subscriptionId}"),
            Arg.Is<ITrigger>(t => t.Key.Name == $"cancel-trigger-{subscriptionId}"));
    }

    [Fact]
    public async Task
        HandleAsync_UnpaidProviderSubscription_WithManualSuspensionViaMetadata_DisablesProviderAndSchedulesCancellation()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var subscriptionId = "sub_test123";

        var previousSubscription = new Subscription
        {
            Id = subscriptionId,
            Status = StripeSubscriptionStatus.Active,
            Metadata = new Dictionary<string, string>
            {
                ["suspend_provider"] = null // This is the key part - metadata exists, but value is null
            }
        };

        var currentSubscription = new Subscription
        {
            Id = subscriptionId,
            Status = StripeSubscriptionStatus.Unpaid,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }
                ]
            },
            Metadata = new Dictionary<string, string>
            {
                ["providerId"] = providerId.ToString(),
                ["suspend_provider"] = "true" // Now has a value, indicating manual suspension
            },
            TestClock = null
        };

        var parsedEvent = new Event
        {
            Id = "evt_test123",
            Type = HandledStripeWebhook.SubscriptionUpdated,
            Data = new EventData
            {
                Object = currentSubscription,
                PreviousAttributes = JObject.FromObject(previousSubscription)
            }
        };

        var provider = new Provider { Id = providerId, Enabled = true };

        _featureService.IsEnabled(FeatureFlagKeys.PM21821_ProviderPortalTakeover).Returns(true);
        _stripeEventService.GetSubscription(parsedEvent, true, Arg.Any<List<string>>()).Returns(currentSubscription);
        _stripeEventUtilityService.GetIdsFromMetadata(currentSubscription.Metadata)
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, null, providerId));
        _providerRepository.GetByIdAsync(providerId).Returns(provider);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        Assert.False(provider.Enabled);
        await _providerService.Received(1).UpdateAsync(provider);

        // Verify that UpdateSubscription was called with both CancelAt and the new metadata
        await _stripeFacade.Received(1).UpdateSubscription(
            subscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.CancelAt.HasValue &&
                options.CancelAt.Value <= DateTime.UtcNow.AddDays(7).AddMinutes(1) &&
                options.Metadata != null &&
                options.Metadata.ContainsKey("suspended_provider_via_webhook_at")));
    }

    [Fact]
    public async Task
        HandleAsync_UnpaidProviderSubscription_WithValidTransition_DisablesProviderAndSchedulesCancellation()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var subscriptionId = "sub_test123";

        var previousSubscription = new Subscription
        {
            Id = subscriptionId,
            Status = StripeSubscriptionStatus.Active,
            Metadata = new Dictionary<string, string> { ["providerId"] = providerId.ToString() }
        };

        var currentSubscription = new Subscription
        {
            Id = subscriptionId,
            Status = StripeSubscriptionStatus.Unpaid,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }
                ]
            },
            Metadata = new Dictionary<string, string> { ["providerId"] = providerId.ToString() },
            LatestInvoice = new Invoice { BillingReason = "subscription_cycle" },
            TestClock = null
        };

        var parsedEvent = new Event
        {
            Id = "evt_test123",
            Type = HandledStripeWebhook.SubscriptionUpdated,
            Data = new EventData
            {
                Object = currentSubscription,
                PreviousAttributes = JObject.FromObject(previousSubscription)
            }
        };

        var provider = new Provider { Id = providerId, Enabled = true };

        _featureService.IsEnabled(FeatureFlagKeys.PM21821_ProviderPortalTakeover).Returns(true);
        _stripeEventService.GetSubscription(parsedEvent, true, Arg.Any<List<string>>()).Returns(currentSubscription);
        _stripeEventUtilityService.GetIdsFromMetadata(currentSubscription.Metadata)
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, null, providerId));
        _providerRepository.GetByIdAsync(providerId).Returns(provider);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        Assert.False(provider.Enabled);
        await _providerService.Received(1).UpdateAsync(provider);

        // Verify that UpdateSubscription was called with CancelAt but WITHOUT suspension metadata
        await _stripeFacade.Received(1).UpdateSubscription(
            subscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.CancelAt.HasValue &&
                options.CancelAt.Value <= DateTime.UtcNow.AddDays(7).AddMinutes(1) &&
                (options.Metadata == null || !options.Metadata.ContainsKey("suspended_provider_via_webhook_at"))));
    }

    [Fact]
    public async Task HandleAsync_UnpaidProviderSubscription_WithoutValidTransition_DisablesProviderOnly()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        const string subscriptionId = "sub_123";

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
            Status = StripeSubscriptionStatus.Unpaid,
            Metadata = new Dictionary<string, string> { { "providerId", providerId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = "subscription_cycle" }
        };

        var provider = new Provider { Id = providerId, Name = "Test Provider", Enabled = true };

        var parsedEvent = new Event
        {
            Data = new EventData
            {
                PreviousAttributes = JObject.FromObject(new
                {
                    status = "unpaid" // No valid transition
                })
            }
        };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);

        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, null, providerId));

        _featureService.IsEnabled(FeatureFlagKeys.PM21821_ProviderPortalTakeover)
            .Returns(true);

        _providerRepository.GetByIdAsync(providerId)
            .Returns(provider);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        Assert.False(provider.Enabled);
        await _providerService.Received(1).UpdateAsync(provider);
        await _stripeFacade.DidNotReceive().UpdateSubscription(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task HandleAsync_UnpaidProviderSubscription_WithNoPreviousAttributes_DisablesProviderOnly()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        const string subscriptionId = "sub_123";

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
            Status = StripeSubscriptionStatus.Unpaid,
            Metadata = new Dictionary<string, string> { { "providerId", providerId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = "subscription_cycle" }
        };

        var provider = new Provider { Id = providerId, Name = "Test Provider", Enabled = true };

        var parsedEvent = new Event { Data = new EventData { PreviousAttributes = null } };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);

        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, null, providerId));

        _featureService.IsEnabled(FeatureFlagKeys.PM21821_ProviderPortalTakeover)
            .Returns(true);

        _providerRepository.GetByIdAsync(providerId)
            .Returns(provider);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        Assert.False(provider.Enabled);
        await _providerService.Received(1).UpdateAsync(provider);
        await _stripeFacade.DidNotReceive().UpdateSubscription(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task HandleAsync_UnpaidProviderSubscription_WithIncompleteExpiredStatus_DisablesProvider()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var subscriptionId = "sub_123";
        var currentPeriodEnd = DateTime.UtcNow.AddDays(30);

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = StripeSubscriptionStatus.IncompleteExpired,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd }
                ]
            },
            Metadata = new Dictionary<string, string> { { "providerId", providerId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = "renewal" }
        };

        var provider = new Provider { Id = providerId, Name = "Test Provider", Enabled = true };

        var parsedEvent = new Event { Data = new EventData() };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);

        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, null, providerId));

        _featureService.IsEnabled(FeatureFlagKeys.PM21821_ProviderPortalTakeover)
            .Returns(true);

        _providerRepository.GetByIdAsync(providerId)
            .Returns(provider);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        Assert.False(provider.Enabled);
        await _providerService.Received(1).UpdateAsync(provider);
        await _stripeFacade.DidNotReceive().UpdateSubscription(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task HandleAsync_UnpaidProviderSubscription_WhenFeatureFlagDisabled_DoesNothing()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var subscriptionId = "sub_123";
        var currentPeriodEnd = DateTime.UtcNow.AddDays(30);

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = StripeSubscriptionStatus.Unpaid,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd }
                ]
            },
            Metadata = new Dictionary<string, string> { { "providerId", providerId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = "subscription_cycle" }
        };

        var parsedEvent = new Event { Data = new EventData() };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);

        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, null, providerId));

        _featureService.IsEnabled(FeatureFlagKeys.PM21821_ProviderPortalTakeover)
            .Returns(false);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _providerRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
        await _providerService.DidNotReceive().UpdateAsync(Arg.Any<Provider>());
    }

    [Fact]
    public async Task HandleAsync_UnpaidProviderSubscription_WhenProviderNotFound_DoesNothing()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var subscriptionId = "sub_123";
        var currentPeriodEnd = DateTime.UtcNow.AddDays(30);

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = StripeSubscriptionStatus.Unpaid,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd }
                ]
            },
            Metadata = new Dictionary<string, string> { { "providerId", providerId.ToString() } },
            LatestInvoice = new Invoice { BillingReason = "subscription_cycle" }
        };

        var parsedEvent = new Event { Data = new EventData() };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);

        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, null, providerId));

        _featureService.IsEnabled(FeatureFlagKeys.PM21821_ProviderPortalTakeover)
            .Returns(true);

        _providerRepository.GetByIdAsync(providerId)
            .Returns((Provider)null);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _providerService.DidNotReceive().UpdateAsync(Arg.Any<Provider>());
        await _stripeFacade.DidNotReceive().UpdateSubscription(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task HandleAsync_UnpaidUserSubscription_DisablesPremiumAndCancelsSubscription()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subscriptionId = "sub_123";
        var currentPeriodEnd = DateTime.UtcNow.AddDays(30);
        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = StripeSubscriptionStatus.Unpaid,
            Metadata = new Dictionary<string, string> { { "userId", userId.ToString() } },
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        CurrentPeriodEnd = currentPeriodEnd,
                        Price = new Price { Id = IStripeEventUtilityService.PremiumPlanId }
                    }
                ]
            }
        };

        var parsedEvent = new Event { Data = new EventData() };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);

        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, userId, null));

        _stripeFacade.ListInvoices(Arg.Any<InvoiceListOptions>())
            .Returns(new StripeList<Invoice> { Data = new List<Invoice>() });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _userService.Received(1)
            .DisablePremiumAsync(userId, currentPeriodEnd);
        await _stripeFacade.Received(1)
            .CancelSubscription(subscriptionId, Arg.Any<SubscriptionCancelOptions>());
        await _stripeFacade.Received(1)
            .ListInvoices(Arg.Is<InvoiceListOptions>(o =>
                o.Status == StripeInvoiceStatus.Open && o.Subscription == subscriptionId));
    }

    [Fact]
    public async Task HandleAsync_ActiveOrganizationSubscription_EnablesOrganizationAndUpdatesExpiration()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var currentPeriodEnd = DateTime.UtcNow.AddDays(30);
        var subscription = new Subscription
        {
            Status = StripeSubscriptionStatus.Active,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } }
        };

        var organization = new Organization { Id = organizationId, PlanType = PlanType.EnterpriseAnnually2023 };
        var parsedEvent = new Event { Data = new EventData() };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);

        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(organizationId, null, null));

        _organizationRepository.GetByIdAsync(organizationId)
            .Returns(organization);

        _stripeFacade.ListInvoices(Arg.Any<InvoiceListOptions>())
            .Returns(new StripeList<Invoice> { Data = [new Invoice { Id = "inv_123" }] });

        var plan = new Enterprise2023Plan(true);
        _pricingClient.GetPlanOrThrow(organization.PlanType)
            .Returns(plan);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _organizationEnableCommand.Received(1)
            .EnableAsync(organizationId);
        await _organizationService.Received(1)
            .UpdateExpirationDateAsync(organizationId, currentPeriodEnd);
        await _pushNotificationAdapter.Received(1)
            .NotifyEnabledChangedAsync(organization);
    }

    [Fact]
    public async Task HandleAsync_ActiveUserSubscription_EnablesPremiumAndUpdatesExpiration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentPeriodEnd = DateTime.UtcNow.AddDays(30);
        var subscription = new Subscription
        {
            Status = StripeSubscriptionStatus.Active,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd }
                ]
            },
            Metadata = new Dictionary<string, string> { { "userId", userId.ToString() } }
        };

        var parsedEvent = new Event { Data = new EventData() };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);

        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, userId, null));

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _userService.Received(1)
            .EnablePremiumAsync(userId, currentPeriodEnd);
        await _userService.Received(1)
            .UpdatePremiumExpirationAsync(userId, currentPeriodEnd);
    }

    [Fact]
    public async Task HandleAsync_SponsoredSubscription_RenewsSponsorship()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var currentPeriodEnd = DateTime.UtcNow.AddDays(30);
        var subscription = new Subscription
        {
            Status = StripeSubscriptionStatus.Active,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } }
        };

        var parsedEvent = new Event { Data = new EventData() };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);

        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(organizationId, null, null));

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
            Status = StripeSubscriptionStatus.Active,
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

        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(organizationId, null, null));

        _organizationRepository.GetByIdAsync(organizationId)
            .Returns(organization);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeFacade.Received(1).DeleteCustomerDiscount(subscription.CustomerId);
        await _stripeFacade.Received(1).DeleteSubscriptionDiscount(subscription.Id);
    }

    [Theory]
    [MemberData(nameof(GetNonActiveSubscriptions))]
    public async Task
        HandleAsync_ActiveProviderSubscriptionEvent_AndPreviousSubscriptionStatusWasNonActive_EnableProviderAndUpdateSubscription(
            Subscription previousSubscription)
    {
        // Arrange
        var (providerId, newSubscription, provider, parsedEvent) =
            CreateProviderTestInputsForUpdatedActiveSubscriptionStatus(previousSubscription);

        _stripeEventService
            .GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(newSubscription);

        _stripeEventUtilityService
            .GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, null, providerId));

        _providerRepository
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(provider);
        _stripeFacade
            .UpdateSubscription(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(newSubscription);
        _featureService.IsEnabled(FeatureFlagKeys.PM21821_ProviderPortalTakeover)
            .Returns(true);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeEventService
            .Received(1)
            .GetSubscription(parsedEvent, true, Arg.Any<List<string>>());
        _stripeEventUtilityService
            .Received(1)
            .GetIdsFromMetadata(newSubscription.Metadata);
        await _providerRepository
            .Received(1)
            .GetByIdAsync(providerId);
        await _providerService
            .Received(1)
            .UpdateAsync(Arg.Is<Provider>(p => p.Id == providerId && p.Enabled == true));
        await _stripeFacade
            .Received(1)
            .UpdateSubscription(newSubscription.Id,
                Arg.Is<SubscriptionUpdateOptions>(options => options.CancelAtPeriodEnd == false));
        _featureService
            .Received(1)
            .IsEnabled(FeatureFlagKeys.PM21821_ProviderPortalTakeover);
    }

    [Fact]
    public async Task
        HandleAsync_ActiveProviderSubscriptionEvent_AndPreviousSubscriptionStatusWasCanceled_EnableProvider()
    {
        // Arrange
        var previousSubscription = new Subscription { Id = "sub_123", Status = StripeSubscriptionStatus.Canceled };
        var (providerId, newSubscription, provider, parsedEvent) =
            CreateProviderTestInputsForUpdatedActiveSubscriptionStatus(previousSubscription);

        _stripeEventService
            .GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(newSubscription);
        _stripeEventUtilityService
            .GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, null, providerId));
        _providerRepository
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(provider);
        _featureService.IsEnabled(FeatureFlagKeys.PM21821_ProviderPortalTakeover)
            .Returns(true);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeEventService
            .Received(1)
            .GetSubscription(parsedEvent, true, Arg.Any<List<string>>());
        _stripeEventUtilityService
            .Received(1)
            .GetIdsFromMetadata(newSubscription.Metadata);
        await _providerRepository.Received(1).GetByIdAsync(providerId);
        await _providerService
            .Received(1)
            .UpdateAsync(Arg.Is<Provider>(p => p.Id == providerId && p.Enabled == true));
        await _stripeFacade
            .DidNotReceiveWithAnyArgs()
            .UpdateSubscription(Arg.Any<string>());
        _featureService
            .Received(1)
            .IsEnabled(FeatureFlagKeys.PM21821_ProviderPortalTakeover);
    }

    [Fact]
    public async Task
        HandleAsync_ActiveProviderSubscriptionEvent_AndPreviousSubscriptionStatusWasAlreadyActive_EnableProvider()
    {
        // Arrange
        var previousSubscription = new Subscription { Id = "sub_123", Status = StripeSubscriptionStatus.Active };
        var (providerId, newSubscription, provider, parsedEvent) =
            CreateProviderTestInputsForUpdatedActiveSubscriptionStatus(previousSubscription);

        _stripeEventService
            .GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(newSubscription);
        _stripeEventUtilityService
            .GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, null, providerId));
        _providerRepository
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(provider);
        _featureService.IsEnabled(FeatureFlagKeys.PM21821_ProviderPortalTakeover)
            .Returns(true);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeEventService
            .Received(1)
            .GetSubscription(parsedEvent, true, Arg.Any<List<string>>());
        _stripeEventUtilityService
            .Received(1)
            .GetIdsFromMetadata(newSubscription.Metadata);
        await _providerRepository.Received(1).GetByIdAsync(providerId);
        await _providerService
            .Received(1)
            .UpdateAsync(Arg.Is<Provider>(p => p.Id == providerId && p.Enabled == true));
        await _stripeFacade
            .DidNotReceiveWithAnyArgs()
            .UpdateSubscription(Arg.Any<string>());
        _featureService
            .Received(1)
            .IsEnabled(FeatureFlagKeys.PM21821_ProviderPortalTakeover);
    }

    [Fact]
    public async Task
        HandleAsync_ActiveProviderSubscriptionEvent_AndPreviousSubscriptionStatusWasTrailing_EnableProvider()
    {
        // Arrange
        var previousSubscription = new Subscription { Id = "sub_123", Status = StripeSubscriptionStatus.Trialing };
        var (providerId, newSubscription, provider, parsedEvent) =
            CreateProviderTestInputsForUpdatedActiveSubscriptionStatus(previousSubscription);

        _stripeEventService
            .GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(newSubscription);
        _stripeEventUtilityService
            .GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, null, providerId));
        _providerRepository
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(provider);
        _featureService.IsEnabled(FeatureFlagKeys.PM21821_ProviderPortalTakeover)
            .Returns(true);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeEventService
            .Received(1)
            .GetSubscription(parsedEvent, true, Arg.Any<List<string>>());
        _stripeEventUtilityService
            .Received(1)
            .GetIdsFromMetadata(newSubscription.Metadata);
        await _providerRepository.Received(1).GetByIdAsync(providerId);
        await _providerService
            .Received(1)
            .UpdateAsync(Arg.Is<Provider>(p => p.Id == providerId && p.Enabled == true));
        await _stripeFacade
            .DidNotReceiveWithAnyArgs()
            .UpdateSubscription(Arg.Any<string>());
        _featureService
            .Received(1)
            .IsEnabled(FeatureFlagKeys.PM21821_ProviderPortalTakeover);
    }

    [Fact]
    public async Task
        HandleAsync_ActiveProviderSubscriptionEvent_AndPreviousSubscriptionStatusWasPastDue_EnableProvider()
    {
        // Arrange
        var previousSubscription = new Subscription { Id = "sub_123", Status = StripeSubscriptionStatus.PastDue };
        var (providerId, newSubscription, provider, parsedEvent) =
            CreateProviderTestInputsForUpdatedActiveSubscriptionStatus(previousSubscription);


        _stripeEventService
            .GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(newSubscription);
        _stripeEventUtilityService
            .GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, null, providerId));
        _providerRepository
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(provider);
        _featureService.IsEnabled(FeatureFlagKeys.PM21821_ProviderPortalTakeover)
            .Returns(true);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeEventService
            .Received(1)
            .GetSubscription(parsedEvent, true, Arg.Any<List<string>>());
        _stripeEventUtilityService
            .Received(1)
            .GetIdsFromMetadata(newSubscription.Metadata);
        await _providerRepository
            .Received(1)
            .GetByIdAsync(Arg.Any<Guid>());
        await _providerService
            .Received(1)
            .UpdateAsync(Arg.Is<Provider>(p => p.Id == providerId && p.Enabled == true));
        await _stripeFacade
            .DidNotReceiveWithAnyArgs()
            .UpdateSubscription(Arg.Any<string>());
        _featureService
            .Received(1)
            .IsEnabled(FeatureFlagKeys.PM21821_ProviderPortalTakeover);
    }

    [Fact]
    public async Task HandleAsync_ActiveProviderSubscriptionEvent_AndProviderDoesNotExist_NoChanges()
    {
        // Arrange
        var previousSubscription = new Subscription { Id = "sub_123", Status = StripeSubscriptionStatus.Unpaid };
        var (providerId, newSubscription, _, parsedEvent) =
            CreateProviderTestInputsForUpdatedActiveSubscriptionStatus(previousSubscription);

        _stripeEventService
            .GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(newSubscription);
        _stripeEventUtilityService
            .GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, null, providerId));
        _providerRepository
            .GetByIdAsync(Arg.Any<Guid>())
            .ReturnsNull();
        _featureService.IsEnabled(FeatureFlagKeys.PM21821_ProviderPortalTakeover)
            .Returns(true);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeEventService
            .Received(1)
            .GetSubscription(parsedEvent, true, Arg.Any<List<string>>());
        _stripeEventUtilityService
            .Received(1)
            .GetIdsFromMetadata(newSubscription.Metadata);
        await _providerRepository
            .Received(1)
            .GetByIdAsync(providerId);
        await _providerService
            .DidNotReceive()
            .UpdateAsync(Arg.Any<Provider>());
        await _stripeFacade
            .DidNotReceive()
            .UpdateSubscription(Arg.Any<string>());
        _featureService
            .Received(1)
            .IsEnabled(FeatureFlagKeys.PM21821_ProviderPortalTakeover);
    }

    [Fact]
    public async Task HandleAsync_ActiveProviderSubscriptionEvent_WithNoPreviousAttributes_EnableProvider()
    {
        // Arrange
        var (providerId, newSubscription, provider, parsedEvent) =
            CreateProviderTestInputsForUpdatedActiveSubscriptionStatus(null);

        _stripeEventService
            .GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(newSubscription);
        _stripeEventUtilityService
            .GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, null, providerId));
        _providerRepository
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(provider);
        _featureService.IsEnabled(FeatureFlagKeys.PM21821_ProviderPortalTakeover)
            .Returns(true);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeEventService
            .Received(1)
            .GetSubscription(parsedEvent, true, Arg.Any<List<string>>());
        _stripeEventUtilityService
            .Received(1)
            .GetIdsFromMetadata(newSubscription.Metadata);
        await _providerRepository
            .Received(1)
            .GetByIdAsync(Arg.Any<Guid>());
        await _providerService
            .Received(1)
            .UpdateAsync(Arg.Is<Provider>(p => p.Id == providerId && p.Enabled == true));
        await _stripeFacade
            .DidNotReceive()
            .UpdateSubscription(Arg.Any<string>());
        _featureService
            .Received(1)
            .IsEnabled(FeatureFlagKeys.PM21821_ProviderPortalTakeover);
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
            Status = StripeSubscriptionStatus.Active,
            Metadata = new Dictionary<string, string> { { "providerId", providerId.ToString() } }
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

    public static IEnumerable<object[]> GetNonActiveSubscriptions()
    {
        return new List<object[]>
        {
            new object[] { new Subscription { Id = "sub_123", Status = StripeSubscriptionStatus.Unpaid } },
            new object[] { new Subscription { Id = "sub_123", Status = StripeSubscriptionStatus.Incomplete } },
            new object[]
            {
                new Subscription { Id = "sub_123", Status = StripeSubscriptionStatus.IncompleteExpired }
            },
            new object[] { new Subscription { Id = "sub_123", Status = StripeSubscriptionStatus.Paused } }
        };
    }
}
