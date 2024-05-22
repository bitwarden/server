using Bit.Api.Billing.Controllers.Providers;
using Bit.Api.Billing.Models.Requests;
using Bit.Api.Billing.Models.Responses;
using Bit.Api.Billing.Models.Responses.Providers;
using Bit.Core;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Entities;
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

namespace Bit.Api.Test.Billing.Controllers;

[ControllerCustomize(typeof(ProviderBillingController))]
[SutProviderCustomize]
public class ProviderBillingControllerTests
{
    #region GetSubscriptionAsync

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
    public async Task GetSubscriptionAsync_NoProvider_NotFound(
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
    public async Task GetSubscriptionAsync_NotProviderAdmin_Unauthorized(
        Guid providerId,
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(providerId).Returns(provider);

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(providerId)
            .Returns(false);

        var result = await sutProvider.Sut.GetSubscriptionAsync(providerId);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionAsync_ProviderNotBillable_Unauthorized(
        Guid providerId,
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        provider.Type = ProviderType.Reseller;

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(providerId).Returns(provider);

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(providerId)
            .Returns(false);

        var result = await sutProvider.Sut.GetSubscriptionAsync(providerId);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionAsync_NoSubscriptionData_NotFound(
        Guid providerId,
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableInputs(providerId, provider, sutProvider);

        sutProvider.GetDependency<IProviderBillingService>().GetConsolidatedBillingSubscription(provider).ReturnsNull();

        var result = await sutProvider.Sut.GetSubscriptionAsync(providerId);

        Assert.IsType<NotFound>(result);
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionAsync_OK(
        Guid providerId,
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableInputs(providerId, provider, sutProvider);

        var configuredProviderPlanDTOList = new List<ConfiguredProviderPlanDTO>
        {
            new (Guid.NewGuid(), providerId, PlanType.TeamsMonthly, 50, 10, 30),
            new (Guid.NewGuid(), providerId, PlanType.EnterpriseMonthly, 100, 0, 90)
        };

        var subscription = new Subscription
        {
            Status = "active",
            CurrentPeriodEnd = new DateTime(2025, 1, 1),
            Customer = new Customer { Discount = new Discount { Coupon = new Coupon { PercentOff = 10 } } }
        };

        var consolidatedBillingSubscriptionDTO = new ConsolidatedBillingSubscriptionDTO(
            configuredProviderPlanDTOList,
            subscription);

        sutProvider.GetDependency<IProviderBillingService>().GetConsolidatedBillingSubscription(provider)
            .Returns(consolidatedBillingSubscriptionDTO);

        var result = await sutProvider.Sut.GetSubscriptionAsync(providerId);

        Assert.IsType<Ok<ConsolidatedBillingSubscriptionResponse>>(result);

        var providerSubscriptionResponse = ((Ok<ConsolidatedBillingSubscriptionResponse>)result).Value;

        Assert.Equal(providerSubscriptionResponse.Status, subscription.Status);
        Assert.Equal(providerSubscriptionResponse.CurrentPeriodEndDate, subscription.CurrentPeriodEnd);
        Assert.Equal(providerSubscriptionResponse.DiscountPercentage, subscription.Customer!.Discount!.Coupon!.PercentOff);

        var teamsPlan = StaticStore.GetPlan(PlanType.TeamsMonthly);
        var providerTeamsPlan = providerSubscriptionResponse.Plans.FirstOrDefault(plan => plan.PlanName == teamsPlan.Name);
        Assert.NotNull(providerTeamsPlan);
        Assert.Equal(50, providerTeamsPlan.SeatMinimum);
        Assert.Equal(10, providerTeamsPlan.PurchasedSeats);
        Assert.Equal(30, providerTeamsPlan.AssignedSeats);
        Assert.Equal(60 * teamsPlan.PasswordManager.SeatPrice, providerTeamsPlan.Cost);
        Assert.Equal("Monthly", providerTeamsPlan.Cadence);

        var enterprisePlan = StaticStore.GetPlan(PlanType.EnterpriseMonthly);
        var providerEnterprisePlan = providerSubscriptionResponse.Plans.FirstOrDefault(plan => plan.PlanName == enterprisePlan.Name);
        Assert.NotNull(providerEnterprisePlan);
        Assert.Equal(100, providerEnterprisePlan.SeatMinimum);
        Assert.Equal(0, providerEnterprisePlan.PurchasedSeats);
        Assert.Equal(90, providerEnterprisePlan.AssignedSeats);
        Assert.Equal(100 * enterprisePlan.PasswordManager.SeatPrice, providerEnterprisePlan.Cost);
        Assert.Equal("Monthly", providerEnterprisePlan.Cadence);
    }

    #endregion

    #region GetPaymentMethodAsync

    [Theory, BitAutoData]
    public async Task GetPaymentMethod_PaymentMethodNull_NotFound(
        Guid providerId,
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableInputs(providerId, provider, sutProvider);

        sutProvider.GetDependency<ISubscriberService>().GetPaymentMethod(provider).ReturnsNull();

        var result = await sutProvider.Sut.GetSubscriptionAsync(providerId);

        Assert.IsType<NotFound>(result);
    }

    [Theory, BitAutoData]
    public async Task GetPaymentMethod_Ok(
        Guid providerId,
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableInputs(providerId, provider, sutProvider);

        sutProvider.GetDependency<ISubscriberService>().GetPaymentMethod(provider).Returns(new PaymentMethodDTO(
            PaymentMethodType.Card, "Description", false));

        var result = await sutProvider.Sut.GetPaymentMethodAsync(providerId);

        Assert.IsType<Ok<PaymentMethodResponse>>(result);

        var paymentMethodResponse = ((Ok<PaymentMethodResponse>)result).Value;

        Assert.Equal(PaymentMethodType.Card, paymentMethodResponse.Type);
        Assert.Equal("Description", paymentMethodResponse.Description);
        Assert.False(paymentMethodResponse.NeedsVerification);
    }

    #endregion

    #region UpdatePaymentMethod

    [Theory, BitAutoData]
    public async Task UpdatePaymentMethod_Ok(
        Guid providerId,
        Provider provider,
        TokenizedPaymentMethodRequestBody requestBody,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableInputs(providerId, provider, sutProvider);

        await sutProvider.Sut.UpdatePaymentMethodAsync(providerId, requestBody);

        await sutProvider.GetDependency<ISubscriberService>().Received(1).UpdatePaymentMethod(
            provider, Arg.Is<TokenizedPaymentMethodDTO>(
                options => options.Type == requestBody.Type && options.Token == requestBody.Token));

        await sutProvider.GetDependency<IStripeAdapter>().Received(1).SubscriptionUpdateAsync(
            provider.GatewaySubscriptionId, Arg.Is<SubscriptionUpdateOptions>(
                options => options.CollectionMethod == StripeConstants.CollectionMethod.ChargeAutomatically));
    }

    #endregion

    #region VerifyBankAccount

    [Theory, BitAutoData]
    public async Task VerifyBankAccount_Ok(
        Guid providerId,
        Provider provider,
        VerifyBankAccountRequestBody requestBody,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableInputs(providerId, provider, sutProvider);

        var result = await sutProvider.Sut.VerifyBankAccountAsync(providerId, requestBody);

        Assert.IsType<Ok>(result);

        await sutProvider.GetDependency<ISubscriberService>().Received(1).VerifyBankAccount(
            provider,
            (requestBody.Amount1, requestBody.Amount2));
    }

    #endregion

    #region GetTaxInformation

    [Theory, BitAutoData]
    public async Task GetTaxInformation_TaxInformationNull_NotFound(
        Guid providerId,
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableInputs(providerId, provider, sutProvider);

        sutProvider.GetDependency<ISubscriberService>().GetTaxInformation(provider).ReturnsNull();

        var result = await sutProvider.Sut.GetSubscriptionAsync(providerId);

        Assert.IsType<NotFound>(result);
    }

    [Theory, BitAutoData]
    public async Task GetTaxInformation_Ok(
        Guid providerId,
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableInputs(providerId, provider, sutProvider);

        sutProvider.GetDependency<ISubscriberService>().GetTaxInformation(provider).Returns(new TaxInformationDTO(
            "US",
            "12345",
            "123456789",
            "123 Example St.",
            null,
            "Example Town",
            "NY"));

        var result = await sutProvider.Sut.GetTaxInformationAsync(providerId);

        Assert.IsType<Ok<TaxInformationResponse>>(result);

        var taxInformationResponse = ((Ok<TaxInformationResponse>)result).Value;

        Assert.Equal("US", taxInformationResponse.Country);
        Assert.Equal("12345", taxInformationResponse.PostalCode);
        Assert.Equal("123456789", taxInformationResponse.TaxId);
        Assert.Equal("123 Example St.", taxInformationResponse.Line1);
        Assert.Null(taxInformationResponse.Line2);
        Assert.Equal("Example Town", taxInformationResponse.City);
        Assert.Equal("NY", taxInformationResponse.State);
    }

    #endregion

    #region UpdateTaxInformation

    [Theory, BitAutoData]
    public async Task UpdateTaxInformation_Ok(
        Guid providerId,
        Provider provider,
        TaxInformationRequestBody requestBody,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableInputs(providerId, provider, sutProvider);

        await sutProvider.Sut.UpdateTaxInformationAsync(providerId, requestBody);

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

    private static void ConfigureStableInputs(
        Guid providerId,
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        provider.Type = ProviderType.Msp;
        provider.Status = ProviderStatusType.Billable;

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(providerId).Returns(provider);

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(providerId)
            .Returns(true);
    }
}
