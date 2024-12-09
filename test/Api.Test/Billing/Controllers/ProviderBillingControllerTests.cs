using Bit.Api.Billing.Controllers;
using Bit.Api.Billing.Models.Requests;
using Bit.Api.Billing.Models.Responses;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Repositories;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Models.Api;
using Bit.Core.Models.BitStripe;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
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
    #region GetInvoicesAsync & TryGetBillableProviderForAdminOperations

    [Theory, BitAutoData]
    public async Task GetInvoicesAsync_NullProvider_NotFound(
        Guid providerId,
        SutProvider<ProviderBillingController> sutProvider)
    {
        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(providerId).ReturnsNull();

        var result = await sutProvider.Sut.GetInvoicesAsync(providerId);

        AssertNotFound(result);
    }

    [Theory, BitAutoData]
    public async Task GetInvoicesAsync_NotProviderUser_Unauthorized(
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(provider.Id)
            .Returns(false);

        var result = await sutProvider.Sut.GetInvoicesAsync(provider.Id);

        AssertUnauthorized(result);
    }

    [Theory, BitAutoData]
    public async Task GetInvoicesAsync_ProviderNotBillable_Unauthorized(
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        provider.Type = ProviderType.Reseller;
        provider.Status = ProviderStatusType.Created;

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(provider.Id)
            .Returns(true);

        var result = await sutProvider.Sut.GetInvoicesAsync(provider.Id);

        AssertUnauthorized(result);
    }

    [Theory, BitAutoData]
    public async Task GetInvoices_Ok(
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableProviderAdminInputs(provider, sutProvider);

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

        sutProvider.GetDependency<IStripeAdapter>().InvoiceListAsync(Arg.Is<StripeInvoiceListOptions>(
            options =>
                options.Customer == provider.GatewayCustomerId)).Returns(invoices);

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

        var paidInvoice = response.Invoices.FirstOrDefault(i => i.Status == "paid");

        Assert.NotNull(paidInvoice);
        Assert.Equal("1", paidInvoice.Id);
        Assert.Equal(new DateTime(2024, 5, 1), paidInvoice.Date);
        Assert.Equal("A", paidInvoice.Number);
        Assert.Equal(1000, paidInvoice.Total);
        Assert.Equal(new DateTime(2024, 6, 1), paidInvoice.DueDate);
        Assert.Equal("https://example.com/invoice/1", paidInvoice.Url);
    }

    #endregion

    #region GenerateClientInvoiceReportAsync

    [Theory, BitAutoData]
    public async Task GenerateClientInvoiceReportAsync_NullReportContent_ServerError(
        Provider provider,
        string invoiceId,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableProviderAdminInputs(provider, sutProvider);

        sutProvider.GetDependency<IProviderBillingService>().GenerateClientInvoiceReport(invoiceId)
            .ReturnsNull();

        var result = await sutProvider.Sut.GenerateClientInvoiceReportAsync(provider.Id, invoiceId);

        Assert.IsType<JsonHttpResult<ErrorResponseModel>>(result);

        var response = (JsonHttpResult<ErrorResponseModel>)result;

        Assert.Equal(StatusCodes.Status500InternalServerError, response.StatusCode);
        Assert.Equal("We had a problem generating your invoice CSV. Please contact support.", response.Value.Message);
    }

    [Theory, BitAutoData]
    public async Task GenerateClientInvoiceReportAsync_Ok(
        Provider provider,
        string invoiceId,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableProviderAdminInputs(provider, sutProvider);

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

    #region GetSubscriptionAsync & TryGetBillableProviderForServiceUserOperation

    [Theory, BitAutoData]
    public async Task GetSubscriptionAsync_NullProvider_NotFound(
        Guid providerId,
        SutProvider<ProviderBillingController> sutProvider)
    {
        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(providerId).ReturnsNull();

        var result = await sutProvider.Sut.GetSubscriptionAsync(providerId);

        AssertNotFound(result);
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionAsync_NotProviderUser_Unauthorized(
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);

        sutProvider.GetDependency<ICurrentContext>().ProviderUser(provider.Id)
            .Returns(false);

        var result = await sutProvider.Sut.GetSubscriptionAsync(provider.Id);

        AssertUnauthorized(result);
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionAsync_ProviderNotBillable_Unauthorized(
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        provider.Type = ProviderType.Reseller;
        provider.Status = ProviderStatusType.Created;

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);

        sutProvider.GetDependency<ICurrentContext>().ProviderUser(provider.Id)
            .Returns(true);

        var result = await sutProvider.Sut.GetSubscriptionAsync(provider.Id);

        AssertUnauthorized(result);
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionAsync_Ok(
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableProviderServiceUserInputs(provider, sutProvider);

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        var (thisYear, thisMonth, _) = DateTime.UtcNow;
        var daysInThisMonth = DateTime.DaysInMonth(thisYear, thisMonth);

        var subscription = new Subscription
        {
            CollectionMethod = StripeConstants.CollectionMethod.ChargeAutomatically,
            CurrentPeriodEnd = new DateTime(thisYear, thisMonth, daysInThisMonth),
            Customer = new Customer
            {
                Address = new Address
                {
                    Country = "US",
                    PostalCode = "12345",
                    Line1 = "123 Example St.",
                    Line2 = "Unit 1",
                    City = "Example Town",
                    State = "NY"
                },
                Balance = -100000,
                Discount = new Discount { Coupon = new Coupon { PercentOff = 10 } },
                TaxIds = new StripeList<TaxId> { Data = [new TaxId { Value = "123456789" }] }
            },
            Status = "unpaid",
        };

        stripeAdapter.SubscriptionGetAsync(provider.GatewaySubscriptionId, Arg.Is<SubscriptionGetOptions>(
            options =>
                options.Expand.Contains("customer.tax_ids") &&
                options.Expand.Contains("test_clock"))).Returns(subscription);

        var lastMonth = thisMonth - 1;
        var daysInLastMonth = DateTime.DaysInMonth(thisYear, lastMonth);

        var overdueInvoice = new Invoice
        {
            Id = "invoice_id",
            Status = "open",
            Created = new DateTime(thisYear, lastMonth, 1),
            PeriodEnd = new DateTime(thisYear, lastMonth, daysInLastMonth),
            Attempted = true
        };

        stripeAdapter.InvoiceSearchAsync(Arg.Is<InvoiceSearchOptions>(
                options => options.Query == $"subscription:'{subscription.Id}' status:'open'"))
            .Returns([overdueInvoice]);

        var providerPlans = new List<ProviderPlan>
        {
            new ()
            {
                Id = Guid.NewGuid(),
                ProviderId = provider.Id,
                PlanType = PlanType.TeamsMonthly,
                SeatMinimum = 50,
                PurchasedSeats = 10,
                AllocatedSeats = 60
            },
            new ()
            {
                Id = Guid.NewGuid(),
                ProviderId = provider.Id,
                PlanType = PlanType.EnterpriseMonthly,
                SeatMinimum = 100,
                PurchasedSeats = 0,
                AllocatedSeats = 90
            }
        };

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id).Returns(providerPlans);

        var result = await sutProvider.Sut.GetSubscriptionAsync(provider.Id);

        Assert.IsType<Ok<ProviderSubscriptionResponse>>(result);

        var response = ((Ok<ProviderSubscriptionResponse>)result).Value;

        Assert.Equal(subscription.Status, response.Status);
        Assert.Equal(subscription.CurrentPeriodEnd, response.CurrentPeriodEndDate);
        Assert.Equal(subscription.Customer!.Discount!.Coupon!.PercentOff, response.DiscountPercentage);
        Assert.Equal(subscription.CollectionMethod, response.CollectionMethod);

        var teamsPlan = StaticStore.GetPlan(PlanType.TeamsMonthly);
        var providerTeamsPlan = response.Plans.FirstOrDefault(plan => plan.PlanName == teamsPlan.Name);
        Assert.NotNull(providerTeamsPlan);
        Assert.Equal(50, providerTeamsPlan.SeatMinimum);
        Assert.Equal(10, providerTeamsPlan.PurchasedSeats);
        Assert.Equal(60, providerTeamsPlan.AssignedSeats);
        Assert.Equal(60 * teamsPlan.PasswordManager.ProviderPortalSeatPrice, providerTeamsPlan.Cost);
        Assert.Equal("Monthly", providerTeamsPlan.Cadence);

        var enterprisePlan = StaticStore.GetPlan(PlanType.EnterpriseMonthly);
        var providerEnterprisePlan = response.Plans.FirstOrDefault(plan => plan.PlanName == enterprisePlan.Name);
        Assert.NotNull(providerEnterprisePlan);
        Assert.Equal(100, providerEnterprisePlan.SeatMinimum);
        Assert.Equal(0, providerEnterprisePlan.PurchasedSeats);
        Assert.Equal(90, providerEnterprisePlan.AssignedSeats);
        Assert.Equal(100 * enterprisePlan.PasswordManager.ProviderPortalSeatPrice, providerEnterprisePlan.Cost);
        Assert.Equal("Monthly", providerEnterprisePlan.Cadence);

        Assert.Equal(1000.00M, response.AccountCredit);

        var customer = subscription.Customer;
        Assert.Equal(customer.Address.Country, response.TaxInformation.Country);
        Assert.Equal(customer.Address.PostalCode, response.TaxInformation.PostalCode);
        Assert.Equal(customer.TaxIds.First().Value, response.TaxInformation.TaxId);
        Assert.Equal(customer.Address.Line1, response.TaxInformation.Line1);
        Assert.Equal(customer.Address.Line2, response.TaxInformation.Line2);
        Assert.Equal(customer.Address.City, response.TaxInformation.City);
        Assert.Equal(customer.Address.State, response.TaxInformation.State);

        Assert.Null(response.CancelAt);

        Assert.Equal(overdueInvoice.Created.AddDays(14), response.Suspension.SuspensionDate);
        Assert.Equal(overdueInvoice.PeriodEnd, response.Suspension.UnpaidPeriodEndDate);
        Assert.Equal(14, response.Suspension.GracePeriod);
    }

    #endregion

    #region UpdateTaxInformationAsync

    [Theory, BitAutoData]
    public async Task UpdateTaxInformation_NoCountry_BadRequest(
        Provider provider,
        TaxInformationRequestBody requestBody,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableProviderAdminInputs(provider, sutProvider);

        requestBody.Country = null;

        var result = await sutProvider.Sut.UpdateTaxInformationAsync(provider.Id, requestBody);

        Assert.IsType<BadRequest<ErrorResponseModel>>(result);

        var response = (BadRequest<ErrorResponseModel>)result;

        Assert.Equal("Country and postal code are required to update your tax information.", response.Value.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateTaxInformation_Ok(
        Provider provider,
        TaxInformationRequestBody requestBody,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableProviderAdminInputs(provider, sutProvider);

        await sutProvider.Sut.UpdateTaxInformationAsync(provider.Id, requestBody);

        await sutProvider.GetDependency<ISubscriberService>().Received(1).UpdateTaxInformation(
            provider, Arg.Is<TaxInformation>(
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
}
