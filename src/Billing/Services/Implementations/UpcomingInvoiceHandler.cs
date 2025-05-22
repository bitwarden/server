﻿using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Pricing;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Stripe;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class UpcomingInvoiceHandler(
    IFeatureService featureService,
    ILogger<StripeEventProcessor> logger,
    IMailService mailService,
    IOrganizationRepository organizationRepository,
    IPricingClient pricingClient,
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

        var setNonUSBusinessUseToReverseCharge = featureService.IsEnabled(FeatureFlagKeys.PM21092_SetNonUSBusinessUseToReverseCharge);

        if (organizationId.HasValue)
        {
            var organization = await organizationRepository.GetByIdAsync(organizationId.Value);

            if (organization == null)
            {
                return;
            }

            await AlignOrganizationTaxConcernsAsync(organization, subscription, parsedEvent.Id, setNonUSBusinessUseToReverseCharge);

            var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);

            if (!plan.IsAnnual)
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

            if (!subscription.AutomaticTax.Enabled && subscription.Customer.HasRecognizedTaxLocation())
            {
                try
                {
                    await stripeFacade.UpdateSubscription(subscription.Id,
                        new SubscriptionUpdateOptions
                        {
                            AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true }
                        });
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Failed to set user's ({UserID}) subscription to automatic tax while processing event with ID {EventID}",
                        user.Id,
                        parsedEvent.Id);
                }
            }

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

            await AlignProviderTaxConcernsAsync(provider, subscription, parsedEvent.Id, setNonUSBusinessUseToReverseCharge);

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

    private async Task AlignOrganizationTaxConcernsAsync(
        Organization organization,
        Subscription subscription,
        string eventId,
        bool setNonUSBusinessUseToReverseCharge)
    {
        var nonUSBusinessUse =
            organization.PlanType.GetProductTier() != ProductTierType.Families &&
            subscription.Customer.Address.Country != "US";

        bool setAutomaticTaxToEnabled;

        if (setNonUSBusinessUseToReverseCharge)
        {
            if (nonUSBusinessUse && subscription.Customer.TaxExempt != StripeConstants.TaxExempt.Reverse)
            {
                try
                {
                    await stripeFacade.UpdateCustomer(subscription.CustomerId,
                        new CustomerUpdateOptions { TaxExempt = StripeConstants.TaxExempt.Reverse });
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Failed to set organization's ({OrganizationID}) to reverse tax exemption while processing event with ID {EventID}",
                        organization.Id,
                        eventId);
                }
            }

            setAutomaticTaxToEnabled = true;
        }
        else
        {
            setAutomaticTaxToEnabled =
                subscription.Customer.HasRecognizedTaxLocation() &&
                (subscription.Customer.Address.Country == "US" ||
                 (nonUSBusinessUse && subscription.Customer.TaxIds.Any()));
        }

        if (!subscription.AutomaticTax.Enabled && setAutomaticTaxToEnabled)
        {
            try
            {
                await stripeFacade.UpdateSubscription(subscription.Id,
                    new SubscriptionUpdateOptions
                    {
                        AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true }
                    });
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to set organization's ({OrganizationID}) subscription to automatic tax while processing event with ID {EventID}",
                    organization.Id,
                    eventId);
            }
        }
    }

    private async Task AlignProviderTaxConcernsAsync(
        Provider provider,
        Subscription subscription,
        string eventId,
        bool setNonUSBusinessUseToReverseCharge)
    {
        bool setAutomaticTaxToEnabled;

        if (setNonUSBusinessUseToReverseCharge)
        {
            if (subscription.Customer.Address.Country != "US" && subscription.Customer.TaxExempt != StripeConstants.TaxExempt.Reverse)
            {
                try
                {
                    await stripeFacade.UpdateCustomer(subscription.CustomerId,
                        new CustomerUpdateOptions { TaxExempt = StripeConstants.TaxExempt.Reverse });
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Failed to set provider's ({ProviderID}) to reverse tax exemption while processing event with ID {EventID}",
                        provider.Id,
                        eventId);
                }
            }

            setAutomaticTaxToEnabled = true;
        }
        else
        {
            setAutomaticTaxToEnabled =
                subscription.Customer.HasRecognizedTaxLocation() &&
                (subscription.Customer.Address.Country == "US" ||
                 subscription.Customer.TaxIds.Any());
        }

        if (!subscription.AutomaticTax.Enabled && setAutomaticTaxToEnabled)
        {
            try
            {
                await stripeFacade.UpdateSubscription(subscription.Id,
                    new SubscriptionUpdateOptions
                    {
                        AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true }
                    });
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to set provider's ({ProviderID}) subscription to automatic tax while processing event with ID {EventID}",
                    provider.Id,
                    eventId);
            }
        }
    }
}
