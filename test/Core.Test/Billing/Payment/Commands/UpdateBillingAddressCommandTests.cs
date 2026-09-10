using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Payment.Commands;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Tax.Services;
using Bit.Core.Test.Billing.Extensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Payment.Commands;

using static StripeConstants;

public class UpdateBillingAddressCommandTests
{
    private readonly ILogger<UpdateBillingAddressCommand> _logger =
        Substitute.For<ILogger<UpdateBillingAddressCommand>>();
    private readonly ISubscriberService _subscriberService = Substitute.For<ISubscriberService>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly ITaxService _taxService = Substitute.For<ITaxService>();
    private readonly UpdateBillingAddressCommand _command;

    public UpdateBillingAddressCommandTests()
    {
        _command = new UpdateBillingAddressCommand(
            _logger,
            _subscriberService,
            _stripeAdapter,
            _taxService);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = new List<SubscriptionSchedule>() });
    }

    [Fact]
    public async Task Run_PersonalOrganization_MakesCorrectInvocations_ReturnsBillingAddress()
    {
        var organization = new Organization
        {
            PlanType = PlanType.FamiliesAnnually,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = "sub_123"
        };

        var input = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345",
            Line1 = "123 Main St.",
            Line2 = "Suite 100",
            City = "New York",
            State = "NY"
        };

        var customer = new Customer
        {
            Address = new Address
            {
                Country = "US",
                PostalCode = "12345",
                Line1 = "123 Main St.",
                Line2 = "Suite 100",
                City = "New York",
                State = "NY"
            },
            Subscriptions = new StripeList<Subscription>
            {
                Data =
                [
                    new Subscription
                    {
                        Id = organization.GatewaySubscriptionId,
                        AutomaticTax = new SubscriptionAutomaticTax { Enabled = false }
                    }
                ]
            }
        };

        _stripeAdapter.UpdateCustomerAsync(organization.GatewayCustomerId, Arg.Is<CustomerUpdateOptions>(options =>
            options.Address.Matches(input) &&
            options.HasExpansions("subscriptions", "subscriptions.data.test_clock")
        )).Returns(customer);

        var result = await _command.Run(organization, input);

        Assert.True(result.IsT0);
        var output = result.AsT0;
        Assert.Equivalent(input, output);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(organization.GatewaySubscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(options => options.AutomaticTax.Enabled == true));

        await _stripeAdapter.Received(1).UpdateCustomerAsync(organization.GatewayCustomerId,
            Arg.Is<CustomerUpdateOptions>(options => options.HasExpansions("discount.source.coupon")));
    }

    [Fact]
    public async Task Run_PersonalOrganization_NoCurrentCustomer_MakesCorrectInvocations_ReturnsBillingAddress()
    {
        var organization = new Organization
        {
            PlanType = PlanType.FamiliesAnnually,
            GatewaySubscriptionId = "sub_123"
        };

        var input = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345",
            Line1 = "123 Main St.",
            Line2 = "Suite 100",
            City = "New York",
            State = "NY"
        };

        var customer = new Customer
        {
            Address = new Address
            {
                Country = "US",
                PostalCode = "12345",
                Line1 = "123 Main St.",
                Line2 = "Suite 100",
                City = "New York",
                State = "NY"
            },
            Subscriptions = new StripeList<Subscription>
            {
                Data =
                [
                    new Subscription
                    {
                        Id = organization.GatewaySubscriptionId,
                        AutomaticTax = new SubscriptionAutomaticTax { Enabled = false }
                    }
                ]
            }
        };

        _stripeAdapter.UpdateCustomerAsync(organization.GatewayCustomerId, Arg.Is<CustomerUpdateOptions>(options =>
            options.Address.Matches(input) &&
            options.HasExpansions("subscriptions", "subscriptions.data.test_clock")
        )).Returns(customer);

        var result = await _command.Run(organization, input);

        Assert.True(result.IsT0);
        var output = result.AsT0;
        Assert.Equivalent(input, output);

        await _subscriberService.Received(1).CreateStripeCustomer(organization);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(organization.GatewaySubscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(options => options.AutomaticTax.Enabled == true));
    }

    [Fact]
    public async Task Run_BusinessOrganization_MakesCorrectInvocations_ReturnsBillingAddress()
    {
        var organization = new Organization
        {
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = "sub_123"
        };

        var input = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345",
            Line1 = "123 Main St.",
            Line2 = "Suite 100",
            City = "New York",
            State = "NY"
        };

        var customer = new Customer
        {
            Address = new Address
            {
                Country = "US",
                PostalCode = "12345",
                Line1 = "123 Main St.",
                Line2 = "Suite 100",
                City = "New York",
                State = "NY"
            },
            Subscriptions = new StripeList<Subscription>
            {
                Data =
                [
                    new Subscription
                    {
                        Id = organization.GatewaySubscriptionId,
                        AutomaticTax = new SubscriptionAutomaticTax { Enabled = false }
                    }
                ]
            }
        };

        _stripeAdapter.GetCustomerAsync(organization.GatewayCustomerId)
            .Returns(new Customer { TaxExempt = TaxExempt.None });

        _stripeAdapter.UpdateCustomerAsync(organization.GatewayCustomerId, Arg.Is<CustomerUpdateOptions>(options =>
            options.Address.Matches(input) &&
            options.HasExpansions("subscriptions", "subscriptions.data.test_clock", "tax_ids")
        )).Returns(customer);

        var result = await _command.Run(organization, input);

        Assert.True(result.IsT0);
        var output = result.AsT0;
        Assert.Equivalent(input, output);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(organization.GatewaySubscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(options => options.AutomaticTax.Enabled == true));
    }

    [Fact]
    public async Task Run_BusinessOrganization_FetchesCustomerWithDiscountSourceCouponExpanded()
    {
        var organization = new Organization
        {
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = "sub_123"
        };

        var input = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345",
            Line1 = "123 Main St.",
            City = "New York",
            State = "NY"
        };

        var customer = new Customer
        {
            Address = new Address { Country = "US" },
            Subscriptions = new StripeList<Subscription>
            {
                Data =
                [
                    new Subscription
                    {
                        Id = organization.GatewaySubscriptionId,
                        AutomaticTax = new SubscriptionAutomaticTax { Enabled = false }
                    }
                ]
            }
        };

        _stripeAdapter.GetCustomerAsync(organization.GatewayCustomerId)
            .Returns(new Customer { TaxExempt = TaxExempt.None });

        _stripeAdapter.UpdateCustomerAsync(organization.GatewayCustomerId, Arg.Any<CustomerUpdateOptions>())
            .Returns(customer);

        await _command.Run(organization, input);

        await _stripeAdapter.Received(1).UpdateCustomerAsync(organization.GatewayCustomerId,
            Arg.Is<CustomerUpdateOptions>(options => options.HasExpansions("discount.source.coupon")));
    }

    [Fact]
    public async Task Run_BusinessOrganization_RemovingTaxId_MakesCorrectInvocations_ReturnsBillingAddress()
    {
        var organization = new Organization
        {
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = "sub_123"
        };

        var input = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345",
            Line1 = "123 Main St.",
            Line2 = "Suite 100",
            City = "New York",
            State = "NY"
        };

        var customer = new Customer
        {
            Address = new Address
            {
                Country = "US",
                PostalCode = "12345",
                Line1 = "123 Main St.",
                Line2 = "Suite 100",
                City = "New York",
                State = "NY"
            },
            Id = organization.GatewayCustomerId,
            Subscriptions = new StripeList<Subscription>
            {
                Data =
                [
                    new Subscription
                    {
                        Id = organization.GatewaySubscriptionId,
                        AutomaticTax = new SubscriptionAutomaticTax { Enabled = false }
                    }
                ]
            },
            TaxIds = new StripeList<TaxId>
            {
                Data =
                [
                    new TaxId { Id = "tax_id_123", Type = "us_ein", Value = "123456789" }
                ]
            }
        };

        _stripeAdapter.GetCustomerAsync(organization.GatewayCustomerId)
            .Returns(new Customer { TaxExempt = TaxExempt.None });

        _stripeAdapter.UpdateCustomerAsync(organization.GatewayCustomerId, Arg.Is<CustomerUpdateOptions>(options =>
            options.Address.Matches(input) &&
            options.HasExpansions("subscriptions", "subscriptions.data.test_clock", "tax_ids")
        )).Returns(customer);

        var result = await _command.Run(organization, input);

        Assert.True(result.IsT0);
        var output = result.AsT0;
        Assert.Equivalent(input, output);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(organization.GatewaySubscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(options => options.AutomaticTax.Enabled == true));

        await _stripeAdapter.Received(1).DeleteTaxIdAsync(customer.Id, "tax_id_123");
    }

    [Fact]
    public async Task Run_BusinessOrganizationWithSpanishCIF_MakesCorrectInvocations_ReturnsBillingAddress()
    {
        var organization = new Organization
        {
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = "sub_123"
        };

        var input = new BillingAddress
        {
            Country = "ES",
            PostalCode = "28001",
            Line1 = "Calle de Serrano 41",
            Line2 = "Planta 3",
            City = "Madrid",
            State = "Madrid",
            TaxId = new TaxID(TaxIdType.SpanishNIF, "A12345678")
        };

        var customer = new Customer
        {
            Address = new Address
            {
                Country = "ES",
                PostalCode = "28001",
                Line1 = "Calle de Serrano 41",
                Line2 = "Planta 3",
                City = "Madrid",
                State = "Madrid"
            },
            Id = organization.GatewayCustomerId,
            Subscriptions = new StripeList<Subscription>
            {
                Data =
                [
                    new Subscription
                    {
                        Id = organization.GatewaySubscriptionId,
                        AutomaticTax = new SubscriptionAutomaticTax { Enabled = false }
                    }
                ]
            }
        };

        _taxService.GetStripeTaxCode(input.Country, input.TaxId.Value).Returns(TaxIdType.SpanishNIF);

        _stripeAdapter.GetCustomerAsync(organization.GatewayCustomerId)
            .Returns(new Customer { TaxExempt = TaxExempt.None });

        _stripeAdapter.UpdateCustomerAsync(organization.GatewayCustomerId, Arg.Is<CustomerUpdateOptions>(options =>
            options.Address.Matches(input) &&
            options.HasExpansions("subscriptions", "subscriptions.data.test_clock", "tax_ids")
        )).Returns(customer);

        _stripeAdapter
            .CreateTaxIdAsync(customer.Id,
                Arg.Is<TaxIdCreateOptions>(options => options.Type == TaxIdType.EUVAT))
            .Returns(new TaxId { Type = TaxIdType.EUVAT, Value = "ESA12345678" });

        var result = await _command.Run(organization, input);

        Assert.True(result.IsT0);
        var output = result.AsT0;
        Assert.Equivalent(input with { TaxId = new TaxID(TaxIdType.EUVAT, "ESA12345678") }, output);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(organization.GatewaySubscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(options => options.AutomaticTax.Enabled == true));

        await _stripeAdapter.Received(1).CreateTaxIdAsync(organization.GatewayCustomerId, Arg.Is<TaxIdCreateOptions>(
            options => options.Type == TaxIdType.SpanishNIF &&
                       options.Value == input.TaxId.Value));
    }

    [Fact]
    public async Task Run_BusinessOrganization_UpdatingWithSameTaxId_DeletesBeforeCreating()
    {
        var organization = new Organization
        {
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = "sub_123"
        };

        var input = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345",
            Line1 = "123 Main St.",
            Line2 = "Suite 100",
            City = "New York",
            State = "NY",
            TaxId = new TaxID("us_ein", "987654321")
        };

        _taxService.GetStripeTaxCode(input.Country, input.TaxId.Value).Returns("us_ein");

        var existingTaxId = new TaxId { Id = "tax_id_123", Type = "us_ein", Value = "987654321" };

        var customer = new Customer
        {
            Address = new Address
            {
                Country = "US",
                PostalCode = "12345",
                Line1 = "123 Main St.",
                Line2 = "Suite 100",
                City = "New York",
                State = "NY"
            },
            Id = organization.GatewayCustomerId,
            Subscriptions = new StripeList<Subscription>
            {
                Data =
                [
                    new Subscription
                    {
                        Id = organization.GatewaySubscriptionId,
                        AutomaticTax = new SubscriptionAutomaticTax { Enabled = false }
                    }
                ]
            },
            TaxIds = new StripeList<TaxId>
            {
                Data = [existingTaxId]
            }
        };

        _stripeAdapter.GetCustomerAsync(organization.GatewayCustomerId)
            .Returns(new Customer { TaxExempt = TaxExempt.None });

        _stripeAdapter.UpdateCustomerAsync(organization.GatewayCustomerId, Arg.Is<CustomerUpdateOptions>(options =>
            options.Address.Matches(input) &&
            options.HasExpansions("subscriptions", "subscriptions.data.test_clock", "tax_ids")
        )).Returns(customer);

        var newTaxId = new TaxId { Id = "tax_id_456", Type = "us_ein", Value = "987654321" };
        _stripeAdapter.CreateTaxIdAsync(customer.Id, Arg.Is<TaxIdCreateOptions>(
            options => options.Type == "us_ein" && options.Value == "987654321"
        )).Returns(newTaxId);

        var result = await _command.Run(organization, input);

        Assert.True(result.IsT0);
        var output = result.AsT0;
        Assert.Equivalent(input, output);

        // Verify that deletion happens before creation
        Received.InOrder(() =>
        {
            _stripeAdapter.DeleteTaxIdAsync(customer.Id, existingTaxId.Id);
            _stripeAdapter.CreateTaxIdAsync(customer.Id, Arg.Any<TaxIdCreateOptions>());
        });

        await _stripeAdapter.Received(1).DeleteTaxIdAsync(customer.Id, existingTaxId.Id);
        await _stripeAdapter.Received(1).CreateTaxIdAsync(customer.Id, Arg.Is<TaxIdCreateOptions>(
            options => options.Type == "us_ein" && options.Value == "987654321"));
    }

    [Fact]
    public async Task Run_BusinessOrganization_UKTaxIdSentAsEUVAT_CreatesGBVATTaxId()
    {
        var organization = new Organization
        {
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = "sub_123"
        };

        var input = new BillingAddress
        {
            Country = "GB",
            PostalCode = "SW1A 1AA",
            TaxId = new TaxID(TaxIdType.EUVAT, "GB123456789")
        };

        var customer = BusinessCustomer(organization, input);

        _taxService.GetStripeTaxCode("GB", "GB123456789").Returns("gb_vat");

        _stripeAdapter.UpdateCustomerAsync(organization.GatewayCustomerId, Arg.Any<CustomerUpdateOptions>())
            .Returns(customer);

        _stripeAdapter.CreateTaxIdAsync(customer.Id, Arg.Any<TaxIdCreateOptions>())
            .Returns(new TaxId { Type = "gb_vat", Value = input.TaxId.Value });

        var result = await _command.Run(organization, input);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).CreateTaxIdAsync(customer.Id, Arg.Is<TaxIdCreateOptions>(
            options => options.Type == "gb_vat" && options.Value == "GB123456789"));
    }

    [Fact]
    public async Task Run_BusinessOrganization_NorthernIrelandTaxId_CreatesEUVATTaxId()
    {
        var organization = new Organization
        {
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = "sub_123"
        };

        var input = new BillingAddress
        {
            Country = "GB",
            PostalCode = "BT1 5GS",
            TaxId = new TaxID("gb_vat", "XI123456789")
        };

        var customer = BusinessCustomer(organization, input);

        _taxService.GetStripeTaxCode("GB", "XI123456789").Returns(TaxIdType.EUVAT);

        _stripeAdapter.UpdateCustomerAsync(organization.GatewayCustomerId, Arg.Any<CustomerUpdateOptions>())
            .Returns(customer);

        _stripeAdapter.CreateTaxIdAsync(customer.Id, Arg.Any<TaxIdCreateOptions>())
            .Returns(new TaxId { Type = TaxIdType.EUVAT, Value = input.TaxId.Value });

        var result = await _command.Run(organization, input);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).CreateTaxIdAsync(customer.Id, Arg.Is<TaxIdCreateOptions>(
            options => options.Type == TaxIdType.EUVAT && options.Value == "XI123456789"));
    }

    [Fact]
    public async Task Run_BusinessOrganization_SpanishCIFSentAsEUVAT_CreatesBothSpanishNIFAndEUVATTaxIds()
    {
        var organization = new Organization
        {
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = "sub_123"
        };

        var input = new BillingAddress
        {
            Country = "ES",
            PostalCode = "28001",
            TaxId = new TaxID(TaxIdType.EUVAT, "A12345678")
        };

        var customer = BusinessCustomer(organization, input);

        _taxService.GetStripeTaxCode("ES", "A12345678").Returns(TaxIdType.SpanishNIF);

        _stripeAdapter.UpdateCustomerAsync(organization.GatewayCustomerId, Arg.Any<CustomerUpdateOptions>())
            .Returns(customer);

        _stripeAdapter.CreateTaxIdAsync(customer.Id,
                Arg.Is<TaxIdCreateOptions>(options => options.Type == TaxIdType.EUVAT))
            .Returns(new TaxId { Type = TaxIdType.EUVAT, Value = "ESA12345678" });

        var result = await _command.Run(organization, input);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).CreateTaxIdAsync(customer.Id, Arg.Is<TaxIdCreateOptions>(
            options => options.Type == TaxIdType.SpanishNIF && options.Value == "A12345678"));

        await _stripeAdapter.Received(1).CreateTaxIdAsync(customer.Id, Arg.Is<TaxIdCreateOptions>(
            options => options.Type == TaxIdType.EUVAT && options.Value == "ESA12345678"));
    }

    [Fact]
    public async Task Run_BusinessOrganization_CanadianBusinessNumberSentAsGSTHST_CreatesCABNTaxId()
    {
        var organization = new Organization
        {
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = "sub_123"
        };

        var input = new BillingAddress
        {
            Country = "CA",
            PostalCode = "M5H 2N2",
            TaxId = new TaxID("ca_gst_hst", "987654321")
        };

        var customer = BusinessCustomer(organization, input);

        _taxService.GetStripeTaxCode("CA", "987654321").Returns("ca_bn");

        _stripeAdapter.UpdateCustomerAsync(organization.GatewayCustomerId, Arg.Any<CustomerUpdateOptions>())
            .Returns(customer);

        _stripeAdapter.CreateTaxIdAsync(customer.Id, Arg.Any<TaxIdCreateOptions>())
            .Returns(new TaxId { Type = "ca_bn", Value = input.TaxId.Value });

        var result = await _command.Run(organization, input);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).CreateTaxIdAsync(customer.Id, Arg.Is<TaxIdCreateOptions>(
            options => options.Type == "ca_bn" && options.Value == "987654321"));
    }

    [Fact]
    public async Task Run_BusinessOrganization_UnderivableTaxId_FallsBackToClientCodeAndWarns()
    {
        var organization = new Organization
        {
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = "sub_123"
        };

        var input = new BillingAddress
        {
            Country = "MK",
            PostalCode = "1000",
            TaxId = new TaxID(TaxIdType.EUVAT, "MK1234567890123")
        };

        var customer = BusinessCustomer(organization, input);

        _taxService.GetStripeTaxCode("MK", "MK1234567890123").Returns((string?)null);

        _stripeAdapter.UpdateCustomerAsync(organization.GatewayCustomerId, Arg.Any<CustomerUpdateOptions>())
            .Returns(customer);

        _stripeAdapter.CreateTaxIdAsync(customer.Id, Arg.Any<TaxIdCreateOptions>())
            .Returns(new TaxId { Type = TaxIdType.EUVAT, Value = input.TaxId.Value });

        var result = await _command.Run(organization, input);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).CreateTaxIdAsync(customer.Id, Arg.Is<TaxIdCreateOptions>(
            options => options.Type == TaxIdType.EUVAT && options.Value == "MK1234567890123"));

        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("MK") &&
                                    state.ToString()!.Contains(TaxIdType.EUVAT) &&
                                    !state.ToString()!.Contains(input.TaxId.Value)),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Run_PersonalOrganization_SchedulePresent_UpdatesSchedulePhasesAndDefaultSettings()
    {
        var organization = new Organization
        {
            PlanType = PlanType.FamiliesAnnually,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = "sub_123"
        };

        var input = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345",
            Line1 = "123 Main St.",
            City = "New York",
            State = "NY"
        };

        var phase1Start = DateTime.UtcNow.AddDays(-10);
        var phase1End = DateTime.UtcNow.AddDays(5);
        var phase2End = DateTime.UtcNow.AddDays(370);

        var customer = new Customer
        {
            Address = new Address { Country = "US", PostalCode = "12345", Line1 = "123 Main St.", City = "New York", State = "NY" },
            Subscriptions = new StripeList<Subscription>
            {
                Data =
                [
                    new Subscription
                    {
                        Id = organization.GatewaySubscriptionId,
                        CustomerId = organization.GatewayCustomerId,
                        AutomaticTax = new SubscriptionAutomaticTax { Enabled = false }
                    }
                ]
            }
        };

        _stripeAdapter.UpdateCustomerAsync(organization.GatewayCustomerId, Arg.Is<CustomerUpdateOptions>(options =>
            options.Address.Matches(input) &&
            options.HasExpansions("subscriptions", "subscriptions.data.test_clock")
        )).Returns(customer);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sub_sched_123",
                        SubscriptionId = organization.GatewaySubscriptionId,
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
                                Discounts = [new SubscriptionSchedulePhaseDiscount { CouponId = "milestone-3" }],
                                ProrationBehavior = "none"
                            }
                        }
                    }
                ]
            });

        var result = await _command.Run(organization, input);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            Arg.Is("sub_sched_123"),
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.DefaultSettings.AutomaticTax.Enabled == true &&
                o.Phases.Count == 2 &&
                o.Phases[0].AutomaticTax.Enabled == true &&
                o.Phases[0].Items[0].Price == "price_old" &&
                o.Phases[1].AutomaticTax.Enabled == true &&
                o.Phases[1].Items[0].Price == "price_new" &&
                o.Phases[1].Discounts[0].Coupon == "milestone-3"));

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task Run_PersonalOrganization_SchedulePresent_OmitsCustomerDiscountFromActivePhase()
    {
        // The customer coupon is omitted from the active phase so it isn't stacked onto the current
        // period; with no live subscription discounts to carry, the active phase has no explicit
        // discounts. It is still re-listed on the future phase, or it would drop off there.
        var organization = new Organization
        {
            PlanType = PlanType.FamiliesAnnually,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = "sub_123"
        };

        var input = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345",
            Line1 = "123 Main St.",
            City = "New York",
            State = "NY"
        };

        var phase1Start = DateTime.UtcNow.AddDays(-10);
        var phase1End = DateTime.UtcNow.AddDays(5);
        var phase2End = DateTime.UtcNow.AddDays(370);

        var customer = new Customer
        {
            Address = new Address { Country = "US", PostalCode = "12345", Line1 = "123 Main St.", City = "New York", State = "NY" },
            // The fetched customer carries a customer-level discount.
            Discount = new Discount { Source = new DiscountSource { Coupon = new Coupon { Id = "retention" } } },
            Subscriptions = new StripeList<Subscription>
            {
                Data =
                [
                    new Subscription
                    {
                        Id = organization.GatewaySubscriptionId,
                        CustomerId = organization.GatewayCustomerId,
                        AutomaticTax = new SubscriptionAutomaticTax { Enabled = false }
                    }
                ]
            }
        };

        _stripeAdapter.UpdateCustomerAsync(organization.GatewayCustomerId, Arg.Is<CustomerUpdateOptions>(options =>
            options.Address.Matches(input) &&
            options.HasExpansions("subscriptions", "subscriptions.data.test_clock")
        )).Returns(customer);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sub_sched_123",
                        SubscriptionId = organization.GatewaySubscriptionId,
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
                                Discounts = [new SubscriptionSchedulePhaseDiscount { CouponId = "milestone-3" }],
                                ProrationBehavior = "none"
                            }
                        }
                    }
                ]
            });

        var result = await _command.Run(organization, input);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            Arg.Is("sub_sched_123"),
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases.Count == 2 &&
                // Active phase 0: customer coupon omitted, no live subscription discounts to carry.
                o.Phases[0].Discounts == null &&
                // Future phase 1: customer coupon carried in, stacked with the existing milestone.
                o.Phases[1].Discounts.Any(d => d.Coupon == "retention") &&
                o.Phases[1].Discounts.Any(d => d.Coupon == "milestone-3")));

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task Run_PersonalOrganization_Phase2Active_ConsumedMilestoneCouponNotReadded()
    {
        // Phase 1 already ended; phase 2 is now the active phase. Its "milestone-3" coupon was a
        // one-time coupon consumed when phase 2 activated -- it must NOT reappear (it's absent
        // from both phase.Discounts' preserved set, since only future phases preserve, and from
        // subscription.Discounts, since it was consumed). With no live subscription discounts, the
        // active phase carries no explicit discounts; the still-valid customer coupon cascades on
        // its own rather than being stacked on.
        var organization = new Organization
        {
            PlanType = PlanType.FamiliesAnnually,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = "sub_123"
        };

        var input = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345",
            Line1 = "123 Main St.",
            City = "New York",
            State = "NY"
        };

        // Phase 0 already ended; phase 1 is the active (consumed) phase.
        var phase0Start = DateTime.UtcNow.AddDays(-370);
        var phase0End = DateTime.UtcNow.AddDays(-5);
        var phase1End = DateTime.UtcNow.AddDays(360);

        var customer = new Customer
        {
            Address = new Address { Country = "US", PostalCode = "12345", Line1 = "123 Main St.", City = "New York", State = "NY" },
            Discount = new Discount { Source = new DiscountSource { Coupon = new Coupon { Id = "retention" } } },
            Subscriptions = new StripeList<Subscription>
            {
                Data =
                [
                    new Subscription
                    {
                        Id = organization.GatewaySubscriptionId,
                        CustomerId = organization.GatewayCustomerId,
                        AutomaticTax = new SubscriptionAutomaticTax { Enabled = false }
                    }
                ]
            }
        };

        _stripeAdapter.UpdateCustomerAsync(organization.GatewayCustomerId, Arg.Any<CustomerUpdateOptions>())
            .Returns(customer);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sub_sched_123",
                        SubscriptionId = organization.GatewaySubscriptionId,
                        Status = SubscriptionScheduleStatus.Active,
                        Phases = new List<SubscriptionSchedulePhase>
                        {
                            new()
                            {
                                StartDate = phase0Start,
                                EndDate = phase0End,
                                Items = [new SubscriptionSchedulePhaseItem { PriceId = "price_old", Quantity = 1 }],
                                Discounts = [],
                                ProrationBehavior = "none"
                            },
                            new()
                            {
                                StartDate = phase0End,
                                EndDate = phase1End,
                                Items = [new SubscriptionSchedulePhaseItem { PriceId = "price_new", Quantity = 1 }],
                                Discounts = [new SubscriptionSchedulePhaseDiscount { CouponId = "milestone-3" }],
                                ProrationBehavior = "none"
                            }
                        }
                    }
                ]
            });

        var result = await _command.Run(organization, input);

        Assert.True(result.IsT0);

        // Only the active phase remains updatable; the consumed milestone coupon is gone and no
        // explicit discounts are listed, so the customer coupon cascades on its own.
        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            Arg.Is("sub_sched_123"),
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases.Count == 1 &&
                o.Phases[0].Discounts == null));
    }

    [Fact]
    public async Task Run_PersonalOrganization_SchedulePresent_LiveSubscriptionDiscountCarriedOntoEveryPhaseByDiscountId()
    {
        // A discount currently live on the subscription (e.g. a "forever" coupon) is carried onto
        // every rebuilt phase, referenced by its discount id -- not re-granted as a new coupon.
        var organization = new Organization
        {
            PlanType = PlanType.FamiliesAnnually,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = "sub_123"
        };

        var input = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345",
            Line1 = "123 Main St.",
            City = "New York",
            State = "NY"
        };

        var phase1Start = DateTime.UtcNow.AddDays(-10);
        var phase1End = DateTime.UtcNow.AddDays(5);
        var phase2End = DateTime.UtcNow.AddDays(370);

        var subscription = new Subscription
        {
            Id = organization.GatewaySubscriptionId,
            CustomerId = organization.GatewayCustomerId,
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = false },
            Discounts = [new Discount { Id = "di_live", Source = new DiscountSource { Coupon = new Coupon { Id = "live-coupon" } } }]
        };

        var customer = new Customer
        {
            Address = new Address { Country = "US", PostalCode = "12345", Line1 = "123 Main St.", City = "New York", State = "NY" },
            Subscriptions = new StripeList<Subscription> { Data = [subscription] }
        };

        _stripeAdapter.UpdateCustomerAsync(organization.GatewayCustomerId, Arg.Any<CustomerUpdateOptions>())
            .Returns(customer);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sub_sched_123",
                        SubscriptionId = organization.GatewaySubscriptionId,
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

        var result = await _command.Run(organization, input);

        Assert.True(result.IsT0);

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
    public async Task Run_PersonalOrganization_SchedulePresent_ItemLevelCouponsPreservedOnRebuild()
    {
        var organization = new Organization
        {
            PlanType = PlanType.FamiliesAnnually,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = "sub_123"
        };

        var input = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345",
            Line1 = "123 Main St.",
            City = "New York",
            State = "NY"
        };

        var phase1Start = DateTime.UtcNow.AddDays(-10);
        var phase1End = DateTime.UtcNow.AddDays(5);
        var phase2End = DateTime.UtcNow.AddDays(370);

        var customer = new Customer
        {
            Address = new Address { Country = "US", PostalCode = "12345", Line1 = "123 Main St.", City = "New York", State = "NY" },
            Subscriptions = new StripeList<Subscription>
            {
                Data =
                [
                    new Subscription
                    {
                        Id = organization.GatewaySubscriptionId,
                        CustomerId = organization.GatewayCustomerId,
                        AutomaticTax = new SubscriptionAutomaticTax { Enabled = false }
                    }
                ]
            }
        };

        _stripeAdapter.UpdateCustomerAsync(organization.GatewayCustomerId, Arg.Any<CustomerUpdateOptions>())
            .Returns(customer);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sub_sched_123",
                        SubscriptionId = organization.GatewaySubscriptionId,
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

        var result = await _command.Run(organization, input);

        Assert.True(result.IsT0);

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
    public async Task Run_PersonalOrganization_SchedulePresent_PreservesPhaseMetadata()
    {
        var organization = new Organization
        {
            PlanType = PlanType.FamiliesAnnually,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = "sub_123"
        };

        var input = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345",
            Line1 = "123 Main St.",
            City = "New York",
            State = "NY"
        };

        var phase1Start = DateTime.UtcNow.AddDays(-10);
        var phase1End = DateTime.UtcNow.AddDays(5);
        var phase2End = DateTime.UtcNow.AddDays(370);

        var customer = new Customer
        {
            Address = new Address { Country = "US", PostalCode = "12345", Line1 = "123 Main St.", City = "New York", State = "NY" },
            Subscriptions = new StripeList<Subscription>
            {
                Data =
                [
                    new Subscription
                    {
                        Id = organization.GatewaySubscriptionId,
                        CustomerId = organization.GatewayCustomerId,
                        AutomaticTax = new SubscriptionAutomaticTax { Enabled = false }
                    }
                ]
            }
        };

        _stripeAdapter.UpdateCustomerAsync(organization.GatewayCustomerId, Arg.Any<CustomerUpdateOptions>())
            .Returns(customer);

        // The non-cohort key pins the passthrough as unconditional — nothing filters by key.
        var phaseMetadata = new Dictionary<string, string>
        {
            { MetadataKeys.MigrationCohortId, "cohort_123" },
            { MetadataKeys.MigrationCohortName, "Families 2020 Annual" },
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
                        SubscriptionId = organization.GatewaySubscriptionId,
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
                                Discounts = [new SubscriptionSchedulePhaseDiscount { CouponId = "milestone-3" }],
                                Metadata = phaseMetadata,
                                ProrationBehavior = "none"
                            }
                        }
                    }
                ]
            });

        var result = await _command.Run(organization, input);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            Arg.Is("sub_sched_123"),
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases.Count == 2 &&
                o.Phases[0].Metadata != null &&
                o.Phases[0].Metadata[MetadataKeys.MigrationCohortId] == "cohort_123" &&
                o.Phases[0].Metadata[MetadataKeys.MigrationCohortName] == "Families 2020 Annual" &&
                o.Phases[0].Metadata["unrelated_key"] == "unrelated_value" &&
                o.Phases[1].Metadata != null &&
                o.Phases[1].Metadata[MetadataKeys.MigrationCohortId] == "cohort_123" &&
                o.Phases[1].Metadata[MetadataKeys.MigrationCohortName] == "Families 2020 Annual" &&
                o.Phases[1].Metadata["unrelated_key"] == "unrelated_value"));
    }

    [Fact]
    public async Task Run_PersonalOrganization_SchedulePresent_PhaseMetadataEmpty_StaysEmpty()
    {
        var organization = new Organization
        {
            PlanType = PlanType.FamiliesAnnually,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = "sub_123"
        };

        var input = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345",
            Line1 = "123 Main St.",
            City = "New York",
            State = "NY"
        };

        var phase1Start = DateTime.UtcNow.AddDays(-10);
        var phase1End = DateTime.UtcNow.AddDays(5);
        var phase2End = DateTime.UtcNow.AddDays(370);

        var customer = new Customer
        {
            Address = new Address { Country = "US", PostalCode = "12345", Line1 = "123 Main St.", City = "New York", State = "NY" },
            Subscriptions = new StripeList<Subscription>
            {
                Data =
                [
                    new Subscription
                    {
                        Id = organization.GatewaySubscriptionId,
                        CustomerId = organization.GatewayCustomerId,
                        AutomaticTax = new SubscriptionAutomaticTax { Enabled = false }
                    }
                ]
            }
        };

        _stripeAdapter.UpdateCustomerAsync(organization.GatewayCustomerId, Arg.Any<CustomerUpdateOptions>())
            .Returns(customer);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sub_sched_123",
                        SubscriptionId = organization.GatewaySubscriptionId,
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

        var result = await _command.Run(organization, input);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            Arg.Is("sub_sched_123"),
            Arg.Is<SubscriptionScheduleUpdateOptions>(o =>
                o.Phases.Count == 2 &&
                o.Phases[0].Metadata != null && o.Phases[0].Metadata.Count == 0 &&
                o.Phases[1].Metadata != null && o.Phases[1].Metadata.Count == 0));
    }

    [Fact]
    public async Task Run_PersonalOrganization_MultiPhaseSchedule_PreservesMetadataOnEveryRebuiltPhase()
    {
        // Phase 0 has already ended, so it is skipped and the surviving phases shift down by one.
        // Distinct metadata per phase is what catches an index-shift bug; a uniform dict would hide it.
        var organization = new Organization
        {
            PlanType = PlanType.FamiliesAnnually,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = "sub_123"
        };

        var input = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345",
            Line1 = "123 Main St.",
            City = "New York",
            State = "NY"
        };

        var phase0Start = DateTime.UtcNow.AddDays(-370);
        var phase0End = DateTime.UtcNow.AddDays(-5);
        var phase1End = DateTime.UtcNow.AddDays(360);
        var phase2End = DateTime.UtcNow.AddDays(725);

        var customer = new Customer
        {
            Address = new Address { Country = "US", PostalCode = "12345", Line1 = "123 Main St.", City = "New York", State = "NY" },
            Subscriptions = new StripeList<Subscription>
            {
                Data =
                [
                    new Subscription
                    {
                        Id = organization.GatewaySubscriptionId,
                        CustomerId = organization.GatewayCustomerId,
                        AutomaticTax = new SubscriptionAutomaticTax { Enabled = false }
                    }
                ]
            }
        };

        _stripeAdapter.UpdateCustomerAsync(organization.GatewayCustomerId, Arg.Any<CustomerUpdateOptions>())
            .Returns(customer);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sub_sched_123",
                        SubscriptionId = organization.GatewaySubscriptionId,
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

        var result = await _command.Run(organization, input);

        Assert.True(result.IsT0);

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
    public async Task Run_PersonalOrganization_NoSchedule_UpdatesSubscriptionDirectly()
    {
        var organization = new Organization
        {
            PlanType = PlanType.FamiliesAnnually,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = "sub_123"
        };

        var input = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345",
            Line1 = "123 Main St.",
            City = "New York",
            State = "NY"
        };

        var customer = new Customer
        {
            Address = new Address { Country = "US", PostalCode = "12345", Line1 = "123 Main St.", City = "New York", State = "NY" },
            Subscriptions = new StripeList<Subscription>
            {
                Data =
                [
                    new Subscription
                    {
                        Id = organization.GatewaySubscriptionId,
                        CustomerId = organization.GatewayCustomerId,
                        AutomaticTax = new SubscriptionAutomaticTax { Enabled = false }
                    }
                ]
            }
        };

        _stripeAdapter.UpdateCustomerAsync(organization.GatewayCustomerId, Arg.Is<CustomerUpdateOptions>(options =>
            options.Address.Matches(input) &&
            options.HasExpansions("subscriptions", "subscriptions.data.test_clock")
        )).Returns(customer);

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = new List<SubscriptionSchedule>() });

        var result = await _command.Run(organization, input);

        Assert.True(result.IsT0);

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionScheduleAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionScheduleUpdateOptions>());

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(organization.GatewaySubscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(options => options.AutomaticTax.Enabled == true));
    }

    [Fact]
    public async Task Run_BusinessOrganization_DoesNotSetTaxExempt()
    {
        var organization = new Organization
        {
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = "sub_123"
        };

        var input = new BillingAddress { Country = "DE", PostalCode = "10115" };

        var customer = new Customer
        {
            Address = new Address { Country = "DE", PostalCode = "10115" },
            Id = organization.GatewayCustomerId,
            Subscriptions = new StripeList<Subscription>
            {
                Data =
                [
                    new Subscription
                    {
                        Id = organization.GatewaySubscriptionId,
                        AutomaticTax = new SubscriptionAutomaticTax { Enabled = true }
                    }
                ]
            }
        };

        _stripeAdapter.UpdateCustomerAsync(organization.GatewayCustomerId, Arg.Any<CustomerUpdateOptions>())
            .Returns(customer);

        await _command.Run(organization, input);

        await _stripeAdapter.Received(1).UpdateCustomerAsync(organization.GatewayCustomerId,
            Arg.Is<CustomerUpdateOptions>(options =>
                options.Address.Country == "DE" &&
                options.TaxExempt == null));

        await _stripeAdapter.DidNotReceive().GetCustomerAsync(organization.GatewayCustomerId);
    }

    private static Customer BusinessCustomer(Organization organization, BillingAddress billingAddress) => new()
    {
        Address = new Address { Country = billingAddress.Country, PostalCode = billingAddress.PostalCode },
        Id = organization.GatewayCustomerId,
        Subscriptions = new StripeList<Subscription>
        {
            Data =
            [
                new Subscription
                {
                    Id = organization.GatewaySubscriptionId,
                    AutomaticTax = new SubscriptionAutomaticTax { Enabled = true }
                }
            ]
        }
    };
}
