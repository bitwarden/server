using System.Net;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Commands.Implementations;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Repositories;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Stripe;
using Xunit;

using static Bit.Core.Test.Billing.Utilities;

namespace Bit.Core.Test.Billing.Commands;

[SutProviderCustomize]
public class StartSubscriptionCommandTests
{
    private const string _customerId = "customer_id";
    private const string _subscriptionId = "subscription_id";

    // These tests are only trying to assert on the thrown exceptions and thus use the least amount of data setup possible.
    #region Error Cases
    [Theory, BitAutoData]
    public async Task StartSubscription_NullProvider_ThrowsArgumentNullException(
        SutProvider<StartSubscriptionCommand> sutProvider,
        TaxInfo taxInfo) =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => sutProvider.Sut.StartSubscription(null, taxInfo));

    [Theory, BitAutoData]
    public async Task StartSubscription_NullTaxInfo_ThrowsArgumentNullException(
        SutProvider<StartSubscriptionCommand> sutProvider,
        Provider provider) =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => sutProvider.Sut.StartSubscription(provider, null));

    [Theory, BitAutoData]
    public async Task StartSubscription_AlreadyHasGatewaySubscriptionId_ThrowsBillingException(
        SutProvider<StartSubscriptionCommand> sutProvider,
        Provider provider,
        TaxInfo taxInfo)
    {
        provider.GatewayCustomerId = _customerId;

        provider.GatewaySubscriptionId = _subscriptionId;

        await ThrowsContactSupportAsync(() => sutProvider.Sut.StartSubscription(provider, taxInfo));

        await DidNotRetrieveCustomerAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task StartSubscription_MissingCountry_ThrowsBillingException(
        SutProvider<StartSubscriptionCommand> sutProvider,
        Provider provider,
        TaxInfo taxInfo)
    {
        provider.GatewayCustomerId = _customerId;

        provider.GatewaySubscriptionId = null;

        taxInfo.BillingAddressCountry = null;

        await ThrowsContactSupportAsync(() => sutProvider.Sut.StartSubscription(provider, taxInfo));

        await DidNotRetrieveCustomerAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task StartSubscription_MissingPostalCode_ThrowsBillingException(
        SutProvider<StartSubscriptionCommand> sutProvider,
        Provider provider,
        TaxInfo taxInfo)
    {
        provider.GatewayCustomerId = _customerId;

        provider.GatewaySubscriptionId = null;

        taxInfo.BillingAddressPostalCode = null;

        await ThrowsContactSupportAsync(() => sutProvider.Sut.StartSubscription(provider, taxInfo));

        await DidNotRetrieveCustomerAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task StartSubscription_MissingStripeCustomer_ThrowsBillingException(
        SutProvider<StartSubscriptionCommand> sutProvider,
        Provider provider,
        TaxInfo taxInfo)
    {
        provider.GatewayCustomerId = _customerId;

        provider.GatewaySubscriptionId = null;

        SetCustomerRetrieval(sutProvider, null);

        await ThrowsContactSupportAsync(() => sutProvider.Sut.StartSubscription(provider, taxInfo));

        await DidNotRetrieveProviderPlansAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task StartSubscription_CustomerDoesNotSupportAutomaticTax_ThrowsBillingException(
        SutProvider<StartSubscriptionCommand> sutProvider,
        Provider provider,
        TaxInfo taxInfo)
    {
        provider.GatewayCustomerId = _customerId;

        provider.GatewaySubscriptionId = null;

        taxInfo.BillingAddressCountry = "US";

        SetCustomerRetrieval(sutProvider, new Customer
        {
            Id = _customerId,
            Tax = new CustomerTax
            {
                AutomaticTax = StripeConstants.AutomaticTaxStatus.NotCollecting
            }
        });

        await ThrowsContactSupportAsync(() => sutProvider.Sut.StartSubscription(provider, taxInfo));

        await DidNotRetrieveProviderPlansAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task StartSubscription_NoProviderPlans_ThrowsBillingException(
        SutProvider<StartSubscriptionCommand> sutProvider,
        Provider provider,
        TaxInfo taxInfo)
    {
        provider.GatewayCustomerId = _customerId;

        provider.GatewaySubscriptionId = null;

        SetCustomerRetrieval(sutProvider, new Customer
        {
            Id = _customerId,
            Tax = new CustomerTax
            {
                AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported
            }
        });

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id)
            .Returns(new List<ProviderPlan>());

        await ThrowsContactSupportAsync(() => sutProvider.Sut.StartSubscription(provider, taxInfo));

        await DidNotCreateSubscriptionAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task StartSubscription_NoProviderTeamsPlan_ThrowsBillingException(
        SutProvider<StartSubscriptionCommand> sutProvider,
        Provider provider,
        TaxInfo taxInfo)
    {
        provider.GatewayCustomerId = _customerId;

        provider.GatewaySubscriptionId = null;

        SetCustomerRetrieval(sutProvider, new Customer
        {
            Id = _customerId,
            Tax = new CustomerTax
            {
                AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported
            }
        });

        var providerPlans = new List<ProviderPlan>
        {
            new ()
            {
                PlanType = PlanType.EnterpriseMonthly
            }
        };

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id)
            .Returns(providerPlans);

        await ThrowsContactSupportAsync(() => sutProvider.Sut.StartSubscription(provider, taxInfo));

        await DidNotCreateSubscriptionAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task StartSubscription_NoProviderEnterprisePlan_ThrowsBillingException(
        SutProvider<StartSubscriptionCommand> sutProvider,
        Provider provider,
        TaxInfo taxInfo)
    {
        provider.GatewayCustomerId = _customerId;

        provider.GatewaySubscriptionId = null;

        SetCustomerRetrieval(sutProvider, new Customer
        {
            Id = _customerId,
            Tax = new CustomerTax
            {
                AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported
            }
        });

        var providerPlans = new List<ProviderPlan>
        {
            new ()
            {
                PlanType = PlanType.TeamsMonthly
            }
        };

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id)
            .Returns(providerPlans);

        await ThrowsContactSupportAsync(() => sutProvider.Sut.StartSubscription(provider, taxInfo));

        await DidNotCreateSubscriptionAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task StartSubscription_SubscriptionIncomplete_ThrowsBillingException(
        SutProvider<StartSubscriptionCommand> sutProvider,
        Provider provider,
        TaxInfo taxInfo)
    {
        provider.GatewayCustomerId = _customerId;

        provider.GatewaySubscriptionId = null;

        SetCustomerRetrieval(sutProvider, new Customer
        {
            Id = _customerId,
            Tax = new CustomerTax
            {
                AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported
            }
        });

        var providerPlans = new List<ProviderPlan>
        {
            new ()
            {
                PlanType = PlanType.TeamsMonthly,
                SeatMinimum = 100
            },
            new ()
            {
                PlanType = PlanType.EnterpriseMonthly,
                SeatMinimum = 100
            }
        };

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id)
            .Returns(providerPlans);

        sutProvider.GetDependency<IStripeAdapter>().SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(new Subscription
        {
            Id = _subscriptionId,
            Status = StripeConstants.SubscriptionStatus.Incomplete
        });

        await ThrowsContactSupportAsync(() => sutProvider.Sut.StartSubscription(provider, taxInfo));

        await sutProvider.GetDependency<IProviderRepository>().Received(1).ReplaceAsync(provider);
    }
    #endregion

    #region Success Cases
    [Theory, BitAutoData]
    public async Task StartSubscription_ExistingCustomer_Succeeds(
        SutProvider<StartSubscriptionCommand> sutProvider,
        Provider provider,
        TaxInfo taxInfo)
    {
        provider.GatewayCustomerId = _customerId;

        provider.GatewaySubscriptionId = null;

        SetCustomerRetrieval(sutProvider, new Customer
        {
            Id = _customerId,
            Tax = new CustomerTax
            {
                AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported
            }
        });

        var providerPlans = new List<ProviderPlan>
        {
            new ()
            {
                PlanType = PlanType.TeamsMonthly,
                SeatMinimum = 100
            },
            new ()
            {
                PlanType = PlanType.EnterpriseMonthly,
                SeatMinimum = 100
            }
        };

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id)
            .Returns(providerPlans);

        var teamsPlan = StaticStore.GetPlan(PlanType.TeamsMonthly);
        var enterprisePlan = StaticStore.GetPlan(PlanType.EnterpriseMonthly);

        sutProvider.GetDependency<IStripeAdapter>().SubscriptionCreateAsync(Arg.Is<SubscriptionCreateOptions>(
            sub =>
                sub.AutomaticTax.Enabled == true &&
                sub.CollectionMethod == StripeConstants.CollectionMethod.SendInvoice &&
                sub.Customer == _customerId &&
                sub.DaysUntilDue == 30 &&
                sub.Items.Count == 2 &&
                sub.Items.ElementAt(0).Price == teamsPlan.PasswordManager.StripeSeatPlanId &&
                sub.Items.ElementAt(0).Quantity == 100 &&
                sub.Items.ElementAt(1).Price == enterprisePlan.PasswordManager.StripeSeatPlanId &&
                sub.Items.ElementAt(1).Quantity == 100 &&
                sub.Metadata["providerId"] == provider.Id.ToString() &&
                sub.OffSession == true &&
                sub.ProrationBehavior == StripeConstants.ProrationBehavior.CreateProrations)).Returns(new Subscription
                {
                    Id = _subscriptionId,
                    Status = StripeConstants.SubscriptionStatus.Active
                });

        await sutProvider.Sut.StartSubscription(provider, taxInfo);

        await sutProvider.GetDependency<IProviderRepository>().Received(1).ReplaceAsync(provider);
    }

    [Theory, BitAutoData]
    public async Task StartSubscription_NewCustomer_Succeeds(
        SutProvider<StartSubscriptionCommand> sutProvider,
        Provider provider,
        TaxInfo taxInfo)
    {
        provider.GatewayCustomerId = null;

        provider.GatewaySubscriptionId = null;

        provider.Name = "MSP";

        taxInfo.BillingAddressCountry = "AD";

        sutProvider.GetDependency<IStripeAdapter>().CustomerCreateAsync(Arg.Is<CustomerCreateOptions>(o =>
                o.Address.Country == taxInfo.BillingAddressCountry &&
                o.Address.PostalCode == taxInfo.BillingAddressPostalCode &&
                o.Address.Line1 == taxInfo.BillingAddressLine1 &&
                o.Address.Line2 == taxInfo.BillingAddressLine2 &&
                o.Address.City == taxInfo.BillingAddressCity &&
                o.Address.State == taxInfo.BillingAddressState &&
                o.Coupon == "msp-discount-35" &&
                o.Description == WebUtility.HtmlDecode(provider.BusinessName) &&
                o.Email == provider.BillingEmail &&
                o.Expand.FirstOrDefault() == "tax" &&
                o.InvoiceSettings.CustomFields.FirstOrDefault().Name == "Provider" &&
                o.InvoiceSettings.CustomFields.FirstOrDefault().Value == "MSP" &&
                o.Metadata["region"] == "" &&
                o.TaxIdData.FirstOrDefault().Type == taxInfo.TaxIdType &&
                o.TaxIdData.FirstOrDefault().Value == taxInfo.TaxIdNumber))
            .Returns(new Customer
            {
                Id = _customerId,
                Tax = new CustomerTax
                {
                    AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported
                }
            });

        var providerPlans = new List<ProviderPlan>
        {
            new ()
            {
                PlanType = PlanType.TeamsMonthly,
                SeatMinimum = 100
            },
            new ()
            {
                PlanType = PlanType.EnterpriseMonthly,
                SeatMinimum = 100
            }
        };

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id)
            .Returns(providerPlans);

        var teamsPlan = StaticStore.GetPlan(PlanType.TeamsMonthly);
        var enterprisePlan = StaticStore.GetPlan(PlanType.EnterpriseMonthly);

        sutProvider.GetDependency<IStripeAdapter>().SubscriptionCreateAsync(Arg.Is<SubscriptionCreateOptions>(
            sub =>
                sub.AutomaticTax.Enabled == true &&
                sub.CollectionMethod == StripeConstants.CollectionMethod.SendInvoice &&
                sub.Customer == _customerId &&
                sub.DaysUntilDue == 30 &&
                sub.Items.Count == 2 &&
                sub.Items.ElementAt(0).Price == teamsPlan.PasswordManager.StripeSeatPlanId &&
                sub.Items.ElementAt(0).Quantity == 100 &&
                sub.Items.ElementAt(1).Price == enterprisePlan.PasswordManager.StripeSeatPlanId &&
                sub.Items.ElementAt(1).Quantity == 100 &&
                sub.Metadata["providerId"] == provider.Id.ToString() &&
                sub.OffSession == true &&
                sub.ProrationBehavior == StripeConstants.ProrationBehavior.CreateProrations)).Returns(new Subscription
                {
                    Id = _subscriptionId,
                    Status = StripeConstants.SubscriptionStatus.Active
                });

        await sutProvider.Sut.StartSubscription(provider, taxInfo);

        await sutProvider.GetDependency<IProviderRepository>().Received(2).ReplaceAsync(provider);
    }
    #endregion

    private static async Task DidNotCreateSubscriptionAsync(SutProvider<StartSubscriptionCommand> sutProvider) =>
        await sutProvider.GetDependency<IStripeAdapter>()
            .DidNotReceiveWithAnyArgs()
            .SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>());

    private static async Task DidNotRetrieveCustomerAsync(SutProvider<StartSubscriptionCommand> sutProvider) =>
        await sutProvider.GetDependency<IStripeAdapter>()
            .DidNotReceiveWithAnyArgs()
            .CustomerGetAsync(Arg.Any<string>(), Arg.Any<CustomerGetOptions>());

    private static async Task DidNotRetrieveProviderPlansAsync(SutProvider<StartSubscriptionCommand> sutProvider) =>
        await sutProvider.GetDependency<IProviderPlanRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByProviderId(Arg.Any<Guid>());

    private static void SetCustomerRetrieval(SutProvider<StartSubscriptionCommand> sutProvider,
        Customer customer) => sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(_customerId, Arg.Is<CustomerGetOptions>(o => o.Expand.FirstOrDefault() == "tax"))
            .Returns(customer);
}
