using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Stripe;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class UpcomingInvoiceHandler(
    ILogger<StripeEventProcessor> logger,
    IMailService mailService,
    IOrganizationRepository organizationRepository,
    IProviderRepository providerRepository,
    IStripeFacade stripeFacade,
    IStripeEventService stripeEventService,
    IStripeEventUtilityService stripeEventUtilityService,
    IUserRepository userRepository,
    IValidateSponsorshipCommand validateSponsorshipCommand)
    : IUpcomingInvoiceHandler
{
    public async Task HandleAsync(Event parsedEvent)
    {
        var invoice = await stripeEventService.GetInvoice(parsedEvent);

        if (string.IsNullOrEmpty(invoice.SubscriptionId))
        {
            logger.LogInformation("Received 'invoice.upcoming' Event with ID '{eventId}' that did not include a Subscription ID", parsedEvent.Id);
            return;
        }

        var subscription = await stripeFacade.GetSubscription(invoice.SubscriptionId, new SubscriptionGetOptions
        {
            Expand = ["customer.tax", "customer.tax_ids"]
        });

        var (organizationId, userId, providerId) = stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata);

        if (organizationId.HasValue)
        {
            var organization = await organizationRepository.GetByIdAsync(organizationId.Value);

            if (organization == null)
            {
                return;
            }

            await TryEnableAutomaticTaxAsync(subscription);

            if (!HasAnnualPlan(organization))
            {
                return;
            }

            if (stripeEventUtilityService.IsSponsoredSubscription(subscription))
            {
                var sponsorshipIsValid = await validateSponsorshipCommand.ValidateSponsorshipAsync(organizationId.Value);

                if (!sponsorshipIsValid)
                {
                    /*
                     * If the sponsorship is invalid, then the subscription was updated to use the regular families plan
                     * price. Given that this is the case, we need the new invoice amount
                     */
                    invoice = await stripeFacade.GetInvoice(subscription.LatestInvoiceId);
                }
            }

            await SendUpcomingInvoiceEmailsAsync(new List<string> { organization.BillingEmail }, invoice);

            /*
             * TODO: https://bitwarden.atlassian.net/browse/PM-4862
             * Disabling this as part of a hot fix. It needs to check whether the organization
             * belongs to a Reseller provider and only send an email to the organization owners if it does.
             * It also requires a new email template as the current one contains too much billing information.
             */

            // var ownerEmails = await _organizationRepository.GetOwnerEmailAddressesById(organization.Id);

            // await SendEmails(ownerEmails);
        }
        else if (userId.HasValue)
        {
            var user = await userRepository.GetByIdAsync(userId.Value);

            if (user == null)
            {
                return;
            }

            await TryEnableAutomaticTaxAsync(subscription);

            if (user.Premium)
            {
                await SendUpcomingInvoiceEmailsAsync(new List<string> { user.Email }, invoice);
            }
        }
        else if (providerId.HasValue)
        {
            var provider = await providerRepository.GetByIdAsync(providerId.Value);

            if (provider == null)
            {
                return;
            }

            await TryEnableAutomaticTaxAsync(subscription);

            await SendUpcomingInvoiceEmailsAsync(new List<string> { provider.BillingEmail }, invoice);
        }
    }

    private async Task SendUpcomingInvoiceEmailsAsync(IEnumerable<string> emails, Invoice invoice)
    {
        var validEmails = emails.Where(e => !string.IsNullOrEmpty(e));

        var items = invoice.Lines.Select(i => i.Description).ToList();

        if (invoice.NextPaymentAttempt.HasValue && invoice.AmountDue > 0)
        {
            await mailService.SendInvoiceUpcoming(
                validEmails,
                invoice.AmountDue / 100M,
                invoice.NextPaymentAttempt.Value,
                items,
                true);
        }
    }

    private async Task TryEnableAutomaticTaxAsync(Subscription subscription)
    {
        if (subscription.AutomaticTax.Enabled ||
            !subscription.Customer.HasBillingLocation() ||
            IsNonTaxableNonUSBusinessUseSubscription(subscription))
        {
            return;
        }

        await stripeFacade.UpdateSubscription(subscription.Id,
            new SubscriptionUpdateOptions
            {
                DefaultTaxRates = [], AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true }
            });

        return;

        bool IsNonTaxableNonUSBusinessUseSubscription(Subscription localSubscription)
        {
            var familyPriceIds = new List<string>
            {
                // TODO: Replace with the PricingClient
                StaticStore.GetPlan(PlanType.FamiliesAnnually2019).PasswordManager.StripePlanId,
                StaticStore.GetPlan(PlanType.FamiliesAnnually).PasswordManager.StripePlanId
            };

            return localSubscription.Customer.Address.Country != "US" &&
                   localSubscription.Metadata.ContainsKey(StripeConstants.MetadataKeys.OrganizationId) &&
                   !localSubscription.Items.Select(item => item.Price.Id).Intersect(familyPriceIds).Any() &&
                   !localSubscription.Customer.TaxIds.Any();
        }
    }

    private static bool HasAnnualPlan(Organization org) => StaticStore.GetPlan(org.PlanType).IsAnnual;
}
