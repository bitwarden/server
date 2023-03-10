using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Braintree;
using NSubstitute;
using Stripe;
using Xunit;
using Customer = Braintree.Customer;
using PaymentMethod = Braintree.PaymentMethod;
using PaymentMethodType = Bit.Core.Enums.PaymentMethodType;
using TaxRate = Bit.Core.Entities.TaxRate;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class StripePaymentServiceTests
{
    [Theory]
    [BitAutoData(PaymentMethodType.BitPay)]
    [BitAutoData(PaymentMethodType.BitPay)]
    [BitAutoData(PaymentMethodType.Credit)]
    [BitAutoData(PaymentMethodType.WireTransfer)]
    [BitAutoData(PaymentMethodType.AppleInApp)]
    [BitAutoData(PaymentMethodType.GoogleInApp)]
    [BitAutoData(PaymentMethodType.Check)]
    public async void PurchaseOrganizationAsync_Invalid(PaymentMethodType paymentMethodType, SutProvider<StripePaymentService> sutProvider)
    {
        var exception = await Assert.ThrowsAsync<GatewayException>(
            () => sutProvider.Sut.PurchaseOrganizationAsync(null, paymentMethodType, null, null, 0, 0, false, null));

        Assert.Equal("Payment method is not supported at this time.", exception.Message);
    }

    [Theory, BitAutoData]
    public async void PurchaseOrganizationAsync_Stripe(SutProvider<StripePaymentService> sutProvider, Organization organization, string paymentToken, TaxInfo taxInfo)
    {
        var plan = StaticStore.Plans.First(p => p.Type == PlanType.EnterpriseAnnually);

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        stripeAdapter.CustomerCreateAsync(default).ReturnsForAnyArgs(new Stripe.Customer
        {
            Id = "C-1",
        });
        stripeAdapter.SubscriptionCreateAsync(default).ReturnsForAnyArgs(new Stripe.Subscription
        {
            Id = "S-1",
            CurrentPeriodEnd = DateTime.Today.AddDays(10),
        });

        var result = await sutProvider.Sut.PurchaseOrganizationAsync(organization, PaymentMethodType.Card, paymentToken, plan, 0, 0, false, taxInfo);

        Assert.Null(result);
        Assert.Equal(GatewayType.Stripe, organization.Gateway);
        Assert.Equal("C-1", organization.GatewayCustomerId);
        Assert.Equal("S-1", organization.GatewaySubscriptionId);
        Assert.True(organization.Enabled);
        Assert.Equal(DateTime.Today.AddDays(10), organization.ExpirationDate);

        await stripeAdapter.Received().CustomerCreateAsync(Arg.Is<Stripe.CustomerCreateOptions>(c =>
            c.Description == organization.BusinessName &&
            c.Email == organization.BillingEmail &&
            c.Source == paymentToken &&
            c.PaymentMethod == null &&
            !c.Metadata.Any() &&
            c.InvoiceSettings.DefaultPaymentMethod == null &&
            c.InvoiceSettings.CustomFields != null &&
            c.InvoiceSettings.CustomFields[0].Name == "Subscriber" &&
            c.InvoiceSettings.CustomFields[0].Value == organization.SubscriberName() &&
            c.Address.Country == taxInfo.BillingAddressCountry &&
            c.Address.PostalCode == taxInfo.BillingAddressPostalCode &&
            c.Address.Line1 == taxInfo.BillingAddressLine1 &&
            c.Address.Line2 == taxInfo.BillingAddressLine2 &&
            c.Address.City == taxInfo.BillingAddressCity &&
            c.Address.State == taxInfo.BillingAddressState &&
            c.TaxIdData == null
        ));

        await stripeAdapter.Received().SubscriptionCreateAsync(Arg.Is<Stripe.SubscriptionCreateOptions>(s =>
            s.Customer == "C-1" &&
            s.Expand[0] == "latest_invoice.payment_intent" &&
            s.Metadata[organization.GatewayIdField()] == organization.Id.ToString() &&
            s.Items.Count == 0
        ));
    }

    [Theory, BitAutoData]
    public async void PurchaseOrganizationAsync_Stripe_PM(SutProvider<StripePaymentService> sutProvider, Organization organization, string paymentToken, TaxInfo taxInfo)
    {
        var plan = StaticStore.Plans.First(p => p.Type == PlanType.EnterpriseAnnually);
        paymentToken = "pm_" + paymentToken;

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        stripeAdapter.CustomerCreateAsync(default).ReturnsForAnyArgs(new Stripe.Customer
        {
            Id = "C-1",
        });
        stripeAdapter.SubscriptionCreateAsync(default).ReturnsForAnyArgs(new Stripe.Subscription
        {
            Id = "S-1",
            CurrentPeriodEnd = DateTime.Today.AddDays(10),
        });

        var result = await sutProvider.Sut.PurchaseOrganizationAsync(organization, PaymentMethodType.Card, paymentToken, plan, 0, 0, false, taxInfo);

        Assert.Null(result);
        Assert.Equal(GatewayType.Stripe, organization.Gateway);
        Assert.Equal("C-1", organization.GatewayCustomerId);
        Assert.Equal("S-1", organization.GatewaySubscriptionId);
        Assert.True(organization.Enabled);
        Assert.Equal(DateTime.Today.AddDays(10), organization.ExpirationDate);

        await stripeAdapter.Received().CustomerCreateAsync(Arg.Is<Stripe.CustomerCreateOptions>(c =>
            c.Description == organization.BusinessName &&
            c.Email == organization.BillingEmail &&
            c.Source == null &&
            c.PaymentMethod == paymentToken &&
            !c.Metadata.Any() &&
            c.InvoiceSettings.DefaultPaymentMethod == paymentToken &&
            c.InvoiceSettings.CustomFields != null &&
            c.InvoiceSettings.CustomFields[0].Name == "Subscriber" &&
            c.InvoiceSettings.CustomFields[0].Value == organization.SubscriberName() &&
            c.Address.Country == taxInfo.BillingAddressCountry &&
            c.Address.PostalCode == taxInfo.BillingAddressPostalCode &&
            c.Address.Line1 == taxInfo.BillingAddressLine1 &&
            c.Address.Line2 == taxInfo.BillingAddressLine2 &&
            c.Address.City == taxInfo.BillingAddressCity &&
            c.Address.State == taxInfo.BillingAddressState &&
            c.TaxIdData == null
        ));

        await stripeAdapter.Received().SubscriptionCreateAsync(Arg.Is<Stripe.SubscriptionCreateOptions>(s =>
            s.Customer == "C-1" &&
            s.Expand[0] == "latest_invoice.payment_intent" &&
            s.Metadata[organization.GatewayIdField()] == organization.Id.ToString() &&
            s.Items.Count == 0
        ));
    }

    [Theory, BitAutoData]
    public async void PurchaseOrganizationAsync_Stripe_TaxRate(SutProvider<StripePaymentService> sutProvider, Organization organization, string paymentToken, TaxInfo taxInfo)
    {
        var plan = StaticStore.Plans.First(p => p.Type == PlanType.EnterpriseAnnually);

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        stripeAdapter.CustomerCreateAsync(default).ReturnsForAnyArgs(new Stripe.Customer
        {
            Id = "C-1",
        });
        stripeAdapter.SubscriptionCreateAsync(default).ReturnsForAnyArgs(new Stripe.Subscription
        {
            Id = "S-1",
            CurrentPeriodEnd = DateTime.Today.AddDays(10),
        });
        sutProvider.GetDependency<ITaxRateRepository>().GetByLocationAsync(Arg.Is<TaxRate>(t =>
                t.Country == taxInfo.BillingAddressCountry && t.PostalCode == taxInfo.BillingAddressPostalCode))
            .Returns(new List<TaxRate> { new() { Id = "T-1" } });

        var result = await sutProvider.Sut.PurchaseOrganizationAsync(organization, PaymentMethodType.Card, paymentToken, plan, 0, 0, false, taxInfo);

        Assert.Null(result);

        await stripeAdapter.Received().SubscriptionCreateAsync(Arg.Is<Stripe.SubscriptionCreateOptions>(s =>
            s.DefaultTaxRates.Count == 1 &&
            s.DefaultTaxRates[0] == "T-1"
        ));
    }

    [Theory, BitAutoData]
    public async void PurchaseOrganizationAsync_Stripe_Declined(SutProvider<StripePaymentService> sutProvider, Organization organization, string paymentToken, TaxInfo taxInfo)
    {
        var plan = StaticStore.Plans.First(p => p.Type == PlanType.EnterpriseAnnually);
        paymentToken = "pm_" + paymentToken;

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        stripeAdapter.CustomerCreateAsync(default).ReturnsForAnyArgs(new Stripe.Customer
        {
            Id = "C-1",
        });
        stripeAdapter.SubscriptionCreateAsync(default).ReturnsForAnyArgs(new Stripe.Subscription
        {
            Id = "S-1",
            CurrentPeriodEnd = DateTime.Today.AddDays(10),
            Status = "incomplete",
            LatestInvoice = new Stripe.Invoice
            {
                PaymentIntent = new Stripe.PaymentIntent
                {
                    Status = "requires_payment_method",
                },
            },
        });

        var exception = await Assert.ThrowsAsync<GatewayException>(
            () => sutProvider.Sut.PurchaseOrganizationAsync(organization, PaymentMethodType.Card, paymentToken, plan, 0, 0, false, taxInfo));

        Assert.Equal("Payment method was declined.", exception.Message);

        await stripeAdapter.Received(1).CustomerDeleteAsync("C-1");
    }

    [Theory, BitAutoData]
    public async void PurchaseOrganizationAsync_Stripe_RequiresAction(SutProvider<StripePaymentService> sutProvider, Organization organization, string paymentToken, TaxInfo taxInfo)
    {
        var plan = StaticStore.Plans.First(p => p.Type == PlanType.EnterpriseAnnually);

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        stripeAdapter.CustomerCreateAsync(default).ReturnsForAnyArgs(new Stripe.Customer
        {
            Id = "C-1",
        });
        stripeAdapter.SubscriptionCreateAsync(default).ReturnsForAnyArgs(new Stripe.Subscription
        {
            Id = "S-1",
            CurrentPeriodEnd = DateTime.Today.AddDays(10),
            Status = "incomplete",
            LatestInvoice = new Stripe.Invoice
            {
                PaymentIntent = new Stripe.PaymentIntent
                {
                    Status = "requires_action",
                    ClientSecret = "clientSecret",
                },
            },
        });

        var result = await sutProvider.Sut.PurchaseOrganizationAsync(organization, PaymentMethodType.Card, paymentToken, plan, 0, 0, false, taxInfo);

        Assert.Equal("clientSecret", result);
        Assert.False(organization.Enabled);
    }

    [Theory, BitAutoData]
    public async void PurchaseOrganizationAsync_Paypal(SutProvider<StripePaymentService> sutProvider, Organization organization, string paymentToken, TaxInfo taxInfo)
    {
        var plan = StaticStore.Plans.First(p => p.Type == PlanType.EnterpriseAnnually);

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        stripeAdapter.CustomerCreateAsync(default).ReturnsForAnyArgs(new Stripe.Customer
        {
            Id = "C-1",
        });
        stripeAdapter.SubscriptionCreateAsync(default).ReturnsForAnyArgs(new Stripe.Subscription
        {
            Id = "S-1",
            CurrentPeriodEnd = DateTime.Today.AddDays(10),
        });

        var customer = Substitute.For<Customer>();
        customer.Id.ReturnsForAnyArgs("Braintree-Id");
        customer.PaymentMethods.ReturnsForAnyArgs(new[] { Substitute.For<PaymentMethod>() });
        var customerResult = Substitute.For<Result<Customer>>();
        customerResult.IsSuccess().Returns(true);
        customerResult.Target.ReturnsForAnyArgs(customer);

        var braintreeGateway = sutProvider.GetDependency<IBraintreeGateway>();
        braintreeGateway.Customer.CreateAsync(default).ReturnsForAnyArgs(customerResult);

        var result = await sutProvider.Sut.PurchaseOrganizationAsync(organization, PaymentMethodType.PayPal, paymentToken, plan, 0, 0, false, taxInfo);

        Assert.Null(result);
        Assert.Equal(GatewayType.Stripe, organization.Gateway);
        Assert.Equal("C-1", organization.GatewayCustomerId);
        Assert.Equal("S-1", organization.GatewaySubscriptionId);
        Assert.True(organization.Enabled);
        Assert.Equal(DateTime.Today.AddDays(10), organization.ExpirationDate);

        await stripeAdapter.Received().CustomerCreateAsync(Arg.Is<Stripe.CustomerCreateOptions>(c =>
            c.Description == organization.BusinessName &&
            c.Email == organization.BillingEmail &&
            c.PaymentMethod == null &&
            c.Metadata.Count == 1 &&
            c.Metadata["btCustomerId"] == "Braintree-Id" &&
            c.InvoiceSettings.DefaultPaymentMethod == null &&
            c.Address.Country == taxInfo.BillingAddressCountry &&
            c.Address.PostalCode == taxInfo.BillingAddressPostalCode &&
            c.Address.Line1 == taxInfo.BillingAddressLine1 &&
            c.Address.Line2 == taxInfo.BillingAddressLine2 &&
            c.Address.City == taxInfo.BillingAddressCity &&
            c.Address.State == taxInfo.BillingAddressState &&
            c.TaxIdData == null
        ));

        await stripeAdapter.Received().SubscriptionCreateAsync(Arg.Is<Stripe.SubscriptionCreateOptions>(s =>
            s.Customer == "C-1" &&
            s.Expand[0] == "latest_invoice.payment_intent" &&
            s.Metadata[organization.GatewayIdField()] == organization.Id.ToString() &&
            s.Items.Count == 0
        ));
    }

    [Theory, BitAutoData]
    public async void PurchaseOrganizationAsync_Paypal_FailedCreate(SutProvider<StripePaymentService> sutProvider, Organization organization, string paymentToken, TaxInfo taxInfo)
    {
        var plan = StaticStore.Plans.First(p => p.Type == PlanType.EnterpriseAnnually);

        var customerResult = Substitute.For<Result<Customer>>();
        customerResult.IsSuccess().Returns(false);

        var braintreeGateway = sutProvider.GetDependency<IBraintreeGateway>();
        braintreeGateway.Customer.CreateAsync(default).ReturnsForAnyArgs(customerResult);

        var exception = await Assert.ThrowsAsync<GatewayException>(
            () => sutProvider.Sut.PurchaseOrganizationAsync(organization, PaymentMethodType.PayPal, paymentToken, plan, 0, 0, false, taxInfo));

        Assert.Equal("Failed to create PayPal customer record.", exception.Message);
    }

    [Theory, BitAutoData]
    public async void PurchaseOrganizationAsync_PayPal_Declined(SutProvider<StripePaymentService> sutProvider, Organization organization, string paymentToken, TaxInfo taxInfo)
    {
        var plan = StaticStore.Plans.First(p => p.Type == PlanType.EnterpriseAnnually);
        paymentToken = "pm_" + paymentToken;

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        stripeAdapter.CustomerCreateAsync(default).ReturnsForAnyArgs(new Stripe.Customer
        {
            Id = "C-1",
        });
        stripeAdapter.SubscriptionCreateAsync(default).ReturnsForAnyArgs(new Stripe.Subscription
        {
            Id = "S-1",
            CurrentPeriodEnd = DateTime.Today.AddDays(10),
            Status = "incomplete",
            LatestInvoice = new Stripe.Invoice
            {
                PaymentIntent = new Stripe.PaymentIntent
                {
                    Status = "requires_payment_method",
                },
            },
        });

        var customer = Substitute.For<Customer>();
        customer.Id.ReturnsForAnyArgs("Braintree-Id");
        customer.PaymentMethods.ReturnsForAnyArgs(new[] { Substitute.For<PaymentMethod>() });
        var customerResult = Substitute.For<Result<Customer>>();
        customerResult.IsSuccess().Returns(true);
        customerResult.Target.ReturnsForAnyArgs(customer);

        var braintreeGateway = sutProvider.GetDependency<IBraintreeGateway>();
        braintreeGateway.Customer.CreateAsync(default).ReturnsForAnyArgs(customerResult);

        var exception = await Assert.ThrowsAsync<GatewayException>(
            () => sutProvider.Sut.PurchaseOrganizationAsync(organization, PaymentMethodType.PayPal, paymentToken, plan, 0, 0, false, taxInfo));

        Assert.Equal("Payment method was declined.", exception.Message);

        await stripeAdapter.Received(1).CustomerDeleteAsync("C-1");
        await braintreeGateway.Customer.Received(1).DeleteAsync("Braintree-Id");
    }

    [Theory, BitAutoData]
    public async void UpgradeFreeOrganizationAsync_Success(SutProvider<StripePaymentService> sutProvider,
        Organization organization, TaxInfo taxInfo)
    {
        organization.GatewaySubscriptionId = null;
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        stripeAdapter.CustomerGetAsync(default).ReturnsForAnyArgs(new Stripe.Customer
        {
            Id = "C-1",
            Metadata = new Dictionary<string, string>
            {
                { "btCustomerId", "B-123" },
            }
        });
        stripeAdapter.InvoiceUpcomingAsync(default).ReturnsForAnyArgs(new Stripe.Invoice
        {
            PaymentIntent = new Stripe.PaymentIntent { Status = "requires_payment_method", },
            AmountDue = 0
        });
        stripeAdapter.SubscriptionCreateAsync(default).ReturnsForAnyArgs(new Stripe.Subscription { });

        var plan = StaticStore.Plans.First(p => p.Type == PlanType.EnterpriseAnnually);
        var result = await sutProvider.Sut.UpgradeFreeOrganizationAsync(organization, plan, 0, 0, false, taxInfo);

        Assert.Null(result);
    }
}
