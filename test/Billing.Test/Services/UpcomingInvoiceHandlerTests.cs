using Bit.Billing.Services;
using Bit.Billing.Services.Implementations;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Pricing.Premium;
using Bit.Core.Entities;
using Bit.Core.Models.Mail.UpdatedInvoiceIncoming;
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
    private readonly IStripeFacade _stripeFacade;
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
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _pricingClient = Substitute.For<IPricingClient>();
        _providerRepository = Substitute.For<IProviderRepository>();
        _stripeFacade = Substitute.For<IStripeFacade>();
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
            _organizationRepository,
            _pricingClient,
            _providerRepository,
            _stripeFacade,
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
        _stripeFacade
            .GetCustomer(invoice.CustomerId, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeFacade.DidNotReceive()
            .UpdateCustomer(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>());
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
                Data = new List<InvoiceLineItem> { new() { Description = "Test Item" } }
            }
        };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = customerId,
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new() { Id = "si_123", Price = new Price { Id = Prices.PremiumAnnually } }
                }
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
        var customer = new Customer
        {
            Id = customerId,
            Tax = new CustomerTax { AutomaticTax = AutomaticTaxStatus.Supported },
            Subscriptions = new StripeList<Subscription> { Data = [subscription] }
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeFacade
            .GetCustomer(customerId, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, _userId, null));

        _userRepository.GetByIdAsync(_userId).Returns(user);
        _pricingClient.GetAvailablePremiumPlan().Returns(plan);

        // If milestone 2 is disabled, the default email is sent
        _featureService
            .IsEnabled(FeatureFlagKeys.PM23341_Milestone_2)
            .Returns(false);


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
                Data = new List<InvoiceLineItem> { new() { Description = "Test Item" } }
            }
        };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = customerId,
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new() { Id = priceSubscriptionId, Price = new Price { Id = Prices.PremiumAnnually } }
                }
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
        var customer = new Customer
        {
            Id = customerId,
            Subscriptions = new StripeList<Subscription> { Data = new List<Subscription> { subscription } }
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeFacade
            .GetCustomer(customerId, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, _userId, null));

        _userRepository.GetByIdAsync(_userId).Returns(user);
        _pricingClient.GetAvailablePremiumPlan().Returns(plan);
        _stripeFacade.UpdateSubscription(
                subscription.Id,
                Arg.Any<SubscriptionUpdateOptions>())
            .Returns(subscription);

        // If milestone 2 is true, the updated invoice email is sent
        _featureService
            .IsEnabled(FeatureFlagKeys.PM23341_Milestone_2)
            .Returns(true);


        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _userRepository.Received(1).GetByIdAsync(_userId);
        await _pricingClient.Received(1).GetAvailablePremiumPlan();
        await _stripeFacade.Received(1).UpdateSubscription(
            Arg.Is("sub_123"),
            Arg.Is<SubscriptionUpdateOptions>(o =>
                o.Items[0].Id == priceSubscriptionId &&
                o.Items[0].Price == priceId &&
                o.Discounts[0].Coupon == CouponIDs.Milestone2SubscriptionDiscount &&
                o.ProrationBehavior == "none"));

        // Verify the updated invoice email was sent
        await _mailer.Received(1).SendEmail(
            Arg.Is<UpdatedInvoiceUpcomingMail>(email =>
                email.ToEmails.Contains("user@example.com") &&
                email.Subject == "Your Subscription Will Renew Soon"));
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
                Data = new List<InvoiceLineItem> { new() { Description = "Test Item" } }
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
            Subscriptions = new StripeList<Subscription> { Data = new List<Subscription> { subscription } },
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
        _stripeFacade
            .GetCustomer(invoice.CustomerId, Arg.Any<CustomerGetOptions>())
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
                Data = new List<InvoiceLineItem> { new() { Description = "Test Item" } }
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
            Subscriptions = new StripeList<Subscription> { Data = new List<Subscription> { subscription } },
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
        _stripeFacade
            .GetCustomer(invoice.CustomerId, Arg.Any<CustomerGetOptions>())
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
        _stripeFacade
            .GetInvoice(subscription.LatestInvoiceId)
            .Returns(invoice);

        _getPaymentMethodQuery.Run(organization).Returns(MaskedPaymentMethod.From(paymentMethod));

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _organizationRepository.Received(1).GetByIdAsync(_organizationId);
        _stripeEventUtilityService.Received(1).IsSponsoredSubscription(subscription);
        await _validateSponsorshipCommand.Received(1).ValidateSponsorshipAsync(_organizationId);
        await _stripeFacade.Received(1).GetInvoice(Arg.Is("inv_latest"));

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
                Data = new List<InvoiceLineItem> { new() { Description = "Test Item" } }
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
            Subscriptions = new StripeList<Subscription> { Data = new List<Subscription> { subscription } },
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
        _stripeFacade
            .GetCustomer(invoice.CustomerId, Arg.Any<CustomerGetOptions>())
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

        _stripeFacade
            .GetInvoice(subscription.LatestInvoiceId)
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
                Data = new List<InvoiceLineItem> { new() { Description = "Test Item" } }
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
            Subscriptions = new StripeList<Subscription> { Data = new List<Subscription> { subscription } },
            Address = new Address { Country = "UK" },
            TaxExempt = TaxExempt.None
        };
        var provider = new Provider { Id = _providerId, BillingEmail = "provider@example.com" };

        var paymentMethod = new Card { Last4 = "4242", Brand = "visa" };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeFacade.GetCustomer(invoice.CustomerId, Arg.Any<CustomerGetOptions>()).Returns(customer);

        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, null, _providerId));

        _providerRepository.GetByIdAsync(_providerId).Returns(provider);
        _getPaymentMethodQuery.Run(provider).Returns(MaskedPaymentMethod.From(paymentMethod));

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _providerRepository.Received(2).GetByIdAsync(_providerId);

        // Verify tax exempt was set to reverse for non-US providers
        await _stripeFacade.Received(1).UpdateCustomer(
            Arg.Is("cus_123"),
            Arg.Is<CustomerUpdateOptions>(o => o.TaxExempt == TaxExempt.Reverse));

        // Verify automatic tax was enabled
        await _stripeFacade.Received(1).UpdateSubscription(
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
    public async Task HandleAsync_WhenUpdateSubscriptionItemPriceIdFails_LogsErrorAndSendsEmail()
    {
        // Arrange
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
                Data = new List<InvoiceLineItem> { new() { Description = "Test Item" } }
            }
        };
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = customerId,
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new() { Id = priceSubscriptionId, Price = new Price { Id = Prices.PremiumAnnually } }
                }
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
        var customer = new Customer
        {
            Id = customerId,
            Subscriptions = new StripeList<Subscription> { Data = new List<Subscription> { subscription } }
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeFacade.GetCustomer(invoice.CustomerId, Arg.Any<CustomerGetOptions>()).Returns(customer);

        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, _userId, null));

        _userRepository.GetByIdAsync(_userId).Returns(user);

        _featureService
            .IsEnabled(FeatureFlagKeys.PM23341_Milestone_2)
            .Returns(true);

        _pricingClient.GetAvailablePremiumPlan().Returns(plan);

        // Setup exception when updating subscription
        _stripeFacade
            .UpdateSubscription(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
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

        // Verify that email was still sent despite the exception
        await _mailer.Received(1).SendEmail(
            Arg.Is<UpdatedInvoiceUpcomingMail>(email =>
                email.ToEmails.Contains("user@example.com") &&
                email.Subject == "Your Subscription Will Renew Soon"));
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
                Data = new List<InvoiceLineItem> { new() { Description = "Test Item" } }
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
        };
        var customer = new Customer
        {
            Id = "cus_123",
            Subscriptions = new StripeList<Subscription> { Data = new List<Subscription> { subscription } }
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeFacade
            .GetCustomer(invoice.CustomerId, Arg.Any<CustomerGetOptions>())
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
                Data = new List<InvoiceLineItem> { new() { Description = "Free Item" } }
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
            Subscriptions = new StripeList<Subscription> { Data = new List<Subscription> { subscription } }
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeFacade
            .GetCustomer(invoice.CustomerId, Arg.Any<CustomerGetOptions>())
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
                Data = new List<InvoiceLineItem> { new() { Description = "Test Item" } }
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
            Subscriptions = new StripeList<Subscription> { Data = new List<Subscription> { subscription } }
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeFacade
            .GetCustomer(invoice.CustomerId, Arg.Any<CustomerGetOptions>())
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

        await _mailer.DidNotReceive().SendEmail(Arg.Any<UpdatedInvoiceUpcomingMail>());
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
                Data = new List<InvoiceLineItem> { new() { Description = "Test Item" } }
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
            Subscriptions = new StripeList<Subscription> { Data = new List<Subscription> { subscription } }
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeFacade
            .GetCustomer(invoice.CustomerId, Arg.Any<CustomerGetOptions>())
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
                Data = new List<InvoiceLineItem> { new() { Description = "Test Item" } }
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
                Data = new List<SubscriptionItem>
                {
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
                }
            },
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true },
            Metadata = new Dictionary<string, string>()
        };

        var customer = new Customer
        {
            Id = customerId,
            Subscriptions = new StripeList<Subscription> { Data = new List<Subscription> { subscription } },
            Address = new Address { Country = "US" }
        };

        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.FamiliesAnnually2019
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeFacade.GetCustomer(customerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019Plan);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesPlan);
        _featureService.IsEnabled(FeatureFlagKeys.PM26462_Milestone_3).Returns(true);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeFacade.Received(1).UpdateSubscription(
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

        await _organizationRepository.Received(1).ReplaceAsync(
            Arg.Is<Organization>(org =>
                org.Id == _organizationId &&
                org.PlanType == PlanType.FamiliesAnnually &&
                org.Plan == familiesPlan.Name &&
                org.UsersGetPremium == familiesPlan.UsersGetPremium &&
                org.Seats == familiesPlan.PasswordManager.BaseSeats));

        await _mailer.Received(1).SendEmail(
            Arg.Is<UpdatedInvoiceUpcomingMail>(email =>
                email.ToEmails.Contains("org@example.com") &&
                email.Subject == "Your Subscription Will Renew Soon"));
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
                Data = new List<InvoiceLineItem> { new() { Description = "Test Item" } }
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
                Data = new List<SubscriptionItem>
                {
                    new()
                    {
                        Id = passwordManagerItemId,
                        Price = new Price { Id = families2019Plan.PasswordManager.StripePlanId }
                    }
                }
            },
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true },
            Metadata = new Dictionary<string, string>()
        };

        var customer = new Customer
        {
            Id = customerId,
            Subscriptions = new StripeList<Subscription> { Data = new List<Subscription> { subscription } },
            Address = new Address { Country = "US" }
        };

        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.FamiliesAnnually2019
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeFacade.GetCustomer(customerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019Plan);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesPlan);
        _featureService.IsEnabled(FeatureFlagKeys.PM26462_Milestone_3).Returns(true);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeFacade.Received(1).UpdateSubscription(
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
    }

    [Fact]
    public async Task HandleAsync_WhenMilestone3Disabled_AndFamilies2019Plan_DoesNotUpdateSubscription()
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
                Data = new List<InvoiceLineItem> { new() { Description = "Test Item" } }
            }
        };

        var families2019Plan = new Families2019Plan();

        var subscription = new Subscription
        {
            Id = subscriptionId,
            CustomerId = customerId,
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new()
                    {
                        Id = passwordManagerItemId,
                        Price = new Price { Id = families2019Plan.PasswordManager.StripePlanId }
                    }
                }
            },
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true },
            Metadata = new Dictionary<string, string>()
        };

        var customer = new Customer
        {
            Id = customerId,
            Subscriptions = new StripeList<Subscription> { Data = new List<Subscription> { subscription } },
            Address = new Address { Country = "US" }
        };

        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.FamiliesAnnually2019
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeFacade.GetCustomer(customerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019Plan);
        _featureService.IsEnabled(FeatureFlagKeys.PM26462_Milestone_3).Returns(false);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert - should not update subscription or organization when feature flag is disabled
        await _stripeFacade.DidNotReceive().UpdateSubscription(
            Arg.Any<string>(),
            Arg.Is<SubscriptionUpdateOptions>(o => o.Discounts != null));

        await _organizationRepository.DidNotReceive().ReplaceAsync(
            Arg.Is<Organization>(org => org.PlanType == PlanType.FamiliesAnnually));
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
                Data = new List<InvoiceLineItem> { new() { Description = "Test Item" } }
            }
        };

        var familiesPlan = new FamiliesPlan();

        var subscription = new Subscription
        {
            Id = subscriptionId,
            CustomerId = customerId,
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new()
                    {
                        Id = "si_pm_123",
                        Price = new Price { Id = familiesPlan.PasswordManager.StripePlanId }
                    }
                }
            },
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true },
            Metadata = new Dictionary<string, string>()
        };

        var customer = new Customer
        {
            Id = customerId,
            Subscriptions = new StripeList<Subscription> { Data = new List<Subscription> { subscription } },
            Address = new Address { Country = "US" }
        };

        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.FamiliesAnnually // Already on the new plan
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeFacade.GetCustomer(customerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesPlan);
        _featureService.IsEnabled(FeatureFlagKeys.PM26462_Milestone_3).Returns(true);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert - should not update subscription when not on FamiliesAnnually2019 plan
        await _stripeFacade.DidNotReceive().UpdateSubscription(
            Arg.Any<string>(),
            Arg.Is<SubscriptionUpdateOptions>(o => o.Discounts != null));

        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
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
                Data = new List<InvoiceLineItem> { new() { Description = "Test Item" } }
            }
        };

        var families2019Plan = new Families2019Plan();

        var subscription = new Subscription
        {
            Id = subscriptionId,
            CustomerId = customerId,
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new()
                    {
                        Id = "si_different_item",
                        Price = new Price { Id = "different-price-id" }
                    }
                }
            },
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true },
            Metadata = new Dictionary<string, string>()
        };

        var customer = new Customer
        {
            Id = customerId,
            Subscriptions = new StripeList<Subscription> { Data = new List<Subscription> { subscription } },
            Address = new Address { Country = "US" }
        };

        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.FamiliesAnnually2019
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeFacade.GetCustomer(customerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019Plan);
        _featureService.IsEnabled(FeatureFlagKeys.PM26462_Milestone_3).Returns(true);
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
        await _stripeFacade.DidNotReceive().UpdateSubscription(
            Arg.Any<string>(),
            Arg.Is<SubscriptionUpdateOptions>(o => o.Discounts != null));

        await _organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
    }

    [Fact]
    public async Task HandleAsync_WhenMilestone3Enabled_AndUpdateFails_LogsError()
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
                Data = new List<InvoiceLineItem> { new() { Description = "Test Item" } }
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
                Data = new List<SubscriptionItem>
                {
                    new()
                    {
                        Id = passwordManagerItemId,
                        Price = new Price { Id = families2019Plan.PasswordManager.StripePlanId }
                    }
                }
            },
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true },
            Metadata = new Dictionary<string, string>()
        };

        var customer = new Customer
        {
            Id = customerId,
            Subscriptions = new StripeList<Subscription> { Data = new List<Subscription> { subscription } },
            Address = new Address { Country = "US" }
        };

        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.FamiliesAnnually2019
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeFacade.GetCustomer(customerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019Plan);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesPlan);
        _featureService.IsEnabled(FeatureFlagKeys.PM26462_Milestone_3).Returns(true);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);

        // Simulate update failure
        _stripeFacade
            .UpdateSubscription(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
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

        // Should still attempt to send email despite the failure
        await _mailer.Received(1).SendEmail(
            Arg.Is<UpdatedInvoiceUpcomingMail>(email =>
                email.ToEmails.Contains("org@example.com") &&
                email.Subject == "Your Subscription Will Renew Soon"));
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
                Data = new List<InvoiceLineItem> { new() { Description = "Test Item" } }
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
                Data = new List<SubscriptionItem>
                {
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
                }
            },
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true },
            Metadata = new Dictionary<string, string>()
        };

        var customer = new Customer
        {
            Id = customerId,
            Subscriptions = new StripeList<Subscription> { Data = new List<Subscription> { subscription } },
            Address = new Address { Country = "US" }
        };

        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.FamiliesAnnually2019
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeFacade.GetCustomer(customerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019Plan);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesPlan);
        _featureService.IsEnabled(FeatureFlagKeys.PM26462_Milestone_3).Returns(true);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeFacade.Received(1).UpdateSubscription(
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

        await _organizationRepository.Received(1).ReplaceAsync(
            Arg.Is<Organization>(org =>
                org.Id == _organizationId &&
                org.PlanType == PlanType.FamiliesAnnually &&
                org.Plan == familiesPlan.Name &&
                org.UsersGetPremium == familiesPlan.UsersGetPremium &&
                org.Seats == familiesPlan.PasswordManager.BaseSeats));

        await _mailer.Received(1).SendEmail(
            Arg.Is<UpdatedInvoiceUpcomingMail>(email =>
                email.ToEmails.Contains("org@example.com") &&
                email.Subject == "Your Subscription Will Renew Soon"));
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
                Data = new List<InvoiceLineItem> { new() { Description = "Test Item" } }
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
                Data = new List<SubscriptionItem>
                {
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
                }
            },
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true },
            Metadata = new Dictionary<string, string>()
        };

        var customer = new Customer
        {
            Id = customerId,
            Subscriptions = new StripeList<Subscription> { Data = new List<Subscription> { subscription } },
            Address = new Address { Country = "US" }
        };

        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.FamiliesAnnually2019
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeFacade.GetCustomer(customerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019Plan);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesPlan);
        _featureService.IsEnabled(FeatureFlagKeys.PM26462_Milestone_3).Returns(true);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeFacade.Received(1).UpdateSubscription(
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

        await _organizationRepository.Received(1).ReplaceAsync(
            Arg.Is<Organization>(org =>
                org.Id == _organizationId &&
                org.PlanType == PlanType.FamiliesAnnually &&
                org.Plan == familiesPlan.Name &&
                org.UsersGetPremium == familiesPlan.UsersGetPremium &&
                org.Seats == familiesPlan.PasswordManager.BaseSeats));

        await _mailer.Received(1).SendEmail(
            Arg.Is<UpdatedInvoiceUpcomingMail>(email =>
                email.ToEmails.Contains("org@example.com") &&
                email.Subject == "Your Subscription Will Renew Soon"));
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
                Data = new List<InvoiceLineItem> { new() { Description = "Test Item" } }
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
                Data = new List<SubscriptionItem>
                {
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
                }
            },
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true },
            Metadata = new Dictionary<string, string>()
        };

        var customer = new Customer
        {
            Id = customerId,
            Subscriptions = new StripeList<Subscription> { Data = new List<Subscription> { subscription } },
            Address = new Address { Country = "US" }
        };

        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.FamiliesAnnually2019
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeFacade.GetCustomer(customerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019).Returns(families2019Plan);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesPlan);
        _featureService.IsEnabled(FeatureFlagKeys.PM26462_Milestone_3).Returns(true);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeFacade.Received(1).UpdateSubscription(
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

        await _organizationRepository.Received(1).ReplaceAsync(
            Arg.Is<Organization>(org =>
                org.Id == _organizationId &&
                org.PlanType == PlanType.FamiliesAnnually &&
                org.Plan == familiesPlan.Name &&
                org.UsersGetPremium == familiesPlan.UsersGetPremium &&
                org.Seats == familiesPlan.PasswordManager.BaseSeats));

        await _mailer.Received(1).SendEmail(
            Arg.Is<UpdatedInvoiceUpcomingMail>(email =>
                email.ToEmails.Contains("org@example.com") &&
                email.Subject == "Your Subscription Will Renew Soon"));
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
                Data = new List<InvoiceLineItem> { new() { Description = "Test Item" } }
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
                Data = new List<SubscriptionItem>
                {
                    new()
                    {
                        Id = passwordManagerItemId,
                        Price = new Price { Id = families2025Plan.PasswordManager.StripePlanId }
                    }
                }
            },
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true },
            Metadata = new Dictionary<string, string>()
        };

        var customer = new Customer
        {
            Id = customerId,
            Subscriptions = new StripeList<Subscription> { Data = new List<Subscription> { subscription } },
            Address = new Address { Country = "US" }
        };

        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.FamiliesAnnually2025
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeFacade.GetCustomer(customerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2025).Returns(families2025Plan);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(familiesPlan);
        _featureService.IsEnabled(FeatureFlagKeys.PM26462_Milestone_3).Returns(true);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert
        await _stripeFacade.Received(1).UpdateSubscription(
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
    }

    [Fact]
    public async Task HandleAsync_WhenMilestone3Disabled_AndFamilies2025Plan_DoesNotUpdateSubscription()
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
                Data = new List<InvoiceLineItem> { new() { Description = "Test Item" } }
            }
        };

        var families2025Plan = new Families2025Plan();

        var subscription = new Subscription
        {
            Id = subscriptionId,
            CustomerId = customerId,
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new()
                    {
                        Id = passwordManagerItemId,
                        Price = new Price { Id = families2025Plan.PasswordManager.StripePlanId }
                    }
                }
            },
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true },
            Metadata = new Dictionary<string, string>()
        };

        var customer = new Customer
        {
            Id = customerId,
            Subscriptions = new StripeList<Subscription> { Data = new List<Subscription> { subscription } },
            Address = new Address { Country = "US" }
        };

        var organization = new Organization
        {
            Id = _organizationId,
            BillingEmail = "org@example.com",
            PlanType = PlanType.FamiliesAnnually2025
        };

        _stripeEventService.GetInvoice(parsedEvent).Returns(invoice);
        _stripeFacade.GetCustomer(customerId, Arg.Any<CustomerGetOptions>()).Returns(customer);
        _stripeEventUtilityService
            .GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(_organizationId, null, null));
        _organizationRepository.GetByIdAsync(_organizationId).Returns(organization);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2025).Returns(families2025Plan);
        _featureService.IsEnabled(FeatureFlagKeys.PM26462_Milestone_3).Returns(false);
        _stripeEventUtilityService.IsSponsoredSubscription(subscription).Returns(false);

        // Act
        await _sut.HandleAsync(parsedEvent);

        // Assert - should not update subscription or organization when feature flag is disabled
        await _stripeFacade.DidNotReceive().UpdateSubscription(
            Arg.Any<string>(),
            Arg.Any<SubscriptionUpdateOptions>());

        await _organizationRepository.DidNotReceive().ReplaceAsync(
            Arg.Is<Organization>(org => org.PlanType == PlanType.FamiliesAnnually));
    }
}
