using Bit.Api.Billing.Attributes;
using Bit.Api.Billing.Models.Requests.Tax;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Premium.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bit.Api.Billing.Controllers;

[Authorize("Application")]
[Route("billing/tax")]
public class TaxController(
    IPreviewOrganizationTaxCommand previewOrganizationTaxCommand,
    IPreviewPremiumTaxCommand previewPremiumTaxCommand) : BaseBillingController
{
    [HttpPost("organizations/subscriptions/purchase")]
    public async Task<IResult> PreviewOrganizationSubscriptionPurchaseTaxAsync(
        [FromBody] PreviewOrganizationSubscriptionPurchaseTaxRequest request)
    {
        var (purchase, billingAddress) = request.ToDomain();
        var result = await previewOrganizationTaxCommand.Run(purchase, billingAddress);
        return Handle(result.Map(pair => new
        {
            pair.Tax,
            pair.Total
        }));
    }

    [HttpPost("organizations/{organizationId:guid}/subscription/plan-change")]
    [InjectOrganization]
    public async Task<IResult> PreviewOrganizationSubscriptionPlanChangeTaxAsync(
        [BindNever] Organization organization,
        [FromBody] PreviewOrganizationSubscriptionPlanChangeTaxRequest request)
    {
        var (planChange, billingAddress) = request.ToDomain();
        var result = await previewOrganizationTaxCommand.Run(organization, planChange, billingAddress);
        return Handle(result.Map(pair => new
        {
            pair.Tax,
            pair.Total
        }));
    }

    [HttpPut("organizations/{organizationId:guid}/subscription/update")]
    [InjectOrganization]
    public async Task<IResult> PreviewOrganizationSubscriptionUpdateTaxAsync(
        [BindNever] Organization organization,
        [FromBody] PreviewOrganizationSubscriptionUpdateTaxRequest request)
    {
        var update = request.ToDomain();
        var result = await previewOrganizationTaxCommand.Run(organization, update);
        return Handle(result.Map(pair => new
        {
            pair.Tax,
            pair.Total
        }));
    }

    [HttpPost("premium/subscriptions/purchase")]
    public async Task<IResult> PreviewPremiumSubscriptionPurchaseTaxAsync(
        [FromBody] PreviewPremiumSubscriptionPurchaseTaxRequest request)
    {
        var (purchase, billingAddress) = request.ToDomain();
        var result = await previewPremiumTaxCommand.Run(purchase, billingAddress);
        return Handle(result.Map(pair => new
        {
            pair.Tax,
            pair.Total
        }));
    }
}
