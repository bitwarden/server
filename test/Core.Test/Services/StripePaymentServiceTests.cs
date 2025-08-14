using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models.StaticStore.Plans;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Tax.Models;
using Bit.Core.Billing.Tax.Requests;
using Bit.Core.Billing.Tax.Services;
using Bit.Core.Enums;
using Bit.Core.Services;
using Bit.Core.Test.Billing.Tax.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class StripePaymentServiceTests
{
    [Theory]
    [BitAutoData]
    public async Task
        PreviewInvoiceAsync_ForOrganization_CalculatesSalesTaxCorrectlyForFamiliesWithoutAdditionalStorage(
            SutProvider<StripePaymentService> sutProvider)
    {
        sutProvider.GetDependency<IAutomaticTaxFactory>()
            .CreateAsync(Arg.Is<AutomaticTaxFactoryParameters>(p => p.PlanType == PlanType.FamiliesAnnually))
            .Returns(new FakeAutomaticTaxStrategy(true));

        var familiesPlan = new FamiliesPlan();
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(Arg.Is<PlanType>(p => p == PlanType.FamiliesAnnually))
            .Returns(familiesPlan);

        var parameters = new PreviewOrganizationInvoiceRequestBody
        {
            PasswordManager =
                new OrganizationPasswordManagerRequestModel
                {
                    Plan = PlanType.FamiliesAnnually, AdditionalStorage = 0
                },
            TaxInformation = new TaxInformationRequestModel { Country = "FR", PostalCode = "12345" }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(p =>
                p.Currency == "usd" &&
                p.SubscriptionDetails.Items.Any(x =>
                    x.Plan == familiesPlan.PasswordManager.StripePlanId &&
                    x.Quantity == 1) &&
                p.SubscriptionDetails.Items.Any(x =>
                    x.Plan == familiesPlan.PasswordManager.StripeStoragePlanId &&
                    x.Quantity == 0)))
            .Returns(new Invoice
            {
                TotalExcludingTax = 4000, TotalTaxes = [new InvoiceTotalTax { Amount = 800 }], Total = 4800
            });

        var actual = await sutProvider.Sut.PreviewInvoiceAsync(parameters, null, null);

        Assert.Equal(8M, actual.TaxAmount);
        Assert.Equal(48M, actual.TotalAmount);
        Assert.Equal(40M, actual.TaxableBaseAmount);
    }

    [Theory]
    [BitAutoData]
    public async Task PreviewInvoiceAsync_ForOrganization_CalculatesSalesTaxCorrectlyForFamiliesWithAdditionalStorage(
        SutProvider<StripePaymentService> sutProvider)
    {
        sutProvider.GetDependency<IAutomaticTaxFactory>()
            .CreateAsync(Arg.Is<AutomaticTaxFactoryParameters>(p => p.PlanType == PlanType.FamiliesAnnually))
            .Returns(new FakeAutomaticTaxStrategy(true));

        var familiesPlan = new FamiliesPlan();
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(Arg.Is<PlanType>(p => p == PlanType.FamiliesAnnually))
            .Returns(familiesPlan);

        var parameters = new PreviewOrganizationInvoiceRequestBody
        {
            PasswordManager =
                new OrganizationPasswordManagerRequestModel
                {
                    Plan = PlanType.FamiliesAnnually, AdditionalStorage = 1
                },
            TaxInformation = new TaxInformationRequestModel { Country = "FR", PostalCode = "12345" }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(p =>
                p.Currency == "usd" &&
                p.SubscriptionDetails.Items.Any(x =>
                    x.Plan == familiesPlan.PasswordManager.StripePlanId &&
                    x.Quantity == 1) &&
                p.SubscriptionDetails.Items.Any(x =>
                    x.Plan == familiesPlan.PasswordManager.StripeStoragePlanId &&
                    x.Quantity == 1)))
            .Returns(new Invoice { TotalExcludingTax = 4000, TotalTaxes = [new InvoiceTotalTax { Amount = 800 }], Total = 4800 });

        var actual = await sutProvider.Sut.PreviewInvoiceAsync(parameters, null, null);

        Assert.Equal(8M, actual.TaxAmount);
        Assert.Equal(48M, actual.TotalAmount);
        Assert.Equal(40M, actual.TaxableBaseAmount);
    }

    [Theory]
    [BitAutoData]
    public async Task
        PreviewInvoiceAsync_ForOrganization_CalculatesSalesTaxCorrectlyForFamiliesForEnterpriseWithoutAdditionalStorage(
            SutProvider<StripePaymentService> sutProvider)
    {
        sutProvider.GetDependency<IAutomaticTaxFactory>()
            .CreateAsync(Arg.Is<AutomaticTaxFactoryParameters>(p => p.PlanType == PlanType.FamiliesAnnually))
            .Returns(new FakeAutomaticTaxStrategy(true));

        var familiesPlan = new FamiliesPlan();
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(Arg.Is<PlanType>(p => p == PlanType.FamiliesAnnually))
            .Returns(familiesPlan);

        var parameters = new PreviewOrganizationInvoiceRequestBody
        {
            PasswordManager = new OrganizationPasswordManagerRequestModel
            {
                Plan = PlanType.FamiliesAnnually,
                SponsoredPlan = PlanSponsorshipType.FamiliesForEnterprise,
                AdditionalStorage = 0
            },
            TaxInformation = new TaxInformationRequestModel { Country = "FR", PostalCode = "12345" }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(p =>
                p.Currency == "usd" &&
                p.SubscriptionDetails.Items.Any(x =>
                    x.Plan == "2021-family-for-enterprise-annually" &&
                    x.Quantity == 1) &&
                p.SubscriptionDetails.Items.Any(x =>
                    x.Plan == familiesPlan.PasswordManager.StripeStoragePlanId &&
                    x.Quantity == 0)))
            .Returns(new Invoice { TotalExcludingTax = 0, TotalTaxes = [new InvoiceTotalTax { Amount = 800 }], Total = 0 });

        var actual = await sutProvider.Sut.PreviewInvoiceAsync(parameters, null, null);

        Assert.Equal(0M, actual.TaxAmount);
        Assert.Equal(0M, actual.TotalAmount);
        Assert.Equal(0M, actual.TaxableBaseAmount);
    }

    [Theory]
    [BitAutoData]
    public async Task
        PreviewInvoiceAsync_ForOrganization_CalculatesSalesTaxCorrectlyForFamiliesForEnterpriseWithAdditionalStorage(
            SutProvider<StripePaymentService> sutProvider)
    {
        sutProvider.GetDependency<IAutomaticTaxFactory>()
            .CreateAsync(Arg.Is<AutomaticTaxFactoryParameters>(p => p.PlanType == PlanType.FamiliesAnnually))
            .Returns(new FakeAutomaticTaxStrategy(true));

        var familiesPlan = new FamiliesPlan();
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(Arg.Is<PlanType>(p => p == PlanType.FamiliesAnnually))
            .Returns(familiesPlan);

        var parameters = new PreviewOrganizationInvoiceRequestBody
        {
            PasswordManager = new OrganizationPasswordManagerRequestModel
            {
                Plan = PlanType.FamiliesAnnually,
                SponsoredPlan = PlanSponsorshipType.FamiliesForEnterprise,
                AdditionalStorage = 1
            },
            TaxInformation = new TaxInformationRequestModel { Country = "FR", PostalCode = "12345" }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(p =>
                p.Currency == "usd" &&
                p.SubscriptionDetails.Items.Any(x =>
                    x.Plan == "2021-family-for-enterprise-annually" &&
                    x.Quantity == 1) &&
                p.SubscriptionDetails.Items.Any(x =>
                    x.Plan == familiesPlan.PasswordManager.StripeStoragePlanId &&
                    x.Quantity == 1)))
            .Returns(new Invoice { TotalExcludingTax = 400, TotalTaxes = [new InvoiceTotalTax { Amount = 800 }], Total = 408 });

        var actual = await sutProvider.Sut.PreviewInvoiceAsync(parameters, null, null);

        Assert.Equal(0.08M, actual.TaxAmount);
        Assert.Equal(4.08M, actual.TotalAmount);
        Assert.Equal(4M, actual.TaxableBaseAmount);
    }
}
