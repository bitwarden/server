using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Braintree;
using NSubstitute;
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
            () => sutProvider.Sut.PurchaseOrganizationAsync(null, paymentMethodType, null, null, 0, 0, false, null, false, -1, -1));

        Assert.Equal("Payment method is not supported at this time.", exception.Message);
    }

    [Theory, BitAutoData]
    public async void PurchaseOrganizationAsync_Stripe_ProviderOrg_Coupon_Add(SutProvider<StripePaymentService> sutProvider, Organization organization, string paymentToken, TaxInfo taxInfo, bool provider = true)
    {
        var plans = StaticStore.Plans.Where(p => p.Type == PlanType.EnterpriseAnnually).ToList();

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
        sutProvider.GetDependency<IGlobalSettings>()
            .BaseServiceUri.CloudRegion
            .Returns("US");

        var result = await sutProvider.Sut.PurchaseOrganizationAsync(organization, PaymentMethodType.Card, paymentToken, plans, 0, 0, false, taxInfo, provider);

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
            c.Coupon == "msp-discount-35" &&
            c.Metadata.Count == 1 &&
            c.Metadata["region"] == "US" &&
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
    public async void PurchaseOrganizationAsync_SM_Stripe_ProviderOrg_Coupon_Add(SutProvider<StripePaymentService> sutProvider, Organization organization,
        string paymentToken, TaxInfo taxInfo, bool provider = true)
    {
        var plans = StaticStore.Plans.Where(p => p.Type == PlanType.EnterpriseAnnually).ToList();

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
        sutProvider.GetDependency<IGlobalSettings>()
            .BaseServiceUri.CloudRegion
            .Returns("US");

        var result = await sutProvider.Sut.PurchaseOrganizationAsync(organization, PaymentMethodType.Card, paymentToken, plans, 1, 1,
            false, taxInfo, provider, 1, 1);

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
            c.Coupon == "msp-discount-35" &&
            c.Metadata.Count == 1 &&
            c.Metadata["region"] == "US" &&
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
            s.Items.Count == 4
        ));
    }

    [Theory, BitAutoData]
    public async void PurchaseOrganizationAsync_Stripe(SutProvider<StripePaymentService> sutProvider, Organization organization, string paymentToken, TaxInfo taxInfo)
    {
        var plans = StaticStore.Plans.Where(p => p.Type == PlanType.EnterpriseAnnually).ToList();

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
        sutProvider.GetDependency<IGlobalSettings>()
            .BaseServiceUri.CloudRegion
            .Returns("US");

        var result = await sutProvider.Sut.PurchaseOrganizationAsync(organization, PaymentMethodType.Card, paymentToken, plans, 0, 0
            , false, taxInfo, false, 8, 10);

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
            c.Metadata.Count == 1 &&
            c.Metadata["region"] == "US" &&
            c.InvoiceSettings.DefaultPaymentMethod == null &&
            c.InvoiceSettings.CustomFields != null &&
            c.InvoiceSettings.CustomFields[0].Name == "Organization" &&
            c.InvoiceSettings.CustomFields[0].Value == organization.SubscriberName().Substring(0, 30) &&
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
            s.Items.Count == 2
        ));
    }

    [Theory, BitAutoData]
    public async void PurchaseOrganizationAsync_Stripe_PM(SutProvider<StripePaymentService> sutProvider, Organization organization, string paymentToken, TaxInfo taxInfo)
    {
        var plans = StaticStore.PasswordManagerPlans.Where(p => p.Type == PlanType.EnterpriseAnnually).ToList();
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
        sutProvider.GetDependency<IGlobalSettings>()
            .BaseServiceUri.CloudRegion
            .Returns("US");

        var result = await sutProvider.Sut.PurchaseOrganizationAsync(organization, PaymentMethodType.Card, paymentToken, plans, 0, 0, false, taxInfo);

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
            c.Metadata.Count == 1 &&
            c.Metadata["region"] == "US" &&
            c.InvoiceSettings.DefaultPaymentMethod == paymentToken &&
            c.InvoiceSettings.CustomFields != null &&
            c.InvoiceSettings.CustomFields[0].Name == "Organization" &&
            c.InvoiceSettings.CustomFields[0].Value == organization.SubscriberName().Substring(0, 30) &&
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
        var plans = StaticStore.Plans.Where(p => p.Type == PlanType.EnterpriseAnnually).ToList();

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

        var result = await sutProvider.Sut.PurchaseOrganizationAsync(organization, PaymentMethodType.Card, paymentToken, plans, 0, 0, false, taxInfo);

        Assert.Null(result);

        await stripeAdapter.Received().SubscriptionCreateAsync(Arg.Is<Stripe.SubscriptionCreateOptions>(s =>
            s.DefaultTaxRates.Count == 1 &&
            s.DefaultTaxRates[0] == "T-1"
        ));
    }

    [Theory, BitAutoData]
    public async void PurchaseOrganizationAsync_Stripe_TaxRate_SM(SutProvider<StripePaymentService> sutProvider, Organization organization, string paymentToken, TaxInfo taxInfo)
    {
        var plans = StaticStore.Plans.Where(p => p.Type == PlanType.EnterpriseAnnually).ToList();

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

        var result = await sutProvider.Sut.PurchaseOrganizationAsync(organization, PaymentMethodType.Card, paymentToken, plans, 2, 2,
            false, taxInfo, false, 2, 2);

        Assert.Null(result);

        await stripeAdapter.Received().SubscriptionCreateAsync(Arg.Is<Stripe.SubscriptionCreateOptions>(s =>
            s.DefaultTaxRates.Count == 1 &&
            s.DefaultTaxRates[0] == "T-1"
        ));
    }

    [Theory, BitAutoData]
    public async void PurchaseOrganizationAsync_Stripe_Declined(SutProvider<StripePaymentService> sutProvider, Organization organization, string paymentToken, TaxInfo taxInfo)
    {
        var plan = StaticStore.Plans.Where(p => p.Type == PlanType.EnterpriseAnnually).ToList();
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
    public async void PurchaseOrganizationAsync_SM_Stripe_Declined(SutProvider<StripePaymentService> sutProvider, Organization organization, string paymentToken, TaxInfo taxInfo)
    {
        var plan = StaticStore.Plans.Where(p => p.Type == PlanType.EnterpriseAnnually).ToList();
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
            () => sutProvider.Sut.PurchaseOrganizationAsync(organization, PaymentMethodType.Card, paymentToken, plan,
                1, 12, false, taxInfo, false, 10, 10));

        Assert.Equal("Payment method was declined.", exception.Message);

        await stripeAdapter.Received(1).CustomerDeleteAsync("C-1");
    }

    [Theory, BitAutoData]
    public async void PurchaseOrganizationAsync_Stripe_RequiresAction(SutProvider<StripePaymentService> sutProvider, Organization organization, string paymentToken, TaxInfo taxInfo)
    {
        var plans = StaticStore.Plans.Where(p => p.Type == PlanType.EnterpriseAnnually).ToList();

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

        var result = await sutProvider.Sut.PurchaseOrganizationAsync(organization, PaymentMethodType.Card, paymentToken, plans, 0, 0, false, taxInfo);

        Assert.Equal("clientSecret", result);
        Assert.False(organization.Enabled);
    }

    [Theory, BitAutoData]
    public async void PurchaseOrganizationAsync_SM_Stripe_RequiresAction(SutProvider<StripePaymentService> sutProvider, Organization organization, string paymentToken, TaxInfo taxInfo)
    {
        var plans = StaticStore.Plans.Where(p => p.Type == PlanType.EnterpriseAnnually).ToList();

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

        var result = await sutProvider.Sut.PurchaseOrganizationAsync(organization, PaymentMethodType.Card, paymentToken, plans,
            10, 10, false, taxInfo, false, 10, 10);

        Assert.Equal("clientSecret", result);
        Assert.False(organization.Enabled);
    }

    [Theory, BitAutoData]
    public async void PurchaseOrganizationAsync_Paypal(SutProvider<StripePaymentService> sutProvider, Organization organization, string paymentToken, TaxInfo taxInfo)
    {
        var plans = StaticStore.Plans.Where(p => p.Type == PlanType.EnterpriseAnnually).ToList();

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

        sutProvider.GetDependency<IGlobalSettings>()
            .BaseServiceUri.CloudRegion
            .Returns("US");

        var customer = Substitute.For<Customer>();
        customer.Id.ReturnsForAnyArgs("Braintree-Id");
        customer.PaymentMethods.ReturnsForAnyArgs(new[] { Substitute.For<PaymentMethod>() });
        var customerResult = Substitute.For<Result<Customer>>();
        customerResult.IsSuccess().Returns(true);
        customerResult.Target.ReturnsForAnyArgs(customer);

        var braintreeGateway = sutProvider.GetDependency<IBraintreeGateway>();
        braintreeGateway.Customer.CreateAsync(default).ReturnsForAnyArgs(customerResult);

        var result = await sutProvider.Sut.PurchaseOrganizationAsync(organization, PaymentMethodType.PayPal, paymentToken, plans, 0, 0, false, taxInfo);

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
            c.Metadata.Count == 2 &&
            c.Metadata["btCustomerId"] == "Braintree-Id" &&
            c.Metadata["region"] == "US" &&
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
    public async void PurchaseOrganizationAsync_SM_Paypal(SutProvider<StripePaymentService> sutProvider, Organization organization, string paymentToken, TaxInfo taxInfo)
    {
        var plans = StaticStore.Plans.Where(p => p.Type == PlanType.EnterpriseAnnually).ToList();
        var passwordManagerPlan = plans.Single(p => p.BitwardenProduct == BitwardenProductType.PasswordManager);
        var secretsManagerPlan = plans.Single(p => p.BitwardenProduct == BitwardenProductType.SecretsManager);

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

        sutProvider.GetDependency<IGlobalSettings>()
            .BaseServiceUri.CloudRegion
            .Returns("US");

        var additionalStorage = (short)2;
        var additionalSeats = 10;
        var additionalSmSeats = 5;
        var additionalServiceAccounts = 20;
        var result = await sutProvider.Sut.PurchaseOrganizationAsync(organization, PaymentMethodType.PayPal, paymentToken, plans,
            additionalStorage, additionalSeats, false, taxInfo, false, additionalSmSeats, additionalServiceAccounts);

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
            c.Metadata.Count == 2 &&
            c.Metadata["region"] == "US" &&
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
            s.Items.Count == 4 &&
            s.Items.Count(i => i.Plan == passwordManagerPlan.StripeSeatPlanId && i.Quantity == additionalSeats) == 1 &&
            s.Items.Count(i => i.Plan == passwordManagerPlan.StripeStoragePlanId && i.Quantity == additionalStorage) == 1 &&
            s.Items.Count(i => i.Plan == secretsManagerPlan.StripeSeatPlanId && i.Quantity == additionalSmSeats) == 1 &&
            s.Items.Count(i => i.Plan == secretsManagerPlan.StripeServiceAccountPlanId && i.Quantity == additionalServiceAccounts) == 1
        ));
    }

    [Theory, BitAutoData]
    public async void PurchaseOrganizationAsync_Paypal_FailedCreate(SutProvider<StripePaymentService> sutProvider, Organization organization, string paymentToken, TaxInfo taxInfo)
    {
        var plans = StaticStore.Plans.Where(p => p.Type == PlanType.EnterpriseAnnually).ToList();

        var customerResult = Substitute.For<Result<Customer>>();
        customerResult.IsSuccess().Returns(false);

        var braintreeGateway = sutProvider.GetDependency<IBraintreeGateway>();
        braintreeGateway.Customer.CreateAsync(default).ReturnsForAnyArgs(customerResult);

        var exception = await Assert.ThrowsAsync<GatewayException>(
            () => sutProvider.Sut.PurchaseOrganizationAsync(organization, PaymentMethodType.PayPal, paymentToken, plans, 0, 0, false, taxInfo));

        Assert.Equal("Failed to create PayPal customer record.", exception.Message);
    }

    [Theory, BitAutoData]
    public async void PurchaseOrganizationAsync_SM_Paypal_FailedCreate(SutProvider<StripePaymentService> sutProvider, Organization organization, string paymentToken, TaxInfo taxInfo)
    {
        var plans = StaticStore.Plans.Where(p => p.Type == PlanType.EnterpriseAnnually).ToList();

        var customerResult = Substitute.For<Result<Customer>>();
        customerResult.IsSuccess().Returns(false);

        var braintreeGateway = sutProvider.GetDependency<IBraintreeGateway>();
        braintreeGateway.Customer.CreateAsync(default).ReturnsForAnyArgs(customerResult);

        var exception = await Assert.ThrowsAsync<GatewayException>(
            () => sutProvider.Sut.PurchaseOrganizationAsync(organization, PaymentMethodType.PayPal, paymentToken, plans,
                1, 1, false, taxInfo, false, 8, 8));

        Assert.Equal("Failed to create PayPal customer record.", exception.Message);
    }

    [Theory, BitAutoData]
    public async void PurchaseOrganizationAsync_PayPal_Declined(SutProvider<StripePaymentService> sutProvider, Organization organization, string paymentToken, TaxInfo taxInfo)
    {
        var plans = StaticStore.Plans.Where(p => p.Type == PlanType.EnterpriseAnnually).ToList();
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
            () => sutProvider.Sut.PurchaseOrganizationAsync(organization, PaymentMethodType.PayPal, paymentToken, plans, 0, 0, false, taxInfo));

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

        var plans = StaticStore.Plans.Where(p => p.Type == PlanType.EnterpriseAnnually).ToList();

        var upgrade = new OrganizationUpgrade()
        {
            AdditionalStorageGb = 0,
            AdditionalSeats = 0,
            PremiumAccessAddon = false,
            TaxInfo = taxInfo,
            AdditionalSmSeats = 0,
            AdditionalServiceAccounts = 0
        };
        var result = await sutProvider.Sut.UpgradeFreeOrganizationAsync(organization, plans, upgrade);

        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async void UpgradeFreeOrganizationAsync_SM_Success(SutProvider<StripePaymentService> sutProvider,
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

        var upgrade = new OrganizationUpgrade()
        {
            AdditionalStorageGb = 1,
            AdditionalSeats = 10,
            PremiumAccessAddon = false,
            TaxInfo = taxInfo,
            AdditionalSmSeats = 5,
            AdditionalServiceAccounts = 50
        };

        var plans = StaticStore.Plans.Where(p => p.Type == PlanType.EnterpriseAnnually).ToList();
        var result = await sutProvider.Sut.UpgradeFreeOrganizationAsync(organization, plans, upgrade);

        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task PreviewUpcomingInvoiceAndPayAsync_WithInAppPaymentMethod_ThrowsBadRequestException(SutProvider<StripePaymentService> sutProvider,
        Organization subscriber, List<Stripe.InvoiceSubscriptionItemOptions> subItemOptions)
    {
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        stripeAdapter.CustomerGetAsync(Arg.Any<string>(), Arg.Any<Stripe.CustomerGetOptions>())
            .Returns(new Stripe.Customer { Metadata = new Dictionary<string, string> { { "appleReceipt", "dummyData" } } });

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PreviewUpcomingInvoiceAndPayAsync(subscriber, subItemOptions));
        Assert.Equal("Cannot perform this action with in-app purchase payment method. Contact support.", ex.Message);
    }

    [Theory, BitAutoData]
    public async void PreviewUpcomingInvoiceAndPayAsync_UpcomingInvoiceBelowThreshold_DoesNotInvoiceNow(SutProvider<StripePaymentService> sutProvider,
        Organization subscriber, List<Stripe.InvoiceSubscriptionItemOptions> subItemOptions)
    {
        var prorateThreshold = 500;
        var invoiceAmountBelowThreshold = prorateThreshold - 100;
        var customer = MockStripeCustomer(subscriber);
        sutProvider.GetDependency<IStripeAdapter>().CustomerGetAsync(default, default).ReturnsForAnyArgs(customer);
        var invoiceItem = MockInoviceItemList(subscriber, "planId", invoiceAmountBelowThreshold, customer);
        sutProvider.GetDependency<IStripeAdapter>().InvoiceItemListAsync(new Stripe.InvoiceItemListOptions
        {
            Customer = subscriber.GatewayCustomerId
        }).ReturnsForAnyArgs(invoiceItem);

        var invoiceLineItem = CreateInvoiceLineTime(subscriber, "planId", invoiceAmountBelowThreshold);
        sutProvider.GetDependency<IStripeAdapter>().InvoiceUpcomingAsync(new Stripe.UpcomingInvoiceOptions
        {
            Customer = subscriber.GatewayCustomerId,
            Subscription = subscriber.GatewaySubscriptionId,
            SubscriptionItems = subItemOptions
        }).ReturnsForAnyArgs(invoiceLineItem);

        sutProvider.GetDependency<IStripeAdapter>().InvoiceCreateAsync(Arg.Is<Stripe.InvoiceCreateOptions>(options =>
            options.CollectionMethod == "send_invoice" &&
            options.DaysUntilDue == 1 &&
            options.Customer == subscriber.GatewayCustomerId &&
            options.Subscription == subscriber.GatewaySubscriptionId &&
            options.DefaultPaymentMethod == customer.InvoiceSettings.DefaultPaymentMethod.Id
        )).ReturnsForAnyArgs(new Stripe.Invoice
        {
            Id = "mockInvoiceId",
            CollectionMethod = "send_invoice",
            DueDate = DateTime.Now.AddDays(1),
            Customer = customer,
            Subscription = new Stripe.Subscription
            {
                Id = "mockSubscriptionId",
                Customer = customer,
                Status = "active",
                CurrentPeriodStart = DateTime.UtcNow,
                CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1),
                CollectionMethod = "charge_automatically",
            },
            DefaultPaymentMethod = customer.InvoiceSettings.DefaultPaymentMethod,
            AmountDue = invoiceAmountBelowThreshold,
            Currency = "usd",
            Status = "draft",
        });

        var result = await sutProvider.Sut.PreviewUpcomingInvoiceAndPayAsync(subscriber, new List<Stripe.InvoiceSubscriptionItemOptions>(), prorateThreshold);

        Assert.False(result.Item1);
        Assert.Null(result.Item2);
    }

    [Theory, BitAutoData]
    public async void PreviewUpcomingInvoiceAndPayAsync_NoPaymentMethod_ThrowsBadRequestException(SutProvider<StripePaymentService> sutProvider,
       Organization subscriber, List<Stripe.InvoiceSubscriptionItemOptions> subItemOptions, string planId)
    {
        var prorateThreshold = 500;
        var invoiceAmountBelowThreshold = prorateThreshold;
        var customer = new Stripe.Customer
        {
            Metadata = new Dictionary<string, string>(),
            Id = subscriber.GatewayCustomerId,
            DefaultSource = null,
            InvoiceSettings = new Stripe.CustomerInvoiceSettings
            {
                DefaultPaymentMethod = null
            }
        };
        sutProvider.GetDependency<IStripeAdapter>().CustomerGetAsync(default, default).ReturnsForAnyArgs(customer);
        var invoiceItem = MockInoviceItemList(subscriber, planId, invoiceAmountBelowThreshold, customer);
        sutProvider.GetDependency<IStripeAdapter>().InvoiceItemListAsync(new Stripe.InvoiceItemListOptions
        {
            Customer = subscriber.GatewayCustomerId
        }).ReturnsForAnyArgs(invoiceItem);

        var invoiceLineItem = CreateInvoiceLineTime(subscriber, planId, invoiceAmountBelowThreshold);
        sutProvider.GetDependency<IStripeAdapter>().InvoiceUpcomingAsync(new Stripe.UpcomingInvoiceOptions
        {
            Customer = subscriber.GatewayCustomerId,
            Subscription = subscriber.GatewaySubscriptionId,
            SubscriptionItems = subItemOptions
        }).ReturnsForAnyArgs(invoiceLineItem);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PreviewUpcomingInvoiceAndPayAsync(subscriber, subItemOptions));
        Assert.Equal("No payment method is available.", ex.Message);
    }

    [Theory, BitAutoData]
    public async void PreviewUpcomingInvoiceAndPayAsync_UpcomingInvoiceAboveThreshold_DoesInvoiceNow(SutProvider<StripePaymentService> sutProvider,
        Organization subscriber, List<Stripe.InvoiceSubscriptionItemOptions> subItemOptions, string planId)
    {
        var prorateThreshold = 500;
        var invoiceAmountBelowThreshold = 1000;
        var customer = MockStripeCustomer(subscriber);
        sutProvider.GetDependency<IStripeAdapter>().CustomerGetAsync(default, default).ReturnsForAnyArgs(customer);
        var invoiceItem = MockInoviceItemList(subscriber, planId, invoiceAmountBelowThreshold, customer);
        sutProvider.GetDependency<IStripeAdapter>().InvoiceItemListAsync(new Stripe.InvoiceItemListOptions
        {
            Customer = subscriber.GatewayCustomerId
        }).ReturnsForAnyArgs(invoiceItem);

        var invoiceLineItem = CreateInvoiceLineTime(subscriber, planId, invoiceAmountBelowThreshold);
        sutProvider.GetDependency<IStripeAdapter>().InvoiceUpcomingAsync(new Stripe.UpcomingInvoiceOptions
        {
            Customer = subscriber.GatewayCustomerId,
            Subscription = subscriber.GatewaySubscriptionId,
            SubscriptionItems = subItemOptions
        }).ReturnsForAnyArgs(invoiceLineItem);

        var invoice = MockInVoice(customer, invoiceAmountBelowThreshold);
        sutProvider.GetDependency<IStripeAdapter>().InvoiceCreateAsync(Arg.Is<Stripe.InvoiceCreateOptions>(options =>
            options.CollectionMethod == "send_invoice" &&
            options.DaysUntilDue == 1 &&
            options.Customer == subscriber.GatewayCustomerId &&
            options.Subscription == subscriber.GatewaySubscriptionId &&
            options.DefaultPaymentMethod == customer.InvoiceSettings.DefaultPaymentMethod.Id
        )).ReturnsForAnyArgs(invoice);

        var result = await sutProvider.Sut.PreviewUpcomingInvoiceAndPayAsync(subscriber, new List<Stripe.InvoiceSubscriptionItemOptions>(), prorateThreshold);

        await sutProvider.GetDependency<IStripeAdapter>().Received(1).InvoicePayAsync(invoice.Id,
            Arg.Is<Stripe.InvoicePayOptions>((options =>
                options.OffSession == true
                )));


        Assert.True(result.Item1);
        Assert.Null(result.Item2);
    }

    private static Stripe.Invoice MockInVoice(Stripe.Customer customer, int invoiceAmountBelowThreshold) =>
        new()
        {
            Id = "mockInvoiceId",
            CollectionMethod = "send_invoice",
            DueDate = DateTime.Now.AddDays(1),
            Customer = customer,
            Subscription = new Stripe.Subscription
            {
                Id = "mockSubscriptionId",
                Customer = customer,
                Status = "active",
                CurrentPeriodStart = DateTime.UtcNow,
                CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1),
                CollectionMethod = "charge_automatically",
            },
            DefaultPaymentMethod = customer.InvoiceSettings.DefaultPaymentMethod,
            AmountDue = invoiceAmountBelowThreshold,
            Currency = "usd",
            Status = "draft",
        };

    private static List<Stripe.InvoiceItem> MockInoviceItemList(Organization subscriber, string planId, int invoiceAmountBelowThreshold, Stripe.Customer customer) =>
        new()
        {
            new Stripe.InvoiceItem
            {
                Id = "ii_1234567890",
                Amount = invoiceAmountBelowThreshold,
                Currency = "usd",
                CustomerId = subscriber.GatewayCustomerId,
                Description = "Sample invoice item 1",
                Date = DateTime.UtcNow,
                Discountable = true,
                InvoiceId = "548458365"
            },
            new Stripe.InvoiceItem
            {
                Id = "ii_0987654321",
                Amount = invoiceAmountBelowThreshold,
                Currency = "usd",
                CustomerId =  customer.Id,
                Description = "Sample invoice item 2",
                Date = DateTime.UtcNow.AddDays(-5),
                Discountable = false,
                InvoiceId = null,
                Proration = true,
                Plan = new Stripe.Plan
                {
                    Id = planId,
                    Amount = invoiceAmountBelowThreshold,
                    Currency = "usd",
                    Interval = "month",
                    IntervalCount = 1,
                },
            }
        };

    private static Stripe.Customer MockStripeCustomer(Organization subscriber)
    {
        var customer = new Stripe.Customer
        {
            Metadata = new Dictionary<string, string>(),
            Id = subscriber.GatewayCustomerId,
            DefaultSource = new Stripe.Card
            {
                Id = "card_12345",
                Last4 = "1234",
                Brand = "Visa",
                ExpYear = 2025,
                ExpMonth = 12
            },
            InvoiceSettings = new Stripe.CustomerInvoiceSettings
            {
                DefaultPaymentMethod = new Stripe.PaymentMethod
                {
                    Id = "pm_12345",
                    Type = "card",
                    Card = new Stripe.PaymentMethodCard
                    {
                        Last4 = "1234",
                        Brand = "Visa",
                        ExpYear = 2025,
                        ExpMonth = 12
                    }
                }
            }
        };
        return customer;
    }

    private static Stripe.Invoice CreateInvoiceLineTime(Organization subscriber, string planId, int invoiceAmountBelowThreshold) =>
        new()
        {
            AmountDue = invoiceAmountBelowThreshold,
            AmountPaid = 0,
            AmountRemaining = invoiceAmountBelowThreshold,
            CustomerId = subscriber.GatewayCustomerId,
            SubscriptionId = subscriber.GatewaySubscriptionId,
            ApplicationFeeAmount = 0,
            Currency = "usd",
            Description = "Upcoming Invoice",
            Discount = null,
            DueDate = DateTime.UtcNow.AddDays(1),
            EndingBalance = 0,
            Number = "INV12345",
            Paid = false,
            PeriodStart = DateTime.UtcNow,
            PeriodEnd = DateTime.UtcNow.AddMonths(1),
            ReceiptNumber = null,
            StartingBalance = 0,
            Status = "draft",
            Id = "ii_0987654321",
            Total = invoiceAmountBelowThreshold,
            Lines = new Stripe.StripeList<Stripe.InvoiceLineItem>
            {
                Data = new List<Stripe.InvoiceLineItem>
                {
                    new Stripe.InvoiceLineItem
                    {
                        Amount = invoiceAmountBelowThreshold,
                        Currency = "usd",
                        Description = "Sample line item",
                        Id = "ii_0987654321",
                        Livemode = false,
                        Object = "line_item",
                        Discountable = false,
                        Period = new Stripe.InvoiceLineItemPeriod()
                        {
                            Start = DateTime.UtcNow,
                            End = DateTime.UtcNow.AddMonths(1)
                        },
                        Plan = new Stripe.Plan
                        {
                            Id = planId,
                            Amount = invoiceAmountBelowThreshold,
                            Currency = "usd",
                            Interval = "month",
                            IntervalCount = 1,
                        },
                        Proration = true,
                        Quantity = 1,
                        Subscription = subscriber.GatewaySubscriptionId,
                        SubscriptionItem = "si_12345",
                        Type = "subscription",
                        UnitAmountExcludingTax = invoiceAmountBelowThreshold,
                    }
                }
            }
        };
}
