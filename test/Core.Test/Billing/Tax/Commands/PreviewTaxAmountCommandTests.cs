using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Tax.Commands;
using Bit.Core.Billing.Tax.Services;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;
using Xunit;
using static Bit.Core.Billing.Tax.Commands.OrganizationTrialParameters;

namespace Bit.Core.Test.Billing.Tax.Commands;

public class PreviewTaxAmountCommandTests
{
    private readonly ILogger<PreviewTaxAmountCommand> _logger = Substitute.For<ILogger<PreviewTaxAmountCommand>>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly ITaxService _taxService = Substitute.For<ITaxService>();

    private readonly PreviewTaxAmountCommand _command;

    public PreviewTaxAmountCommandTests()
    {
        _command = new PreviewTaxAmountCommand(_logger, _pricingClient, _stripeAdapter, _taxService);
    }

    [Fact]
    public async Task Run_WithSeatBasedPasswordManagerPlan_GetsTaxAmount()
    {
        // Arrange
        var parameters = new OrganizationTrialParameters
        {
            PlanType = PlanType.EnterpriseAnnually,
            ProductType = ProductType.PasswordManager,
            TaxInformation = new TaxInformationDTO
            {
                Country = "US",
                PostalCode = "12345"
            }
        };

        var plan = StaticStore.GetPlan(parameters.PlanType);

        _pricingClient.GetPlanOrThrow(parameters.PlanType).Returns(plan);

        var expectedInvoice = new Invoice { Tax = 1000 }; // $10.00 in cents

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
                options.Currency == "usd" &&
                options.CustomerDetails.Address.Country == "US" &&
                options.CustomerDetails.Address.PostalCode == "12345" &&
                options.SubscriptionDetails.Items.Count == 1 &&
                options.SubscriptionDetails.Items[0].Price == plan.PasswordManager.StripeSeatPlanId &&
                options.SubscriptionDetails.Items[0].Quantity == 1 &&
                options.AutomaticTax.Enabled == true
            ))
            .Returns(expectedInvoice);

        // Act
        var result = await _command.Run(parameters);

        // Assert
        Assert.True(result.IsT0);
        var taxAmount = result.AsT0;
        Assert.Equal(expectedInvoice.Tax, (long)taxAmount * 100);
    }

    [Fact]
    public async Task Run_WithNonSeatBasedPasswordManagerPlan_GetsTaxAmount()
    {
        // Arrange
        var parameters = new OrganizationTrialParameters
        {
            PlanType = PlanType.FamiliesAnnually,
            ProductType = ProductType.PasswordManager,
            TaxInformation = new TaxInformationDTO
            {
                Country = "US",
                PostalCode = "12345"
            }
        };

        var plan = StaticStore.GetPlan(parameters.PlanType);

        _pricingClient.GetPlanOrThrow(parameters.PlanType).Returns(plan);

        var expectedInvoice = new Invoice { Tax = 1000 }; // $10.00 in cents

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
                options.Currency == "usd" &&
                options.CustomerDetails.Address.Country == "US" &&
                options.CustomerDetails.Address.PostalCode == "12345" &&
                options.SubscriptionDetails.Items.Count == 1 &&
                options.SubscriptionDetails.Items[0].Price == plan.PasswordManager.StripePlanId &&
                options.SubscriptionDetails.Items[0].Quantity == 1 &&
                options.AutomaticTax.Enabled == true
            ))
            .Returns(expectedInvoice);

        // Act
        var result = await _command.Run(parameters);

        // Assert
        Assert.True(result.IsT0);
        var taxAmount = result.AsT0;
        Assert.Equal(expectedInvoice.Tax, (long)taxAmount * 100);
    }

    [Fact]
    public async Task Run_WithSecretsManagerPlan_GetsTaxAmount()
    {
        // Arrange
        var parameters = new OrganizationTrialParameters
        {
            PlanType = PlanType.EnterpriseAnnually,
            ProductType = ProductType.SecretsManager,
            TaxInformation = new TaxInformationDTO
            {
                Country = "US",
                PostalCode = "12345"
            }
        };

        var plan = StaticStore.GetPlan(parameters.PlanType);

        _pricingClient.GetPlanOrThrow(parameters.PlanType).Returns(plan);

        var expectedInvoice = new Invoice { Tax = 1000 }; // $10.00 in cents

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
                options.Currency == "usd" &&
                options.CustomerDetails.Address.Country == "US" &&
                options.CustomerDetails.Address.PostalCode == "12345" &&
                options.SubscriptionDetails.Items.Count == 2 &&
                options.SubscriptionDetails.Items[0].Price == plan.PasswordManager.StripeSeatPlanId &&
                options.SubscriptionDetails.Items[0].Quantity == 1 &&
                options.SubscriptionDetails.Items[1].Price == plan.SecretsManager.StripeSeatPlanId &&
                options.SubscriptionDetails.Items[1].Quantity == 1 &&
                options.Coupon == StripeConstants.CouponIDs.SecretsManagerStandalone &&
                options.AutomaticTax.Enabled == true
            ))
            .Returns(expectedInvoice);

        // Act
        var result = await _command.Run(parameters);

        // Assert
        Assert.True(result.IsT0);
        var taxAmount = result.AsT0;
        Assert.Equal(expectedInvoice.Tax, (long)taxAmount * 100);
    }

    [Fact]
    public async Task Run_NonUSWithoutTaxId_GetsTaxAmount()
    {
        // Arrange
        var parameters = new OrganizationTrialParameters
        {
            PlanType = PlanType.EnterpriseAnnually,
            ProductType = ProductType.PasswordManager,
            TaxInformation = new TaxInformationDTO
            {
                Country = "CA",
                PostalCode = "12345"
            }
        };

        var plan = StaticStore.GetPlan(parameters.PlanType);

        _pricingClient.GetPlanOrThrow(parameters.PlanType).Returns(plan);

        var expectedInvoice = new Invoice { Tax = 1000 }; // $10.00 in cents

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
                options.Currency == "usd" &&
                options.CustomerDetails.Address.Country == "CA" &&
                options.CustomerDetails.Address.PostalCode == "12345" &&
                options.SubscriptionDetails.Items.Count == 1 &&
                options.SubscriptionDetails.Items[0].Price == plan.PasswordManager.StripeSeatPlanId &&
                options.SubscriptionDetails.Items[0].Quantity == 1 &&
                options.AutomaticTax.Enabled == false
            ))
            .Returns(expectedInvoice);

        // Act
        var result = await _command.Run(parameters);

        // Assert
        Assert.True(result.IsT0);
        var taxAmount = result.AsT0;
        Assert.Equal(expectedInvoice.Tax, (long)taxAmount * 100);
    }

    [Fact]
    public async Task Run_NonUSWithTaxId_GetsTaxAmount()
    {
        // Arrange
        var parameters = new OrganizationTrialParameters
        {
            PlanType = PlanType.EnterpriseAnnually,
            ProductType = ProductType.PasswordManager,
            TaxInformation = new TaxInformationDTO
            {
                Country = "CA",
                PostalCode = "12345",
                TaxId = "123456789"
            }
        };

        var plan = StaticStore.GetPlan(parameters.PlanType);

        _pricingClient.GetPlanOrThrow(parameters.PlanType).Returns(plan);

        _taxService.GetStripeTaxCode(parameters.TaxInformation.Country, parameters.TaxInformation.TaxId)
            .Returns("ca_st");

        var expectedInvoice = new Invoice { Tax = 1000 }; // $10.00 in cents

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
                options.Currency == "usd" &&
                options.CustomerDetails.Address.Country == "CA" &&
                options.CustomerDetails.Address.PostalCode == "12345" &&
                options.CustomerDetails.TaxIds.Count == 1 &&
                options.CustomerDetails.TaxIds[0].Type == "ca_st" &&
                options.CustomerDetails.TaxIds[0].Value == "123456789" &&
                options.SubscriptionDetails.Items.Count == 1 &&
                options.SubscriptionDetails.Items[0].Price == plan.PasswordManager.StripeSeatPlanId &&
                options.SubscriptionDetails.Items[0].Quantity == 1 &&
                options.AutomaticTax.Enabled == true
            ))
            .Returns(expectedInvoice);

        // Act
        var result = await _command.Run(parameters);

        // Assert
        Assert.True(result.IsT0);
        var taxAmount = result.AsT0;
        Assert.Equal(expectedInvoice.Tax, (long)taxAmount * 100);
    }

    [Fact]
    public async Task Run_NonUSWithTaxId_UnknownTaxIdType_BadRequest()
    {
        // Arrange
        var parameters = new OrganizationTrialParameters
        {
            PlanType = PlanType.EnterpriseAnnually,
            ProductType = ProductType.PasswordManager,
            TaxInformation = new TaxInformationDTO
            {
                Country = "CA",
                PostalCode = "12345",
                TaxId = "123456789"
            }
        };

        var plan = StaticStore.GetPlan(parameters.PlanType);

        _pricingClient.GetPlanOrThrow(parameters.PlanType).Returns(plan);

        _taxService.GetStripeTaxCode(parameters.TaxInformation.Country, parameters.TaxInformation.TaxId)
            .Returns((string)null);

        // Act
        var result = await _command.Run(parameters);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal(BillingErrorTranslationKeys.UnknownTaxIdType, badRequest.TranslationKey);
    }

    [Fact]
    public async Task Run_CustomerTaxLocationInvalid_BadRequest()
    {
        // Arrange
        var parameters = new OrganizationTrialParameters
        {
            PlanType = PlanType.EnterpriseAnnually,
            ProductType = ProductType.PasswordManager,
            TaxInformation = new TaxInformationDTO
            {
                Country = "US",
                PostalCode = "12345"
            }
        };

        var plan = StaticStore.GetPlan(parameters.PlanType);

        _pricingClient.GetPlanOrThrow(parameters.PlanType).Returns(plan);

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Throws(new StripeException
            {
                StripeError = new StripeError { Code = StripeConstants.ErrorCodes.CustomerTaxLocationInvalid }
            });

        // Act
        var result = await _command.Run(parameters);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal(BillingErrorTranslationKeys.CustomerTaxLocationInvalid, badRequest.TranslationKey);
    }

    [Fact]
    public async Task Run_TaxIdInvalid_BadRequest()
    {
        // Arrange
        var parameters = new OrganizationTrialParameters
        {
            PlanType = PlanType.EnterpriseAnnually,
            ProductType = ProductType.PasswordManager,
            TaxInformation = new TaxInformationDTO
            {
                Country = "US",
                PostalCode = "12345"
            }
        };

        var plan = StaticStore.GetPlan(parameters.PlanType);

        _pricingClient.GetPlanOrThrow(parameters.PlanType).Returns(plan);

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Throws(new StripeException
            {
                StripeError = new StripeError { Code = StripeConstants.ErrorCodes.TaxIdInvalid }
            });

        // Act
        var result = await _command.Run(parameters);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal(BillingErrorTranslationKeys.TaxIdInvalid, badRequest.TranslationKey);
    }
}
