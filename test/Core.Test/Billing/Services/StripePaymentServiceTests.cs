using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models.StaticStore.Plans;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Services.Implementations;
using Bit.Core.Billing.Tax.Requests;
using Bit.Core.Enums;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Services;

[SutProviderCustomize]
public class StripePaymentServiceTests
{
    [Theory]
    [BitAutoData]
    public async Task
        PreviewInvoiceAsync_ForOrganization_CalculatesSalesTaxCorrectlyForFamiliesWithoutAdditionalStorage(
            SutProvider<StripePaymentService> sutProvider)
    {
        var familiesPlan = new FamiliesPlan();
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(Arg.Is<PlanType>(p => p == PlanType.FamiliesAnnually))
            .Returns(familiesPlan);

        var parameters = new PreviewOrganizationInvoiceRequestBody
        {
            PasswordManager =
                new OrganizationPasswordManagerRequestModel
                {
                    Plan = PlanType.FamiliesAnnually,
                    AdditionalStorage = 0
                },
            TaxInformation = new TaxInformationRequestModel { Country = "FR", PostalCode = "12345" }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(p =>
                p.Currency == "usd" &&
                p.SubscriptionDetails.Items.Any(x =>
                    x.Plan == familiesPlan.PasswordManager.StripePlanId &&
                    x.Quantity == 1) &&
                p.SubscriptionDetails.Items.Any(x =>
                    x.Plan == familiesPlan.PasswordManager.StripeStoragePlanId &&
                    x.Quantity == 0)))
            .Returns(new Invoice
            {
                TotalExcludingTax = 4000,
                TotalTaxes = [new InvoiceTotalTax { Amount = 800 }],
                Total = 4800
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
        var familiesPlan = new FamiliesPlan();
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(Arg.Is<PlanType>(p => p == PlanType.FamiliesAnnually))
            .Returns(familiesPlan);

        var parameters = new PreviewOrganizationInvoiceRequestBody
        {
            PasswordManager =
                new OrganizationPasswordManagerRequestModel
                {
                    Plan = PlanType.FamiliesAnnually,
                    AdditionalStorage = 1
                },
            TaxInformation = new TaxInformationRequestModel { Country = "FR", PostalCode = "12345" }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(p =>
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
            .CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(p =>
                p.Currency == "usd" &&
                p.SubscriptionDetails.Items.Any(x =>
                    x.Plan == "2021-family-for-enterprise-annually" &&
                    x.Quantity == 1) &&
                p.SubscriptionDetails.Items.Any(x =>
                    x.Plan == familiesPlan.PasswordManager.StripeStoragePlanId &&
                    x.Quantity == 0)))
            .Returns(new Invoice { TotalExcludingTax = 0, TotalTaxes = [new InvoiceTotalTax { Amount = 0 }], Total = 0 });

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
            .CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(p =>
                p.Currency == "usd" &&
                p.SubscriptionDetails.Items.Any(x =>
                    x.Plan == "2021-family-for-enterprise-annually" &&
                    x.Quantity == 1) &&
                p.SubscriptionDetails.Items.Any(x =>
                    x.Plan == familiesPlan.PasswordManager.StripeStoragePlanId &&
                    x.Quantity == 1)))
            .Returns(new Invoice { TotalExcludingTax = 400, TotalTaxes = [new InvoiceTotalTax { Amount = 8 }], Total = 408 });

        var actual = await sutProvider.Sut.PreviewInvoiceAsync(parameters, null, null);

        Assert.Equal(0.08M, actual.TaxAmount);
        Assert.Equal(4.08M, actual.TotalAmount);
        Assert.Equal(4M, actual.TaxableBaseAmount);
    }

    [Theory]
    [BitAutoData]
    public async Task PreviewInvoiceAsync_USBased_PersonalUse_SetsAutomaticTaxEnabled(SutProvider<StripePaymentService> sutProvider)
    {
        // Arrange
        var familiesPlan = new FamiliesPlan();
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(Arg.Is<PlanType>(p => p == PlanType.FamiliesAnnually))
            .Returns(familiesPlan);

        var parameters = new PreviewOrganizationInvoiceRequestBody
        {
            PasswordManager = new OrganizationPasswordManagerRequestModel
            {
                Plan = PlanType.FamiliesAnnually
            },
            TaxInformation = new TaxInformationRequestModel
            {
                Country = "US",
                PostalCode = "12345"
            }
        };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        stripeAdapter
            .CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(new Invoice
            {
                TotalExcludingTax = 400,
                TotalTaxes = [new InvoiceTotalTax { Amount = 8 }],
                Total = 408
            });

        // Act
        await sutProvider.Sut.PreviewInvoiceAsync(parameters, null, null);

        // Assert
        await stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true
        ));
    }

    [Theory]
    [BitAutoData]
    public async Task PreviewInvoiceAsync_USBased_BusinessUse_SetsAutomaticTaxEnabled(SutProvider<StripePaymentService> sutProvider)
    {
        // Arrange
        var plan = new EnterprisePlan(true);
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(Arg.Is<PlanType>(p => p == PlanType.EnterpriseAnnually))
            .Returns(plan);

        var parameters = new PreviewOrganizationInvoiceRequestBody
        {
            PasswordManager = new OrganizationPasswordManagerRequestModel
            {
                Plan = PlanType.EnterpriseAnnually
            },
            TaxInformation = new TaxInformationRequestModel
            {
                Country = "US",
                PostalCode = "12345"
            }
        };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        stripeAdapter
            .CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(new Invoice
            {
                TotalExcludingTax = 400,
                TotalTaxes = [new InvoiceTotalTax { Amount = 8 }],
                Total = 408
            });

        // Act
        await sutProvider.Sut.PreviewInvoiceAsync(parameters, null, null);

        // Assert
        await stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true
        ));
    }

    [Theory]
    [BitAutoData]
    public async Task PreviewInvoiceAsync_NonUSBased_PersonalUse_SetsAutomaticTaxEnabled(SutProvider<StripePaymentService> sutProvider)
    {
        // Arrange
        var familiesPlan = new FamiliesPlan();
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(Arg.Is<PlanType>(p => p == PlanType.FamiliesAnnually))
            .Returns(familiesPlan);

        var parameters = new PreviewOrganizationInvoiceRequestBody
        {
            PasswordManager = new OrganizationPasswordManagerRequestModel
            {
                Plan = PlanType.FamiliesAnnually
            },
            TaxInformation = new TaxInformationRequestModel
            {
                Country = "FR",
                PostalCode = "12345"
            }
        };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        stripeAdapter
            .CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(new Invoice
            {
                TotalExcludingTax = 400,
                TotalTaxes = [new InvoiceTotalTax { Amount = 8 }],
                Total = 408
            });

        // Act
        await sutProvider.Sut.PreviewInvoiceAsync(parameters, null, null);

        // Assert
        await stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true
        ));
    }

    [Theory]
    [BitAutoData]
    public async Task PreviewInvoiceAsync_NonUSBased_BusinessUse_SetsAutomaticTaxEnabled(SutProvider<StripePaymentService> sutProvider)
    {
        // Arrange
        var plan = new EnterprisePlan(true);
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(Arg.Is<PlanType>(p => p == PlanType.EnterpriseAnnually))
            .Returns(plan);

        var parameters = new PreviewOrganizationInvoiceRequestBody
        {
            PasswordManager = new OrganizationPasswordManagerRequestModel
            {
                Plan = PlanType.EnterpriseAnnually
            },
            TaxInformation = new TaxInformationRequestModel
            {
                Country = "FR",
                PostalCode = "12345"
            }
        };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        stripeAdapter
            .CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(new Invoice
            {
                TotalExcludingTax = 400,
                TotalTaxes = [new InvoiceTotalTax { Amount = 8 }],
                Total = 408
            });

        // Act
        await sutProvider.Sut.PreviewInvoiceAsync(parameters, null, null);

        // Assert
        await stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true
        ));
    }

    [Theory]
    [BitAutoData]
    public async Task PreviewInvoiceAsync_USBased_PersonalUse_DoesNotSetTaxExempt(SutProvider<StripePaymentService> sutProvider)
    {
        // Arrange
        var familiesPlan = new FamiliesPlan();
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(Arg.Is<PlanType>(p => p == PlanType.FamiliesAnnually))
            .Returns(familiesPlan);

        var parameters = new PreviewOrganizationInvoiceRequestBody
        {
            PasswordManager = new OrganizationPasswordManagerRequestModel
            {
                Plan = PlanType.FamiliesAnnually
            },
            TaxInformation = new TaxInformationRequestModel
            {
                Country = "US",
                PostalCode = "12345"
            }
        };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        stripeAdapter
            .CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(new Invoice
            {
                TotalExcludingTax = 400,
                TotalTaxes = [new InvoiceTotalTax { Amount = 8 }],
                Total = 408
            });

        // Act
        await sutProvider.Sut.PreviewInvoiceAsync(parameters, null, null);

        // Assert
        await stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.CustomerDetails.TaxExempt == null
        ));
    }

    [Theory]
    [BitAutoData]
    public async Task PreviewInvoiceAsync_USBased_BusinessUse_DoesNotSetTaxExempt(SutProvider<StripePaymentService> sutProvider)
    {
        // Arrange
        var plan = new EnterprisePlan(true);
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(Arg.Is<PlanType>(p => p == PlanType.EnterpriseAnnually))
            .Returns(plan);

        var parameters = new PreviewOrganizationInvoiceRequestBody
        {
            PasswordManager = new OrganizationPasswordManagerRequestModel
            {
                Plan = PlanType.EnterpriseAnnually
            },
            TaxInformation = new TaxInformationRequestModel
            {
                Country = "US",
                PostalCode = "12345"
            }
        };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        stripeAdapter
            .CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(new Invoice
            {
                TotalExcludingTax = 400,
                TotalTaxes = [new InvoiceTotalTax { Amount = 8 }],
                Total = 408
            });

        // Act
        await sutProvider.Sut.PreviewInvoiceAsync(parameters, null, null);

        // Assert
        await stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.CustomerDetails.TaxExempt == null
        ));
    }

    [Theory]
    [BitAutoData]
    public async Task PreviewInvoiceAsync_NonUSBased_PersonalUse_DoesNotSetTaxExempt(SutProvider<StripePaymentService> sutProvider)
    {
        // Arrange
        var familiesPlan = new FamiliesPlan();
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(Arg.Is<PlanType>(p => p == PlanType.FamiliesAnnually))
            .Returns(familiesPlan);

        var parameters = new PreviewOrganizationInvoiceRequestBody
        {
            PasswordManager = new OrganizationPasswordManagerRequestModel
            {
                Plan = PlanType.FamiliesAnnually
            },
            TaxInformation = new TaxInformationRequestModel
            {
                Country = "FR",
                PostalCode = "12345"
            }
        };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        stripeAdapter
            .CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(new Invoice
            {
                TotalExcludingTax = 400,
                TotalTaxes = [new InvoiceTotalTax { Amount = 8 }],
                Total = 408
            });

        // Act
        await sutProvider.Sut.PreviewInvoiceAsync(parameters, null, null);

        // Assert
        await stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.CustomerDetails.TaxExempt == null
        ));
    }

    [Theory]
    [BitAutoData]
    public async Task PreviewInvoiceAsync_NonUSBased_BusinessUse_SetsTaxExemptReverse(SutProvider<StripePaymentService> sutProvider)
    {
        // Arrange
        var plan = new EnterprisePlan(true);
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(Arg.Is<PlanType>(p => p == PlanType.EnterpriseAnnually))
            .Returns(plan);

        var parameters = new PreviewOrganizationInvoiceRequestBody
        {
            PasswordManager = new OrganizationPasswordManagerRequestModel
            {
                Plan = PlanType.EnterpriseAnnually
            },
            TaxInformation = new TaxInformationRequestModel
            {
                Country = "FR",
                PostalCode = "12345"
            }
        };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        stripeAdapter
            .CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(new Invoice
            {
                TotalExcludingTax = 400,
                TotalTaxes = [new InvoiceTotalTax { Amount = 8 }],
                Total = 408
            });

        // Act
        await sutProvider.Sut.PreviewInvoiceAsync(parameters, null, null);

        // Assert
        await stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.CustomerDetails.TaxExempt == StripeConstants.TaxExempt.Reverse
        ));
    }
}
