using Bit.Api.Billing.Controllers;
using Bit.Api.Billing.Models.Requests;
using Bit.Api.Billing.Models.Responses;
using Bit.Core;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Stripe;
using Xunit;

using static Bit.Api.Test.Billing.Utilities;

namespace Bit.Api.Test.Billing.Controllers;

[ControllerCustomize(typeof(ProviderBillingController))]
[SutProviderCustomize]
public class ProviderBillingControllerTests
{
    #region GetInvoicesAsync

    [Theory, BitAutoData]
    public async Task GetInvoices_Ok(
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableAdminInputs(provider, sutProvider);

        var invoices = new List<Invoice>
        {
            new ()
            {
                Id = "3",
                Created = new DateTime(2024, 7, 1),
                Status = "draft",
                Total = 100000,
                HostedInvoiceUrl = "https://example.com/invoice/3",
                InvoicePdf = "https://example.com/invoice/3/pdf"
            },
            new ()
            {
                Id = "2",
                Created = new DateTime(2024, 6, 1),
                Number = "B",
                Status = "open",
                Total = 100000,
                DueDate = new DateTime(2024, 7, 1),
                HostedInvoiceUrl = "https://example.com/invoice/2",
                InvoicePdf = "https://example.com/invoice/2/pdf"
            },
            new ()
            {
                Id = "1",
                Created = new DateTime(2024, 5, 1),
                Number = "A",
                Status = "paid",
                Total = 100000,
                DueDate = new DateTime(2024, 6, 1),
                HostedInvoiceUrl = "https://example.com/invoice/1",
                InvoicePdf = "https://example.com/invoice/1/pdf"
            }
        };

        sutProvider.GetDependency<ISubscriberService>().GetInvoices(provider).Returns(invoices);

        var result = await sutProvider.Sut.GetInvoicesAsync(provider.Id);

        Assert.IsType<Ok<InvoicesResponse>>(result);

        var response = ((Ok<InvoicesResponse>)result).Value;

        Assert.Equal(2, response.Invoices.Count);

        var openInvoice = response.Invoices.FirstOrDefault(i => i.Status == "open");

        Assert.NotNull(openInvoice);
        Assert.Equal("2", openInvoice.Id);
        Assert.Equal(new DateTime(2024, 6, 1), openInvoice.Date);
        Assert.Equal("B", openInvoice.Number);
        Assert.Equal(1000, openInvoice.Total);
        Assert.Equal(new DateTime(2024, 7, 1), openInvoice.DueDate);
        Assert.Equal("https://example.com/invoice/2", openInvoice.Url);
        Assert.Equal("https://example.com/invoice/2/pdf", openInvoice.PdfUrl);

        var paidInvoice = response.Invoices.FirstOrDefault(i => i.Status == "paid");

        Assert.NotNull(paidInvoice);
        Assert.Equal("1", paidInvoice.Id);
        Assert.Equal(new DateTime(2024, 5, 1), paidInvoice.Date);
        Assert.Equal("A", paidInvoice.Number);
        Assert.Equal(1000, paidInvoice.Total);
        Assert.Equal(new DateTime(2024, 6, 1), paidInvoice.DueDate);
        Assert.Equal("https://example.com/invoice/1", paidInvoice.Url);
        Assert.Equal("https://example.com/invoice/1/pdf", paidInvoice.PdfUrl);
    }

    #endregion

    #region GenerateClientInvoiceReportAsync

    [Theory, BitAutoData]
    public async Task GenerateClientInvoiceReportAsync_Ok(
        Provider provider,
        string invoiceId,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableAdminInputs(provider, sutProvider);

        var reportContent = "Report"u8.ToArray();

        sutProvider.GetDependency<IProviderBillingService>().GenerateClientInvoiceReport(invoiceId)
            .Returns(reportContent);

        var result = await sutProvider.Sut.GenerateClientInvoiceReportAsync(provider.Id, invoiceId);

        Assert.IsType<FileContentHttpResult>(result);

        var response = (FileContentHttpResult)result;

        Assert.Equal("text/csv", response.ContentType);
        Assert.Equal(reportContent, response.FileContents);
    }

    #endregion

    #region GetPaymentInformationAsync & TryGetBillableProviderForAdminOperation

    [Theory, BitAutoData]
    public async Task GetPaymentInformationAsync_FFDisabled_NotFound(
        Guid providerId,
        SutProvider<ProviderBillingController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(false);

        var result = await sutProvider.Sut.GetPaymentInformationAsync(providerId);

        Assert.IsType<NotFound>(result);
    }

    [Theory, BitAutoData]
    public async Task GetPaymentInformationAsync_NullProvider_NotFound(
        Guid providerId,
        SutProvider<ProviderBillingController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(providerId).ReturnsNull();

        var result = await sutProvider.Sut.GetPaymentInformationAsync(providerId);

        Assert.IsType<NotFound>(result);
    }

    [Theory, BitAutoData]
    public async Task GetPaymentInformationAsync_NotProviderUser_Unauthorized(
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(provider.Id)
            .Returns(false);

        var result = await sutProvider.Sut.GetPaymentInformationAsync(provider.Id);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Theory, BitAutoData]
    public async Task GetPaymentInformationAsync_ProviderNotBillable_Unauthorized(
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        provider.Type = ProviderType.Reseller;
        provider.Status = ProviderStatusType.Created;

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(provider.Id)
            .Returns(true);

        var result = await sutProvider.Sut.GetPaymentInformationAsync(provider.Id);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Theory, BitAutoData]
    public async Task GetPaymentInformation_PaymentInformationNull_NotFound(
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableAdminInputs(provider, sutProvider);

        sutProvider.GetDependency<ISubscriberService>().GetPaymentInformation(provider).ReturnsNull();

        var result = await sutProvider.Sut.GetPaymentInformationAsync(provider.Id);

        Assert.IsType<NotFound>(result);
    }

    [Theory, BitAutoData]
    public async Task GetPaymentInformation_Ok(
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableAdminInputs(provider, sutProvider);

        var maskedPaymentMethod = new MaskedPaymentMethodDTO(PaymentMethodType.Card, "VISA *1234", false);

        var taxInformation =
            new TaxInformationDTO("US", "12345", "123456789", "123 Example St.", null, "Example Town", "NY");

        sutProvider.GetDependency<ISubscriberService>().GetPaymentInformation(provider).Returns(new PaymentInformationDTO(
            100,
            maskedPaymentMethod,
            taxInformation));

        var result = await sutProvider.Sut.GetPaymentInformationAsync(provider.Id);

        Assert.IsType<Ok<PaymentInformationResponse>>(result);

        var response = ((Ok<PaymentInformationResponse>)result).Value;

        Assert.Equal(100, response.AccountCredit);
        Assert.Equal(maskedPaymentMethod.Description, response.PaymentMethod.Description);
        Assert.Equal(taxInformation.TaxId, response.TaxInformation.TaxId);
    }

    #endregion

    #region GetPaymentMethodAsync

    [Theory, BitAutoData]
    public async Task GetPaymentMethod_PaymentMethodNull_NotFound(
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableAdminInputs(provider, sutProvider);

        sutProvider.GetDependency<ISubscriberService>().GetPaymentMethod(provider).ReturnsNull();

        var result = await sutProvider.Sut.GetPaymentMethodAsync(provider.Id);

        Assert.IsType<NotFound>(result);
    }

    [Theory, BitAutoData]
    public async Task GetPaymentMethod_Ok(
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableAdminInputs(provider, sutProvider);

        sutProvider.GetDependency<ISubscriberService>().GetPaymentMethod(provider).Returns(new MaskedPaymentMethodDTO(
            PaymentMethodType.Card, "Description", false));

        var result = await sutProvider.Sut.GetPaymentMethodAsync(provider.Id);

        Assert.IsType<Ok<MaskedPaymentMethodResponse>>(result);

        var response = ((Ok<MaskedPaymentMethodResponse>)result).Value;

        Assert.Equal(PaymentMethodType.Card, response.Type);
        Assert.Equal("Description", response.Description);
        Assert.False(response.NeedsVerification);
    }

    #endregion

    #region GetSubscriptionAsync & TryGetBillableProviderForServiceUserOperation

    [Theory, BitAutoData]
    public async Task GetSubscriptionAsync_FFDisabled_NotFound(
        Guid providerId,
        SutProvider<ProviderBillingController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(false);

        var result = await sutProvider.Sut.GetSubscriptionAsync(providerId);

        Assert.IsType<NotFound>(result);
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionAsync_NullProvider_NotFound(
        Guid providerId,
        SutProvider<ProviderBillingController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(providerId).ReturnsNull();

        var result = await sutProvider.Sut.GetSubscriptionAsync(providerId);

        Assert.IsType<NotFound>(result);
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionAsync_NotProviderUser_Unauthorized(
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);

        sutProvider.GetDependency<ICurrentContext>().ProviderUser(provider.Id)
            .Returns(false);

        var result = await sutProvider.Sut.GetSubscriptionAsync(provider.Id);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionAsync_ProviderNotBillable_Unauthorized(
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        provider.Type = ProviderType.Reseller;
        provider.Status = ProviderStatusType.Created;

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);

        sutProvider.GetDependency<ICurrentContext>().ProviderUser(provider.Id)
            .Returns(true);

        var result = await sutProvider.Sut.GetSubscriptionAsync(provider.Id);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionAsync_NullConsolidatedBillingSubscription_NotFound(
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableServiceUserInputs(provider, sutProvider);

        sutProvider.GetDependency<IProviderBillingService>().GetConsolidatedBillingSubscription(provider).ReturnsNull();

        var result = await sutProvider.Sut.GetSubscriptionAsync(provider.Id);

        Assert.IsType<NotFound>(result);
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionAsync_Ok(
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableServiceUserInputs(provider, sutProvider);

        var configuredProviderPlans = new List<ConfiguredProviderPlanDTO>
        {
            new (Guid.NewGuid(), provider.Id, PlanType.TeamsMonthly, 50, 10, 30),
            new (Guid.NewGuid(), provider.Id , PlanType.EnterpriseMonthly, 100, 0, 90)
        };

        var subscription = new Subscription
        {
            Status = "active",
            CurrentPeriodEnd = new DateTime(2025, 1, 1),
            Customer = new Customer { Discount = new Discount { Coupon = new Coupon { PercentOff = 10 } } }
        };

        DateTime? SuspensionDate = new DateTime();
        DateTime? UnpaidPeriodEndDate = new DateTime();
        var gracePeriod = 30;
        var consolidatedBillingSubscription = new ConsolidatedBillingSubscriptionDTO(
            configuredProviderPlans,
            subscription,
            SuspensionDate,
            UnpaidPeriodEndDate);

        sutProvider.GetDependency<IProviderBillingService>().GetConsolidatedBillingSubscription(provider)
            .Returns(consolidatedBillingSubscription);

        var result = await sutProvider.Sut.GetSubscriptionAsync(provider.Id);

        Assert.IsType<Ok<ConsolidatedBillingSubscriptionResponse>>(result);

        var response = ((Ok<ConsolidatedBillingSubscriptionResponse>)result).Value;

        Assert.Equal(response.Status, subscription.Status);
        Assert.Equal(response.CurrentPeriodEndDate, subscription.CurrentPeriodEnd);
        Assert.Equal(response.DiscountPercentage, subscription.Customer!.Discount!.Coupon!.PercentOff);
        Assert.Equal(response.CollectionMethod, subscription.CollectionMethod);
        Assert.Equal(response.UnpaidPeriodEndDate, UnpaidPeriodEndDate);
        Assert.Equal(response.GracePeriod, gracePeriod);
        Assert.Equal(response.SuspensionDate, SuspensionDate);

        var teamsPlan = StaticStore.GetPlan(PlanType.TeamsMonthly);
        var providerTeamsPlan = response.Plans.FirstOrDefault(plan => plan.PlanName == teamsPlan.Name);
        Assert.NotNull(providerTeamsPlan);
        Assert.Equal(50, providerTeamsPlan.SeatMinimum);
        Assert.Equal(10, providerTeamsPlan.PurchasedSeats);
        Assert.Equal(30, providerTeamsPlan.AssignedSeats);
        Assert.Equal(60 * teamsPlan.PasswordManager.SeatPrice, providerTeamsPlan.Cost);
        Assert.Equal("Monthly", providerTeamsPlan.Cadence);

        var enterprisePlan = StaticStore.GetPlan(PlanType.EnterpriseMonthly);
        var providerEnterprisePlan = response.Plans.FirstOrDefault(plan => plan.PlanName == enterprisePlan.Name);
        Assert.NotNull(providerEnterprisePlan);
        Assert.Equal(100, providerEnterprisePlan.SeatMinimum);
        Assert.Equal(0, providerEnterprisePlan.PurchasedSeats);
        Assert.Equal(90, providerEnterprisePlan.AssignedSeats);
        Assert.Equal(100 * enterprisePlan.PasswordManager.SeatPrice, providerEnterprisePlan.Cost);
        Assert.Equal("Monthly", providerEnterprisePlan.Cadence);
    }
    #endregion

    #region GetTaxInformationAsync

    [Theory, BitAutoData]
    public async Task GetTaxInformation_TaxInformationNull_NotFound(
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableAdminInputs(provider, sutProvider);

        sutProvider.GetDependency<ISubscriberService>().GetTaxInformation(provider).ReturnsNull();

        var result = await sutProvider.Sut.GetTaxInformationAsync(provider.Id);

        Assert.IsType<NotFound>(result);
    }

    [Theory, BitAutoData]
    public async Task GetTaxInformation_Ok(
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableAdminInputs(provider, sutProvider);

        sutProvider.GetDependency<ISubscriberService>().GetTaxInformation(provider).Returns(new TaxInformationDTO(
            "US",
            "12345",
            "123456789",
            "123 Example St.",
            null,
            "Example Town",
            "NY"));

        var result = await sutProvider.Sut.GetTaxInformationAsync(provider.Id);

        Assert.IsType<Ok<TaxInformationResponse>>(result);

        var response = ((Ok<TaxInformationResponse>)result).Value;

        Assert.Equal("US", response.Country);
        Assert.Equal("12345", response.PostalCode);
        Assert.Equal("123456789", response.TaxId);
        Assert.Equal("123 Example St.", response.Line1);
        Assert.Null(response.Line2);
        Assert.Equal("Example Town", response.City);
        Assert.Equal("NY", response.State);
    }

    #endregion

    #region UpdatePaymentMethodAsync

    [Theory, BitAutoData]
    public async Task UpdatePaymentMethod_Ok(
        Provider provider,
        TokenizedPaymentMethodRequestBody requestBody,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableAdminInputs(provider, sutProvider);

        await sutProvider.Sut.UpdatePaymentMethodAsync(provider.Id, requestBody);

        await sutProvider.GetDependency<ISubscriberService>().Received(1).UpdatePaymentMethod(
            provider, Arg.Is<TokenizedPaymentMethodDTO>(
                options => options.Type == requestBody.Type && options.Token == requestBody.Token));

        await sutProvider.GetDependency<IStripeAdapter>().Received(1).SubscriptionUpdateAsync(
            provider.GatewaySubscriptionId, Arg.Is<SubscriptionUpdateOptions>(
                options => options.CollectionMethod == StripeConstants.CollectionMethod.ChargeAutomatically));
    }

    #endregion

    #region UpdateTaxInformationAsync

    [Theory, BitAutoData]
    public async Task UpdateTaxInformation_Ok(
        Provider provider,
        TaxInformationRequestBody requestBody,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableAdminInputs(provider, sutProvider);

        await sutProvider.Sut.UpdateTaxInformationAsync(provider.Id, requestBody);

        await sutProvider.GetDependency<ISubscriberService>().Received(1).UpdateTaxInformation(
            provider, Arg.Is<TaxInformationDTO>(
                options =>
                    options.Country == requestBody.Country &&
                    options.PostalCode == requestBody.PostalCode &&
                    options.TaxId == requestBody.TaxId &&
                    options.Line1 == requestBody.Line1 &&
                    options.Line2 == requestBody.Line2 &&
                    options.City == requestBody.City &&
                    options.State == requestBody.State));
    }

    #endregion

    #region VerifyBankAccount

    [Theory, BitAutoData]
    public async Task VerifyBankAccount_Ok(
        Provider provider,
        VerifyBankAccountRequestBody requestBody,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableAdminInputs(provider, sutProvider);

        var result = await sutProvider.Sut.VerifyBankAccountAsync(provider.Id, requestBody);

        Assert.IsType<Ok>(result);

        await sutProvider.GetDependency<ISubscriberService>().Received(1).VerifyBankAccount(
            provider,
            (requestBody.Amount1, requestBody.Amount2));
    }

    #endregion
}
