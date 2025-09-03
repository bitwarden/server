using Bit.Api.AdminConsole.Authorization;
using Bit.Api.AdminConsole.Authorization.Requirements;
using Bit.Api.Billing.Attributes;
using Bit.Api.Billing.Models.Requests.Payment;
using Bit.Api.Billing.Models.Requirements;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.Queries;
using Bit.Core.Billing.Payment.Commands;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
// ReSharper disable RouteTemplates.MethodMissingRouteParameters

namespace Bit.Api.Billing.Controllers.VNext;

[Authorize("Application")]
[Route("organizations/{organizationId:guid}/billing/vnext")]
[SelfHosted(NotSelfHostedOnly = true)]
public class OrganizationBillingVNextController(
    ICreateBitPayInvoiceForCreditCommand createBitPayInvoiceForCreditCommand,
    IGetBillingAddressQuery getBillingAddressQuery,
    IGetCreditQuery getCreditQuery,
    IGetOrganizationWarningsQuery getOrganizationWarningsQuery,
    IGetPaymentMethodQuery getPaymentMethodQuery,
    IUpdateBillingAddressCommand updateBillingAddressCommand,
    IUpdatePaymentMethodCommand updatePaymentMethodCommand,
    IVerifyBankAccountCommand verifyBankAccountCommand) : BaseBillingController
{
    [Authorize<ManageOrganizationBillingRequirement>]
    [HttpGet("address")]
    [InjectOrganization]
    public async Task<IResult> GetBillingAddressAsync(
        [BindNever] Organization organization)
    {
        var billingAddress = await getBillingAddressQuery.Run(organization);
        return TypedResults.Ok(billingAddress);
    }

    [Authorize<ManageOrganizationBillingRequirement>]
    [HttpPut("address")]
    [InjectOrganization]
    public async Task<IResult> UpdateBillingAddressAsync(
        [BindNever] Organization organization,
        [FromBody] BillingAddressRequest request)
    {
        var billingAddress = request.ToDomain();
        var result = await updateBillingAddressCommand.Run(organization, billingAddress);
        return Handle(result);
    }

    [Authorize<ManageOrganizationBillingRequirement>]
    [HttpGet("credit")]
    [InjectOrganization]
    public async Task<IResult> GetCreditAsync(
        [BindNever] Organization organization)
    {
        var credit = await getCreditQuery.Run(organization);
        return TypedResults.Ok(credit);
    }

    [Authorize<ManageOrganizationBillingRequirement>]
    [HttpPost("credit/bitpay")]
    [InjectOrganization]
    public async Task<IResult> AddCreditViaBitPayAsync(
        [BindNever] Organization organization,
        [FromBody] BitPayCreditRequest request)
    {
        var result = await createBitPayInvoiceForCreditCommand.Run(
            organization,
            request.Amount,
            request.RedirectUrl);
        return Handle(result);
    }

    [Authorize<ManageOrganizationBillingRequirement>]
    [HttpGet("payment-method")]
    [InjectOrganization]
    public async Task<IResult> GetPaymentMethodAsync(
        [BindNever] Organization organization)
    {
        var paymentMethod = await getPaymentMethodQuery.Run(organization);
        return TypedResults.Ok(paymentMethod);
    }

    [Authorize<ManageOrganizationBillingRequirement>]
    [HttpPut("payment-method")]
    [InjectOrganization]
    public async Task<IResult> UpdatePaymentMethodAsync(
        [BindNever] Organization organization,
        [FromBody] TokenizedPaymentMethodRequest request)
    {
        var (paymentMethod, billingAddress) = request.ToDomain();
        var result = await updatePaymentMethodCommand.Run(organization, paymentMethod, billingAddress);
        return Handle(result);
    }

    [Authorize<ManageOrganizationBillingRequirement>]
    [HttpPost("payment-method/verify-bank-account")]
    [InjectOrganization]
    public async Task<IResult> VerifyBankAccountAsync(
        [BindNever] Organization organization,
        [FromBody] VerifyBankAccountRequest request)
    {
        var result = await verifyBankAccountCommand.Run(organization, request.DescriptorCode);
        return Handle(result);
    }

    [Authorize<MemberOrProviderRequirement>]
    [HttpGet("warnings")]
    [InjectOrganization]
    public async Task<IResult> GetWarningsAsync(
        [BindNever] Organization organization)
    {
        var warnings = await getOrganizationWarningsQuery.Run(organization);
        return TypedResults.Ok(warnings);
    }
}
