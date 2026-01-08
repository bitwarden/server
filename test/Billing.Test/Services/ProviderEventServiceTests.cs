using Bit.Billing.Services;
using Bit.Billing.Services.Implementations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Providers.Entities;
using Bit.Core.Billing.Providers.Repositories;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Test.Billing.Mocks;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Billing.Test.Services;

public class ProviderEventServiceTests
{
    private readonly IOrganizationRepository _organizationRepository =
        Substitute.For<IOrganizationRepository>();

    private readonly IPricingClient _pricingClient =
        Substitute.For<IPricingClient>();

    private readonly IProviderInvoiceItemRepository _providerInvoiceItemRepository =
        Substitute.For<IProviderInvoiceItemRepository>();

    private readonly IProviderOrganizationRepository _providerOrganizationRepository =
        Substitute.For<IProviderOrganizationRepository>();

    private readonly IProviderPlanRepository _providerPlanRepository =
        Substitute.For<IProviderPlanRepository>();

    private readonly IStripeEventService _stripeEventService =
        Substitute.For<IStripeEventService>();

    private readonly IStripeFacade _stripeFacade =
        Substitute.For<IStripeFacade>();

    private readonly ProviderEventService _providerEventService;

    public ProviderEventServiceTests()
    {
        _providerEventService = new ProviderEventService(
            _organizationRepository,
            _pricingClient,
            _providerInvoiceItemRepository,
            _providerOrganizationRepository,
            _providerPlanRepository,
            _stripeEventService,
            _stripeFacade);
    }

    #region TryRecordInvoiceLineItems
    [Fact]
    public async Task TryRecordInvoiceLineItems_EventTypeNotInvoiceCreatedOrInvoiceFinalized_NoOp()
    {
        // Arrange
        var stripeEvent = new Event { Type = "payment_method.attached" };

        // Act
        await _providerEventService.TryRecordInvoiceLineItems(stripeEvent);

        // Assert
        await _stripeEventService.DidNotReceiveWithAnyArgs().GetInvoice(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>?>());
    }

    [Fact]
    public async Task TryRecordInvoiceLineItems_InvoiceParentTypeNotSubscriptionDetails_NoOp()
    {
        // Arrange
        var stripeEvent = new Event
        {
            Type = "invoice.created"
        };

        var invoice = new Invoice
        {
            Parent = new InvoiceParent
            {
                Type = "credit_note",
                SubscriptionDetails = new InvoiceParentSubscriptionDetails
                {
                    SubscriptionId = "sub_1"
                }
            }
        };

        _stripeEventService.GetInvoice(stripeEvent, true, Arg.Any<List<string>?>()).Returns(invoice);

        // Act
        await _providerEventService.TryRecordInvoiceLineItems(stripeEvent);

        // Assert
        await _stripeFacade.DidNotReceiveWithAnyArgs().GetSubscription(Arg.Any<string>());
    }

    [Fact]
    public async Task TryRecordInvoiceLineItems_EventNotProviderRelated_NoOp()
    {
        // Arrange
        var stripeEvent = new Event
        {
            Type = "invoice.created"
        };

        const string subscriptionId = "sub_1";

        var invoice = new Invoice
        {
            Parent = new InvoiceParent
            {
                Type = "subscription_details",
                SubscriptionDetails = new InvoiceParentSubscriptionDetails
                {
                    SubscriptionId = subscriptionId
                }
            }
        };

        _stripeEventService.GetInvoice(stripeEvent, true, Arg.Any<List<string>?>()).Returns(invoice);

        var subscription = new Subscription
        {
            Metadata = new Dictionary<string, string> { { "organizationId", Guid.NewGuid().ToString() } }
        };

        _stripeFacade.GetSubscription(subscriptionId).Returns(subscription);

        // Act
        await _providerEventService.TryRecordInvoiceLineItems(stripeEvent);

        // Assert
        await _providerOrganizationRepository.DidNotReceiveWithAnyArgs().GetManyDetailsByProviderAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task TryRecordInvoiceLineItems_InvoiceCreated_Succeeds()
    {
        // Arrange
        var stripeEvent = new Event
        {
            Type = "invoice.created"
        };

        const string subscriptionId = "sub_1";
        var providerId = Guid.NewGuid();

        var invoice = new Invoice
        {
            Id = "invoice_1",
            Number = "A",
            Parent = new InvoiceParent
            {
                Type = "subscription_details",
                SubscriptionDetails = new InvoiceParentSubscriptionDetails
                {
                    SubscriptionId = subscriptionId
                }
            },
            Discounts = [
                new Discount
                {
                    Coupon = new Coupon
                    {
                        PercentOff = 35
                    }
                }
            ]
        };

        _stripeEventService.GetInvoice(stripeEvent, true, Arg.Any<List<string>?>()).Returns(invoice);

        var subscription = new Subscription
        {
            Metadata = new Dictionary<string, string> { { "providerId", providerId.ToString() } }
        };

        _stripeFacade.GetSubscription(subscriptionId).Returns(subscription);

        var client1Id = Guid.NewGuid();
        var client2Id = Guid.NewGuid();

        var clients = new List<ProviderOrganizationOrganizationDetails>
        {
            new ()
            {
                OrganizationId = client1Id,
                OrganizationName = "Client 1",
                Plan = "Teams (Monthly)",
                Seats = 50,
                OccupiedSeats = 30,
                Status = OrganizationStatusType.Managed
            },
            new ()
            {
                OrganizationId = client2Id,
                OrganizationName = "Client 2",
                Plan = "Enterprise (Monthly)",
                Seats = 50,
                OccupiedSeats = 30,
                Status = OrganizationStatusType.Managed
            }
        };

        _providerOrganizationRepository.GetManyDetailsByProviderAsync(providerId).Returns(clients);

        _organizationRepository.GetByIdAsync(client1Id)
            .Returns(new Organization { PlanType = PlanType.TeamsMonthly });

        _organizationRepository.GetByIdAsync(client2Id)
            .Returns(new Organization { PlanType = PlanType.EnterpriseMonthly });

        var providerPlans = new List<ProviderPlan>
        {
            new ()
            {
                Id = Guid.NewGuid(),
                ProviderId = providerId,
                PlanType = PlanType.TeamsMonthly,
                AllocatedSeats = 50,
                PurchasedSeats = 0,
                SeatMinimum = 100
            },
            new ()
            {
                Id = Guid.NewGuid(),
                ProviderId = providerId,
                PlanType = PlanType.EnterpriseMonthly,
                AllocatedSeats = 50,
                PurchasedSeats = 0,
                SeatMinimum = 100
            }
        };

        foreach (var providerPlan in providerPlans)
        {
            _pricingClient.GetPlanOrThrow(providerPlan.PlanType).Returns(MockPlans.Get(providerPlan.PlanType));
        }

        _providerPlanRepository.GetByProviderId(providerId).Returns(providerPlans);

        // Act
        await _providerEventService.TryRecordInvoiceLineItems(stripeEvent);

        // Assert
        var teamsPlan = MockPlans.Get(PlanType.TeamsMonthly);
        var enterprisePlan = MockPlans.Get(PlanType.EnterpriseMonthly);

        await _providerInvoiceItemRepository.Received(1).CreateAsync(Arg.Is<ProviderInvoiceItem>(
            options =>
                options.ProviderId == providerId &&
                options.InvoiceId == invoice.Id &&
                options.InvoiceNumber == invoice.Number &&
                options.ClientName == "Client 1" &&
                options.ClientId == client1Id &&
                options.PlanName == "Teams (Monthly)" &&
                options.AssignedSeats == 50 &&
                options.UsedSeats == 30 &&
                options.Total == options.AssignedSeats * teamsPlan.PasswordManager.ProviderPortalSeatPrice * 0.65M));

        await _providerInvoiceItemRepository.Received(1).CreateAsync(Arg.Is<ProviderInvoiceItem>(
            options =>
                options.ProviderId == providerId &&
                options.InvoiceId == invoice.Id &&
                options.InvoiceNumber == invoice.Number &&
                options.ClientName == "Client 2" &&
                options.ClientId == client2Id &&
                options.PlanName == "Enterprise (Monthly)" &&
                options.AssignedSeats == 50 &&
                options.UsedSeats == 30 &&
                options.Total == options.AssignedSeats * enterprisePlan.PasswordManager.ProviderPortalSeatPrice * 0.65M));

        await _providerInvoiceItemRepository.Received(1).CreateAsync(Arg.Is<ProviderInvoiceItem>(
            options =>
                options.ProviderId == providerId &&
                options.InvoiceId == invoice.Id &&
                options.InvoiceNumber == invoice.Number &&
                options.ClientName == "Unassigned seats" &&
                options.PlanName == "Teams (Monthly)" &&
                options.AssignedSeats == 50 &&
                options.UsedSeats == 0 &&
                options.Total == options.AssignedSeats * teamsPlan.PasswordManager.ProviderPortalSeatPrice * 0.65M));

        await _providerInvoiceItemRepository.Received(1).CreateAsync(Arg.Is<ProviderInvoiceItem>(
            options =>
                options.ProviderId == providerId &&
                options.InvoiceId == invoice.Id &&
                options.InvoiceNumber == invoice.Number &&
                options.ClientName == "Unassigned seats" &&
                options.PlanName == "Enterprise (Monthly)" &&
                options.AssignedSeats == 50 &&
                options.UsedSeats == 0 &&
                options.Total == options.AssignedSeats * enterprisePlan.PasswordManager.ProviderPortalSeatPrice * 0.65M));
    }

    [Fact]
    public async Task TryRecordInvoiceLineItems_InvoiceFinalized_Succeeds()
    {
        // Arrange
        var stripeEvent = new Event
        {
            Type = "invoice.finalized"
        };

        const string subscriptionId = "sub_1";
        var providerId = Guid.NewGuid();

        var invoice = new Invoice
        {
            Id = "invoice_1",
            Number = "A",
            Parent = new InvoiceParent
            {
                Type = "subscription_details",
                SubscriptionDetails = new InvoiceParentSubscriptionDetails
                {
                    SubscriptionId = subscriptionId
                }
            },
        };

        _stripeEventService.GetInvoice(stripeEvent, true, Arg.Any<List<string>?>()).Returns(invoice);

        var subscription = new Subscription
        {
            Metadata = new Dictionary<string, string> { { "providerId", providerId.ToString() } }
        };

        _stripeFacade.GetSubscription(subscriptionId).Returns(subscription);

        var invoiceItems = new List<ProviderInvoiceItem>
        {
            new ()
            {
                Id = Guid.NewGuid(),
                ClientName = "Client 1"
            },
            new ()
            {
                Id = Guid.NewGuid(),
                ClientName = "Client 2"
            }
        };

        _providerInvoiceItemRepository.GetByInvoiceId(invoice.Id).Returns(invoiceItems);

        // Act
        await _providerEventService.TryRecordInvoiceLineItems(stripeEvent);

        // Assert
        await _providerInvoiceItemRepository.Received(2).ReplaceAsync(Arg.Is<ProviderInvoiceItem>(
            options => options.InvoiceNumber == "A"));
    }
    #endregion
}
