using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Payment.Commands;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Services;
using Bit.Core.Test.Billing.Extensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Payment.Commands;

using static StripeConstants;

public class UpdateBillingAddressCommandTests
{
    private readonly IStripeAdapter _stripeAdapter;
    private readonly UpdateBillingAddressCommand _command;

    public UpdateBillingAddressCommandTests()
    {
        _stripeAdapter = Substitute.For<IStripeAdapter>();
        _command = new UpdateBillingAddressCommand(
            Substitute.For<ILogger<UpdateBillingAddressCommand>>(),
            _stripeAdapter);
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

        _stripeAdapter.CustomerUpdateAsync(organization.GatewayCustomerId, Arg.Is<CustomerUpdateOptions>(options =>
            options.Address.Matches(input) &&
            options.HasExpansions("subscriptions")
        )).Returns(customer);

        var result = await _command.Run(organization, input);

        Assert.True(result.IsT0);
        var output = result.AsT0;
        Assert.Equivalent(input, output);

        await _stripeAdapter.Received(1).SubscriptionUpdateAsync(organization.GatewaySubscriptionId,
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

        _stripeAdapter.CustomerUpdateAsync(organization.GatewayCustomerId, Arg.Is<CustomerUpdateOptions>(options =>
            options.Address.Matches(input) &&
            options.HasExpansions("subscriptions", "tax_ids") &&
            options.TaxExempt == TaxExempt.None
        )).Returns(customer);

        var result = await _command.Run(organization, input);

        Assert.True(result.IsT0);
        var output = result.AsT0;
        Assert.Equivalent(input, output);

        await _stripeAdapter.Received(1).SubscriptionUpdateAsync(organization.GatewaySubscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(options => options.AutomaticTax.Enabled == true));
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

        _stripeAdapter.CustomerUpdateAsync(organization.GatewayCustomerId, Arg.Is<CustomerUpdateOptions>(options =>
            options.Address.Matches(input) &&
            options.HasExpansions("subscriptions", "tax_ids") &&
            options.TaxExempt == TaxExempt.None
        )).Returns(customer);

        var result = await _command.Run(organization, input);

        Assert.True(result.IsT0);
        var output = result.AsT0;
        Assert.Equivalent(input, output);

        await _stripeAdapter.Received(1).SubscriptionUpdateAsync(organization.GatewaySubscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(options => options.AutomaticTax.Enabled == true));

        await _stripeAdapter.Received(1).TaxIdDeleteAsync(customer.Id, "tax_id_123");
    }

    [Fact]
    public async Task Run_NonUSBusinessOrganization_MakesCorrectInvocations_ReturnsBillingAddress()
    {
        var organization = new Organization
        {
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = "sub_123"
        };

        var input = new BillingAddress
        {
            Country = "DE",
            PostalCode = "10115",
            Line1 = "Friedrichstraße 123",
            Line2 = "Stock 3",
            City = "Berlin",
            State = "Berlin"
        };

        var customer = new Customer
        {
            Address = new Address
            {
                Country = "DE",
                PostalCode = "10115",
                Line1 = "Friedrichstraße 123",
                Line2 = "Stock 3",
                City = "Berlin",
                State = "Berlin"
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

        _stripeAdapter.CustomerUpdateAsync(organization.GatewayCustomerId, Arg.Is<CustomerUpdateOptions>(options =>
            options.Address.Matches(input) &&
            options.HasExpansions("subscriptions", "tax_ids") &&
            options.TaxExempt == TaxExempt.Reverse
        )).Returns(customer);

        var result = await _command.Run(organization, input);

        Assert.True(result.IsT0);
        var output = result.AsT0;
        Assert.Equivalent(input, output);

        await _stripeAdapter.Received(1).SubscriptionUpdateAsync(organization.GatewaySubscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(options => options.AutomaticTax.Enabled == true));
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

        _stripeAdapter.CustomerUpdateAsync(organization.GatewayCustomerId, Arg.Is<CustomerUpdateOptions>(options =>
            options.Address.Matches(input) &&
            options.HasExpansions("subscriptions", "tax_ids") &&
            options.TaxExempt == TaxExempt.Reverse
        )).Returns(customer);

        _stripeAdapter
            .TaxIdCreateAsync(customer.Id,
                Arg.Is<TaxIdCreateOptions>(options => options.Type == TaxIdType.EUVAT))
            .Returns(new TaxId { Type = TaxIdType.EUVAT, Value = "ESA12345678" });

        var result = await _command.Run(organization, input);

        Assert.True(result.IsT0);
        var output = result.AsT0;
        Assert.Equivalent(input with { TaxId = new TaxID(TaxIdType.EUVAT, "ESA12345678") }, output);

        await _stripeAdapter.Received(1).SubscriptionUpdateAsync(organization.GatewaySubscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(options => options.AutomaticTax.Enabled == true));

        await _stripeAdapter.Received(1).TaxIdCreateAsync(organization.GatewayCustomerId, Arg.Is<TaxIdCreateOptions>(
            options => options.Type == TaxIdType.SpanishNIF &&
                       options.Value == input.TaxId.Value));
    }
}
