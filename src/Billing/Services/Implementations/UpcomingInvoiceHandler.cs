﻿// FIXME: Update this file to be null safe and then delete the line below

#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Stripe;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class UpcomingInvoiceHandler(
    IGetPaymentMethodQuery getPaymentMethodQuery,
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

        if (organizationId.HasValue)
        {
            var organization = await organizationRepository.GetByIdAsync(organizationId.Value);

            if (organization == null)
            {
                return;
            }

            await AlignOrganizationTaxConcernsAsync(organization, subscription, parsedEvent.Id);

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

            await AlignProviderTaxConcernsAsync(provider, subscription, parsedEvent.Id);

            await SendProviderUpcomingInvoiceEmailsAsync(new List<string> { provider.BillingEmail }, invoice, subscription, providerId.Value);
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

    private async Task SendProviderUpcomingInvoiceEmailsAsync(IEnumerable<string> emails, Invoice invoice, Subscription subscription, Guid providerId)
    {
        var validEmails = emails.Where(e => !string.IsNullOrEmpty(e));

        var items = invoice.FormatForProvider(subscription);

        if (invoice.NextPaymentAttempt.HasValue && invoice.AmountDue > 0)
        {
            var provider = await providerRepository.GetByIdAsync(providerId);
            if (provider == null)
            {
                logger.LogWarning("Provider {ProviderId} not found for invoice upcoming email", providerId);
                return;
            }

            var collectionMethod = subscription.CollectionMethod;
            var paymentMethod = await getPaymentMethodQuery.Run(provider);

            var hasPaymentMethod = paymentMethod != null;
            var paymentMethodDescription = paymentMethod?.Match(
                bankAccount => $"Bank account ending in {bankAccount.Last4}",
                card => $"{card.Brand} ending in {card.Last4}",
                payPal => $"PayPal account {payPal.Email}"
            );

            await mailService.SendProviderInvoiceUpcoming(
                validEmails,
                invoice.AmountDue / 100M,
                invoice.NextPaymentAttempt.Value,
                items,
                collectionMethod,
                hasPaymentMethod,
                paymentMethodDescription);
        }
    }

    private async Task AlignOrganizationTaxConcernsAsync(
        Organization organization,
        Subscription subscription,
        string eventId)
    {
        var nonUSBusinessUse =
            organization.PlanType.GetProductTier() != ProductTierType.Families &&
            subscription.Customer.Address.Country != Core.Constants.CountryAbbreviations.UnitedStates;

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

        if (!subscription.AutomaticTax.Enabled)
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
        string eventId)
    {
        if (subscription.Customer.Address.Country != Core.Constants.CountryAbbreviations.UnitedStates &&
            subscription.Customer.TaxExempt != StripeConstants.TaxExempt.Reverse)
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

        if (!subscription.AutomaticTax.Enabled)
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
