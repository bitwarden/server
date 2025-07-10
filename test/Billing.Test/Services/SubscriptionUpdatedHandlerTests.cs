using Bit.Billing.Constants;
using Bit.Billing.Services;
using Bit.Billing.Services.Implementations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models.StaticStore.Plans;
using Bit.Core.Billing.Pricing;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Newtonsoft.Json.Linq;
using NSubstitute;
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
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IOrganizationEnableCommand _organizationEnableCommand;
    private readonly IOrganizationDisableCommand _organizationDisableCommand;
    private readonly IPricingClient _pricingClient;
    private readonly IScheduler _scheduler;
    private readonly SubscriptionUpdatedHandler _sut;

    public SubscriptionUpdatedHandlerTests()
    {
        _stripeEventService = Substitute.For<IStripeEventService>();
        _stripeEventUtilityService = Substitute.For<IStripeEventUtilityService>();
        _organizationService = Substitute.For<IOrganizationService>();
        _stripeFacade = Substitute.For<IStripeFacade>();
        _organizationSponsorshipRenewCommand = Substitute.For<IOrganizationSponsorshipRenewCommand>();
        _userService = Substitute.For<IUserService>();
        _pushNotificationService = Substitute.For<IPushNotificationService>();
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _schedulerFactory = Substitute.For<ISchedulerFactory>();
        _organizationEnableCommand = Substitute.For<IOrganizationEnableCommand>();
        _organizationDisableCommand = Substitute.For<IOrganizationDisableCommand>();
        _pricingClient = Substitute.For<IPricingClient>();
        _scheduler = Substitute.For<IScheduler>();

        _schedulerFactory.GetScheduler().Returns(_scheduler);

        _sut = new SubscriptionUpdatedHandler(
            _stripeEventService,
            _stripeEventUtilityService,
            _organizationService,
            _stripeFacade,
            _organizationSponsorshipRenewCommand,
            _userService,
            _pushNotificationService,
            _organizationRepository,
            _schedulerFactory,
            _organizationEnableCommand,
            _organizationDisableCommand,
            _pricingClient);
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
            CurrentPeriodEnd = currentPeriodEnd,
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
            CurrentPeriodEnd = currentPeriodEnd,
            Metadata = new Dictionary<string, string> { { "userId", userId.ToString() } },
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new() { Price = new Price { Id = IStripeEventUtilityService.PremiumPlanId } }
                }
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
            CurrentPeriodEnd = currentPeriodEnd,
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } }
        };

        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.EnterpriseAnnually2023
        };
        var parsedEvent = new Event { Data = new EventData() };

        _stripeEventService.GetSubscription(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>())
            .Returns(subscription);

        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(organizationId, null, null));

        _organizationRepository.GetByIdAsync(organizationId)
            .Returns(organization);

        _stripeFacade.ListInvoices(Arg.Any<InvoiceListOptions>())
            .Returns(new StripeList<Invoice> { Data = new List<Invoice> { new Invoice { Id = "inv_123" } } });

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
        await _pushNotificationService.Received(1)
            .PushSyncOrganizationStatusAsync(organization);
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
            CurrentPeriodEnd = currentPeriodEnd,
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
            CurrentPeriodEnd = currentPeriodEnd,
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
    public async Task HandleAsync_WhenSubscriptionIsActive_AndOrganizationHasSecretsManagerTrial_AndRemovingSecretsManagerTrial_RemovesPasswordManagerCoupon()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var subscription = new Subscription
        {
            Id = "sub_123",
            Status = StripeSubscriptionStatus.Active,
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(10),
            CustomerId = "cus_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new() { Plan = new Stripe.Plan { Id = "2023-enterprise-org-seat-annually" } }
                }
            },
            Customer = new Customer
            {
                Balance = 0,
                Discount = new Discount
                {
                    Coupon = new Coupon { Id = "sm-standalone" }
                }
            },
            Discount = new Discount
            {
                Coupon = new Coupon { Id = "sm-standalone" }
            },
            Metadata = new Dictionary<string, string>
            {
                { "organizationId", organizationId.ToString() }
            }
        };

        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.EnterpriseAnnually2023
        };

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
                        data = new[]
                        {
                            new { plan = new { id = "secrets-manager-enterprise-seat-annually" } }
                        }
                    },
                    Items = new StripeList<SubscriptionItem>
                    {
                        Data = new List<SubscriptionItem>
                        {
                            new SubscriptionItem
                            {
                                Plan = new Stripe.Plan { Id = "secrets-manager-enterprise-seat-annually" }
                            }
                        }
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
}
