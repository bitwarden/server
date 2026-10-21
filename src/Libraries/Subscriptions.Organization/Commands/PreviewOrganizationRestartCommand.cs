using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Subscriptions.Queries;
using Bit.Core.Billing.Tax.Services;
using Bit.Core.Exceptions;
using Bit.Invoicing.InvoicePreviews;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.Organization.Helpers;
using Bit.Subscriptions.Organization.Models.Requests;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Subscriptions.Organization.Commands;

using static StripeConstants;
using OrganizationEntity = Core.AdminConsole.Entities.Organization;

internal interface IPreviewOrganizationRestartCommand
{
    /**
     * <summary>
     * Previews the invoice for restarting an organization's canceled subscription.
     * </summary>
     * <param name="organization">The organization whose canceled subscription would be restarted.</param>
     * <param name="request">The request containing the billing address.</param>
     * <returns>An <see cref="InvoicePreview"/> of what restarting the subscription would charge.</returns>
     *
     * <exception cref="BadRequestException">Thrown when the billing address is invalid or the subscription is not canceled.</exception>
     * <exception cref="ConflictException">Thrown when the canceled subscription can't be mapped to a plan and items.</exception>
     */
    Task<InvoicePreview> Run(OrganizationEntity organization, PreviewOrganizationRestartRequest request);
}

internal sealed class PreviewOrganizationRestartCommand(
    ILogger<PreviewOrganizationRestartCommand> logger,
    ISubscriberService subscriberService,
    IGetOrganizationRestartQuery getOrganizationRestartQuery,
    ITaxService taxService,
    IInvoicePreviewService invoicePreviewService) : IPreviewOrganizationRestartCommand
{
    public async Task<InvoicePreview> Run(OrganizationEntity organization, PreviewOrganizationRestartRequest request)
    {
        var address = BillingAddressResolver.ResolveAddress(request.BillingAddress);
        var requestTaxId = BillingAddressResolver.ValidateTaxId(request.BillingAddress!.TaxId);

        var canceledSubscription = await subscriberService.GetSubscription(organization);

        if (canceledSubscription is not { Status: SubscriptionStatus.Canceled })
        {
            throw new BadRequestException("Cannot restart a subscription that is not canceled.");
        }

        var restart = await getOrganizationRestartQuery.Run(organization, canceledSubscription);

        var tier = restart.Plan.ProductTier switch
        {
            ProductTierType.Families => PlanTierType.Families,
            ProductTierType.Teams or ProductTierType.TeamsStarter => PlanTierType.Teams,
            ProductTierType.Enterprise => PlanTierType.Enterprise,
            _ => throw new ConflictException($"Cannot preview restarting a subscription on the {restart.Plan.ProductTier} tier.")
        };
        var cadence = restart.Plan.IsAnnual ? PlanCadenceType.Annually : PlanCadenceType.Monthly;

        // Only the request's tax ID is sent. Restart replaces the customer's tax IDs with the request's, so
        // the ones on file must not leak into the preview.
        var options = new InvoiceCreatePreviewOptions
        {
            Customer = canceledSubscription.CustomerId,
            SubscriptionDetails = new InvoiceSubscriptionDetailsOptions
            {
                Items = restart.Items
                    .Select(item => new InvoiceSubscriptionDetailsItemOptions { Price = item.PriceId, Quantity = item.Quantity })
                    .ToList(),
                BillingMode = new InvoiceSubscriptionDetailsBillingModeOptions { Type = BillingMode.Classic }
            },
            AutomaticTax = new InvoiceAutomaticTaxOptions { Enabled = true },
            CustomerDetails = new InvoiceCustomerDetailsOptions
            {
                Address = address,
                TaxIds = BillingAddressResolver.ResolveTaxIds(
                    taxService, logger, request.BillingAddress.Country!, requestTaxId)
            }
        };

        try
        {
            return await invoicePreviewService.GetInvoicePreviewAsync(options, tier, cadence);
        }
        catch (StripeException stripeException)
            when (stripeException.StripeError?.Code == ErrorCodes.CustomerTaxLocationInvalid)
        {
            throw new BadRequestException(
                "Your location wasn't recognized. Please ensure your country and postal code are valid and try again.");
        }
        catch (StripeException stripeException)
            when (stripeException.StripeError?.Code == ErrorCodes.TaxIdInvalid)
        {
            throw new BadRequestException(
                "Your tax ID wasn't recognized for your selected country. Please ensure your country and tax ID are valid.");
        }
    }
}
