#nullable enable
using System.Diagnostics;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.Billing.Models.Requests;
using Bit.Api.Billing.Models.Responses;
using Bit.Api.Billing.Queries.Organizations;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Models.Sales;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.Services;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Providers.Services;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Tax.Models;
using Bit.Core.Context;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers;

[Route("organizations/{organizationId:guid}/billing")]
[Authorize("Application")]
public class OrganizationBillingController(
    IBusinessUnitConverter businessUnitConverter,
    ICurrentContext currentContext,
    IOrganizationBillingService organizationBillingService,
    IOrganizationRepository organizationRepository,
    IOrganizationWarningsQuery organizationWarningsQuery,
    IPaymentService paymentService,
    IPricingClient pricingClient,
    ISubscriberService subscriberService,
    IPaymentHistoryService paymentHistoryService,
    IUserService userService) : BaseBillingController
{
    [HttpGet("metadata")]
    public async Task<IResult> GetMetadataAsync([FromRoute] Guid organizationId)
    {
        if (!await currentContext.OrganizationUser(organizationId))
        {
            return Error.Unauthorized();
        }

        var metadata = await organizationBillingService.GetMetadata(organizationId);

        if (metadata == null)
        {
            return Error.NotFound();
        }

        var response = OrganizationMetadataResponse.From(metadata);

        return TypedResults.Ok(response);
    }

    [HttpGet("history")]
    public async Task<IResult> GetHistoryAsync([FromRoute] Guid organizationId)
    {
        if (!await currentContext.ViewBillingHistory(organizationId))
        {
            return Error.Unauthorized();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return Error.NotFound();
        }

        var billingInfo = await paymentService.GetBillingHistoryAsync(organization);

        return TypedResults.Ok(billingInfo);
    }

    [HttpGet("invoices")]
    public async Task<IResult> GetInvoicesAsync([FromRoute] Guid organizationId, [FromQuery] string? status = null, [FromQuery] string? startAfter = null)
    {
        if (!await currentContext.ViewBillingHistory(organizationId))
        {
            return TypedResults.Unauthorized();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return TypedResults.NotFound();
        }

        var invoices = await paymentHistoryService.GetInvoiceHistoryAsync(
            organization,
            5,
            status,
            startAfter);

        return TypedResults.Ok(invoices);
    }

    [HttpGet("transactions")]
    public async Task<IResult> GetTransactionsAsync([FromRoute] Guid organizationId, [FromQuery] DateTime? startAfter = null)
    {
        if (!await currentContext.ViewBillingHistory(organizationId))
        {
            return TypedResults.Unauthorized();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return TypedResults.NotFound();
        }

        var transactions = await paymentHistoryService.GetTransactionHistoryAsync(
            organization,
            5,
            startAfter);

        return TypedResults.Ok(transactions);
    }

    [HttpGet]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<IResult> GetBillingAsync(Guid organizationId)
    {
        if (!await currentContext.ViewBillingHistory(organizationId))
        {
            return Error.Unauthorized();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return Error.NotFound();
        }

        var billingInfo = await paymentService.GetBillingAsync(organization);

        var response = new BillingResponseModel(billingInfo);

        return TypedResults.Ok(response);
    }

    [HttpGet("payment-method")]
    public async Task<IResult> GetPaymentMethodAsync([FromRoute] Guid organizationId)
    {
        if (!await currentContext.EditPaymentMethods(organizationId))
        {
            return Error.Unauthorized();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return Error.NotFound();
        }

        var paymentMethod = await subscriberService.GetPaymentMethod(organization);

        var response = PaymentMethodResponse.From(paymentMethod);

        return TypedResults.Ok(response);
    }

    [HttpPut("payment-method")]
    public async Task<IResult> UpdatePaymentMethodAsync(
        [FromRoute] Guid organizationId,
        [FromBody] UpdatePaymentMethodRequestBody requestBody)
    {
        if (!await currentContext.EditPaymentMethods(organizationId))
        {
            return Error.Unauthorized();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return Error.NotFound();
        }

        var tokenizedPaymentSource = requestBody.PaymentSource.ToDomain();

        var taxInformation = requestBody.TaxInformation.ToDomain();

        await organizationBillingService.UpdatePaymentMethod(organization, tokenizedPaymentSource, taxInformation);

        return TypedResults.Ok();
    }

    [HttpPost("payment-method/verify-bank-account")]
    public async Task<IResult> VerifyBankAccountAsync(
        [FromRoute] Guid organizationId,
        [FromBody] VerifyBankAccountRequestBody requestBody)
    {
        if (!await currentContext.EditPaymentMethods(organizationId))
        {
            return Error.Unauthorized();
        }

        if (requestBody.DescriptorCode.Length != 6 || !requestBody.DescriptorCode.StartsWith("SM"))
        {
            return Error.BadRequest("Statement descriptor should be a 6-character value that starts with 'SM'");
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return Error.NotFound();
        }

        await subscriberService.VerifyBankAccount(organization, requestBody.DescriptorCode);

        return TypedResults.Ok();
    }

    [HttpGet("tax-information")]
    public async Task<IResult> GetTaxInformationAsync([FromRoute] Guid organizationId)
    {
        if (!await currentContext.EditPaymentMethods(organizationId))
        {
            return Error.Unauthorized();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return Error.NotFound();
        }

        var taxInformation = await subscriberService.GetTaxInformation(organization);

        var response = TaxInformationResponse.From(taxInformation);

        return TypedResults.Ok(response);
    }

    [HttpPut("tax-information")]
    public async Task<IResult> UpdateTaxInformationAsync(
        [FromRoute] Guid organizationId,
        [FromBody] TaxInformationRequestBody requestBody)
    {
        if (!await currentContext.EditPaymentMethods(organizationId))
        {
            return Error.Unauthorized();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return Error.NotFound();
        }

        var taxInformation = requestBody.ToDomain();

        await subscriberService.UpdateTaxInformation(organization, taxInformation);

        return TypedResults.Ok();
    }

    [HttpPost("restart-subscription")]
    public async Task<IResult> RestartSubscriptionAsync([FromRoute] Guid organizationId,
        [FromBody] OrganizationCreateRequestModel model)
    {
        var user = await userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        if (!await currentContext.EditPaymentMethods(organizationId))
        {
            return Error.Unauthorized();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);
        if (organization == null)
        {
            return Error.NotFound();
        }
        var existingPlan = organization.PlanType;
        var organizationSignup = model.ToOrganizationSignup(user);
        var sale = OrganizationSale.From(organization, organizationSignup);
        var plan = await pricingClient.GetPlanOrThrow(model.PlanType);
        sale.Organization.PlanType = plan.Type;
        sale.Organization.Plan = plan.Name;
        sale.SubscriptionSetup.SkipTrial = true;
        if (existingPlan == PlanType.Free && organization.GatewaySubscriptionId is not null)
        {
            sale.Organization.UseTotp = plan.HasTotp;
            sale.Organization.UseGroups = plan.HasGroups;
            sale.Organization.UseDirectory = plan.HasDirectory;
            sale.Organization.SelfHost = plan.HasSelfHost;
            sale.Organization.UsersGetPremium = plan.UsersGetPremium;
            sale.Organization.UseEvents = plan.HasEvents;
            sale.Organization.Use2fa = plan.Has2fa;
            sale.Organization.UseApi = plan.HasApi;
            sale.Organization.UsePolicies = plan.HasPolicies;
            sale.Organization.UseSso = plan.HasSso;
            sale.Organization.UseResetPassword = plan.HasResetPassword;
            sale.Organization.UseKeyConnector = plan.HasKeyConnector;
            sale.Organization.UseScim = plan.HasScim;
            sale.Organization.UseCustomPermissions = plan.HasCustomPermissions;
            sale.Organization.UseOrganizationDomains = plan.HasOrganizationDomains;
            sale.Organization.MaxCollections = plan.PasswordManager.MaxCollections;
        }

        if (organizationSignup.PaymentMethodType == null || string.IsNullOrEmpty(organizationSignup.PaymentToken))
        {
            return Error.BadRequest("A payment method is required to restart the subscription.");
        }
        var org = await organizationRepository.GetByIdAsync(organizationId);
        Debug.Assert(org is not null, "This organization has already been found via this same ID, this should be fine.");
        var paymentSource = new TokenizedPaymentSource(organizationSignup.PaymentMethodType.Value, organizationSignup.PaymentToken);
        var taxInformation = TaxInformation.From(organizationSignup.TaxInfo);
        await organizationBillingService.Finalize(sale);
        var updatedOrg = await organizationRepository.GetByIdAsync(organizationId);
        if (updatedOrg != null)
        {
            await organizationBillingService.UpdatePaymentMethod(updatedOrg, paymentSource, taxInformation);
        }

        return TypedResults.Ok();
    }

    [HttpPost("setup-business-unit")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<IResult> SetupBusinessUnitAsync(
        [FromRoute] Guid organizationId,
        [FromBody] SetupBusinessUnitRequestBody requestBody)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return Error.NotFound();
        }

        if (!await currentContext.OrganizationUser(organizationId))
        {
            return Error.Unauthorized();
        }

        var providerId = await businessUnitConverter.FinalizeConversion(
            organization,
            requestBody.UserId,
            requestBody.Token,
            requestBody.ProviderKey,
            requestBody.OrganizationKey);

        return TypedResults.Ok(providerId);
    }

    [HttpGet("warnings")]
    public async Task<IResult> GetWarningsAsync([FromRoute] Guid organizationId)
    {
        /*
         * We'll keep these available at the User level, because we're hiding any pertinent information and
         * we want to throw as few errors as possible since these are not core features.
         */
        if (!await currentContext.OrganizationUser(organizationId))
        {
            return Error.Unauthorized();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return Error.NotFound();
        }

        var response = await organizationWarningsQuery.Run(organization);

        return TypedResults.Ok(response);
    }
}
