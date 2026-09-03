using Bit.Billing.Services;
using Bit.Billing.Services.Implementations;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Services;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Pricing.Premium;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Models.Mail.Billing.Renewal.Families2020Renewal;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.Billing.Mocks.Plans;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;
using Xunit;
using static Bit.Core.Billing.Constants.StripeConstants;
using Address = Stripe.Address;
using Event = Stripe.Event;
using PremiumPlan = Bit.Core.Billing.Pricing.Premium.Plan;

namespace Bit.Billing.Test.Services;

public class UpcomingInvoiceHandlerTests
{
    private readonly IGetPaymentMethodQuery _getPaymentMethodQuery;
    private readonly ILogger<StripeEventProcessor> _logger;
    private readonly IMailService _mailService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IPricingClient _pricingClient;
    private readonly IProviderRepository _providerRepository;
    private readonly IStripeAdapter _stripeAdapter;
    private readonly IPriceIncreaseScheduler _priceIncreaseScheduler;
    private readonly IStripeEventService _stripeEventService;
    private readonly IStripeEventUtilityService _stripeEventUtilityService;
    private readonly IUserRepository _userRepository;
    private readonly IValidateSponsorshipCommand _validateSponsorshipCommand;
    private readonly IMailer _mailer;
    private readonly IFeatureService _featureService;
    private readonly IBusinessPlanMigrationCoordinator _businessPlanMigrationCoordinator;

    private readonly UpcomingInvoiceHandler _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _providerId = Guid.NewGuid();

    public UpcomingInvoiceHandlerTests()
    {
        _getPaymentMethodQuery = Substitute.For<IGetPaymentMethodQuery>();
        _logger = Substitute.For<ILogger<StripeEventProcessor>>();
        _mailService = Substitute.For<IMailService>();
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _pricingClient = Substitute.For<IPricingClient>();
        _pricingClient.ListPremiumPlans().Returns(new List<PremiumPlan>());
        _providerRepository = Substitute.For<IProviderRepository>();
        _stripeAdapter = Substitute.For<IStripeAdapter>();
        _priceIncreaseScheduler = Substitute.For<IPriceIncreaseScheduler>();
        _stripeEventService = Substitute.For<IStripeEventService>();
        _stripeEventUtilityService = Substitute.For<IStripeEventUtilityService>();
        _userRepository = Substitute.For<IUserRepository>();
        _validateSponsorshipCommand = Substitute.For<IValidateSponsorshipCommand>();
        _mailer = Substitute.For<IMailer>();
        _featureService = Substitute.For<IFeatureService>();
        _businessPlanMigrationCoordinator = Substitute.For<IBusinessPlanMigrationCoordinator>();

        _sut = new UpcomingInvoiceHandler(
            _getPaymentMethodQuery,
            _logger,
            _mailService,
            _organizationRepository,
            _pricingClient,
            _providerRepository,
            _stripeAdapter,
            _priceIncreaseScheduler,
            _stripeEventService,
            _stripeEventUtilityService,
            _userRepository,
            _validateSponsorshipCommand,
            _mailer,
            _featureService,
            _businessPlanMigrationCoordinator);
    }

    [Fact]
    public async Task HandleAsync_WhenNullSubscription_DoesNothing()
    {
        // Arrange
        var parsedEvent = new Event();
        var invoice = new Invoice { CustomerId = "cus_123" };
        var customer = new Customer { Id = "cus_123", Subscriptions = new StripeList<Subscription> { Data = [] } };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter
            .GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeAdapter.DidNotReceive()
            .UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>());
    }

    [Fact]
    public async Task HandleAsync_WhenOrganizationHasSponsorship_SendsEmail()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123" };
        var invoice = new Invoice
        {
            CustomerId = "cus_123",
            AmountDue = 10000,
            NextPaymentAttempt = DateTime.UtcNow.AddDays(7),
            Lines = new StripeList<InvoiceLineItem>
            {
                Data = [new() { Description = "Test Item" }]
            }
        };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Items = new StripeList<SubscriptionItem>(),
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = false },
            Customer = new Customer { Id = "cus_123" },
            Metadata = new Dictionary<string, string>(),
            LatestInvoiceId = "inv_latest"
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "US" }
        };
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually
        };
        var plan = new FamiliesPlan();

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter
            .GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>())
            .Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));

        _organizationRepository
            .GetByIdAsync(_organizationId)
            .Returns(organization);

        _pricingClient
            .GetPlanOrThrow(organization.PlanType)
            .Returns(plan);

        _stripeEventUtilityService
            .IsSponsoredSubscription(subscription)
            .Returns(true);
        // Configure that this is a sponsored subscription
        _stripeEventUtilityService
            .IsSponsoredSubscription(subscription)
            .Returns(true);
        _validateSponsorshipCommand
            .ValidateSponsorshipAsync(_organizationId)
            .Returns(true);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _organizationRepository.Received(1).GetByIdAsync(_organizationId);
        await _validateSponsorshipCommand.Received(1).ValidateSponsorshipAsync(_organizationId);

        await _mailService.Received(1).SendInvoiceUpcoming(
            Arg.Is<IEnumerable<string>>(emails => emails.Contains("org@example.com")),
            Arg.Is<decimal>(amount => amount == invoice.AmountDue / 100M),
            Arg.Is<DateTime>(dueDate => dueDate == invoice.NextPaymentAttempt.Value),
            Arg.Is<List<string>>(items => items.Count == invoice.Lines.Data.Count),
            Arg.Is<bool>(b => b == true));
    }

    [Fact]
    public async Task
        HandleAsync_WhenOrganizationHasSponsorship_ButInvalidSponsorship_RetrievesUpdatedInvoice_SendsEmail()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123" };
        var invoice = new Invoice
        {
            CustomerId = "cus_123",
            AmountDue = 10000,
            NextPaymentAttempt = DateTime.UtcNow.AddDays(7),
            Lines = new StripeList<InvoiceLineItem>
            {
                Data = [new() { Description = "Test Item" }]
            }
        };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                    [new SubscriptionItem { Price = new Price { Id = "2021-family-for-enterprise-annually" } }]
            },
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = false },
            Customer = new Customer { Id = "cus_123" },
            Metadata = new Dictionary<string, string>(),
            LatestInvoiceId = "inv_latest"
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "US" }
        };
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually
        };
        var plan = new FamiliesPlan();

        var paymentMethod = new Card { Last4 = "4242", Brand = "visa" };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter
            .GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>())
            .Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));

        _organizationRepository
            .GetByIdAsync(_organizationId)
            .Returns(organization);

        _pricingClient
            .GetPlanOrThrow(organization.PlanType)
            .Returns(plan);

        // Configure that this is not a sponsored subscription
        _stripeEventUtilityService
            .IsSponsoredSubscription(subscription)
            .Returns(true);

        // Validate sponsorship should return false
        _validateSponsorshipCommand
            .ValidateSponsorshipAsync(_organizationId)
            .Returns(false);
        _stripeAdapter
            .GetInvoiceAsync(subscription.LatestInvoiceId)
            .Returns(invoice);

        _getPaymentMethodQuery.Run(organization).Returns(MaskedPaymentMethod.From(paymentMethod));

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _organizationRepository.Received(1).GetByIdAsync(_organizationId);
        _stripeEventUtilityService.Received(1).IsSponsoredSubscription(subscription);
        await _validateSponsorshipCommand.Received(1).ValidateSponsorshipAsync(_organizationId);
        await _stripeAdapter.Received(1).GetInvoiceAsync(Arg.Is("inv_latest"));

        await _mailService.Received(1).SendInvoiceUpcoming(
            Arg.Is<IEnumerable<string>>(emails => emails.Contains("org@example.com")),
            Arg.Is<decimal>(amount => amount == invoice.AmountDue / 100M),
            Arg.Is<DateTime>(dueDate => dueDate == invoice.NextPaymentAttempt.Value),
            Arg.Is<List<string>>(items => items.Count == invoice.Lines.Data.Count),
            Arg.Is<bool>(b => b == true));
    }

    [Fact]
    public async Task HandleAsync_WhenValidOrganization_SendsEmail()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123" };
        var invoice = new Invoice
        {
            CustomerId = "cus_123",
            AmountDue = 10000,
            NextPaymentAttempt = DateTime.UtcNow.AddDays(7),
            Lines = new StripeList<InvoiceLineItem>
            {
                Data = [new() { Description = "Test Item" }]
            }
        };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                    [new SubscriptionItem { Price = new Price { Id = "enterprise-annually" } }]
            },
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = false },
            Customer = new Customer { Id = "cus_123" },
            Metadata = new Dictionary<string, string>(),
            LatestInvoiceId = "inv_latest"
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "US" }
        };
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually
        };
        var plan = new FamiliesPlan();

        var paymentMethod = new Card { Last4 = "4242", Brand = "visa" };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter
            .GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>())
            .Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));

        _organizationRepository
            .GetByIdAsync(_organizationId)
            .Returns(organization);

        _pricingClient
            .GetPlanOrThrow(organization.PlanType)
            .Returns(plan);

        _stripeEventUtilityService
            .IsSponsoredSubscription(subscription)
            .Returns(false);

        _stripeAdapter
            .GetInvoiceAsync(subscription.LatestInvoiceId)
            .Returns(invoice);

        _getPaymentMethodQuery.Run(organization).Returns(MaskedPaymentMethod.From(paymentMethod));

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _organizationRepository.Received(1).GetByIdAsync(_organizationId);
        _stripeEventUtilityService.Received(1).IsSponsoredSubscription(subscription);

        // Should not validate sponsorship for non-sponsored subscription
        await _validateSponsorshipCommand.DidNotReceive().ValidateSponsorshipAsync(Arg.Any<Guid>());

        await _mailService.Received(1).SendInvoiceUpcoming(
            Arg.Is<IEnumerable<string>>(emails => emails.Contains("org@example.com")),
            Arg.Is<decimal>(amount => amount == invoice.AmountDue / 100M),
            Arg.Is<DateTime>(dueDate => dueDate == invoice.NextPaymentAttempt.Value),
            Arg.Is<List<string>>(items => items.Count == invoice.Lines.Data.Count),
            Arg.Is<bool>(b => b == true));
    }
    [Fact]
    public async Task HandleAsync_WhenOrganizationCustomerIsExempt_DoesNotUpdateTaxExemption()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123" };
        var invoice = new Invoice { CustomerId = "cus_123", AmountDue = 0, Lines = new StripeList<InvoiceLineItem> { Data = [] } };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Items = new StripeList<SubscriptionItem>(),
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true },
            Customer = new Customer { Id = "cus_123" },
            Metadata = new Dictionary<string, string>()
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "DE" },
            TaxExempt = TaxExempt.Exempt
        };
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(new EnterprisePlan(isAnnual: true));
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeAdapter.DidNotReceive().UpdateCustomerAsync(
            Arg.Any<string>(),
            Arg.Any<CustomerUpdateOptions>());
    }

    [Fact]
    public async Task HandleAsync_WhenValidProviderSubscription_SendsEmail()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123" };
        var invoice = new Invoice
        {
            CustomerId = "cus_123",
            AmountDue = 10000,
            NextPaymentAttempt = DateTime.UtcNow.AddDays(7),
            Lines = new StripeList<InvoiceLineItem>
            {
                Data = [new() { Description = "Test Item" }]
            }
        };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Items = new StripeList<SubscriptionItem>(),
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = false },
            Customer = new Customer { Id = "cus_123" },
            Metadata = new Dictionary<string, string>(),
            CollectionMethod = "charge_automatically"
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "UK" },
            TaxExempt = TaxExempt.None
        };
        var provider = new Provider { Id = _providerId, BillingEmail = "provider@example.com" };

        var paymentMethod = new Card { Last4 = "4242", Brand = "visa" };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, null, _providerId));

        _providerRepository.GetByIdAsync(_providerId).Returns(provider);
        _getPaymentMethodQuery.Run(provider).Returns(MaskedPaymentMethod.From(paymentMethod));

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _providerRepository.Received(2).GetByIdAsync(_providerId);

        // Verify automatic tax was enabled
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            Arg.Is("sub_123"),
            Arg.Is<SubscriptionUpdateOptions>(o => o.AutomaticTax.Enabled == true));

        // Verify provider invoice email was sent
        await _mailService.Received(1).SendProviderInvoiceUpcoming(
            Arg.Is<IEnumerable<string>>(e => e.Contains("provider@example.com")),
            Arg.Is<decimal>(amount => amount == invoice.AmountDue / 100M),
            Arg.Is<DateTime>(dueDate => dueDate == invoice.NextPaymentAttempt.Value),
            Arg.Is<List<string>>(items => items.Count == invoice.Lines.Data.Count),
            Arg.Is<string>(s => s == subscription.CollectionMethod),
            Arg.Is<bool>(b => b == true),
            Arg.Is<string>(s => s == $"{paymentMethod.Brand} ending in {paymentMethod.Last4}"));
    }
    [Fact]
    public async Task HandleAsync_WhenProviderCustomerIsExempt_DoesNotUpdateTaxExemption()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123" };
        var invoice = new Invoice
        {
            CustomerId = "cus_123",
            AmountDue = 10000,
            NextPaymentAttempt = DateTime.UtcNow.AddDays(7),
            Lines = new StripeList<InvoiceLineItem>
            {
                Data = [new() { Description = "Test Item" }]
            }
        };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Items = new StripeList<SubscriptionItem>(),
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true },
            Customer = new Customer { Id = "cus_123" },
            Metadata = new Dictionary<string, string>(),
            CollectionMethod = "charge_automatically"
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "DE" },
            TaxExempt = TaxExempt.Exempt
        };
        var provider = new Provider { Id = _providerId, BillingEmail = "provider@example.com" };
        var paymentMethod = new Card { Last4 = "4242", Brand = "visa" };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, null, _providerId));
        _providerRepository.GetByIdAsync(_providerId).Returns(provider);
        _getPaymentMethodQuery.Run(provider).Returns(MaskedPaymentMethod.From(paymentMethod));

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeAdapter.DidNotReceive().UpdateCustomerAsync(
            Arg.Any<string>(),
            Arg.Any<CustomerUpdateOptions>());
    }
    [Fact]
    public async Task HandleAsync_WhenOrganizationNotFound_DoesNothing()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123" };
        var invoice = new Invoice
        {
            CustomerId = "cus_123",
            AmountDue = 10000,
            NextPaymentAttempt = DateTime.UtcNow.AddDays(7),
            Lines = new StripeList<InvoiceLineItem>
            {
                Data = [new() { Description = "Test Item" }]
            }
        };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Items = new StripeList<SubscriptionItem>(),
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = false },
            Customer = new Customer { Id = "cus_123" },
            Metadata = new Dictionary<string, string>()
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] }
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter
            .GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>())
            .Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));

        // Organization not found
        _organizationRepository.GetByIdAsync(_organizationId).Returns((Organization)null);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _organizationRepository.Received(1).GetByIdAsync(_organizationId);

        // Verify no emails were sent
        await _mailService.DidNotReceive().SendInvoiceUpcoming(
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<decimal>(),
            Arg.Any<DateTime>(),
            Arg.Any<List<string>>(),
            Arg.Any<bool>());
    }

    [Fact]
    public async Task HandleAsync_WhenZeroAmountInvoice_DoesNothing()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123" };
        var invoice = new Invoice
        {
            CustomerId = "cus_123",
            AmountDue = 0, // Zero amount due
            NextPaymentAttempt = DateTime.UtcNow.AddDays(7),
            Lines = new StripeList<InvoiceLineItem>
            {
                Data = [new() { Description = "Free Item" }]
            }
        };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Items = new StripeList<SubscriptionItem>(),
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = false },
            Customer = new Customer { Id = "cus_123" },
            Metadata = new Dictionary<string, string>()
        };
        var user = new User { Id = _userId, Email = "user@example.com", Premium = true };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] }
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter
            .GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>())
            .Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, _userId, null));

        _userRepository.GetByIdAsync(_userId).Returns(user);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _userRepository.Received(1).GetByIdAsync(_userId);

        // Should not
        await _mailService.DidNotReceive().SendInvoiceUpcoming(
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<decimal>(),
            Arg.Any<DateTime>(),
            Arg.Any<List<string>>(),
            Arg.Any<bool>());
    }

    [Fact]
    public async Task HandleAsync_WhenUserNotFound_DoesNothing()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123" };
        var invoice = new Invoice
        {
            CustomerId = "cus_123",
            AmountDue = 10000,
            NextPaymentAttempt = DateTime.UtcNow.AddDays(7),
            Lines = new StripeList<InvoiceLineItem>
            {
                Data = [new() { Description = "Test Item" }]
            }
        };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Items = new StripeList<SubscriptionItem>(),
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = false },
            Customer = new Customer { Id = "cus_123" },
            Metadata = new Dictionary<string, string>()
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] }
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter
            .GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>())
            .Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, _userId, null));

        // User not found
        _userRepository.GetByIdAsync(_userId).Returns((User)null);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _userRepository.Received(1).GetByIdAsync(_userId);

        // Verify no emails were sent
        await _mailService.DidNotReceive().SendInvoiceUpcoming(
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<decimal>(),
            Arg.Any<DateTime>(),
            Arg.Any<List<string>>(),
            Arg.Any<bool>());

        await _mailer.DidNotReceive().SendEmail(Arg.Any<Families2020RenewalMail>());
    }

    [Fact]
    public async Task HandleAsync_WhenProviderNotFound_DoesNothing()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123" };
        var invoice = new Invoice
        {
            CustomerId = "cus_123",
            AmountDue = 10000,
            NextPaymentAttempt = DateTime.UtcNow.AddDays(7),
            Lines = new StripeList<InvoiceLineItem>
            {
                Data = [new() { Description = "Test Item" }]
            }
        };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Items = new StripeList<SubscriptionItem>(),
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = false },
            Customer = new Customer { Id = "cus_123" },
            Metadata = new Dictionary<string, string>()
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] }
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter
            .GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>())
            .Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, null, _providerId));

        // Provider not found
        _providerRepository.GetByIdAsync(_providerId).Returns((Provider)null);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _providerRepository.Received(1).GetByIdAsync(_providerId);

        // Verify no provider emails were sent
        await _mailService.DidNotReceive().SendProviderInvoiceUpcoming(
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<decimal>(),
            Arg.Any<DateTime>(),
            Arg.Any<List<string>>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<string>());
    }
    [Fact]
    public async Task HandleAsync_WhenMilestone3Enabled_ButNotFamilies2019Plan_DoesNotUpdateSubscription()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var customerId = "cus_123";
        var subscriptionId = "sub_123";

        var invoice = new Invoice
        {
            CustomerId = customerId,
            AmountDue = 40000,
            NextPaymentAttempt = DateTime.UtcNow.AddDays(7),
            Lines = new StripeList<InvoiceLineItem>
            {
                Data = [new() { Description = "Test Item" }]
            }
        };

        var familiesPlan = new FamiliesPlan();

        var subscription = new Subscription
        {
            Id = subscriptionId,
            CustomerId = customerId,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new() { Id = "si_pm_123", Price = new Price { Id = familiesPlan.PasswordManager.StripePlanId } }
                ]
            },
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true },
            Metadata = new Dictionary<string, string>()
        };

        var customer = new Customer
        {
            Id = customerId,
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "US" }
        };

        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.FamiliesAnnually // Already on the new plan
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesPlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert - should not update subscription when not on FamiliesAnnually2019 plan
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Any<string>(),
            Arg.Is<SubscriptionUpdateOptions>(o => o.Discounts != null));

        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
        // Families plan is excluded from tax exempt alignment
        await _stripeAdapter.DidNotReceive().UpdateCustomerAsync(
            Arg.Any<string>(),
            Arg.Any<CustomerUpdateOptions>());
    }

    [Fact]
    public async Task HandleAsync_WhenMilestone3Enabled_AndPasswordManagerItemNotFound_LogsWarning()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var customerId = "cus_123";
        var subscriptionId = "sub_123";

        var invoice = new Invoice
        {
            CustomerId = customerId,
            AmountDue = 40000,
            NextPaymentAttempt = DateTime.UtcNow.AddDays(7),
            Lines = new StripeList<InvoiceLineItem>
            {
                Data = [new() { Description = "Test Item" }]
            }
        };

        var families2019Plan = new Families2019Plan();

        var subscription = new Subscription
        {
            Id = subscriptionId,
            CustomerId = customerId,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new() { Id = "si_different_item", Price = new Price { Id = "different-price-id" } }
                ]
            },
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true },
            Metadata = new Dictionary<string, string>()
        };

        var customer = new Customer
        {
            Id = customerId,
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "US" }
        };

        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.FamiliesAnnually2019
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019Plan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString().Contains($"Could not find Organization's ({_organizationId}) password manager item") &&
                o.ToString().Contains(parsedEvent.Id)),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());

        // Should not update subscription or organization when password manager item not found
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Any<string>(),
            Arg.Is<SubscriptionUpdateOptions>(o => o.Discounts != null));

        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
    }
    [Fact]
    public async Task HandleAsync_Premium_CallsScheduler()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var customerId = "cus_123";
        var subscriptionId = "sub_123";

        var invoice = new Invoice { CustomerId = customerId };
        var subscription = new Subscription
        {
            Id = subscriptionId,
            CustomerId = customerId,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new() { Id = "si_premium_123", Price = new Price { Id = Prices.PremiumAnnually }, Quantity = 1 }
                ]
            },
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true },
            Metadata = new Dictionary<string, string>()
        };
        var user = new User { Id = _userId, Email = "user@example.com", Premium = true };
        var plan = new PremiumPlan
        {
            Name = "Premium",
            Available = true,
            LegacyYear = null,
            Seat = new Purchasable { Price = 10M, StripePriceId = "premium-annually-2025" },
            Storage = new Purchasable { Price = 4M, StripePriceId = Prices.StoragePlanPersonal }
        };
        var oldPlan = new PremiumPlan
        {
            Name = "Premium (Old)",
            Available = false,
            LegacyYear = 2023,
            Seat = new Purchasable { Price = 10M, StripePriceId = Prices.PremiumAnnually },
            Storage = new Purchasable { Price = 4M, StripePriceId = Prices.StoragePlanPersonal }
        };
        var customer = new Customer
        {
            Id = customerId,
            Subscriptions = new StripeList<Subscription> { Data = [subscription] }
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>()).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, _userId, null));
        _userRepository.GetByIdAsync(_userId).Returns(user);
        _pricingClient.ListPremiumPlans().Returns(new List<PremiumPlan> { oldPlan, plan });
        _stripeAdapter.GetCouponAsync(CouponIDs.Milestone2SubscriptionDiscount)
            .Returns(new Coupon { PercentOff = 20, Id = CouponIDs.Milestone2SubscriptionDiscount });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _priceIncreaseScheduler.Received(1).SchedulePersonalPriceIncrease(subscription);
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }
    [Fact]
    public async Task HandleAsync_Families_CallsScheduler()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var customerId = "cus_123";
        var subscriptionId = "sub_123";

        var families2019Plan = new Families2019Plan();

        var subscription = new Subscription
        {
            Id = subscriptionId,
            CustomerId = customerId,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new()
                    {
                        Id = "si_pm_123",
                        Price = new Price { Id = families2019Plan.PasswordManager.StripePlanId },
                        Quantity = 1
                    }
                ]
            },
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true },
            Metadata = new Dictionary<string, string>()
        };
        var invoice = new Invoice { CustomerId = customerId };
        var customer = new Customer
        {
            Id = customerId,
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "US" }
        };
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.FamiliesAnnually2019
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019Plan);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(new FamiliesPlan());
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _stripeAdapter.GetCouponAsync(CouponIDs.Milestone3SubscriptionDiscount)
            .Returns(new Coupon { PercentOff = 25, Id = CouponIDs.Milestone3SubscriptionDiscount });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _priceIncreaseScheduler.Received(1).SchedulePersonalPriceIncrease(subscription);
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task HandleAsync_WhenOrganizationTaxNotEnabled_SchedulePresent_UpdatesSchedulePhasesAndDefaultSettings()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var invoice = new Invoice { CustomerId = "cus_123", Lines = new StripeList<InvoiceLineItem> { Data = [] } };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = false },
            Items = new StripeList<SubscriptionItem> { Data = [] },
            Metadata = new Dictionary<string, string> { { "organizationId", _organizationId.ToString() } },
            // subscriptions.data.customer is expanded, so subscription.Customer carries the discount.
            Customer = new Customer { Id = "cus_123" }
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "US" }
        };
        var organization = new Organization { Id = _organizationId, PlanType = PlanType.TeamsAnnually, BillingEmail = "test@test.com" };

        var phase1Start = DateTime.UtcNow.AddDays(-10);
        var phase1End = DateTime.UtcNow.AddDays(5);
        var phase2End = DateTime.UtcNow.AddDays(370);

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(new TeamsPlan(isAnnual: true));

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sub_sched_123",
                        SubscriptionId = "sub_123",
                        Status = SubscriptionScheduleStatus.Active,
                        Phases = new List<SubscriptionSchedulePhase>
                        {
                            new()
                            {
                                StartDate = phase1Start,
                                EndDate = phase1End,
                                Items = [new SubscriptionSchedulePhaseItem { PriceId = "price_old", Quantity = 1 }],
                                Discounts = [],
                                ProrationBehavior = "none"
                            },
                            new()
                            {
                                StartDate = phase1End,
                                EndDate = phase2End,
                                Items = [new SubscriptionSchedulePhaseItem { PriceId = "price_new", Quantity = 1 }],
                                Discounts = [new SubscriptionSchedulePhaseDiscount { CouponId = "milestone-coupon" }],
                                ProrationBehavior = "none"
                            }
                        }
                    }
                ]
            });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — schedule updated with phases and default_settings
        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            Arg.Is("sub_sched_123"),
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.DefaultSettings.AutomaticTax.Enabled == true &&
                o.Phases.Count == 2 &&
                o.Phases[0].AutomaticTax.Enabled == true &&
                o.Phases[0].Items[0].Price == "price_old" &&
                o.Phases[1].AutomaticTax.Enabled == true &&
                o.Phases[1].Items[0].Price == "price_new" &&
                o.Phases[1].Discounts[0].Coupon == "milestone-coupon"));

        // Assert — subscription NOT updated directly for tax
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Is("sub_123"), Arg.Is<SubscriptionUpdateOptions>(o => o.AutomaticTax != null));
    }

    [Fact]
    public async Task HandleAsync_WhenOrganizationTaxNotEnabled_SchedulePresent_OmitsCustomerDiscountFromActivePhase()
    {
        // The customer coupon is omitted from the active phase so it isn't stacked onto the current
        // period; with no live subscription discounts to carry, the active phase has no explicit
        // discounts. It is still re-listed on the future phase, or it would drop off there.
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var invoice = new Invoice { CustomerId = "cus_123", Lines = new StripeList<InvoiceLineItem> { Data = [] } };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = false },
            Items = new StripeList<SubscriptionItem> { Data = [] },
            Metadata = new Dictionary<string, string> { { "organizationId", _organizationId.ToString() } },
            // subscriptions.data.customer is expanded, so subscription.Customer carries the discount.
            Customer = new Customer
            {
                Id = "cus_123",
                Discount = new Discount { Source = new DiscountSource { Coupon = new Coupon { Id = "retention" } } }
            }
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "US" }
        };
        var organization = new Organization { Id = _organizationId, PlanType = PlanType.TeamsAnnually, BillingEmail = "test@test.com" };

        var phase1Start = DateTime.UtcNow.AddDays(-10);
        var phase1End = DateTime.UtcNow.AddDays(5);
        var phase2End = DateTime.UtcNow.AddDays(370);

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(new TeamsPlan(isAnnual: true));

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sub_sched_123",
                        SubscriptionId = "sub_123",
                        Status = SubscriptionScheduleStatus.Active,
                        Phases = new List<SubscriptionSchedulePhase>
                        {
                            new()
                            {
                                StartDate = phase1Start,
                                EndDate = phase1End,
                                Items = [new SubscriptionSchedulePhaseItem { PriceId = "price_old", Quantity = 1 }],
                                Discounts = [],
                                ProrationBehavior = "none"
                            },
                            new()
                            {
                                StartDate = phase1End,
                                EndDate = phase2End,
                                Items = [new SubscriptionSchedulePhaseItem { PriceId = "price_new", Quantity = 1 }],
                                Discounts = [new SubscriptionSchedulePhaseDiscount { CouponId = "milestone-coupon" }],
                                ProrationBehavior = "none"
                            }
                        }
                    }
                ]
            });

        await _sut.HandleAsync(parsedEvent);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            Arg.Is("sub_sched_123"),
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases.Count == 2 &&
                // Active phase 0: customer coupon omitted, no live subscription discounts to carry.
                o.Phases[0].Discounts == null &&
                // Future phase 1: customer coupon carried in, stacked with the existing milestone.
                o.Phases[1].Discounts.Any(d => d.Coupon == "retention") &&
                o.Phases[1].Discounts.Any(d => d.Coupon == "milestone-coupon")));
    }

    [Fact]
    public async Task HandleAsync_WhenOrganizationTaxNotEnabled_SchedulePresent_LiveSubscriptionDiscountCarriedOntoEveryPhaseByDiscountId()
    {
        // A discount currently live on the subscription (e.g. a "forever" coupon) is carried onto
        // every rebuilt phase, referenced by its discount id -- not re-granted as a new coupon.
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var invoice = new Invoice { CustomerId = "cus_123", Lines = new StripeList<InvoiceLineItem> { Data = [] } };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = false },
            Items = new StripeList<SubscriptionItem> { Data = [] },
            Metadata = new Dictionary<string, string> { { "organizationId", _organizationId.ToString() } },
            Customer = new Customer { Id = "cus_123" },
            Discounts = [new Discount { Id = "di_live", Source = new DiscountSource { Coupon = new Coupon { Id = "live-coupon" } } }]
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "US" }
        };
        var organization = new Organization { Id = _organizationId, PlanType = PlanType.TeamsAnnually, BillingEmail = "test@test.com" };

        var phase1Start = DateTime.UtcNow.AddDays(-10);
        var phase1End = DateTime.UtcNow.AddDays(5);
        var phase2End = DateTime.UtcNow.AddDays(370);

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(new TeamsPlan(isAnnual: true));

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sub_sched_123",
                        SubscriptionId = "sub_123",
                        Status = SubscriptionScheduleStatus.Active,
                        Phases = new List<SubscriptionSchedulePhase>
                        {
                            new()
                            {
                                StartDate = phase1Start,
                                EndDate = phase1End,
                                Items = [new SubscriptionSchedulePhaseItem { PriceId = "price_old", Quantity = 1 }],
                                Discounts = [],
                                ProrationBehavior = "none"
                            },
                            new()
                            {
                                StartDate = phase1End,
                                EndDate = phase2End,
                                Items = [new SubscriptionSchedulePhaseItem { PriceId = "price_new", Quantity = 1 }],
                                Discounts = [],
                                ProrationBehavior = "none"
                            }
                        }
                    }
                ]
            });

        await _sut.HandleAsync(parsedEvent);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            Arg.Is("sub_sched_123"),
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases.Count == 2 &&
                o.Phases[0].Discounts != null &&
                o.Phases[0].Discounts.Any(d => d.Discount == "di_live") &&
                o.Phases[1].Discounts != null &&
                o.Phases[1].Discounts.Any(d => d.Discount == "di_live")));
    }

    [Fact]
    public async Task HandleAsync_WhenOrganizationTaxNotEnabled_SchedulePresent_ItemLevelCouponsPreservedOnRebuild()
    {
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var invoice = new Invoice { CustomerId = "cus_123", Lines = new StripeList<InvoiceLineItem> { Data = [] } };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = false },
            Items = new StripeList<SubscriptionItem> { Data = [] },
            Metadata = new Dictionary<string, string> { { "organizationId", _organizationId.ToString() } },
            Customer = new Customer { Id = "cus_123" }
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "US" }
        };
        var organization = new Organization { Id = _organizationId, PlanType = PlanType.TeamsAnnually, BillingEmail = "test@test.com" };

        var phase1Start = DateTime.UtcNow.AddDays(-10);
        var phase1End = DateTime.UtcNow.AddDays(5);
        var phase2End = DateTime.UtcNow.AddDays(370);

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(new TeamsPlan(isAnnual: true));

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sub_sched_123",
                        SubscriptionId = "sub_123",
                        Status = SubscriptionScheduleStatus.Active,
                        Phases = new List<SubscriptionSchedulePhase>
                        {
                            new()
                            {
                                StartDate = phase1Start,
                                EndDate = phase1End,
                                Items =
                                [
                                    new SubscriptionSchedulePhaseItem
                                    {
                                        PriceId = "price_old",
                                        Quantity = 1,
                                        Discounts = [new SubscriptionSchedulePhaseItemDiscount { CouponId = "item-coupon-1" }]
                                    }
                                ],
                                Discounts = [],
                                ProrationBehavior = "none"
                            },
                            new()
                            {
                                StartDate = phase1End,
                                EndDate = phase2End,
                                Items =
                                [
                                    new SubscriptionSchedulePhaseItem
                                    {
                                        PriceId = "price_new",
                                        Quantity = 1,
                                        Discounts = [new SubscriptionSchedulePhaseItemDiscount { CouponId = "item-coupon-2" }]
                                    }
                                ],
                                Discounts = [],
                                ProrationBehavior = "none"
                            }
                        }
                    }
                ]
            });

        await _sut.HandleAsync(parsedEvent);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            Arg.Is("sub_sched_123"),
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases.Count == 2 &&
                o.Phases[0].Items[0].Discounts != null &&
                o.Phases[0].Items[0].Discounts.Any(d => d.Coupon == "item-coupon-1") &&
                o.Phases[1].Items[0].Discounts != null &&
                o.Phases[1].Items[0].Discounts.Any(d => d.Coupon == "item-coupon-2")));
    }

    [Fact]
    public async Task HandleAsync_WhenOrganizationTaxNotEnabled_SchedulePresent_PreservesPhaseMetadata()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var invoice = new Invoice { CustomerId = "cus_123", Lines = new StripeList<InvoiceLineItem> { Data = [] } };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = false },
            Items = new StripeList<SubscriptionItem> { Data = [] },
            Metadata = new Dictionary<string, string> { { "organizationId", _organizationId.ToString() } },
            Customer = new Customer { Id = "cus_123" }
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "US" }
        };
        var organization = new Organization { Id = _organizationId, PlanType = PlanType.TeamsAnnually, BillingEmail = "test@test.com" };

        var phase1Start = DateTime.UtcNow.AddDays(-10);
        var phase1End = DateTime.UtcNow.AddDays(5);
        var phase2End = DateTime.UtcNow.AddDays(370);

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>()).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(new TeamsPlan(isAnnual: true));

        // The non-cohort key pins the passthrough as unconditional — nothing filters by key.
        var phaseMetadata = new Dictionary<string, string>
        {
            { MetadataKeys.MigrationCohortId, "cohort_123" },
            { MetadataKeys.MigrationCohortName, "Teams 2020 Annual" },
            { "unrelated_key", "unrelated_value" }
        };

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sub_sched_123",
                        SubscriptionId = "sub_123",
                        Status = SubscriptionScheduleStatus.Active,
                        Phases = new List<SubscriptionSchedulePhase>
                        {
                            new()
                            {
                                StartDate = phase1Start,
                                EndDate = phase1End,
                                Items = [new SubscriptionSchedulePhaseItem { PriceId = "price_old", Quantity = 1 }],
                                Discounts = [],
                                Metadata = phaseMetadata,
                                ProrationBehavior = "none"
                            },
                            new()
                            {
                                StartDate = phase1End,
                                EndDate = phase2End,
                                Items = [new SubscriptionSchedulePhaseItem { PriceId = "price_new", Quantity = 1 }],
                                Discounts = [new SubscriptionSchedulePhaseDiscount { CouponId = "milestone-coupon" }],
                                Metadata = phaseMetadata,
                                ProrationBehavior = "none"
                            }
                        }
                    }
                ]
            });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            Arg.Is("sub_sched_123"),
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases.Count == 2 &&
                o.Phases[0].Metadata != null &&
                o.Phases[0].Metadata[MetadataKeys.MigrationCohortId] == "cohort_123" &&
                o.Phases[0].Metadata[MetadataKeys.MigrationCohortName] == "Teams 2020 Annual" &&
                o.Phases[0].Metadata["unrelated_key"] == "unrelated_value" &&
                o.Phases[1].Metadata != null &&
                o.Phases[1].Metadata[MetadataKeys.MigrationCohortId] == "cohort_123" &&
                o.Phases[1].Metadata[MetadataKeys.MigrationCohortName] == "Teams 2020 Annual" &&
                o.Phases[1].Metadata["unrelated_key"] == "unrelated_value"));
    }

    [Fact]
    public async Task HandleAsync_WhenOrganizationTaxNotEnabled_SchedulePresent_PhaseMetadataEmpty_StaysEmpty()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var invoice = new Invoice { CustomerId = "cus_123", Lines = new StripeList<InvoiceLineItem> { Data = [] } };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = false },
            Items = new StripeList<SubscriptionItem> { Data = [] },
            Metadata = new Dictionary<string, string> { { "organizationId", _organizationId.ToString() } },
            Customer = new Customer { Id = "cus_123" }
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "US" }
        };
        var organization = new Organization { Id = _organizationId, PlanType = PlanType.TeamsAnnually, BillingEmail = "test@test.com" };

        var phase1Start = DateTime.UtcNow.AddDays(-10);
        var phase1End = DateTime.UtcNow.AddDays(5);
        var phase2End = DateTime.UtcNow.AddDays(370);

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>()).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(new TeamsPlan(isAnnual: true));

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sub_sched_123",
                        SubscriptionId = "sub_123",
                        Status = SubscriptionScheduleStatus.Active,
                        Phases = new List<SubscriptionSchedulePhase>
                        {
                            new()
                            {
                                StartDate = phase1Start,
                                EndDate = phase1End,
                                Items = [new SubscriptionSchedulePhaseItem { PriceId = "price_old", Quantity = 1 }],
                                Discounts = [],
                                Metadata = new Dictionary<string, string>(),
                                ProrationBehavior = "none"
                            },
                            new()
                            {
                                StartDate = phase1End,
                                EndDate = phase2End,
                                Items = [new SubscriptionSchedulePhaseItem { PriceId = "price_new", Quantity = 1 }],
                                Discounts = [],
                                Metadata = new Dictionary<string, string>(),
                                ProrationBehavior = "none"
                            }
                        }
                    }
                ]
            });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            Arg.Is("sub_sched_123"),
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases.Count == 2 &&
                o.Phases[0].Metadata != null && o.Phases[0].Metadata.Count == 0 &&
                o.Phases[1].Metadata != null && o.Phases[1].Metadata.Count == 0));
    }

    [Fact]
    public async Task HandleAsync_WhenOrganizationTaxNotEnabled_MultiPhaseSchedule_PreservesMetadataOnEveryRebuiltPhase()
    {
        // Phase 0 has already ended, so it is skipped and the surviving phases shift down by one.
        // Distinct metadata per phase is what catches an index-shift bug; a uniform dict would hide it.
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var invoice = new Invoice { CustomerId = "cus_123", Lines = new StripeList<InvoiceLineItem> { Data = [] } };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = false },
            Items = new StripeList<SubscriptionItem> { Data = [] },
            Metadata = new Dictionary<string, string> { { "organizationId", _organizationId.ToString() } },
            Customer = new Customer { Id = "cus_123" }
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "US" }
        };
        var organization = new Organization { Id = _organizationId, PlanType = PlanType.TeamsAnnually, BillingEmail = "test@test.com" };

        var phase0Start = DateTime.UtcNow.AddDays(-370);
        var phase0End = DateTime.UtcNow.AddDays(-5);
        var phase1End = DateTime.UtcNow.AddDays(360);
        var phase2End = DateTime.UtcNow.AddDays(725);

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>()).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(new TeamsPlan(isAnnual: true));

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sub_sched_123",
                        SubscriptionId = "sub_123",
                        Status = SubscriptionScheduleStatus.Active,
                        Phases = new List<SubscriptionSchedulePhase>
                        {
                            new()
                            {
                                StartDate = phase0Start,
                                EndDate = phase0End,
                                Items = [new SubscriptionSchedulePhaseItem { PriceId = "price_oldest", Quantity = 1 }],
                                Discounts = [],
                                Metadata = new Dictionary<string, string> { { MetadataKeys.MigrationCohortId, "cohort_0" } },
                                ProrationBehavior = "none"
                            },
                            new()
                            {
                                StartDate = phase0End,
                                EndDate = phase1End,
                                Items = [new SubscriptionSchedulePhaseItem { PriceId = "price_old", Quantity = 1 }],
                                Discounts = [],
                                Metadata = new Dictionary<string, string> { { MetadataKeys.MigrationCohortId, "cohort_1" } },
                                ProrationBehavior = "none"
                            },
                            new()
                            {
                                StartDate = phase1End,
                                EndDate = phase2End,
                                Items = [new SubscriptionSchedulePhaseItem { PriceId = "price_new", Quantity = 1 }],
                                Discounts = [],
                                Metadata = new Dictionary<string, string> { { MetadataKeys.MigrationCohortId, "cohort_2" } },
                                ProrationBehavior = "none"
                            }
                        }
                    }
                ]
            });

        await _sut.HandleAsync(parsedEvent);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            Arg.Is("sub_sched_123"),
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases.Count == 2 &&
                o.Phases[0].Items[0].Price == "price_old" &&
                o.Phases[0].Metadata != null &&
                o.Phases[0].Metadata[MetadataKeys.MigrationCohortId] == "cohort_1" &&
                o.Phases[1].Items[0].Price == "price_new" &&
                o.Phases[1].Metadata != null &&
                o.Phases[1].Metadata[MetadataKeys.MigrationCohortId] == "cohort_2"));
    }

    [Fact]
    public async Task HandleAsync_WhenOrganizationTaxNotEnabled_NoSchedule_UpdatesSubscriptionDirectly()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var invoice = new Invoice { CustomerId = "cus_123", Lines = new StripeList<InvoiceLineItem> { Data = [] } };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = false },
            Items = new StripeList<SubscriptionItem> { Data = [] },
            Metadata = new Dictionary<string, string> { { "organizationId", _organizationId.ToString() } }
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "US" }
        };
        var organization = new Organization { Id = _organizationId, PlanType = PlanType.TeamsAnnually, BillingEmail = "test@test.com" };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(new TeamsPlan(isAnnual: true));

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = new List<SubscriptionSchedule>() });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — subscription updated directly
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            Arg.Is("sub_123"),
            Arg.Is<SubscriptionUpdateOptions>(o => o.AutomaticTax.Enabled == true));

        // Assert — schedule NOT updated
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionScheduleAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionScheduleUpdateOptions>());
    }

    [Fact]
    public async Task HandleAsync_WhenPremiumUserTaxNotEnabled_SchedulePresent_UpdatesSchedulePhasesAndDefaultSettings()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var invoice = new Invoice { CustomerId = "cus_123", Lines = new StripeList<InvoiceLineItem> { Data = [] } };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = false },
            Items = new StripeList<SubscriptionItem> { Data = [] },
            Metadata = new Dictionary<string, string> { { "userId", _userId.ToString() } },
            Customer = new Customer { Id = "cus_123" }
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Tax = new CustomerTax { AutomaticTax = AutomaticTaxStatus.Supported }
        };
        var user = new User { Id = _userId, Email = "test@test.com", Premium = true };

        var phase1Start = DateTime.UtcNow.AddDays(-10);
        var phase1End = DateTime.UtcNow.AddDays(5);
        var phase2End = DateTime.UtcNow.AddDays(370);

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, _userId, null));
        _userRepository.GetByIdAsync(_userId).Returns(user);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sub_sched_456",
                        SubscriptionId = "sub_123",
                        Status = SubscriptionScheduleStatus.Active,
                        Phases = new List<SubscriptionSchedulePhase>
                        {
                            new()
                            {
                                StartDate = phase1Start,
                                EndDate = phase1End,
                                Items = [new SubscriptionSchedulePhaseItem { PriceId = "premium-annually", Quantity = 1 }],
                                Discounts = [],
                                ProrationBehavior = "none"
                            },
                            new()
                            {
                                StartDate = phase1End,
                                EndDate = phase2End,
                                Items = [new SubscriptionSchedulePhaseItem { PriceId = "premium-annually-new", Quantity = 1 }],
                                Discounts = [new SubscriptionSchedulePhaseDiscount { CouponId = "milestone-2c" }],
                                ProrationBehavior = "none"
                            }
                        }
                    }
                ]
            });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — schedule updated with phases and default_settings
        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            Arg.Is("sub_sched_456"),
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.DefaultSettings.AutomaticTax.Enabled == true &&
                o.Phases.Count == 2 &&
                o.Phases[0].AutomaticTax.Enabled == true &&
                o.Phases[0].Items[0].Price == "premium-annually" &&
                o.Phases[1].AutomaticTax.Enabled == true &&
                o.Phases[1].Items[0].Price == "premium-annually-new" &&
                o.Phases[1].Discounts[0].Coupon == "milestone-2c"));

        // Assert — subscription NOT updated directly for tax
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Is("sub_123"), Arg.Is<SubscriptionUpdateOptions>(o => o.AutomaticTax != null));
    }

    [Fact]
    public async Task HandleAsync_WhenPremiumUserTaxNotEnabled_NoSchedule_UpdatesSubscriptionDirectly()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var invoice = new Invoice { CustomerId = "cus_123", Lines = new StripeList<InvoiceLineItem> { Data = [] } };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = false },
            Items = new StripeList<SubscriptionItem> { Data = [] },
            Metadata = new Dictionary<string, string> { { "userId", _userId.ToString() } }
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Tax = new CustomerTax { AutomaticTax = AutomaticTaxStatus.Supported }
        };
        var user = new User { Id = _userId, Email = "test@test.com", Premium = true };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, _userId, null));
        _userRepository.GetByIdAsync(_userId).Returns(user);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = new List<SubscriptionSchedule>() });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — subscription updated directly
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            Arg.Is("sub_123"),
            Arg.Is<SubscriptionUpdateOptions>(o => o.AutomaticTax.Enabled == true));

        // Assert — schedule NOT updated
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionScheduleAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionScheduleUpdateOptions>());
    }

    [Fact]
    public async Task HandleAsync_WhenTaxNotEnabled_Phase2Active_SkipsCompletedPhaseAndConsumedCouponNotReadded()
    {
        // Arrange — Phase 1 has ended, Phase 2 is now the active phase.
        // Phase 2's one-time migration discount was consumed at transition and must not be re-included.
        // There is no other live discount to carry, so the phase's Discounts is null, never [].
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var invoice = new Invoice { CustomerId = "cus_123", Lines = new StripeList<InvoiceLineItem> { Data = [] } };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = false },
            Items = new StripeList<SubscriptionItem> { Data = [] },
            Metadata = new Dictionary<string, string> { { "userId", _userId.ToString() } },
            Customer = new Customer { Id = "cus_123" }
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Tax = new CustomerTax { AutomaticTax = AutomaticTaxStatus.Supported }
        };
        var user = new User { Id = _userId, Email = "test@test.com", Premium = true };

        // Phase 1 ended yesterday, Phase 2 active now
        var phase1Start = DateTime.UtcNow.AddDays(-375);
        var phase1End = DateTime.UtcNow.AddDays(-1);
        var phase2End = DateTime.UtcNow.AddDays(364);

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, _userId, null));
        _userRepository.GetByIdAsync(_userId).Returns(user);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sub_sched_789",
                        SubscriptionId = "sub_123",
                        Status = SubscriptionScheduleStatus.Active,
                        Phases = new List<SubscriptionSchedulePhase>
                        {
                            new()
                            {
                                StartDate = phase1Start,
                                EndDate = phase1End,
                                Items = [new SubscriptionSchedulePhaseItem { PriceId = "price_old", Quantity = 1 }],
                                Discounts = [],
                                ProrationBehavior = "none"
                            },
                            new()
                            {
                                StartDate = phase1End,
                                EndDate = phase2End,
                                Items = [new SubscriptionSchedulePhaseItem { PriceId = "price_new", Quantity = 1 }],
                                Discounts = [new SubscriptionSchedulePhaseDiscount { CouponId = "milestone-2c" }],
                                ProrationBehavior = "none"
                            }
                        }
                    }
                ]
            });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — schedule updated: Phase 1 skipped, Phase 2 included, consumed coupon not re-added
        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            Arg.Is("sub_sched_789"),
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.DefaultSettings.AutomaticTax.Enabled == true &&
                o.Phases.Count == 1 &&
                o.Phases[0].AutomaticTax.Enabled == true &&
                o.Phases[0].Items[0].Price == "price_new" &&
                o.Phases[0].Discounts == null));
    }

    [Fact]
    public async Task HandleAsync_OrganizationWithMismatchedTaxExempt_DoesNotUpdateCustomerTaxExempt()
    {
        var parsedEvent = new Event { Id = "evt_123" };
        var invoice = new Invoice { CustomerId = "cus_123", AmountDue = 0, Lines = new StripeList<InvoiceLineItem> { Data = [] } };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Items = new StripeList<SubscriptionItem>(),
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true },
            Customer = new Customer { Id = "cus_123" },
            Metadata = new Dictionary<string, string>()
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "DE" },
            TaxExempt = TaxExempt.None
        };
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(new EnterprisePlan(isAnnual: true));
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);

        await _sut.HandleAsync(parsedEvent);

        await _stripeAdapter.DidNotReceive().UpdateCustomerAsync(
            Arg.Any<string>(), Arg.Is<CustomerUpdateOptions>(o => o.TaxExempt != null));
    }

    [Fact]
    public async Task HandleAsync_ProviderWithMismatchedTaxExempt_DoesNotUpdateCustomerTaxExempt()
    {
        var parsedEvent = new Event { Id = "evt_123" };
        var invoice = new Invoice { CustomerId = "cus_123", AmountDue = 0, Lines = new StripeList<InvoiceLineItem> { Data = [] } };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Items = new StripeList<SubscriptionItem>(),
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true },
            Customer = new Customer { Id = "cus_123" },
            Metadata = new Dictionary<string, string>()
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "DE" },
            TaxExempt = TaxExempt.None
        };
        var provider = new Provider { Id = _providerId, BillingEmail = "provider@example.com" };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, null, _providerId));
        _providerRepository.GetByIdAsync(_providerId).Returns(provider);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);

        await _sut.HandleAsync(parsedEvent);

        await _stripeAdapter.DidNotReceive().UpdateCustomerAsync(
            Arg.Any<string>(), Arg.Is<CustomerUpdateOptions>(o => o.TaxExempt != null));
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessMigrationCompleted_DoesNotSendStandardUpcomingInvoiceEmail()
    {
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Metadata = new Dictionary<string, string>(),
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true }
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "US" }
        };
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(new Invoice { CustomerId = "cus_123" });
        _stripeAdapter.GetCustomerAsync("cus_123", Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>()).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _businessPlanMigrationCoordinator.ExecuteAsync(organization, subscription)
            .Returns(BusinessPlanMigrationResult.Completed);

        await _sut.HandleAsync(parsedEvent);

        await _mailService.DidNotReceiveWithAnyArgs().SendInvoiceUpcoming(
            Arg.Any<IEnumerable<string>>(), Arg.Any<decimal>(), Arg.Any<DateTime>(), Arg.Any<List<string>>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessMigrationCompletedWithoutNotification_DoesNotSendStandardEmailAndLogsError()
    {
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Metadata = new Dictionary<string, string>(),
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true }
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "US" }
        };
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(new Invoice { CustomerId = "cus_123" });
        _stripeAdapter.GetCustomerAsync("cus_123", Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>()).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _businessPlanMigrationCoordinator.ExecuteAsync(organization, subscription)
            .Returns(BusinessPlanMigrationResult.CompletedWithoutNotification);

        await _sut.HandleAsync(parsedEvent);

        await _mailService.DidNotReceiveWithAnyArgs().SendInvoiceUpcoming(
            Arg.Any<IEnumerable<string>>(), Arg.Any<decimal>(), Arg.Any<DateTime>(), Arg.Any<List<string>>(), Arg.Any<bool>());
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString()!.Contains("no renewal notification was sent") && o.ToString()!.Contains(parsedEvent.Id)),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessMigrationNotAssigned_SendsStandardUpcomingInvoiceEmail()
    {
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var invoice = new Invoice
        {
            CustomerId = "cus_123",
            AmountDue = 10000,
            NextPaymentAttempt = DateTime.UtcNow.AddDays(7),
            Lines = new StripeList<InvoiceLineItem> { Data = [new() { Description = "Test Item" }] }
        };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Metadata = new Dictionary<string, string>(),
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true }
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "US" }
        };
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync("cus_123", Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>()).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(new EnterprisePlan(isAnnual: true));
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _businessPlanMigrationCoordinator.ExecuteAsync(organization, subscription)
            .Returns(BusinessPlanMigrationResult.NotAssigned);

        await _sut.HandleAsync(parsedEvent);

        await _mailService.Received(1).SendInvoiceUpcoming(
            Arg.Is<IEnumerable<string>>(emails => emails.Contains("org@example.com")),
            Arg.Any<decimal>(), Arg.Any<DateTime>(), Arg.Any<List<string>>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessMigrationThrows_SendsStandardEmailAndLogsError()
    {
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var invoice = new Invoice
        {
            CustomerId = "cus_123",
            AmountDue = 10000,
            NextPaymentAttempt = DateTime.UtcNow.AddDays(7),
            Lines = new StripeList<InvoiceLineItem> { Data = [new() { Description = "Test Item" }] }
        };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Metadata = new Dictionary<string, string>(),
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true }
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "US" }
        };
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync("cus_123", Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>()).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(new EnterprisePlan(isAnnual: true));
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _businessPlanMigrationCoordinator.ExecuteAsync(organization, subscription)
            .ThrowsAsync(new Exception("scheduling blew up"));

        await _sut.HandleAsync(parsedEvent);

        await _mailService.Received(1).SendInvoiceUpcoming(
            Arg.Is<IEnumerable<string>>(emails => emails.Contains("org@example.com")),
            Arg.Any<decimal>(), Arg.Any<DateTime>(), Arg.Any<List<string>>(), Arg.Any<bool>());
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString()!.Contains("Failed to run business plan price migration") && o.ToString()!.Contains(parsedEvent.Id)),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessMigrationFlagOff_DoesNotInvokeCoordinatorAndSendsStandardEmail()
    {
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var invoice = new Invoice
        {
            CustomerId = "cus_123",
            AmountDue = 10000,
            NextPaymentAttempt = DateTime.UtcNow.AddDays(7),
            Lines = new StripeList<InvoiceLineItem> { Data = [new() { Description = "Test Item" }] }
        };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Metadata = new Dictionary<string, string>(),
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true }
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "US" }
        };
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync("cus_123", Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>()).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(new EnterprisePlan(isAnnual: true));
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(false);

        await _sut.HandleAsync(parsedEvent);

        await _businessPlanMigrationCoordinator.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default!);
        await _mailService.Received(1).SendInvoiceUpcoming(
            Arg.Is<IEnumerable<string>>(emails => emails.Contains("org@example.com")),
            Arg.Any<decimal>(), Arg.Any<DateTime>(), Arg.Any<List<string>>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task HandleAsync_DoesNotRequestCustomerDiscountExpansionDeeperThanStripeAllows()
    {
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Metadata = new Dictionary<string, string>(),
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true }
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "US" }
        };
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(new Invoice { CustomerId = "cus_123" });
        _stripeAdapter.GetCustomerAsync("cus_123", Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>()).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _businessPlanMigrationCoordinator.ExecuteAsync(organization, subscription)
            .Returns(BusinessPlanMigrationResult.Completed);

        await _sut.HandleAsync(parsedEvent);

        // Stripe rejects (400) an expand path deeper than 4 levels, which would 500 the whole webhook.
        // Guard that the customer fetch never requests one (e.g. subscriptions.data.customer.discount.coupon).
        await _stripeAdapter.Received().GetCustomerAsync(
            Arg.Any<string>(),
            Arg.Is<CustomerGetOptions>(options =>
                options.Expand != null && options.Expand.All(path => path.Split('.').Length <= 4)));
    }

    [Fact]
    public async Task HandleAsync_WhenFreeTier_DoesNotInvokeCoordinator_AndSendsNoStandardEmail()
    {
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Metadata = new Dictionary<string, string>(),
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true }
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "US" }
        };
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.Free
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(new Invoice { CustomerId = "cus_123" });
        _stripeAdapter.GetCustomerAsync("cus_123", Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>()).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.Free).Returns(new FreePlan());
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);

        await _sut.HandleAsync(parsedEvent);

        await _businessPlanMigrationCoordinator.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default!);
        await _mailService.DidNotReceive().SendInvoiceUpcoming(
            Arg.Any<IEnumerable<string>>(), Arg.Any<decimal>(), Arg.Any<DateTime>(), Arg.Any<List<string>>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task HandleAsync_WhenTeamsStarterTier_RoutesToBusinessMigration_AndDoesNotSendStandardEmail()
    {
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Metadata = new Dictionary<string, string>(),
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true }
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "US" }
        };
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.TeamsStarter2023
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(new Invoice { CustomerId = "cus_123" });
        _stripeAdapter.GetCustomerAsync("cus_123", Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionGetOptions>()).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _businessPlanMigrationCoordinator.ExecuteAsync(organization, subscription)
            .Returns(BusinessPlanMigrationResult.Completed);

        await _sut.HandleAsync(parsedEvent);

        await _businessPlanMigrationCoordinator.Received(1).ExecuteAsync(organization, subscription);
        await _mailService.DidNotReceiveWithAnyArgs().SendInvoiceUpcoming(
            Arg.Any<IEnumerable<string>>(), Arg.Any<decimal>(), Arg.Any<DateTime>(), Arg.Any<List<string>>(), Arg.Any<bool>());
    }
}
