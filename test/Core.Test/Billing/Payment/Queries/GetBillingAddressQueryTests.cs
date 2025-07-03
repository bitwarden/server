using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Test.Billing.Extensions;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Payment.Queries;

public class GetBillingAddressQueryTests
{
    private readonly ISubscriberService _subscriberService = Substitute.For<ISubscriberService>();
    private readonly GetBillingAddressQuery _query;

    public GetBillingAddressQueryTests()
    {
        _query = new GetBillingAddressQuery(_subscriberService);
    }

    [Fact]
    public async Task Run_ForUserWithNoAddress_ReturnsNull()
    {
        var user = new User();

        var customer = new Customer();

        _subscriberService.GetCustomer(user, Arg.Is<CustomerGetOptions>(
            options => options.Expand == null)).Returns(customer);

        var billingAddress = await _query.Run(user);

        Assert.Null(billingAddress);
    }

    [Fact]
    public async Task Run_ForUserWithAddress_ReturnsBillingAddress()
    {
        var user = new User();

        var address = GetAddress();

        var customer = new Customer
        {
            Address = address
        };

        _subscriberService.GetCustomer(user, Arg.Is<CustomerGetOptions>(
            options => options.Expand == null)).Returns(customer);

        var billingAddress = await _query.Run(user);

        AssertEquality(address, billingAddress);
    }

    [Fact]
    public async Task Run_ForPersonalOrganizationWithNoAddress_ReturnsNull()
    {
        var organization = new Organization
        {
            PlanType = PlanType.FamiliesAnnually
        };

        var customer = new Customer();

        _subscriberService.GetCustomer(organization, Arg.Is<CustomerGetOptions>(
            options => options.Expand == null)).Returns(customer);

        var billingAddress = await _query.Run(organization);

        Assert.Null(billingAddress);
    }

    [Fact]
    public async Task Run_ForPersonalOrganizationWithAddress_ReturnsBillingAddress()
    {
        var organization = new Organization
        {
            PlanType = PlanType.FamiliesAnnually
        };

        var address = GetAddress();

        var customer = new Customer
        {
            Address = address
        };

        _subscriberService.GetCustomer(organization, Arg.Is<CustomerGetOptions>(
            options => options.Expand == null)).Returns(customer);

        var billingAddress = await _query.Run(organization);

        AssertEquality(customer.Address, billingAddress);
    }

    [Fact]
    public async Task Run_ForBusinessOrganizationWithNoAddress_ReturnsNull()
    {
        var organization = new Organization
        {
            PlanType = PlanType.EnterpriseAnnually
        };

        var customer = new Customer();

        _subscriberService.GetCustomer(organization, Arg.Is<CustomerGetOptions>(
            options => options.HasExpansions("tax_ids"))).Returns(customer);

        var billingAddress = await _query.Run(organization);

        Assert.Null(billingAddress);
    }

    [Fact]
    public async Task Run_ForBusinessOrganizationWithAddressAndTaxId_ReturnsBillingAddressWithTaxId()
    {
        var organization = new Organization
        {
            PlanType = PlanType.EnterpriseAnnually
        };

        var address = GetAddress();

        var taxId = GetTaxId();

        var customer = new Customer
        {
            Address = address,
            TaxIds = new StripeList<TaxId>
            {
                Data = [taxId]
            }
        };

        _subscriberService.GetCustomer(organization, Arg.Is<CustomerGetOptions>(
            options => options.HasExpansions("tax_ids"))).Returns(customer);

        var billingAddress = await _query.Run(organization);

        AssertEquality(address, taxId, billingAddress);
    }

    [Fact]
    public async Task Run_ForProviderWithAddressAndTaxId_ReturnsBillingAddressWithTaxId()
    {
        var provider = new Provider();

        var address = GetAddress();

        var taxId = GetTaxId();

        var customer = new Customer
        {
            Address = address,
            TaxIds = new StripeList<TaxId>
            {
                Data = [taxId]
            }
        };

        _subscriberService.GetCustomer(provider, Arg.Is<CustomerGetOptions>(
            options => options.HasExpansions("tax_ids"))).Returns(customer);

        var billingAddress = await _query.Run(provider);

        AssertEquality(address, taxId, billingAddress);
    }

    private static void AssertEquality(Address address, BillingAddress? billingAddress)
    {
        Assert.NotNull(billingAddress);
        Assert.Equal(address.Country, billingAddress.Country);
        Assert.Equal(address.PostalCode, billingAddress.PostalCode);
        Assert.Equal(address.Line1, billingAddress.Line1);
        Assert.Equal(address.Line2, billingAddress.Line2);
        Assert.Equal(address.City, billingAddress.City);
        Assert.Equal(address.State, billingAddress.State);
    }

    private static void AssertEquality(Address address, TaxId taxId, BillingAddress? billingAddress)
    {
        AssertEquality(address, billingAddress);
        Assert.NotNull(billingAddress!.TaxId);
        Assert.Equal(taxId.Type, billingAddress.TaxId!.Code);
        Assert.Equal(taxId.Value, billingAddress.TaxId!.Value);
    }

    private static Address GetAddress() => new()
    {
        Country = "US",
        PostalCode = "12345",
        Line1 = "123 Main St.",
        Line2 = "Suite 100",
        City = "New York",
        State = "NY"
    };

    private static TaxId GetTaxId() => new() { Type = "us_ein", Value = "123456789" };
}
