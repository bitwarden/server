using System.Globalization;
using Bit.Billing.Services;
using Bit.Billing.Services.Implementations;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Pricing.Premium;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Models.Mail.Billing.Renewal.BusinessPlanRenewal2020Migration;
using Bit.Core.Models.Mail.Billing.Renewal.Families2019Renewal;
using Bit.Core.Models.Mail.Billing.Renewal.Families2020Renewal;
using Bit.Core.Models.Mail.Billing.Renewal.Premium;
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
    private readonly IOrganizationPlanMigrationCohortAssignmentRepository _assignmentRepository;
    private readonly IOrganizationPlanMigrationCohortRepository _cohortRepository;
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

    private readonly UpcomingInvoiceHandler _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _providerId = Guid.NewGuid();

    public UpcomingInvoiceHandlerTests()
    {
        _getPaymentMethodQuery = Substitute.For<IGetPaymentMethodQuery>();
        _logger = Substitute.For<ILogger<StripeEventProcessor>>();
        _mailService = Substitute.For<IMailService>();
        _assignmentRepository = Substitute.For<IOrganizationPlanMigrationCohortAssignmentRepository>();
        _cohortRepository = Substitute.For<IOrganizationPlanMigrationCohortRepository>();
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

        _sut = new UpcomingInvoiceHandler(
            _getPaymentMethodQuery,
            _logger,
            _mailService,
            _assignmentRepository,
            _cohortRepository,
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
            _featureService);
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
    public async Task HandleAsync_WhenValidUser_SendsEmail()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123" };
        var customerId = "cus_123";
        var invoice = new Invoice
        {
            CustomerId = customerId,
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
            CustomerId = customerId,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new() { Id = "si_123", Price = new Price { Id = Prices.PremiumAnnually } }]
            },
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = false },
            Customer = new Customer { Id = customerId },
            Metadata = new Dictionary<string, string>()
        };
        var user = new User { Id = _userId, Email = "user@example.com", Premium = true };
        var plan = new PremiumPlan
        {
            Name = "Premium",
            Available = true,
            LegacyYear = null,
            Seat = new Purchasable { Price = 10M, StripePriceId = Prices.PremiumAnnually },
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
            Tax = new CustomerTax { AutomaticTax = AutomaticTaxStatus.Supported },
            Subscriptions = new StripeList<Subscription> { Data = [subscription] }
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter
            .GetCustomerAsync(customerId, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, _userId, null));

        _userRepository.GetByIdAsync(_userId).Returns(user);
        _pricingClient.ListPremiumPlans().Returns(new List<PremiumPlan> { oldPlan, plan });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _userRepository.Received(1).GetByIdAsync(_userId);

        await _mailService.Received(1).SendInvoiceUpcoming(
            Arg.Is<IEnumerable<string>>(emails => emails.Contains("user@example.com")),
            Arg.Is<decimal>(amount => amount == invoice.AmountDue / 100M),
            Arg.Is<DateTime>(dueDate => dueDate == invoice.NextPaymentAttempt.Value),
            Arg.Is<List<string>>(items => items.Count == invoice.Lines.Data.Count),
            Arg.Is<bool>(b => b == true));
    }

    [Fact]
    public async Task
        HandleAsync_WhenUserValid_AndMilestone2Enabled_UpdatesPriceId_AndSendsUpdatedInvoiceUpcomingEmail()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123" };
        var customerId = "cus_123";
        var priceSubscriptionId = "sub-1";
        var priceId = "price-id-2";
        var invoice = new Invoice
        {
            CustomerId = customerId,
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
            CustomerId = customerId,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new() { Id = priceSubscriptionId, Price = new Price { Id = Prices.PremiumAnnually } }]
            },
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = false },
            Customer = new Customer
            {
                Id = customerId,
                Tax = new CustomerTax { AutomaticTax = AutomaticTaxStatus.Supported }
            },
            Metadata = new Dictionary<string, string>()
        };
        var user = new User { Id = _userId, Email = "user@example.com", Premium = true };
        var plan = new PremiumPlan
        {
            Name = "Premium",
            Available = true,
            LegacyYear = null,
            Seat = new Purchasable { Price = 10M, StripePriceId = priceId },
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
        _stripeAdapter
            .GetCustomerAsync(customerId, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, _userId, null));

        _userRepository.GetByIdAsync(_userId).Returns(user);
        _pricingClient.ListPremiumPlans().Returns(new List<PremiumPlan> { oldPlan, plan });
        _stripeAdapter.UpdateSubscriptionAsync(
                subscription.Id,
                Arg.Any<SubscriptionUpdateOptions>())
            .Returns(subscription);

        var coupon = new Coupon { PercentOff = 20, Id = CouponIDs.Milestone2SubscriptionDiscount };

        _stripeAdapter.GetCouponAsync(CouponIDs.Milestone2SubscriptionDiscount).Returns(coupon);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _userRepository.Received(1).GetByIdAsync(_userId);
        await _pricingClient.Received(1).ListPremiumPlans();
        await _stripeAdapter.Received(1).GetCouponAsync(CouponIDs.Milestone2SubscriptionDiscount);
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            Arg.Is("sub_123"),
            Arg.Is<SubscriptionUpdateOptions>(o =>
                o.Items[0].Id == priceSubscriptionId &&
                o.Items[0].Price == priceId &&
                o.Discounts[0].Coupon == CouponIDs.Milestone2SubscriptionDiscount &&
                o.ProrationBehavior == "none"));

        // Verify the updated invoice email was sent with correct price
        var discountedPrice = plan.Seat.Price * (100 - coupon.PercentOff.Value) / 100;
        await _mailer.Received(1).SendEmail(
            Arg.Is<PremiumRenewalMail>(email =>
                email.ToEmails.Contains("user@example.com") &&
                email.Subject == "Your Bitwarden Premium renewal is updating" &&
                email.View.BaseMonthlyRenewalPrice == (plan.Seat.Price / 12).ToString("C", new CultureInfo("en-US")) &&
                email.View.DiscountedAnnualRenewalPrice == discountedPrice.ToString("C", new CultureInfo("en-US")) &&
                email.View.DiscountAmount == $"{coupon.PercentOff}%"
            ));
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
    public async Task HandleAsync_WhenUpdateSubscriptionItemPriceIdFails_LogsErrorAndSendsTraditionalEmail()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123" };
        var customerId = "cus_123";
        var priceSubscriptionId = "sub-1";
        var priceId = "price-id-2";
        var invoice = new Invoice
        {
            CustomerId = customerId,
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
            CustomerId = customerId,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new() { Id = priceSubscriptionId, Price = new Price { Id = Prices.PremiumAnnually } }]
            },
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true },
            Customer = new Customer
            {
                Id = customerId,
                Tax = new CustomerTax { AutomaticTax = AutomaticTaxStatus.Supported }
            },
            Metadata = new Dictionary<string, string>()
        };
        var user = new User { Id = _userId, Email = "user@example.com", Premium = true };
        var plan = new PremiumPlan
        {
            Name = "Premium",
            Available = true,
            LegacyYear = null,
            Seat = new Purchasable { Price = 10M, StripePriceId = priceId },
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
        _stripeAdapter.GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>()).Returns(customer);

        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, _userId, null));

        _userRepository.GetByIdAsync(_userId).Returns(user);

        _pricingClient.ListPremiumPlans().Returns(new List<PremiumPlan> { oldPlan, plan });

        // Setup exception when updating subscription
        _stripeAdapter
            .UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .ThrowsAsync(new Exception());

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString()
                    .Contains(
                        $"Failed to update user's ({user.Id}) subscription price id while processing event with ID {parsedEvent.Id}")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());

        // Verify that traditional email was sent when update fails
        await _mailService.Received(1).SendInvoiceUpcoming(
            Arg.Is<IEnumerable<string>>(emails => emails.Contains("user@example.com")),
            Arg.Is<decimal>(amount => amount == invoice.AmountDue / 100M),
            Arg.Is<DateTime>(dueDate => dueDate == invoice.NextPaymentAttempt.Value),
            Arg.Is<List<string>>(items => items.Count == invoice.Lines.Data.Count),
            Arg.Is<bool>(b => b == true));

        // Verify renewal email was NOT sent
        await _mailer.DidNotReceive().SendEmail(Arg.Any<Families2020RenewalMail>());
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
    public async Task HandleAsync_WhenMilestone3Enabled_AndFamilies2019Plan_UpdatesSubscriptionAndOrganization()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var customerId = "cus_123";
        var subscriptionId = "sub_123";
        var passwordManagerItemId = "si_pm_123";
        var premiumAccessItemId = "si_premium_123";

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
        var familiesPlan = new FamiliesPlan();

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
                        Id = passwordManagerItemId,
                        Price = new Price { Id = families2019Plan.PasswordManager.StripePlanId }
                    },
                    new()
                    {
                        Id = premiumAccessItemId,
                        Price = new Price { Id = families2019Plan.PasswordManager.StripePremiumAccessPlanId }
                    }
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

        var coupon = new Coupon { PercentOff = 25, Id = CouponIDs.Milestone3SubscriptionDiscount };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetCouponAsync(CouponIDs.Milestone3SubscriptionDiscount).Returns(coupon);
        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019Plan);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesPlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            Arg.Is(subscriptionId),
            Arg.Is<SubscriptionUpdateOptions>(o =>
                o.Items.Count == 2 &&
                o.Items[0].Id == passwordManagerItemId &&
                o.Items[0].Price == familiesPlan.PasswordManager.StripePlanId &&
                o.Items[1].Id == premiumAccessItemId &&
                o.Items[1].Deleted == true &&
                o.Discounts.Count == 1 &&
                o.Discounts[0].Coupon == CouponIDs.Milestone3SubscriptionDiscount &&
                o.ProrationBehavior == ProrationBehavior.None));

        await _stripeAdapter.Received(1).GetCouponAsync(CouponIDs.Milestone3SubscriptionDiscount);

        await _organizationRepository.Received(1).ReplaceAsync(
            Arg.Is<Organization>(org =>
                org.Id == _organizationId &&
                org.PlanType == PlanType.FamiliesAnnually &&
                org.Plan == familiesPlan.Name &&
                org.UsersGetPremium == familiesPlan.UsersGetPremium &&
                org.Seats == familiesPlan.PasswordManager.BaseSeats));

        await _mailer.Received(1).SendEmail(
            Arg.Is<Families2019RenewalMail>(email =>
                email.ToEmails.Contains("org@example.com") &&
                email.Subject == "Your Bitwarden Families subscription is updating" &&
                email.View.BaseMonthlyRenewalPrice == (familiesPlan.PasswordManager.BasePrice / 12).ToString("C", new CultureInfo("en-US")) &&
                email.View.BaseAnnualRenewalPrice == familiesPlan.PasswordManager.BasePrice.ToString("C", new CultureInfo("en-US")) &&
                email.View.DiscountAmount == $"{coupon.PercentOff}%"
                ));

        // Families plan is excluded from tax exempt alignment
        await _stripeAdapter.DidNotReceive().UpdateCustomerAsync(
            Arg.Any<string>(),
            Arg.Any<CustomerUpdateOptions>());
    }

    [Fact]
    public async Task HandleAsync_WhenMilestone3Enabled_AndFamilies2019Plan_WithoutPremiumAccess_UpdatesSubscriptionAndOrganization()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var customerId = "cus_123";
        var subscriptionId = "sub_123";
        var passwordManagerItemId = "si_pm_123";

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
        var familiesPlan = new FamiliesPlan();

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
                        Id = passwordManagerItemId,
                        Price = new Price { Id = families2019Plan.PasswordManager.StripePlanId }
                    }
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
        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019Plan);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesPlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            Arg.Is(subscriptionId),
            Arg.Is<SubscriptionUpdateOptions>(o =>
                o.Items.Count == 1 &&
                o.Items[0].Id == passwordManagerItemId &&
                o.Items[0].Price == familiesPlan.PasswordManager.StripePlanId &&
                o.Discounts.Count == 1 &&
                o.Discounts[0].Coupon == CouponIDs.Milestone3SubscriptionDiscount &&
                o.ProrationBehavior == ProrationBehavior.None));

        await _organizationRepository.Received(1).ReplaceAsync(
            Arg.Is<Organization>(org =>
                org.Id == _organizationId &&
                org.PlanType == PlanType.FamiliesAnnually &&
                org.Plan == familiesPlan.Name &&
                org.UsersGetPremium == familiesPlan.UsersGetPremium &&
                org.Seats == familiesPlan.PasswordManager.BaseSeats));

        // Families plan is excluded from tax exempt alignment
        await _stripeAdapter.DidNotReceive().UpdateCustomerAsync(
            Arg.Any<string>(),
            Arg.Any<CustomerUpdateOptions>());
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
    public async Task HandleAsync_WhenMilestone3Enabled_AndUpdateFails_LogsErrorAndSendsTraditionalEmail()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var customerId = "cus_123";
        var subscriptionId = "sub_123";
        var passwordManagerItemId = "si_pm_123";

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
        var familiesPlan = new FamiliesPlan();

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
                        Id = passwordManagerItemId,
                        Price = new Price { Id = families2019Plan.PasswordManager.StripePlanId }
                    }
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
        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019Plan);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesPlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);

        // Simulate update failure
        _stripeAdapter
            .UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .ThrowsAsync(new Exception("Stripe API error"));

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString().Contains($"Failed to align subscription concerns for Organization ({_organizationId})") &&
                o.ToString().Contains(parsedEvent.Type) &&
                o.ToString().Contains(parsedEvent.Id)),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());

        // Should send traditional email when update fails
        await _mailService.Received(1).SendInvoiceUpcoming(
            Arg.Is<IEnumerable<string>>(emails => emails.Contains("org@example.com")),
            Arg.Is<decimal>(amount => amount == invoice.AmountDue / 100M),
            Arg.Is<DateTime>(dueDate => dueDate == invoice.NextPaymentAttempt.Value),
            Arg.Is<List<string>>(items => items.Count == invoice.Lines.Data.Count),
            Arg.Is<bool>(b => b == true));

        // Verify renewal email was NOT sent
        await _mailer.DidNotReceive().SendEmail(Arg.Any<Families2020RenewalMail>());
    }

    [Fact]
    public async Task HandleAsync_WhenMilestone3Enabled_AndCouponNotFound_LogsErrorAndSendsTraditionalEmail()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var customerId = "cus_123";
        var subscriptionId = "sub_123";
        var passwordManagerItemId = "si_pm_123";

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
        var familiesPlan = new FamiliesPlan();

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
                        Id = passwordManagerItemId,
                        Price = new Price { Id = families2019Plan.PasswordManager.StripePlanId }
                    }
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
        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019Plan);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesPlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _stripeAdapter.GetCouponAsync(CouponIDs.Milestone3SubscriptionDiscount).Returns((Coupon)null);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(subscription);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert - Exception is caught, error is logged, and traditional email is sent
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString().Contains($"Failed to align subscription concerns for Organization ({_organizationId})") &&
                o.ToString().Contains(parsedEvent.Type) &&
                o.ToString().Contains(parsedEvent.Id)),
            Arg.Is<Exception>(e => e is InvalidOperationException && e.Message.Contains("Coupon for sending families 2019 email")),
            Arg.Any<Func<object, Exception, string>>());

        await _mailer.DidNotReceive().SendEmail(Arg.Any<Families2019RenewalMail>());

        await _mailService.Received(1).SendInvoiceUpcoming(
            Arg.Is<IEnumerable<string>>(emails => emails.Contains("org@example.com")),
            Arg.Is<decimal>(amount => amount == invoice.AmountDue / 100M),
            Arg.Is<DateTime>(dueDate => dueDate == invoice.NextPaymentAttempt.Value),
            Arg.Is<List<string>>(items => items.Count == invoice.Lines.Data.Count),
            Arg.Is<bool>(b => b == true));
    }

    [Fact]
    public async Task HandleAsync_WhenMilestone3Enabled_AndCouponPercentOffIsNull_LogsErrorAndSendsTraditionalEmail()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var customerId = "cus_123";
        var subscriptionId = "sub_123";
        var passwordManagerItemId = "si_pm_123";

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
        var familiesPlan = new FamiliesPlan();

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
                        Id = passwordManagerItemId,
                        Price = new Price { Id = families2019Plan.PasswordManager.StripePlanId }
                    }
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

        var coupon = new Coupon
        {
            Id = CouponIDs.Milestone3SubscriptionDiscount,
            PercentOff = null
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019Plan);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesPlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _stripeAdapter.GetCouponAsync(CouponIDs.Milestone3SubscriptionDiscount).Returns(coupon);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(subscription);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert - Exception is caught, error is logged, and traditional email is sent
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString().Contains($"Failed to align subscription concerns for Organization ({_organizationId})") &&
                o.ToString().Contains(parsedEvent.Type) &&
                o.ToString().Contains(parsedEvent.Id)),
            Arg.Is<Exception>(e => e is InvalidOperationException && e.Message.Contains("coupon.PercentOff")),
            Arg.Any<Func<object, Exception, string>>());

        await _mailer.DidNotReceive().SendEmail(Arg.Any<Families2019RenewalMail>());

        await _mailService.Received(1).SendInvoiceUpcoming(
            Arg.Is<IEnumerable<string>>(emails => emails.Contains("org@example.com")),
            Arg.Is<decimal>(amount => amount == invoice.AmountDue / 100M),
            Arg.Is<DateTime>(dueDate => dueDate == invoice.NextPaymentAttempt.Value),
            Arg.Is<List<string>>(items => items.Count == invoice.Lines.Data.Count),
            Arg.Is<bool>(b => b == true));
    }

    [Fact]
    public async Task HandleAsync_WhenMilestone3Enabled_AndSeatAddOnExists_DeletesItem()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var customerId = "cus_123";
        var subscriptionId = "sub_123";
        var passwordManagerItemId = "si_pm_123";
        var seatAddOnItemId = "si_seat_123";

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
        var familiesPlan = new FamiliesPlan();

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
                        Id = passwordManagerItemId,
                        Price = new Price { Id = families2019Plan.PasswordManager.StripePlanId }
                    },

                    new()
                    {
                        Id = seatAddOnItemId,
                        Price = new Price { Id = "personal-org-seat-annually" },
                        Quantity = 3
                    }
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

        var coupon = new Coupon { PercentOff = 25, Id = CouponIDs.Milestone3SubscriptionDiscount };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetCouponAsync(CouponIDs.Milestone3SubscriptionDiscount).Returns(coupon);
        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019Plan);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesPlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            Arg.Is(subscriptionId),
            Arg.Is<SubscriptionUpdateOptions>(o =>
                o.Items.Count == 2 &&
                o.Items[0].Id == passwordManagerItemId &&
                o.Items[0].Price == familiesPlan.PasswordManager.StripePlanId &&
                o.Items[1].Id == seatAddOnItemId &&
                o.Items[1].Deleted == true &&
                o.Discounts.Count == 1 &&
                o.Discounts[0].Coupon == CouponIDs.Milestone3SubscriptionDiscount &&
                o.ProrationBehavior == ProrationBehavior.None));

        await _stripeAdapter.Received(1).GetCouponAsync(CouponIDs.Milestone3SubscriptionDiscount);

        await _organizationRepository.Received(1).ReplaceAsync(
            Arg.Is<Organization>(org =>
                org.Id == _organizationId &&
                org.PlanType == PlanType.FamiliesAnnually &&
                org.Plan == familiesPlan.Name &&
                org.UsersGetPremium == familiesPlan.UsersGetPremium &&
                org.Seats == familiesPlan.PasswordManager.BaseSeats));

        await _mailer.Received(1).SendEmail(
            Arg.Is<Families2019RenewalMail>(email =>
                email.ToEmails.Contains("org@example.com") &&
                email.Subject == "Your Bitwarden Families subscription is updating" &&
                email.View.BaseMonthlyRenewalPrice == (familiesPlan.PasswordManager.BasePrice / 12).ToString("C", new CultureInfo("en-US")) &&
                email.View.BaseAnnualRenewalPrice == familiesPlan.PasswordManager.BasePrice.ToString("C", new CultureInfo("en-US")) &&
                email.View.DiscountAmount == $"{coupon.PercentOff}%"
            ));
    }

    [Fact]
    public async Task HandleAsync_WhenMilestone3Enabled_AndSeatAddOnWithQuantityOne_DeletesItem()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var customerId = "cus_123";
        var subscriptionId = "sub_123";
        var passwordManagerItemId = "si_pm_123";
        var seatAddOnItemId = "si_seat_123";

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
        var familiesPlan = new FamiliesPlan();

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
                        Id = passwordManagerItemId,
                        Price = new Price { Id = families2019Plan.PasswordManager.StripePlanId }
                    },

                    new()
                    {
                        Id = seatAddOnItemId,
                        Price = new Price { Id = "personal-org-seat-annually" },
                        Quantity = 1
                    }
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

        var coupon = new Coupon { PercentOff = 25, Id = CouponIDs.Milestone3SubscriptionDiscount };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetCouponAsync(CouponIDs.Milestone3SubscriptionDiscount).Returns(coupon);
        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019Plan);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesPlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            Arg.Is(subscriptionId),
            Arg.Is<SubscriptionUpdateOptions>(o =>
                o.Items.Count == 2 &&
                o.Items[0].Id == passwordManagerItemId &&
                o.Items[0].Price == familiesPlan.PasswordManager.StripePlanId &&
                o.Items[1].Id == seatAddOnItemId &&
                o.Items[1].Deleted == true &&
                o.Discounts.Count == 1 &&
                o.Discounts[0].Coupon == CouponIDs.Milestone3SubscriptionDiscount &&
                o.ProrationBehavior == ProrationBehavior.None));

        await _stripeAdapter.Received(1).GetCouponAsync(CouponIDs.Milestone3SubscriptionDiscount);

        await _organizationRepository.Received(1).ReplaceAsync(
            Arg.Is<Organization>(org =>
                org.Id == _organizationId &&
                org.PlanType == PlanType.FamiliesAnnually &&
                org.Plan == familiesPlan.Name &&
                org.UsersGetPremium == familiesPlan.UsersGetPremium &&
                org.Seats == familiesPlan.PasswordManager.BaseSeats));

        await _mailer.Received(1).SendEmail(
            Arg.Is<Families2019RenewalMail>(email =>
                email.ToEmails.Contains("org@example.com") &&
                email.Subject == "Your Bitwarden Families subscription is updating" &&
                email.View.BaseMonthlyRenewalPrice == (familiesPlan.PasswordManager.BasePrice / 12).ToString("C", new CultureInfo("en-US")) &&
                email.View.BaseAnnualRenewalPrice == familiesPlan.PasswordManager.BasePrice.ToString("C", new CultureInfo("en-US")) &&
                email.View.DiscountAmount == $"{coupon.PercentOff}%"
            ));
    }

    [Fact]
    public async Task HandleAsync_WhenMilestone3Enabled_WithPremiumAccessAndSeatAddOn_UpdatesBothItems()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var customerId = "cus_123";
        var subscriptionId = "sub_123";
        var passwordManagerItemId = "si_pm_123";
        var premiumAccessItemId = "si_premium_123";
        var seatAddOnItemId = "si_seat_123";

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
        var familiesPlan = new FamiliesPlan();

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
                        Id = passwordManagerItemId,
                        Price = new Price { Id = families2019Plan.PasswordManager.StripePlanId }
                    },

                    new()
                    {
                        Id = premiumAccessItemId,
                        Price = new Price { Id = families2019Plan.PasswordManager.StripePremiumAccessPlanId }
                    },

                    new()
                    {
                        Id = seatAddOnItemId,
                        Price = new Price { Id = "personal-org-seat-annually" },
                        Quantity = 2
                    }
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

        var coupon = new Coupon { PercentOff = 25, Id = CouponIDs.Milestone3SubscriptionDiscount };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeAdapter.GetCouponAsync(CouponIDs.Milestone3SubscriptionDiscount).Returns(coupon);
        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019Plan);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesPlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            Arg.Is(subscriptionId),
            Arg.Is<SubscriptionUpdateOptions>(o =>
                o.Items.Count == 3 &&
                o.Items[0].Id == passwordManagerItemId &&
                o.Items[0].Price == familiesPlan.PasswordManager.StripePlanId &&
                o.Items[1].Id == premiumAccessItemId &&
                o.Items[1].Deleted == true &&
                o.Items[2].Id == seatAddOnItemId &&
                o.Items[2].Deleted == true &&
                o.Discounts.Count == 1 &&
                o.Discounts[0].Coupon == CouponIDs.Milestone3SubscriptionDiscount &&
                o.ProrationBehavior == ProrationBehavior.None));

        await _stripeAdapter.Received(1).GetCouponAsync(CouponIDs.Milestone3SubscriptionDiscount);

        await _organizationRepository.Received(1).ReplaceAsync(
            Arg.Is<Organization>(org =>
                org.Id == _organizationId &&
                org.PlanType == PlanType.FamiliesAnnually &&
                org.Plan == familiesPlan.Name &&
                org.UsersGetPremium == familiesPlan.UsersGetPremium &&
                org.Seats == familiesPlan.PasswordManager.BaseSeats));

        await _mailer.Received(1).SendEmail(
            Arg.Is<Families2019RenewalMail>(email =>
                email.ToEmails.Contains("org@example.com") &&
                email.Subject == "Your Bitwarden Families subscription is updating" &&
                email.View.BaseMonthlyRenewalPrice == (familiesPlan.PasswordManager.BasePrice / 12).ToString("C", new CultureInfo("en-US")) &&
                email.View.BaseAnnualRenewalPrice == familiesPlan.PasswordManager.BasePrice.ToString("C", new CultureInfo("en-US")) &&
                email.View.DiscountAmount == $"{coupon.PercentOff}%"
            ));
    }

    [Fact]
    public async Task HandleAsync_WhenMilestone3Enabled_AndFamilies2025Plan_UpdatesSubscriptionOnlyNoAddons()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var customerId = "cus_123";
        var subscriptionId = "sub_123";
        var passwordManagerItemId = "si_pm_123";

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

        var families2025Plan = new Families2025Plan();
        var familiesPlan = new FamiliesPlan();

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
                        Id = passwordManagerItemId,
                        Price = new Price { Id = families2025Plan.PasswordManager.StripePlanId }
                    }
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
            PlanType = PlanType.FamiliesAnnually2025
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2025).Returns(families2025Plan);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesPlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            Arg.Is(subscriptionId),
            Arg.Is<SubscriptionUpdateOptions>(o =>
                o.Items.Count == 1 &&
                o.Items[0].Id == passwordManagerItemId &&
                o.Items[0].Price == familiesPlan.PasswordManager.StripePlanId &&
                o.Discounts == null &&
                o.ProrationBehavior == ProrationBehavior.None));

        await _organizationRepository.Received(1).ReplaceAsync(
            Arg.Is<Organization>(org =>
                org.Id == _organizationId &&
                org.PlanType == PlanType.FamiliesAnnually &&
                org.Plan == familiesPlan.Name &&
                org.UsersGetPremium == familiesPlan.UsersGetPremium &&
                org.Seats == familiesPlan.PasswordManager.BaseSeats));

        await _mailer.Received(1).SendEmail(
            Arg.Is<Families2020RenewalMail>(email =>
                email.ToEmails.Contains("org@example.com") &&
                email.Subject == "Your Bitwarden Families renewal is updating" &&
                email.View.MonthlyRenewalPrice == (familiesPlan.PasswordManager.BasePrice / 12).ToString("C", new CultureInfo("en-US"))));
    }

    [Fact]
    public async Task HandleAsync_WhenMilestone2Enabled_AndCouponNotFound_LogsErrorAndSendsTraditionalEmail()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123" };
        var customerId = "cus_123";
        var invoice = new Invoice
        {
            CustomerId = customerId,
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
            CustomerId = customerId,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new() { Id = "si_123", Price = new Price { Id = Prices.PremiumAnnually } }]
            },
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = false },
            Customer = new Customer { Id = customerId },
            Metadata = new Dictionary<string, string>()
        };
        var user = new User { Id = _userId, Email = "user@example.com", Premium = true };
        var plan = new PremiumPlan
        {
            Name = "Premium",
            Available = true,
            LegacyYear = null,
            Seat = new Purchasable { Price = 10M, StripePriceId = Prices.PremiumAnnually },
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
            Tax = new CustomerTax { AutomaticTax = AutomaticTaxStatus.Supported },
            Subscriptions = new StripeList<Subscription> { Data = [subscription] }
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, _userId, null));
        _userRepository.GetByIdAsync(_userId).Returns(user);
        _pricingClient.ListPremiumPlans().Returns(new List<PremiumPlan> { oldPlan, plan });
        _stripeAdapter.GetCouponAsync(CouponIDs.Milestone2SubscriptionDiscount).Returns((Coupon)null);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(subscription);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert - Exception is caught, error is logged, and traditional email is sent
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString().Contains($"Failed to update user's ({user.Id}) subscription price id") &&
                o.ToString().Contains(parsedEvent.Id)),
            Arg.Is<Exception>(e => e is InvalidOperationException
                                   && e.Message == $"Coupon for sending premium renewal email id:{CouponIDs.Milestone2SubscriptionDiscount} not found"),
            Arg.Any<Func<object, Exception, string>>());

        await _mailer.DidNotReceive().SendEmail(Arg.Any<PremiumRenewalMail>());

        await _mailService.Received(1).SendInvoiceUpcoming(
            Arg.Is<IEnumerable<string>>(emails => emails.Contains("user@example.com")),
            Arg.Is<decimal>(amount => amount == invoice.AmountDue / 100M),
            Arg.Is<DateTime>(dueDate => dueDate == invoice.NextPaymentAttempt.Value),
            Arg.Is<List<string>>(items => items.Count == invoice.Lines.Data.Count),
            Arg.Is<bool>(b => b == true));
    }

    [Fact]
    public async Task HandleAsync_WhenMilestone2Enabled_AndCouponPercentOffIsNull_LogsErrorAndSendsTraditionalEmail()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123" };
        var customerId = "cus_123";
        var invoice = new Invoice
        {
            CustomerId = customerId,
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
            CustomerId = customerId,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new() { Id = "si_123", Price = new Price { Id = Prices.PremiumAnnually } }]
            },
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = false },
            Customer = new Customer { Id = customerId },
            Metadata = new Dictionary<string, string>()
        };
        var user = new User { Id = _userId, Email = "user@example.com", Premium = true };
        var plan = new PremiumPlan
        {
            Name = "Premium",
            Available = true,
            LegacyYear = null,
            Seat = new Purchasable { Price = 10M, StripePriceId = Prices.PremiumAnnually },
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
            Tax = new CustomerTax { AutomaticTax = AutomaticTaxStatus.Supported },
            Subscriptions = new StripeList<Subscription> { Data = [subscription] }
        };
        var coupon = new Coupon
        {
            Id = CouponIDs.Milestone2SubscriptionDiscount,
            PercentOff = null
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, _userId, null));
        _userRepository.GetByIdAsync(_userId).Returns(user);
        _pricingClient.ListPremiumPlans().Returns(new List<PremiumPlan> { oldPlan, plan });
        _stripeAdapter.GetCouponAsync(CouponIDs.Milestone2SubscriptionDiscount).Returns(coupon);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(subscription);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert - Exception is caught, error is logged, and traditional email is sent
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString().Contains($"Failed to update user's ({user.Id}) subscription price id") &&
                o.ToString().Contains(parsedEvent.Id)),
            Arg.Is<Exception>(e => e is InvalidOperationException
                                   && e.Message == $"coupon.PercentOff for sending premium renewal email id:{CouponIDs.Milestone2SubscriptionDiscount} is null"),
            Arg.Any<Func<object, Exception, string>>());

        await _mailer.DidNotReceive().SendEmail(Arg.Any<PremiumRenewalMail>());

        await _mailService.Received(1).SendInvoiceUpcoming(
            Arg.Is<IEnumerable<string>>(emails => emails.Contains("user@example.com")),
            Arg.Is<decimal>(amount => amount == invoice.AmountDue / 100M),
            Arg.Is<DateTime>(dueDate => dueDate == invoice.NextPaymentAttempt.Value),
            Arg.Is<List<string>>(items => items.Count == invoice.Lines.Data.Count),
            Arg.Is<bool>(b => b == true));
    }

    [Fact]
    public async Task HandleAsync_WhenMilestone2Enabled_AndValidCoupon_SendsPremiumRenewalEmail()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123" };
        var customerId = "cus_123";
        var invoice = new Invoice
        {
            CustomerId = customerId,
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
            CustomerId = customerId,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new() { Id = "si_123", Price = new Price { Id = Prices.PremiumAnnually } }]
            },
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = false },
            Customer = new Customer { Id = customerId },
            Metadata = new Dictionary<string, string>()
        };
        var user = new User { Id = _userId, Email = "user@example.com", Premium = true };
        var plan = new PremiumPlan
        {
            Name = "Premium",
            Available = true,
            LegacyYear = null,
            Seat = new Purchasable { Price = 10M, StripePriceId = Prices.PremiumAnnually },
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
            Tax = new CustomerTax { AutomaticTax = AutomaticTaxStatus.Supported },
            Subscriptions = new StripeList<Subscription> { Data = [subscription] }
        };
        var coupon = new Coupon
        {
            Id = CouponIDs.Milestone2SubscriptionDiscount,
            PercentOff = 30
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, _userId, null));
        _userRepository.GetByIdAsync(_userId).Returns(user);
        _pricingClient.ListPremiumPlans().Returns(new List<PremiumPlan> { oldPlan, plan });
        _stripeAdapter.GetCouponAsync(CouponIDs.Milestone2SubscriptionDiscount).Returns(coupon);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(subscription);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        var expectedDiscountedPrice = plan.Seat.Price * (100 - coupon.PercentOff.Value) / 100;
        await _mailer.Received(1).SendEmail(
            Arg.Is<PremiumRenewalMail>(email =>
                email.ToEmails.Contains("user@example.com") &&
                email.Subject == "Your Bitwarden Premium renewal is updating" &&
                email.View.BaseMonthlyRenewalPrice == (plan.Seat.Price / 12).ToString("C", new CultureInfo("en-US")) &&
                email.View.DiscountAmount == "30%" &&
                email.View.DiscountedAnnualRenewalPrice == expectedDiscountedPrice.ToString("C", new CultureInfo("en-US"))
            ));

        await _mailService.DidNotReceive().SendInvoiceUpcoming(
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<decimal>(),
            Arg.Any<DateTime>(),
            Arg.Any<List<string>>(),
            Arg.Any<bool>());
    }

    [Fact]
    public async Task HandleAsync_WhenMilestone2Enabled_AndGetCouponThrowsException_LogsErrorAndSendsTraditionalEmail()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123" };
        var customerId = "cus_123";
        var invoice = new Invoice
        {
            CustomerId = customerId,
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
            CustomerId = customerId,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new() { Id = "si_123", Price = new Price { Id = Prices.PremiumAnnually } }]
            },
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = false },
            Customer = new Customer { Id = customerId },
            Metadata = new Dictionary<string, string>()
        };
        var user = new User { Id = _userId, Email = "user@example.com", Premium = true };
        var plan = new PremiumPlan
        {
            Name = "Premium",
            Available = true,
            LegacyYear = null,
            Seat = new Purchasable { Price = 10M, StripePriceId = Prices.PremiumAnnually },
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
            Tax = new CustomerTax { AutomaticTax = AutomaticTaxStatus.Supported },
            Subscriptions = new StripeList<Subscription> { Data = [subscription] }
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, _userId, null));
        _userRepository.GetByIdAsync(_userId).Returns(user);
        _pricingClient.ListPremiumPlans().Returns(new List<PremiumPlan> { oldPlan, plan });
        _stripeAdapter.GetCouponAsync(CouponIDs.Milestone2SubscriptionDiscount)
            .ThrowsAsync(new StripeException("Stripe API error"));
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(subscription);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert - Exception is caught, error is logged, and traditional email is sent
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString().Contains($"Failed to update user's ({user.Id}) subscription price id") &&
                o.ToString().Contains(parsedEvent.Id)),
            Arg.Is<Exception>(e => e is StripeException),
            Arg.Any<Func<object, Exception, string>>());

        await _mailer.DidNotReceive().SendEmail(Arg.Any<PremiumRenewalMail>());

        await _mailService.Received(1).SendInvoiceUpcoming(
            Arg.Is<IEnumerable<string>>(emails => emails.Contains("user@example.com")),
            Arg.Is<decimal>(amount => amount == invoice.AmountDue / 100M),
            Arg.Is<DateTime>(dueDate => dueDate == invoice.NextPaymentAttempt.Value),
            Arg.Is<List<string>>(items => items.Count == invoice.Lines.Data.Count),
            Arg.Is<bool>(b => b == true));
    }

    [Fact]
    public async Task HandleAsync_Premium_DeferEnabled_CallsScheduler()
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
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, _userId, null));
        _userRepository.GetByIdAsync(_userId).Returns(user);
        _pricingClient.ListPremiumPlans().Returns(new List<PremiumPlan> { oldPlan, plan });
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);
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
    public async Task HandleAsync_Families_DeferEnabled_CallsScheduler()
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
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019Plan);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(new FamiliesPlan());
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);
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
    public async Task HandleAsync_WhenOrganizationTaxNotEnabled_FlagOn_SchedulePresent_UpdatesSchedulePhasesAndDefaultSettings()
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

        var phase1Start = DateTime.UtcNow.AddDays(-10);
        var phase1End = DateTime.UtcNow.AddDays(5);
        var phase2End = DateTime.UtcNow.AddDays(370);

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(new TeamsPlan(isAnnual: true));
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

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
    public async Task HandleAsync_WhenOrganizationTaxNotEnabled_FlagOn_SchedulePresent_CarriesCustomerDiscountIntoFuturePhaseOnly()
    {
        // C1 (worker): carry the customer discount into the FUTURE phase only (StartDate > now), not
        // the active phase 0 — whose discountConsumed predicate is false but which is still billing.
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
                Discount = new Discount { Coupon = new Coupon { Id = "retention" } }
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
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(new TeamsPlan(isAnnual: true));
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

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
                // Active phase 0: customer coupon NOT injected.
                (o.Phases[0].Discounts == null || o.Phases[0].Discounts.All(d => d.Coupon != "retention")) &&
                // Future phase 1: customer coupon carried in, stacked with the existing milestone.
                o.Phases[1].Discounts.Any(d => d.Coupon == "retention") &&
                o.Phases[1].Discounts.Any(d => d.Coupon == "milestone-coupon")));
    }

    [Fact]
    public async Task HandleAsync_WhenOrganizationTaxNotEnabled_FlagOn_NoSchedule_UpdatesSubscriptionDirectly()
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
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(new TeamsPlan(isAnnual: true));
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

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
    public async Task HandleAsync_WhenPremiumUserTaxNotEnabled_FlagOn_SchedulePresent_UpdatesSchedulePhasesAndDefaultSettings()
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

        var phase1Start = DateTime.UtcNow.AddDays(-10);
        var phase1End = DateTime.UtcNow.AddDays(5);
        var phase2End = DateTime.UtcNow.AddDays(370);

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, _userId, null));
        _userRepository.GetByIdAsync(_userId).Returns(user);
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

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
    public async Task HandleAsync_WhenPremiumUserTaxNotEnabled_FlagOn_NoSchedule_UpdatesSubscriptionDirectly()
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
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, _userId, null));
        _userRepository.GetByIdAsync(_userId).Returns(user);
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

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
    public async Task HandleAsync_WhenTaxNotEnabled_FlagOn_Phase2Active_SkipsCompletedPhaseAndClearsConsumedDiscounts()
    {
        // Arrange — Phase 1 has ended, Phase 2 is now the active phase.
        // Phase 2's one-time migration discount was consumed at transition and must not be re-included.
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

        // Phase 1 ended yesterday, Phase 2 active now
        var phase1Start = DateTime.UtcNow.AddDays(-375);
        var phase1End = DateTime.UtcNow.AddDays(-1);
        var phase2End = DateTime.UtcNow.AddDays(364);

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(invoice.CustomerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, _userId, null));
        _userRepository.GetByIdAsync(_userId).Returns(user);
        _featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal).Returns(true);

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

        // Assert — schedule updated: Phase 1 skipped, Phase 2 included with cleared discounts
        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            Arg.Is("sub_sched_789"),
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.DefaultSettings.AutomaticTax.Enabled == true &&
                o.Phases.Count == 1 &&
                o.Phases[0].AutomaticTax.Enabled == true &&
                o.Phases[0].Items[0].Price == "price_new" &&
                o.Phases[0].Discounts.Count == 0));
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
    public async Task HandleAsync_WhenBusinessTier_AndFeatureFlagOff_FallsThroughToStandardEmail()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(false);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.EnterpriseAnnually2020);
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually2020
        };
        var enterprise2020Plan = new Enterprise2020Plan(isAnnual: true);

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Plan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _assignmentRepository.DidNotReceiveWithAnyArgs().GetByOrganizationIdAsync(Arg.Any<Guid>());
        await _priceIncreaseScheduler.DidNotReceiveWithAnyArgs()
            .ScheduleBusinessPriceIncrease(default!, default!);
        await _mailService.Received(1).SendInvoiceUpcoming(
            Arg.Is<IEnumerable<string>>(emails => emails.Contains("org@example.com")),
            Arg.Any<decimal>(),
            Arg.Any<DateTime>(),
            Arg.Any<List<string>>(),
            Arg.Is<bool>(b => b));
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndNoCohortAssignment_FallsThroughToStandardEmail()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.EnterpriseAnnually);
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually
        };
        var enterprisePlan = new EnterprisePlan(isAnnual: true);

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(enterprisePlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _assignmentRepository.GetByOrganizationIdAsync(_organizationId)
            .Returns((OrganizationPlanMigrationCohortAssignment?)null);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _priceIncreaseScheduler.DidNotReceiveWithAnyArgs()
            .ScheduleBusinessPriceIncrease(default!, default!);
        await _mailService.Received(1).SendInvoiceUpcoming(
            Arg.Is<IEnumerable<string>>(emails => emails.Contains("org@example.com")),
            Arg.Is<decimal>(amount => amount == invoice.AmountDue / 100M),
            Arg.Is<DateTime>(dueDate => dueDate == invoice.NextPaymentAttempt!.Value),
            Arg.Any<List<string>>(),
            Arg.Is<bool>(b => b));
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndAssignmentAlreadyScheduled_FallsThroughToStandardEmail()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.EnterpriseAnnually2020);
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually2020
        };
        var enterprise2020Plan = new Enterprise2020Plan(isAnnual: true);
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CohortId = Guid.NewGuid(),
            ScheduledDate = DateTime.UtcNow.AddDays(-1)
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Plan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _assignmentRepository.GetByOrganizationIdAsync(_organizationId).Returns(assignment);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _priceIncreaseScheduler.DidNotReceiveWithAnyArgs()
            .ScheduleBusinessPriceIncrease(default!, default!);
        await _cohortRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(Arg.Any<Guid>());
        await _mailService.Received(1).SendInvoiceUpcoming(
            Arg.Is<IEnumerable<string>>(emails => emails.Contains("org@example.com")),
            Arg.Any<decimal>(),
            Arg.Any<DateTime>(),
            Arg.Any<List<string>>(),
            Arg.Is<bool>(b => b));
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndCohortInactive_FallsThroughToStandardEmail()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.EnterpriseAnnually2020);
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually2020
        };
        var enterprise2020Plan = new Enterprise2020Plan(isAnnual: true);
        var cohortId = Guid.NewGuid();
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CohortId = cohortId,
            ScheduledDate = null
        };
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "enterprise-2020-annual-paused",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            IsActive = false
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Plan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _assignmentRepository.GetByOrganizationIdAsync(_organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(cohort);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _priceIncreaseScheduler.Received(1)
            .ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>());
        await _mailService.Received(1).SendInvoiceUpcoming(
            Arg.Is<IEnumerable<string>>(emails => emails.Contains("org@example.com")),
            Arg.Is<decimal>(amount => amount == invoice.AmountDue / 100M),
            Arg.Is<DateTime>(dueDate => dueDate == invoice.NextPaymentAttempt!.Value),
            Arg.Any<List<string>>(),
            Arg.Is<bool>(b => b));
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndCohortHasNoMigrationPath_FallsThroughSilently()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.EnterpriseAnnually2020);
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually2020
        };
        var enterprise2020Plan = new Enterprise2020Plan(isAnnual: true);
        var cohortId = Guid.NewGuid();
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CohortId = cohortId,
            ScheduledDate = null
        };
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "churn-only-cohort",
            MigrationPathId = null,
            IsActive = true
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Plan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _assignmentRepository.GetByOrganizationIdAsync(_organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(cohort);
        _priceIncreaseScheduler.ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>())
            .Returns(true);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _priceIncreaseScheduler.Received(1)
            .ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>());
        // The migration was scheduled, so the standard upcoming-invoice email must be suppressed even though
        // the cohort has no migration path and therefore no renewal email is sent.
        await _mailer.DidNotReceive().SendEmail(Arg.Any<BusinessPlanRenewal2020MigrationMail>());
        await _mailService.DidNotReceive().SendInvoiceUpcoming(
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<decimal>(),
            Arg.Any<DateTime>(),
            Arg.Any<List<string>>(),
            Arg.Any<bool>());
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndRenewalEmailSendFailsAfterScheduling_SuppressesStandardEmail_AndLogsError()
    {
        // Arrange — the migration is scheduled successfully, but sending the renewal email throws. Because the
        // migration is already committed at Stripe, we must NOT fall through to the standard upcoming-invoice
        // email (which would quote the pre-migration price); the failure is logged instead.
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.EnterpriseAnnually2020);
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually2020
        };
        var enterprise2020Plan = new Enterprise2020Plan(isAnnual: true);
        var enterprisePlan = new EnterprisePlan(isAnnual: true);
        var cohortId = Guid.NewGuid();
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CohortId = cohortId,
            ScheduledDate = null
        };
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "enterprise-2020-annual",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            IsActive = true
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Plan);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(enterprisePlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _assignmentRepository.GetByOrganizationIdAsync(_organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(cohort);
        _priceIncreaseScheduler.ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>())
            .Returns(true);
        _mailer.SendEmail(Arg.Any<BusinessPlanRenewal2020MigrationMail>())
            .ThrowsAsync(new Exception("Delivery service unavailable"));

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — the send was attempted, the standard email was suppressed, and the failure was logged.
        await _mailer.Received(1).SendEmail(Arg.Any<BusinessPlanRenewal2020MigrationMail>());
        await _mailService.DidNotReceive().SendInvoiceUpcoming(
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<decimal>(),
            Arg.Any<DateTime>(),
            Arg.Any<List<string>>(),
            Arg.Any<bool>());
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString()!.Contains("renewal notification email failed") &&
                o.ToString()!.Contains(_organizationId.ToString())),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndUnknownMigrationPathId_LogsErrorAndFallsThrough()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.EnterpriseAnnually2020);
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually2020
        };
        var enterprise2020Plan = new Enterprise2020Plan(isAnnual: true);
        var cohortId = Guid.NewGuid();
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CohortId = cohortId,
            ScheduledDate = null
        };
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "data-corruption-cohort",
            MigrationPathId = (MigrationPathId)99,
            IsActive = true
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Plan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _assignmentRepository.GetByOrganizationIdAsync(_organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(cohort);
        _priceIncreaseScheduler.ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>())
            .Returns(true);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _priceIncreaseScheduler.Received(1)
            .ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>());
        // The migration was scheduled, so the standard upcoming-invoice email must be suppressed even though
        // the unknown migration path id means no renewal email is sent.
        await _mailer.DidNotReceive().SendEmail(Arg.Any<BusinessPlanRenewal2020MigrationMail>());
        await _mailService.DidNotReceive().SendInvoiceUpcoming(
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<decimal>(),
            Arg.Any<DateTime>(),
            Arg.Any<List<string>>(),
            Arg.Any<bool>());
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndOrgPlanDriftedFromCohortSource_LogsAndFallsThrough()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.EnterpriseAnnually);
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually
        };
        var enterprisePlan = new EnterprisePlan(isAnnual: true);
        var cohortId = Guid.NewGuid();
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CohortId = cohortId,
            ScheduledDate = null
        };
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "enterprise-2020-annual",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            IsActive = true
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(enterprisePlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _assignmentRepository.GetByOrganizationIdAsync(_organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(cohort);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _priceIncreaseScheduler.Received(1)
            .ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>());
        await _mailService.Received(1).SendInvoiceUpcoming(
            Arg.Is<IEnumerable<string>>(emails => emails.Contains("org@example.com")),
            Arg.Is<decimal>(amount => amount == invoice.AmountDue / 100M),
            Arg.Is<DateTime>(dueDate => dueDate == invoice.NextPaymentAttempt!.Value),
            Arg.Any<List<string>>(),
            Arg.Is<bool>(b => b));
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndSchedulerReturnsTrue_SendsBusinessRenewalEmail_WithDiscount_AndSuppressesStandardEmail()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.EnterpriseAnnually2020);
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually2020
        };
        var enterprise2020Plan = new Enterprise2020Plan(isAnnual: true);
        var enterprisePlan = new EnterprisePlan(isAnnual: true);
        var cohortId = Guid.NewGuid();
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CohortId = cohortId,
            ScheduledDate = null
        };
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "enterprise-2020-annual",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            ProactiveDiscountCouponCode = "loyalty-20",
            IsActive = true
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Plan);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(enterprisePlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _assignmentRepository.GetByOrganizationIdAsync(_organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(cohort);
        _priceIncreaseScheduler.ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>())
            .Returns(true);
        _stripeAdapter.GetCouponAsync("loyalty-20", Arg.Any<CouponGetOptions>())
            .Returns(new Coupon { PercentOff = 20 });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _priceIncreaseScheduler.Received(1)
            .ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>());
        // EnterprisePlan annual SeatPrice is $72.00; BasePrice is $0.00. Asserting the per-user
        // monthly renders as SeatPrice/12 (not $0.00) guards against the BasePrice copy-paste bug.
        // Annual cohorts are quoted a per-year total: 320 seats x $72 = $23,040 gross; less 20% = $18,432.
        // Whole-dollar amounts render without the trailing .00.
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.ToEmails.Contains("org@example.com") &&
            mail.View.HasDiscount &&
            mail.View.IsAnnual &&
            mail.View.Seats == 320 &&
            mail.View.RenewalDate == "June 12, 2026" &&
            mail.View.PerUserMonthlyPrice == "$6" &&
            mail.View.DiscountLines.Count == 1 &&
            mail.View.DiscountLines[0] == "20%" &&
            mail.View.TotalPrice == "$18,432"));
        await _mailService.DidNotReceive().SendInvoiceUpcoming(
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<decimal>(),
            Arg.Any<DateTime>(),
            Arg.Any<List<string>>(),
            Arg.Any<bool>());
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndDiscountedTotalHasCents_RendersTotalWithTwoDecimals()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.EnterpriseAnnually2020);
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually2020
        };
        var enterprise2020Plan = new Enterprise2020Plan(isAnnual: true);
        var enterprisePlan = new EnterprisePlan(isAnnual: true);
        var cohortId = Guid.NewGuid();
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CohortId = cohortId,
            ScheduledDate = null
        };
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "enterprise-2020-annual",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            ProactiveDiscountCouponCode = "loyalty-33",
            IsActive = true
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Plan);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(enterprisePlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _assignmentRepository.GetByOrganizationIdAsync(_organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(cohort);
        _priceIncreaseScheduler.ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>())
            .Returns(true);
        _stripeAdapter.GetCouponAsync("loyalty-33", Arg.Any<CouponGetOptions>())
            .Returns(new Coupon { PercentOff = 33 });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — annual per-year total: 320 seats x $72 = $23,040 gross; less 33% = $15,436.80.
        // The fractional total keeps two decimals, while the whole-dollar per-user monthly ($6) drops them.
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.View.HasDiscount &&
            mail.View.IsAnnual &&
            mail.View.Seats == 320 &&
            mail.View.PerUserMonthlyPrice == "$6" &&
            mail.View.DiscountLines.Count == 1 &&
            mail.View.DiscountLines[0] == "33%" &&
            mail.View.TotalPrice == "$15,436.80"));
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndCouponIsAmountOff_SubtractsFixedAmountFromTotal()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.EnterpriseAnnually2020);
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually2020
        };
        var enterprise2020Plan = new Enterprise2020Plan(isAnnual: true);
        var enterprisePlan = new EnterprisePlan(isAnnual: true);
        var cohortId = Guid.NewGuid();
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CohortId = cohortId,
            ScheduledDate = null
        };
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "enterprise-2020-annual",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            ProactiveDiscountCouponCode = "fifty-off",
            IsActive = true
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Plan);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(enterprisePlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _assignmentRepository.GetByOrganizationIdAsync(_organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(cohort);
        _priceIncreaseScheduler.ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>())
            .Returns(true);
        // Stripe reports amount-off coupons in minor units (cents): $50.00 off = 5000.
        _stripeAdapter.GetCouponAsync("fifty-off", Arg.Any<CouponGetOptions>())
            .Returns(new Coupon { AmountOff = 5000 });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — annual per-year total: 320 seats x $72 = $23,040 gross; less the $50 fixed amount = $22,990.
        // The discount line shows the formatted dollar amount (whole-dollar, so .00 is trimmed), not a percentage.
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.View.HasDiscount &&
            mail.View.IsAnnual &&
            mail.View.Seats == 320 &&
            mail.View.PerUserMonthlyPrice == "$6" &&
            mail.View.DiscountLines.Count == 1 &&
            mail.View.DiscountLines[0] == "$50" &&
            mail.View.TotalPrice == "$22,990"));
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndCohortHasNoCoupon_SendsPriceOnlyRenewalEmail()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.EnterpriseAnnually2020);
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually2020
        };
        var enterprise2020Plan = new Enterprise2020Plan(isAnnual: true);
        var enterprisePlan = new EnterprisePlan(isAnnual: true);
        var cohortId = Guid.NewGuid();
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CohortId = cohortId,
            ScheduledDate = null
        };
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "enterprise-2020-annual",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            ProactiveDiscountCouponCode = null,
            IsActive = true
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Plan);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(enterprisePlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _assignmentRepository.GetByOrganizationIdAsync(_organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(cohort);
        _priceIncreaseScheduler.ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>())
            .Returns(true);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — full price, no discount section; annual per-year total: 320 seats x $72 = $23,040.
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.ToEmails.Contains("org@example.com") &&
            !mail.View.HasDiscount &&
            !mail.View.ShowProactiveDiscountCopy &&
            mail.View.IsAnnual &&
            mail.View.Seats == 320 &&
            mail.View.PerUserMonthlyPrice == "$6" &&
            mail.View.TotalPrice == "$23,040"));
        await _stripeAdapter.DidNotReceive().GetCouponAsync(Arg.Any<string>(), Arg.Any<CouponGetOptions>());
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndCouponUnresolvable_SendsPriceOnlyRenewalEmail()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.EnterpriseAnnually2020);
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually2020
        };
        var enterprise2020Plan = new Enterprise2020Plan(isAnnual: true);
        var enterprisePlan = new EnterprisePlan(isAnnual: true);
        var cohortId = Guid.NewGuid();
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CohortId = cohortId,
            ScheduledDate = null
        };
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "enterprise-2020-annual",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            ProactiveDiscountCouponCode = "missing-coupon",
            IsActive = true
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Plan);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(enterprisePlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _assignmentRepository.GetByOrganizationIdAsync(_organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(cohort);
        _priceIncreaseScheduler.ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>())
            .Returns(true);
        _stripeAdapter.GetCouponAsync("missing-coupon", Arg.Any<CouponGetOptions>())
            .ThrowsAsync(new StripeException("No such coupon"));

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — the email is still sent, price-only, and the StripeException does not propagate.
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.ToEmails.Contains("org@example.com") &&
            !mail.View.HasDiscount &&
            !mail.View.ShowProactiveDiscountCopy));
    }

    [Theory]
    [InlineData("repeating", 12L, 12, true)]
    [InlineData("once", null, 0, false)]
    [InlineData("forever", null, 0, false)]
    public async Task HandleAsync_WhenBusinessTier_SetsProactiveDiscountMonths_FromCouponDuration(
        string duration, long? durationInMonths, int expectedMonths, bool expectedShow)
    {
        // Arrange — a proactive coupon whose Stripe duration drives the loyalty-discount copy. Only a
        // "repeating" coupon has a finite month span; "once"/"forever" suppress the copy.
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.EnterpriseAnnually2020);
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually2020
        };
        var enterprise2020Plan = new Enterprise2020Plan(isAnnual: true);
        var enterprisePlan = new EnterprisePlan(isAnnual: true);
        var cohortId = Guid.NewGuid();
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CohortId = cohortId,
            ScheduledDate = null
        };
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "enterprise-2020-annual",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            ProactiveDiscountCouponCode = "loyalty",
            IsActive = true
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Plan);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(enterprisePlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _assignmentRepository.GetByOrganizationIdAsync(_organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(cohort);
        _priceIncreaseScheduler.ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>())
            .Returns(true);
        _stripeAdapter.GetCouponAsync("loyalty", Arg.Any<CouponGetOptions>())
            .Returns(new Coupon { PercentOff = 20, Duration = duration, DurationInMonths = durationInMonths });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.View.ProactiveDiscountMonths == expectedMonths &&
            mail.View.ShowProactiveDiscountCopy == expectedShow));
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndCouponHasNeitherPercentNorAmount_SendsPriceOnlyRenewalEmail_AndLogsError()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.EnterpriseAnnually2020);
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually2020
        };
        var enterprise2020Plan = new Enterprise2020Plan(isAnnual: true);
        var enterprisePlan = new EnterprisePlan(isAnnual: true);
        var cohortId = Guid.NewGuid();
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CohortId = cohortId,
            ScheduledDate = null
        };
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "enterprise-2020-annual",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            ProactiveDiscountCouponCode = "empty-coupon",
            IsActive = true
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Plan);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(enterprisePlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _assignmentRepository.GetByOrganizationIdAsync(_organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(cohort);
        _priceIncreaseScheduler.ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>())
            .Returns(true);
        // A coupon that resolves but exposes neither PercentOff nor AmountOff is a cohort misconfiguration.
        _stripeAdapter.GetCouponAsync("empty-coupon", Arg.Any<CouponGetOptions>())
            .Returns(new Coupon { PercentOff = null, AmountOff = null });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — email is sent price-only, and the misconfiguration is logged as an error (a coupon with no
        // usable discount mis-quotes every org in the cohort, so it must reach alerting).
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.ToEmails.Contains("org@example.com") &&
            !mail.View.HasDiscount));
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString()!.Contains("neither PercentOff nor AmountOff") &&
                o.ToString()!.Contains("empty-coupon") &&
                o.ToString()!.Contains(_organizationId.ToString())),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndMonthlyTargetPlan_QuotesMonthlySeatPriceWithoutDividing()
    {
        // Arrange — the monthly migration path. SeatPrice on a monthly plan is already the per-user
        // monthly figure, so it must NOT be divided by 12, and the annual total is SeatPrice x 12 x
        // seats. This is the regression guard for the cadence bug the review flagged.
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.EnterpriseMonthly2020);
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseMonthly2020
        };
        var enterprise2020MonthlyPlan = new Enterprise2020Plan(isAnnual: false);
        var enterpriseMonthlyPlan = new EnterprisePlan(isAnnual: false);
        var cohortId = Guid.NewGuid();
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CohortId = cohortId,
            ScheduledDate = null
        };
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "enterprise-2020-monthly",
            MigrationPathId = MigrationPathId.Enterprise2020MonthlyToCurrent,
            ProactiveDiscountCouponCode = null,
            IsActive = true
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly2020).Returns(enterprise2020MonthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly).Returns(enterpriseMonthlyPlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _assignmentRepository.GetByOrganizationIdAsync(_organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(cohort);
        _priceIncreaseScheduler.ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>())
            .Returns(true);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — monthly EnterprisePlan SeatPrice is $7.00 (used as-is, NOT /12). Monthly cohorts are quoted a
        // per-month total, so IsAnnual is false and TotalPrice = $7 x 320 = $2,240 (NOT annualized).
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.View.Seats == 320 &&
            !mail.View.IsAnnual &&
            mail.View.PerUserMonthlyPrice == "$7" &&
            mail.View.TotalPrice == "$2,240"));
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndTeamsAnnualMigrationPath_QuotesTeamsTargetPlanPricing()
    {
        // Arrange — the Teams annual migration path. Guards the Teams source/target plan resolution, whose
        // SeatPrice ($48 annual) differs from Enterprise ($72), so a swapped From/To mapping would surface here.
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.TeamsAnnually2020);
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.TeamsAnnually2020
        };
        var teams2020Plan = new Teams2020Plan(isAnnual: true);
        var teamsPlan = new TeamsPlan(isAnnual: true);
        var cohortId = Guid.NewGuid();
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CohortId = cohortId,
            ScheduledDate = null
        };
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "teams-2020-annual",
            MigrationPathId = MigrationPathId.Teams2020AnnualToCurrent,
            ProactiveDiscountCouponCode = null,
            IsActive = true
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually2020).Returns(teams2020Plan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(teamsPlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _assignmentRepository.GetByOrganizationIdAsync(_organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(cohort);
        _priceIncreaseScheduler.ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>())
            .Returns(true);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — annual TeamsPlan SeatPrice is $48.00; per-user monthly is $48/12 = $4;
        // annual per-year total = $48 x 320 = $15,360. No coupon, so no discount section.
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.ToEmails.Contains("org@example.com") &&
            !mail.View.HasDiscount &&
            mail.View.IsAnnual &&
            mail.View.Seats == 320 &&
            mail.View.PerUserMonthlyPrice == "$4" &&
            mail.View.TotalPrice == "$15,360"));
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndTeamsMonthlyMigrationPath_QuotesMonthlySeatPriceWithoutDividing()
    {
        // Arrange — the Teams monthly migration path. Combines the monthly cadence (SeatPrice used as-is,
        // not divided by 12) with the Teams tier, completing the four-path cadence x tier matrix.
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.TeamsMonthly2020);
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.TeamsMonthly2020
        };
        var teams2020MonthlyPlan = new Teams2020Plan(isAnnual: false);
        var teamsMonthlyPlan = new TeamsPlan(isAnnual: false);
        var cohortId = Guid.NewGuid();
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CohortId = cohortId,
            ScheduledDate = null
        };
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "teams-2020-monthly",
            MigrationPathId = MigrationPathId.Teams2020MonthlyToCurrent,
            ProactiveDiscountCouponCode = null,
            IsActive = true
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(teams2020MonthlyPlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(teamsMonthlyPlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _assignmentRepository.GetByOrganizationIdAsync(_organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(cohort);
        _priceIncreaseScheduler.ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>())
            .Returns(true);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — monthly TeamsPlan SeatPrice is $5.00 (used as-is, NOT /12). Monthly cohorts are quoted a
        // per-month total, so IsAnnual is false and TotalPrice = $5 x 320 = $1,600 (NOT annualized).
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.View.Seats == 320 &&
            !mail.View.IsAnnual &&
            mail.View.PerUserMonthlyPrice == "$5" &&
            mail.View.TotalPrice == "$1,600"));
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndSecretsManagerItemsPrecedePasswordManagerSeat_QuotesPasswordManagerSeatCount()
    {
        // Arrange — the subscription carries Secrets Manager seats and service accounts ahead of the
        // password-manager seat line, with different quantities. Stripe does not guarantee item
        // ordering, so the email must resolve seats by matching the source plan's seat price ID, not
        // by taking the first positive-quantity item. This is the regression guard for the seat-count
        // bug the review flagged.
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.EnterpriseAnnually2020);
        // Prepend non-seat lines with quantities a naive "first positive quantity" lookup would grab.
        subscription.Items.Data =
        [
            new SubscriptionItem
            {
                Price = new Price { Id = "secrets-manager-enterprise-seat-annually" },
                Quantity = 50,
                CurrentPeriodEnd = new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc)
            },
            new SubscriptionItem
            {
                Price = new Price { Id = "secrets-manager-service-account-annually" },
                Quantity = 75,
                CurrentPeriodEnd = new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc)
            },
            new SubscriptionItem
            {
                Price = new Price { Id = "2020-enterprise-org-seat-annually" },
                Quantity = 320,
                CurrentPeriodEnd = new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc)
            }
        ];
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually2020
        };
        var enterprise2020Plan = new Enterprise2020Plan(isAnnual: true);
        var enterprisePlan = new EnterprisePlan(isAnnual: true);
        var cohortId = Guid.NewGuid();
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CohortId = cohortId,
            ScheduledDate = null
        };
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "enterprise-2020-annual",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            ProactiveDiscountCouponCode = null,
            IsActive = true
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Plan);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(enterprisePlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _assignmentRepository.GetByOrganizationIdAsync(_organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(cohort);
        _priceIncreaseScheduler.ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>())
            .Returns(true);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — the password-manager seat quantity (320) is quoted, NOT the SM-seat (50) or
        // service-account (75) quantity. Annual EnterprisePlan SeatPrice is $72: 320 x $72 = $23,040.
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.View.Seats == 320 &&
            mail.View.IsAnnual &&
            mail.View.PerUserMonthlyPrice == "$6" &&
            mail.View.TotalPrice == "$23,040"));
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndNoRenewalDate_SkipsRenewalEmail_AndLogsError()
    {
        // Arrange — subscription has no items, so the current period (and renewal date) can't be
        // determined; we must skip the email (no blank-date send) and log an error (the migration is
        // already committed, so an indeterminate renewal date needs alerting, not just a warning).
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.EnterpriseAnnually2020);
        subscription.Items.Data = [];
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually2020
        };
        var enterprise2020Plan = new Enterprise2020Plan(isAnnual: true);
        var enterprisePlan = new EnterprisePlan(isAnnual: true);
        var cohortId = Guid.NewGuid();
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CohortId = cohortId,
            ScheduledDate = null
        };
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "enterprise-2020-annual",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            ProactiveDiscountCouponCode = null,
            IsActive = true
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Plan);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(enterprisePlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _assignmentRepository.GetByOrganizationIdAsync(_organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(cohort);
        _priceIncreaseScheduler.ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>())
            .Returns(true);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — no email sent, and because the migration is already committed the indeterminate renewal
        // date is logged at Error level (same alerting severity as a post-schedule send failure).
        await _mailer.DidNotReceive().SendEmail(Arg.Any<BusinessPlanRenewal2020MigrationMail>());
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString()!.Contains("renewal date") &&
                o.ToString()!.Contains("indeterminate") &&
                o.ToString()!.Contains(_organizationId.ToString())),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndSchedulerReturnsFalse_FallsThroughToStandardEmail()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.EnterpriseAnnually2020);
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually2020
        };
        var enterprise2020Plan = new Enterprise2020Plan(isAnnual: true);
        var cohortId = Guid.NewGuid();
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CohortId = cohortId,
            ScheduledDate = null
        };
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "enterprise-2020-annual",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            IsActive = true
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Plan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _assignmentRepository.GetByOrganizationIdAsync(_organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(cohort);

        // Act — scheduler returns false (NSubstitute default for Task<bool>)
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _mailService.Received(1).SendInvoiceUpcoming(
            Arg.Is<IEnumerable<string>>(emails => emails.Contains("org@example.com")),
            Arg.Is<decimal>(amount => amount == invoice.AmountDue / 100M),
            Arg.Is<DateTime>(dueDate => dueDate == invoice.NextPaymentAttempt!.Value),
            Arg.Any<List<string>>(),
            Arg.Is<bool>(b => b));
        _logger.DidNotReceive().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Business renewal email is not yet wired up")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
        await _pricingClient.DidNotReceive().GetPlanOrThrow(PlanType.EnterpriseAnnually);
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndSchedulerThrows_LogsErrorAndFallsThroughToStandardEmail()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.EnterpriseAnnually2020);
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually2020
        };
        var enterprise2020Plan = new Enterprise2020Plan(isAnnual: true);
        var cohortId = Guid.NewGuid();
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CohortId = cohortId,
            ScheduledDate = null
        };
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "enterprise-2020-annual",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            IsActive = true
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(enterprise2020Plan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _assignmentRepository.GetByOrganizationIdAsync(_organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(cohort);
        _priceIncreaseScheduler.ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>())
            .ThrowsAsync(new Exception("boom"));

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString()!.Contains("Failed to schedule business price migration for Organization") &&
                o.ToString()!.Contains(_organizationId.ToString()) &&
                o.ToString()!.Contains(parsedEvent.Id)),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
        await _mailService.Received(1).SendInvoiceUpcoming(
            Arg.Is<IEnumerable<string>>(emails => emails.Contains("org@example.com")),
            Arg.Any<decimal>(),
            Arg.Any<DateTime>(),
            Arg.Any<List<string>>(),
            Arg.Is<bool>(b => b));
    }

    [Fact]
    public async Task HandleAsync_WhenFreeTier_DispatcherReturnsFalse_AndStandardEmailSent()
    {
        // Arrange
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.Free);
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.Free
        };
        var freePlan = new FreePlan();

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.Free).Returns(freePlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — dispatcher never enters the business branch.
        await _assignmentRepository.DidNotReceiveWithAnyArgs()
            .GetByOrganizationIdAsync(Arg.Any<Guid>());
        // FreePlan.IsAnnual == false, so the upcoming-invoice email path short-circuits — verifying
        // existing behavior is preserved.
        await _mailService.DidNotReceive().SendInvoiceUpcoming(
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<decimal>(),
            Arg.Any<DateTime>(),
            Arg.Any<List<string>>(),
            Arg.Any<bool>());
    }

    // PM-37512: a Teams Starter org now routes to the business-migration path (previously it fell to the
    // default arm and got the standard email); the standard email is suppressed once migration is scheduled.
    [Fact]
    public async Task HandleAsync_TeamsStarterOrg_RoutesToBusinessMigration_SchedulesAndSuppressesStandardEmail()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.TeamsStarter2023);
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.TeamsStarter2023,
            Seats = 10
        };
        var sourcePlan = new TeamsStarterPlan2023();
        var targetPlan = new TeamsPlan(isAnnual: false);
        var cohortId = Guid.NewGuid();
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CohortId = cohortId,
            ScheduledDate = null
        };
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "teams-starter-2023",
            MigrationPathId = MigrationPathId.TeamsStarter2023ToCurrent,
            IsActive = true
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsStarter2023).Returns(sourcePlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(targetPlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _assignmentRepository.GetByOrganizationIdAsync(_organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(cohort);
        _organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(_organizationId)
            .Returns(new OrganizationSeatCounts { Users = 6 });
        _priceIncreaseScheduler.ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>())
            .Returns(true);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — the business path took ownership and the standard upcoming-invoice email is suppressed.
        await _priceIncreaseScheduler.Received(1)
            .ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>());
        await _mailService.DidNotReceive().SendInvoiceUpcoming(
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<decimal>(),
            Arg.Any<DateTime>(),
            Arg.Any<List<string>>(),
            Arg.Any<bool>());
    }

    // PM-37512: the renewal email must quote the org's occupied seat count, not organization.Seats (the
    // bundle cap of 10), and the per-user monthly reflects TeamsMonthly's $5 seat price.
    [Fact]
    public async Task SendBusinessRenewalEmail_TeamsStarter_QuotesOccupiedSeatCount()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.TeamsStarter2023);
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.TeamsStarter2023,
            Seats = 10
        };
        var sourcePlan = new TeamsStarterPlan2023();
        var targetPlan = new TeamsPlan(isAnnual: false);
        var cohortId = Guid.NewGuid();
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CohortId = cohortId,
            ScheduledDate = null
        };
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "teams-starter-2023",
            MigrationPathId = MigrationPathId.TeamsStarter2023ToCurrent,
            IsActive = true
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsStarter2023).Returns(sourcePlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(targetPlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _assignmentRepository.GetByOrganizationIdAsync(_organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(cohort);
        _organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(_organizationId)
            .Returns(new OrganizationSeatCounts { Users = 7 });
        _priceIncreaseScheduler.ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>())
            .Returns(true);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — the email quotes 7 occupied seats (not the bundle cap of 10) at the $5 monthly seat price.
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.ToEmails.Contains("org@example.com") &&
            !mail.View.IsAnnual &&
            mail.View.Seats == 7 &&
            mail.View.PerUserMonthlyPrice == "$5"));
    }

    // PM-39816: when the org holds more Secrets Manager seats than occupied members, the renewal email must
    // quote the raised Password Manager seat count so it matches what the scheduler bills (current Teams
    // requires SM <= PM). 9 SM seats / 7 occupied members => the email quotes 9, flooring on the Stripe SM line.
    [Fact]
    public async Task SendBusinessRenewalEmail_TeamsStarter_RaisesQuotedSeatsToCoverSecretsManager()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.TeamsStarter2023);
        var sourcePlan = new TeamsStarterPlan2023();
        var targetPlan = new TeamsPlan(isAnnual: false);

        // The surviving Stripe SM seat line (9) is what the scheduler floors Password Manager on.
        subscription.Items.Data.Add(new SubscriptionItem
        {
            Price = new Price { Id = sourcePlan.SecretsManager.StripeSeatPlanId },
            Quantity = 9
        });

        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.TeamsStarter2023,
            Seats = 10,
            SmSeats = 9
        };
        var cohortId = Guid.NewGuid();
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CohortId = cohortId,
            ScheduledDate = null
        };
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "teams-starter-2023",
            MigrationPathId = MigrationPathId.TeamsStarter2023ToCurrent,
            IsActive = true
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsStarter2023).Returns(sourcePlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(targetPlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _assignmentRepository.GetByOrganizationIdAsync(_organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(cohort);
        _organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(_organizationId)
            .Returns(new OrganizationSeatCounts { Users = 7 });
        _priceIncreaseScheduler.ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>())
            .Returns(true);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — the email quotes 9 seats (raised from 7 occupied to cover SM), matching the invoice.
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.ToEmails.Contains("org@example.com") &&
            !mail.View.IsAnnual &&
            mail.View.Seats == 9));
    }

    // PM-39816: the renewal email also raises the quoted seat count for the Teams 2019 packaged source,
    // matching the scheduler's floor on the Stripe SM seat line. 9 SM seats / 7 occupied -> quote 9.
    [Fact]
    public async Task SendBusinessRenewalEmail_Teams2019_RaisesQuotedSeatsToCoverSecretsManager()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.TeamsMonthly2019);
        var sourcePlan = new Teams2019Plan(isAnnual: false);
        var targetPlan = new TeamsPlan(isAnnual: false);

        // The surviving Stripe SM seat line (9) is what the scheduler floors Password Manager on.
        subscription.Items.Data.Add(new SubscriptionItem
        {
            Price = new Price { Id = sourcePlan.SecretsManager.StripeSeatPlanId },
            Quantity = 9
        });

        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.TeamsMonthly2019,
            Seats = 5,
            SmSeats = 9
        };
        var cohortId = Guid.NewGuid();
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CohortId = cohortId,
            ScheduledDate = null
        };
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "teams-2019-monthly",
            MigrationPathId = MigrationPathId.Teams2019MonthlyToCurrent,
            IsActive = true
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2019).Returns(sourcePlan);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(targetPlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _assignmentRepository.GetByOrganizationIdAsync(_organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(cohort);
        _organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(_organizationId)
            .Returns(new OrganizationSeatCounts { Users = 7 });
        _priceIncreaseScheduler.ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>())
            .Returns(true);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — the email quotes 9 seats (raised from 7 occupied to cover SM), matching the invoice.
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.ToEmails.Contains("org@example.com") &&
            mail.View.Seats == 9));
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndCohortCouponOnPhase_PlusSubscriptionCoupon_ItemizesAndTotalsBoth()
    {
        // Arrange — the ticket repro: a 20% cohort coupon carried on the post-renewal schedule phase plus a
        // 5% subscription-level coupon. Both must be itemized and reflected in the total, matching Stripe's
        // upcoming invoice. This is the regression test for PM-38729.
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var now = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var (invoice, subscription, customer) = BuildBusinessFixture(
            PlanType.EnterpriseAnnually2020,
            subscriptionDiscounts:
            [
                new Discount { Coupon = new Coupon { Id = "churn-5", PercentOff = 5 } }
            ],
            frozenTime: now);
        var (organization, enterprise2020Plan, enterprisePlan, assignment, cohort, cohortId) =
            BuildBusinessMigrationContext(coupon: "cohort-20");

        StubBusinessMigration(parsedEvent, invoice, subscription, customer, organization, enterprise2020Plan,
            enterprisePlan, assignment, cohort, cohortId);
        StubActiveScheduleWithPhases(subscription, now, futurePhaseCouponId: "cohort-20");
        _stripeAdapter.GetCouponAsync("cohort-20", Arg.Any<CouponGetOptions>())
            .Returns(new Coupon { Id = "cohort-20", PercentOff = 20 });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — 320 seats x $72 = $23,040 gross; discounts compound like Stripe ($23,040 x 0.80 x 0.95 =
        // $17,510.40), not summed to a flat 25%. Cohort line first.
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.View.HasDiscount &&
            mail.View.DiscountLines.Count == 2 &&
            mail.View.DiscountLines[0] == "20%" &&
            mail.View.DiscountLines[1] == "5%" &&
            mail.View.TotalPrice == "$17,510.40"));
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndSameCouponInMultipleSources_ResolvesOnceWithoutDoubleSubtracting()
    {
        // Arrange — the same coupon id appears as the cohort coupon, a subscription discount, and the phase
        // discount. Dedup must resolve it once, so the total reflects a single 20% reduction.
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var now = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var (invoice, subscription, customer) = BuildBusinessFixture(
            PlanType.EnterpriseAnnually2020,
            subscriptionDiscounts:
            [
                new Discount { Coupon = new Coupon { Id = "loyalty-20", PercentOff = 20 } }
            ],
            frozenTime: now);
        var (organization, enterprise2020Plan, enterprisePlan, assignment, cohort, cohortId) =
            BuildBusinessMigrationContext(coupon: "loyalty-20");

        StubBusinessMigration(parsedEvent, invoice, subscription, customer, organization, enterprise2020Plan,
            enterprisePlan, assignment, cohort, cohortId);
        StubActiveScheduleWithPhases(subscription, now, futurePhaseCouponId: "loyalty-20");
        _stripeAdapter.GetCouponAsync("loyalty-20", Arg.Any<CouponGetOptions>())
            .Returns(new Coupon { Id = "loyalty-20", PercentOff = 20 });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — one line, single 20% reduction: $23,040 x 0.8 = $18,432 (not double-subtracted). The coupon is
        // fetched exactly once: the phase loop's seen-id short-circuit must skip the already-resolved coupon
        // before re-fetching it, not just dedup on add.
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.View.HasDiscount &&
            mail.View.DiscountLines.Count == 1 &&
            mail.View.DiscountLines[0] == "20%" &&
            mail.View.TotalPrice == "$18,432"));
        await _stripeAdapter.Received(1).GetCouponAsync("loyalty-20", Arg.Any<CouponGetOptions>());
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndNoCohortCoupon_ButSubscriptionCoupon_ItemizesSubscriptionDiscount()
    {
        // Arrange — no cohort coupon, but the subscription carries a 10% coupon. Today this shows nothing;
        // the fix itemizes the subscription discount.
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(
            PlanType.EnterpriseAnnually2020,
            subscriptionDiscounts:
            [
                new Discount { Coupon = new Coupon { Id = "sub-10", PercentOff = 10 } }
            ]);
        var (organization, enterprise2020Plan, enterprisePlan, assignment, cohort, cohortId) =
            BuildBusinessMigrationContext(coupon: null);

        StubBusinessMigration(parsedEvent, invoice, subscription, customer, organization, enterprise2020Plan,
            enterprisePlan, assignment, cohort, cohortId);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — one line from the subscription discount: $23,040 x 0.9 = $20,736.
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.View.HasDiscount &&
            mail.View.DiscountLines.Count == 1 &&
            mail.View.DiscountLines[0] == "10%" &&
            mail.View.TotalPrice == "$20,736"));
        // No cohort coupon, so the cohort source never fetches a coupon by code.
        await _stripeAdapter.DidNotReceive().GetCouponAsync(Arg.Any<string>(), Arg.Any<CouponGetOptions>());
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndDiscountOnlyOnPostRenewalPhase_ResolvesPhaseCouponById()
    {
        // Arrange — no cohort coupon, no subscription discount; the only discount is on the post-renewal
        // schedule phase, exposed as a CouponId that must be resolved via GetCouponAsync.
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var now = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var (invoice, subscription, customer) = BuildBusinessFixture(
            PlanType.EnterpriseAnnually2020, frozenTime: now);
        var (organization, enterprise2020Plan, enterprisePlan, assignment, cohort, cohortId) =
            BuildBusinessMigrationContext(coupon: null);

        StubBusinessMigration(parsedEvent, invoice, subscription, customer, organization, enterprise2020Plan,
            enterprisePlan, assignment, cohort, cohortId);
        StubActiveScheduleWithPhases(subscription, now, futurePhaseCouponId: "phase-15");
        _stripeAdapter.GetCouponAsync("phase-15", Arg.Any<CouponGetOptions>())
            .Returns(new Coupon { Id = "phase-15", PercentOff = 15 });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — resolved from the phase coupon id: $23,040 x 0.85 = $19,584.
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.View.HasDiscount &&
            mail.View.DiscountLines.Count == 1 &&
            mail.View.DiscountLines[0] == "15%" &&
            mail.View.TotalPrice == "$19,584"));
        await _stripeAdapter.Received(1).GetCouponAsync("phase-15", Arg.Any<CouponGetOptions>());
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndMixedPercentAndFixedAcrossSources_OrdersPercentBeforeFixed_AndTrimsDecimals()
    {
        // Arrange — a percentage cohort coupon and a fixed-amount subscription coupon. The cohort source is
        // read first, so the percentage line precedes the fixed line, and the whole-dollar fixed amount trims
        // its trailing .00.
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(
            PlanType.EnterpriseAnnually2020,
            subscriptionDiscounts:
            [
                // $100.00 off reported in minor units (cents).
                new Discount { Coupon = new Coupon { Id = "hundred-off", AmountOff = 10000 } }
            ]);
        var (organization, enterprise2020Plan, enterprisePlan, assignment, cohort, cohortId) =
            BuildBusinessMigrationContext(coupon: "ten-pct");

        StubBusinessMigration(parsedEvent, invoice, subscription, customer, organization, enterprise2020Plan,
            enterprisePlan, assignment, cohort, cohortId);
        _stripeAdapter.GetCouponAsync("ten-pct", Arg.Any<CouponGetOptions>())
            .Returns(new Coupon { Id = "ten-pct", PercentOff = 10 });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — percentage line first (from the cohort), then the fixed line. Math applies the percentage
        // then subtracts the fixed amount: $23,040 x 0.9 = $20,736; less $100 = $20,636.
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.View.HasDiscount &&
            mail.View.DiscountLines.Count == 2 &&
            mail.View.DiscountLines[0] == "10%" &&
            mail.View.DiscountLines[1] == "$100" &&
            mail.View.TotalPrice == "$20,636"));
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndOnceAndForeverCoupons_BothItemizedAndApplied()
    {
        // Arrange — a "once" subscription coupon and a "forever" cohort coupon. The locked decision is to match
        // Stripe's upcoming invoice, so both are itemized and applied regardless of duration.
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(
            PlanType.EnterpriseAnnually2020,
            subscriptionDiscounts:
            [
                new Discount { Coupon = new Coupon { Id = "once-10", PercentOff = 10, Duration = "once" } }
            ]);
        var (organization, enterprise2020Plan, enterprisePlan, assignment, cohort, cohortId) =
            BuildBusinessMigrationContext(coupon: "forever-20");

        StubBusinessMigration(parsedEvent, invoice, subscription, customer, organization, enterprise2020Plan,
            enterprisePlan, assignment, cohort, cohortId);
        _stripeAdapter.GetCouponAsync("forever-20", Arg.Any<CouponGetOptions>())
            .Returns(new Coupon { Id = "forever-20", PercentOff = 20, Duration = "forever" });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — both applied and compounded like Stripe: $23,040 x 0.80 x 0.90 = $16,588.80.
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.View.HasDiscount &&
            mail.View.DiscountLines.Count == 2 &&
            mail.View.DiscountLines[0] == "20%" &&
            mail.View.DiscountLines[1] == "10%" &&
            mail.View.TotalPrice == "$16,588.80"));
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndNoDiscountAnywhere_QuotesFullPrice_AndNeverFetchesCoupon()
    {
        // Arrange — no cohort coupon, empty subscription discounts, no schedule. Full price, no discount
        // section, and no coupon fetch occurs.
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(
            PlanType.EnterpriseAnnually2020, subscriptionDiscounts: []);
        var (organization, enterprise2020Plan, enterprisePlan, assignment, cohort, cohortId) =
            BuildBusinessMigrationContext(coupon: null);

        StubBusinessMigration(parsedEvent, invoice, subscription, customer, organization, enterprise2020Plan,
            enterprisePlan, assignment, cohort, cohortId);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — full price, no discount section.
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            !mail.View.HasDiscount &&
            mail.View.TotalPrice == "$23,040"));
        await _stripeAdapter.DidNotReceive().GetCouponAsync(Arg.Any<string>(), Arg.Any<CouponGetOptions>());
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndPhaseCouponFetchFails_OmitsThatCoupon_KeepsOthers_AndLogsError()
    {
        // Arrange — a cohort coupon resolves, but the post-renewal phase coupon fetch throws. The phase coupon
        // is omitted, the cohort discount is still itemized, an error is logged, and the email is still sent.
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var now = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var (invoice, subscription, customer) = BuildBusinessFixture(
            PlanType.EnterpriseAnnually2020, frozenTime: now);
        var (organization, enterprise2020Plan, enterprisePlan, assignment, cohort, cohortId) =
            BuildBusinessMigrationContext(coupon: "cohort-20");

        StubBusinessMigration(parsedEvent, invoice, subscription, customer, organization, enterprise2020Plan,
            enterprisePlan, assignment, cohort, cohortId);
        StubActiveScheduleWithPhases(subscription, now, futurePhaseCouponId: "phase-missing");
        _stripeAdapter.GetCouponAsync("cohort-20", Arg.Any<CouponGetOptions>())
            .Returns(new Coupon { Id = "cohort-20", PercentOff = 20 });
        _stripeAdapter.GetCouponAsync("phase-missing", Arg.Any<CouponGetOptions>())
            .ThrowsAsync(new StripeException("No such coupon"));

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — only the cohort 20% line survives: $23,040 x 0.8 = $18,432; the email is still sent.
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.View.HasDiscount &&
            mail.View.DiscountLines.Count == 1 &&
            mail.View.DiscountLines[0] == "20%" &&
            mail.View.TotalPrice == "$18,432"));
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString()!.Contains("Could not retrieve discount coupon") &&
                o.ToString()!.Contains("phase-missing") &&
                o.ToString()!.Contains(_organizationId.ToString())),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndScheduleListFails_KeepsCohortAndSubscriptionDiscounts_AndLogsError()
    {
        // Arrange — the schedule-list call itself throws (distinct from a per-phase coupon fetch failing). The
        // cohort and subscription discounts still resolve, the email is still sent, and the failure is logged so a
        // potentially missed schedule-phase discount reaches alerting.
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(
            PlanType.EnterpriseAnnually2020,
            subscriptionDiscounts:
            [
                new Discount { Coupon = new Coupon { Id = "sub-5", PercentOff = 5 } }
            ]);
        var (organization, enterprise2020Plan, enterprisePlan, assignment, cohort, cohortId) =
            BuildBusinessMigrationContext(coupon: "cohort-20");

        StubBusinessMigration(parsedEvent, invoice, subscription, customer, organization, enterprise2020Plan,
            enterprisePlan, assignment, cohort, cohortId);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .ThrowsAsync(new StripeException("Stripe API error"));
        _stripeAdapter.GetCouponAsync("cohort-20", Arg.Any<CouponGetOptions>())
            .Returns(new Coupon { Id = "cohort-20", PercentOff = 20 });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — cohort 20% + subscription 5% still compound: $23,040 x 0.80 x 0.95 = $17,510.40; email sent.
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.View.HasDiscount &&
            mail.View.DiscountLines.Count == 2 &&
            mail.View.DiscountLines[0] == "20%" &&
            mail.View.DiscountLines[1] == "5%" &&
            mail.View.TotalPrice == "$17,510.40"));
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString()!.Contains("Could not list subscription schedules") &&
                o.ToString()!.Contains(_organizationId.ToString())),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndSubscriptionDiscountCouponNotExpanded_OmitsIt_AndLogsError()
    {
        // Arrange — a subscription discount is present but its Coupon isn't expanded (a Stripe.Discount exposes
        // the id only via Coupon.Id, so there's nothing to fetch by). The cohort discount still resolves, the
        // email is still sent, and the unexpanded discount is logged rather than silently dropped.
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(
            PlanType.EnterpriseAnnually2020,
            subscriptionDiscounts:
            [
                new Discount { Id = "di_unexpanded", Coupon = null }
            ]);
        var (organization, enterprise2020Plan, enterprisePlan, assignment, cohort, cohortId) =
            BuildBusinessMigrationContext(coupon: "cohort-20");

        StubBusinessMigration(parsedEvent, invoice, subscription, customer, organization, enterprise2020Plan,
            enterprisePlan, assignment, cohort, cohortId);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });
        _stripeAdapter.GetCouponAsync("cohort-20", Arg.Any<CouponGetOptions>())
            .Returns(new Coupon { Id = "cohort-20", PercentOff = 20 });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — only the cohort 20% applies ($23,040 x 0.80 = $18,432); the unexpanded discount is logged.
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.View.HasDiscount &&
            mail.View.DiscountLines.Count == 1 &&
            mail.View.DiscountLines[0] == "20%" &&
            mail.View.TotalPrice == "$18,432"));
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString()!.Contains("has no expanded Coupon") &&
                o.ToString()!.Contains("di_unexpanded") &&
                o.ToString()!.Contains(_organizationId.ToString())),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndFixedDiscountExceedsTotal_ClampsToZero_AndLogsWarning()
    {
        // Arrange — a fixed-amount coupon larger than the discounted seat total drives the quote below zero. We
        // clamp the displayed total to $0 but log a warning, since a $0 renewal quote is anomalous.
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(
            PlanType.EnterpriseAnnually2020,
            subscriptionDiscounts:
            [
                // $30,000 off (3,000,000 minor units) exceeds the $23,040 gross.
                new Discount { Coupon = new Coupon { Id = "huge-amount", AmountOff = 3_000_000 } }
            ]);
        var (organization, enterprise2020Plan, enterprisePlan, assignment, cohort, cohortId) =
            BuildBusinessMigrationContext(coupon: null);

        StubBusinessMigration(parsedEvent, invoice, subscription, customer, organization, enterprise2020Plan,
            enterprisePlan, assignment, cohort, cohortId);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — total is clamped to $0 and the clamp is logged at Warning.
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.View.HasDiscount &&
            mail.View.TotalPrice == "$0"));
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString()!.Contains("went below zero after discounts") &&
                o.ToString()!.Contains(_organizationId.ToString())),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndNoActiveSchedule_FallsBackToCohortAndSubscriptionDiscounts()
    {
        // Arrange — no active schedule (empty list). The cohort and subscription discounts still resolve and
        // no exception propagates.
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(
            PlanType.EnterpriseAnnually2020,
            subscriptionDiscounts:
            [
                new Discount { Coupon = new Coupon { Id = "sub-5", PercentOff = 5 } }
        ]);
        var (organization, enterprise2020Plan, enterprisePlan, assignment, cohort, cohortId) =
            BuildBusinessMigrationContext(coupon: "cohort-20");

        StubBusinessMigration(parsedEvent, invoice, subscription, customer, organization, enterprise2020Plan,
            enterprisePlan, assignment, cohort, cohortId);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });
        _stripeAdapter.GetCouponAsync("cohort-20", Arg.Any<CouponGetOptions>())
            .Returns(new Coupon { Id = "cohort-20", PercentOff = 20 });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — cohort 20% + subscription 5%, compounded like Stripe: $23,040 x 0.80 x 0.95 = $17,510.40.
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.View.HasDiscount &&
            mail.View.DiscountLines.Count == 2 &&
            mail.View.DiscountLines[0] == "20%" &&
            mail.View.DiscountLines[1] == "5%" &&
            mail.View.TotalPrice == "$17,510.40"));
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndScheduleHasCurrentAndPostRenewalPhases_ReadsPostRenewalPhaseDiscount()
    {
        // Arrange — phase-selection coverage modeling the real migration layout. The schedule has an expired
        // anchor phase (EndDate <= now) that must be filtered out, then the canonical [Phase 1, Phase 2] shape:
        // Phase 1 is the current phase (ends at the renewal date — still in the future, carries a stale coupon)
        // and Phase 2 is the post-renewal phase (carries the live coupon). We must read Phase 2's discount,
        // proving the "second unexpired phase" selection — not the first unexpired phase, and not a phase the
        // EndDate > now filter should have dropped.
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var now = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var renewalDate = now.AddMonths(1);
        var (invoice, subscription, customer) = BuildBusinessFixture(
            PlanType.EnterpriseAnnually2020, frozenTime: now);
        var (organization, enterprise2020Plan, enterprisePlan, assignment, cohort, cohortId) =
            BuildBusinessMigrationContext(coupon: null);

        StubBusinessMigration(parsedEvent, invoice, subscription, customer, organization, enterprise2020Plan,
            enterprisePlan, assignment, cohort, cohortId);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sched_phase_select",
                        SubscriptionId = subscription.Id,
                        Status = SubscriptionScheduleStatus.Active,
                        Phases =
                        [
                            new SubscriptionSchedulePhase
                            {
                                StartDate = now.AddMonths(-12),
                                EndDate = now.AddDays(-1),
                                Discounts = [new SubscriptionSchedulePhaseDiscount { CouponId = "expired-50" }]
                            },
                            new SubscriptionSchedulePhase
                            {
                                StartDate = now.AddMonths(-11),
                                EndDate = renewalDate,
                                Discounts = [new SubscriptionSchedulePhaseDiscount { CouponId = "stale-50" }]
                            },
                            new SubscriptionSchedulePhase
                            {
                                StartDate = renewalDate,
                                EndDate = renewalDate.AddMonths(12),
                                Discounts = [new SubscriptionSchedulePhaseDiscount { CouponId = "live-25" }]
                            }
                        ]
                    }
                ]
            });
        _stripeAdapter.GetCouponAsync("live-25", Arg.Any<CouponGetOptions>())
            .Returns(new Coupon { Id = "live-25", PercentOff = 25 });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — only Phase 2's live coupon is used ($23,040 x 0.75 = $17,280); the current phase's stale
        // coupon and the expired anchor phase's coupon are never fetched.
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.View.HasDiscount &&
            mail.View.DiscountLines.Count == 1 &&
            mail.View.DiscountLines[0] == "25%" &&
            mail.View.TotalPrice == "$17,280"));
        await _stripeAdapter.Received(1).GetCouponAsync("live-25", Arg.Any<CouponGetOptions>());
        await _stripeAdapter.DidNotReceive().GetCouponAsync("stale-50", Arg.Any<CouponGetOptions>());
        await _stripeAdapter.DidNotReceive().GetCouponAsync("expired-50", Arg.Any<CouponGetOptions>());
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndCustomerLevelDiscountMirroredOntoPhase_ItemizesIt()
    {
        // Arrange — a customer-level discount. The scheduler mirrors customer-level discounts onto the
        // post-renewal phase (PriceIncreaseScheduler.ResolvePhase2ForBusinessAsync), so the email picks it up via
        // the schedule-phase source. We do NOT expand the customer discount directly: that path
        // (subscriptions.data.customer.discount.coupon) exceeds Stripe's 4-level expansion limit and 400s the
        // whole webhook.
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var now = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var (invoice, subscription, customer) = BuildBusinessFixture(
            PlanType.EnterpriseAnnually2020, frozenTime: now);
        var (organization, enterprise2020Plan, enterprisePlan, assignment, cohort, cohortId) =
            BuildBusinessMigrationContext(coupon: null);

        StubBusinessMigration(parsedEvent, invoice, subscription, customer, organization, enterprise2020Plan,
            enterprisePlan, assignment, cohort, cohortId);
        StubActiveScheduleWithPhases(subscription, now, futurePhaseCouponId: "cust-10");
        _stripeAdapter.GetCouponAsync("cust-10", Arg.Any<CouponGetOptions>())
            .Returns(new Coupon { Id = "cust-10", PercentOff = 10 });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — the mirrored customer-level 10% is itemized: $23,040 x 0.90 = $20,736.
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.View.HasDiscount &&
            mail.View.DiscountLines.Count == 1 &&
            mail.View.DiscountLines[0] == "10%" &&
            mail.View.TotalPrice == "$20,736"));
    }

    // PM-37514: a Teams 2019 (ActualUsage) renewal email must quote the same seat count the scheduler
    // bills — for a sub-5 org that is the occupied count, NOT organization.Seats (the base allotment)
    // and NOT the seat-overage line. 3 occupied of a 5-base org -> the email quotes 3 seats.
    [Fact]
    public async Task HandleAsync_Teams2019Migration_SubFiveOrg_RenewalEmailQuotesOccupiedSeats()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.TeamsMonthly2019);

        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.TeamsMonthly2019,
            Seats = 5 // base allotment; only 3 are occupied
        };
        var source = new Teams2019Plan(isAnnual: false);
        var target = new TeamsPlan(isAnnual: false);
        var cohortId = Guid.NewGuid();
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "teams-2019-monthly",
            MigrationPathId = MigrationPathId.Teams2019MonthlyToCurrent,
            IsActive = true
        };
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CohortId = cohortId,
            ScheduledDate = null
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(_organizationId)
            .Returns(new OrganizationSeatCounts { Users = 3 });
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly2019).Returns(source);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly).Returns(target);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _assignmentRepository.GetByOrganizationIdAsync(_organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(cohort);
        _priceIncreaseScheduler.ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>())
            .Returns(true);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — quotes occupied (3), not the 5-seat base allotment.
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.View.Seats == 3));
    }

    [Fact]
    public async Task HandleAsync_DoesNotRequestCustomerDiscountExpansionDeeperThanStripeAllows()
    {
        // Regression guard for the webhook-500: expanding the customer discount as
        // subscriptions.data.customer.discount[.coupon] is 5 levels deep and exceeds Stripe's 4-level limit,
        // which 400s GetCustomerAsync and fails every invoice.upcoming. Pin that we never request it.
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var (invoice, subscription, customer) = BuildBusinessFixture(PlanType.EnterpriseAnnually2020);
        var (organization, enterprise2020Plan, enterprisePlan, assignment, cohort, cohortId) =
            BuildBusinessMigrationContext(coupon: null);
        StubBusinessMigration(parsedEvent, invoice, subscription, customer, organization, enterprise2020Plan,
            enterprisePlan, assignment, cohort, cohortId);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — the customer fetch's Expand list contains no path 5+ levels deep.
        await _stripeAdapter.Received().GetCustomerAsync(
            Arg.Any<string>(),
            Arg.Is<CustomerGetOptions>(o =>
                o.Expand != null && o.Expand.All(e => e.Split('.').Length <= 4)));
    }

    [Fact]
    public async Task HandleAsync_WhenBusinessTier_AndScheduleHasUnexpectedPhaseCount_SkipsPhaseDiscounts_AndLogsWarning()
    {
        // Arrange — the schedule has only one unexpired phase (e.g. a webhook race advanced Phase 1 -> Phase 2).
        // The canonical [Phase 1, Phase 2] shape is gone, so we must not read its discounts as if it were the
        // post-renewal phase; we log a warning and still send the email with the cohort discount.
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        var parsedEvent = new Event { Id = "evt_123", Type = "invoice.upcoming" };
        var now = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var (invoice, subscription, customer) = BuildBusinessFixture(
            PlanType.EnterpriseAnnually2020, frozenTime: now);
        var (organization, enterprise2020Plan, enterprisePlan, assignment, cohort, cohortId) =
            BuildBusinessMigrationContext(coupon: "cohort-20");

        StubBusinessMigration(parsedEvent, invoice, subscription, customer, organization, enterprise2020Plan,
            enterprisePlan, assignment, cohort, cohortId);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sched_one_phase",
                        SubscriptionId = subscription.Id,
                        Status = SubscriptionScheduleStatus.Active,
                        Phases =
                        [
                            new SubscriptionSchedulePhase
                            {
                                StartDate = now.AddMonths(-11),
                                EndDate = now.AddMonths(1),
                                Discounts = [new SubscriptionSchedulePhaseDiscount { CouponId = "phase-only" }]
                            }
                        ]
                    }
                ]
            });
        _stripeAdapter.GetCouponAsync("cohort-20", Arg.Any<CouponGetOptions>())
            .Returns(new Coupon { Id = "cohort-20", PercentOff = 20 });

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert — only the cohort 20% applies ($23,040 x 0.80 = $18,432); the lone phase's coupon is not read,
        // and the off-nominal phase count is logged at Warning with the schedule id.
        await _mailer.Received(1).SendEmail(Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
            mail.View.HasDiscount &&
            mail.View.DiscountLines.Count == 1 &&
            mail.View.DiscountLines[0] == "20%" &&
            mail.View.TotalPrice == "$18,432"));
        await _stripeAdapter.DidNotReceive().GetCouponAsync("phase-only", Arg.Any<CouponGetOptions>());
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString()!.Contains("1 unexpired phase") &&
                o.ToString()!.Contains("sched_one_phase")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // Builds the organization/plan/cohort context shared by the business-migration discount tests. The cohort's
    // ProactiveDiscountCouponCode is set from <paramref name="coupon"/> (pass null for the no-cohort-coupon cases).
    private (Organization organization, Enterprise2020Plan sourcePlan, EnterprisePlan targetPlan,
        OrganizationPlanMigrationCohortAssignment assignment, OrganizationPlanMigrationCohort cohort, Guid cohortId)
        BuildBusinessMigrationContext(string? coupon)
    {
        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.EnterpriseAnnually2020
        };
        var cohortId = Guid.NewGuid();
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CohortId = cohortId,
            ScheduledDate = null
        };
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "enterprise-2020-annual",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            ProactiveDiscountCouponCode = coupon,
            IsActive = true
        };
        return (organization, new Enterprise2020Plan(isAnnual: true), new EnterprisePlan(isAnnual: true),
            assignment, cohort, cohortId);
    }

    // Wires the standard happy-path stubs that land the business-migration renewal email path.
    private void StubBusinessMigration(
        Event parsedEvent,
        Invoice invoice,
        Subscription subscription,
        Customer customer,
        Organization organization,
        Enterprise2020Plan sourcePlan,
        EnterprisePlan targetPlan,
        OrganizationPlanMigrationCohortAssignment assignment,
        OrganizationPlanMigrationCohort cohort,
        Guid cohortId)
    {
        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeAdapter.GetCustomerAsync(customer.Id, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(sourcePlan);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(targetPlan);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);
        _assignmentRepository.GetByOrganizationIdAsync(_organizationId).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(cohort);
        _priceIncreaseScheduler.ScheduleForSubscription(subscription, Arg.Any<OrganizationPriceIncreaseOptions>())
            .Returns(true);
    }

    // Registers an active subscription schedule modeling the real migration layout: a current phase that ends at
    // the renewal date (its EndDate is still in the future when the upcoming-invoice event fires) and a
    // post-renewal phase that starts at the renewal date and carries the given coupon. The renewal-bearing coupon
    // lives only on the post-renewal phase, so reading the current phase (EndDate > now) would miss it.
    private void StubActiveScheduleWithPhases(Subscription subscription, DateTime now, string futurePhaseCouponId)
    {
        var renewalDate = now.AddMonths(1);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sched_active",
                        SubscriptionId = subscription.Id,
                        Status = SubscriptionScheduleStatus.Active,
                        Phases =
                        [
                            new SubscriptionSchedulePhase
                            {
                                StartDate = now.AddMonths(-11),
                                EndDate = renewalDate
                            },
                            new SubscriptionSchedulePhase
                            {
                                StartDate = renewalDate,
                                EndDate = renewalDate.AddMonths(12),
                                Discounts =
                                    [new SubscriptionSchedulePhaseDiscount { CouponId = futurePhaseCouponId }]
                            }
                        ]
                    }
                ]
            });
    }

    private (Invoice invoice, Subscription subscription, Customer customer) BuildBusinessFixture(
        PlanType planType,
        List<Discount>? subscriptionDiscounts = null,
        DateTime? frozenTime = null)
    {
        var customerId = $"cus_{planType}";
        var subscriptionId = $"sub_{planType}";
        var invoice = new Invoice
        {
            CustomerId = customerId,
            AmountDue = 60000,
            NextPaymentAttempt = DateTime.UtcNow.AddDays(7),
            Lines = new StripeList<InvoiceLineItem>
            {
                Data = [new() { Description = "Test Item" }]
            }
        };
        // The renewal email resolves the seat count by matching the source plan's
        // password-manager seat price ID, so the fixture's seat line must carry that ID.
        var seatPriceId = SourceSeatPriceId(planType);
        var subscription = new Subscription
        {
            Id = subscriptionId,
            CustomerId = customerId,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = seatPriceId },
                        Quantity = 320,
                        CurrentPeriodEnd = new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc)
                    }
                ]
            },
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true },
            Customer = new Customer { Id = customerId },
            Metadata = new Dictionary<string, string>(),
            LatestInvoiceId = "inv_latest",
            // Subscription-level discounts are expanded in HandleAsync, so the fixture seeds them directly.
            Discounts = subscriptionDiscounts
        };

        if (frozenTime.HasValue)
        {
            // A "ready" test clock short-circuits the migration path's test-clock wait, and FrozenTime is what
            // the discount-resolution "now" reads to pick the post-renewal schedule phase.
            subscription.TestClock = new Stripe.TestHelpers.TestClock
            {
                FrozenTime = frozenTime.Value,
                Status = "ready"
            };
        }
        var customer = new Customer
        {
            Id = customerId,
            Subscriptions = new StripeList<Subscription> { Data = [subscription] },
            Address = new Address { Country = "US" }
        };
        return (invoice, subscription, customer);
    }

    // Mirrors the password-manager seat price IDs on the mock source plans so the renewal email's
    // price-ID-based seat lookup resolves the fixture's seat line. Only the 2020 source plans drive
    // the business renewal email; other plan types return a non-matching stub (the email path isn't
    // reached for them).
    private static string SourceSeatPriceId(PlanType planType) => planType switch
    {
        PlanType.EnterpriseAnnually2020 => "2020-enterprise-org-seat-annually",
        PlanType.EnterpriseMonthly2020 => "2020-enterprise-seat-monthly",
        PlanType.TeamsAnnually2020 => "2020-teams-org-seat-annually",
        PlanType.TeamsMonthly2020 => "2020-teams-org-seat-monthly",
        _ => "stub-price"
    };

}
