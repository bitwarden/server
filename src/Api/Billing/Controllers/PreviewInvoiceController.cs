using Bit.Api.Billing.Attributes;
using Bit.Api.Billing.Models.Requests.PreviewInvoice;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Premium.Commands;
using Bit.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bit.Api.Billing.Controllers;

[Authorize("Application")]
[Route("billing/preview-invoice")]
public class PreviewInvoiceController(
    IPreviewOrganizationTaxCommand previewOrganizationTaxCommand,
    IPreviewPremiumTaxCommand previewPremiumTaxCommand,
    IPreviewPremiumUpgradeProrationCommand previewPremiumUpgradeProrationCommand) : BaseBillingController
{
    [HttpPost("organizations/subscriptions/purchase")]
    [InjectUser]
    public async Task<IResult> PreviewOrganizationSubscriptionPurchaseTaxAsync(
        [BindNever] User user,
        [FromBody] PreviewOrganizationSubscriptionPurchaseTaxRequest request)
    {
        var (purchase, billingAddress) = request.ToDomain();
        var result = await previewOrganizationTaxCommand.Run(user, purchase, billingAddress);
        return Handle(result.Map(pair => new { pair.Tax, pair.Total }));
    }

    [HttpPost("organizations/{organizationId:guid}/subscription/plan-change")]
    [InjectOrganization]
    public async Task<IResult> PreviewOrganizationSubscriptionPlanChangeTaxAsync(
        [BindNever] Organization organization,
        [FromBody] PreviewOrganizationSubscriptionPlanChangeTaxRequest request)
    {
        var (planChange, billingAddress) = request.ToDomain();
        var result = await previewOrganizationTaxCommand.Run(organization, planChange, billingAddress);
        return Handle(result.Map(pair => new { pair.Tax, pair.Total }));
    }

    [HttpPut("organizations/{organizationId:guid}/subscription/update")]
    [InjectOrganization]
    public async Task<IResult> PreviewOrganizationSubscriptionUpdateTaxAsync(
        [BindNever] Organization organization,
        [FromBody] PreviewOrganizationSubscriptionUpdateTaxRequest request)
    {
        var update = request.ToDomain();
        var result = await previewOrganizationTaxCommand.Run(organization, update);
        return Handle(result.Map(pair => new { pair.Tax, pair.Total }));
    }

    [HttpPost("premium/subscriptions/purchase")]
    [InjectUser]
    public async Task<IResult> PreviewPremiumSubscriptionPurchaseTaxAsync(
        [BindNever] User user,
        [FromBody] PreviewPremiumSubscriptionPurchaseTaxRequest request)
    {
        var (preview, billingAddress) = request.ToDomain();
        var result = await previewPremiumTaxCommand.Run(user, preview, billingAddress);
        return Handle(result.Map(pair => new { pair.Tax, pair.Total }));
    }

    [HttpPost("premium/subscriptions/upgrade")]
    [InjectUser]
    public async Task<IResult> PreviewPremiumUpgradeProrationAsync(
        [BindNever] User user,
        [FromBody] PreviewPremiumUpgradeProrationRequest request)
    {
        var (planType, billingAddress) = request.ToDomain();

        var result = await previewPremiumUpgradeProrationCommand.Run(
            user,
            planType,
            billingAddress);

        return Handle(result.Map(proration => new
        {
            proration.NewPlanProratedAmount,
            proration.Credit,
            proration.Tax,
            proration.Total,
            proration.NewPlanProratedMonths
        }));
    }
}
