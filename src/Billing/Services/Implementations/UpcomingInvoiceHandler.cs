using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Entities;
using Bit.Core.Models.Mail.UpdatedInvoiceIncoming;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Stripe;
using Event = Stripe.Event;
using Plan = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Billing.Services.Implementations;

using static StripeConstants;

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
    IValidateSponsorshipCommand validateSponsorshipCommand,
    IMailer mailer,
    IFeatureService featureService)
    : IUpcomingInvoiceHandler
{
    public async Task HandleAsync(Event parsedEvent)
    {
        var invoice = await stripeEventService.GetInvoice(parsedEvent);

        var customer =
            await stripeFacade.GetCustomer(invoice.CustomerId,
                new CustomerGetOptions { Expand = ["subscriptions", "tax", "tax_ids"] });

        var subscription = customer.Subscriptions.FirstOrDefault();

        if (subscription == null)
        {
            return;
        }

        var (organizationId, userId, providerId) = stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata);

        if (organizationId.HasValue)
        {
            await HandleOrganizationUpcomingInvoiceAsync(
                organizationId.Value,
                parsedEvent,
                invoice,
                customer,
                subscription);
        }
        else if (userId.HasValue)
        {
            await HandlePremiumUsersUpcomingInvoiceAsync(
                userId.Value,
                parsedEvent,
                invoice,
                customer,
                subscription);
        }
        else if (providerId.HasValue)
        {
            await HandleProviderUpcomingInvoiceAsync(
                providerId.Value,
                parsedEvent,
                invoice,
                customer,
                subscription);
        }
    }

    #region Organizations

    private async Task HandleOrganizationUpcomingInvoiceAsync(
        Guid organizationId,
        Event @event,
        Invoice invoice,
        Customer customer,
        Subscription subscription)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            logger.LogWarning("Could not find Organization ({OrganizationID}) for '{EventType}' event ({EventID})",
                organizationId, @event.Type, @event.Id);
            return;
        }

        await AlignOrganizationTaxConcernsAsync(organization, subscription, customer, @event.Id);

        var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);

        var milestone3 = featureService.IsEnabled(FeatureFlagKeys.PM26462_Milestone_3);

        await AlignOrganizationSubscriptionConcernsAsync(
            organization,
            @event,
            subscription,
            plan,
            milestone3);

        // Don't send the upcoming invoice email unless the organization's on an annual plan.
        if (!plan.IsAnnual)
        {
            return;
        }

        if (stripeEventUtilityService.IsSponsoredSubscription(subscription))
        {
            var sponsorshipIsValid =
                await validateSponsorshipCommand.ValidateSponsorshipAsync(organizationId);

            if (!sponsorshipIsValid)
            {
                /*
                 * If the sponsorship is invalid, then the subscription was updated to use the regular families plan
                 * price. Given that this is the case, we need the new invoice amount
                 */
                invoice = await stripeFacade.GetInvoice(subscription.LatestInvoiceId);
            }
        }

        await (milestone3
            ? SendUpdatedUpcomingInvoiceEmailsAsync([organization.BillingEmail])
            : SendUpcomingInvoiceEmailsAsync([organization.BillingEmail], invoice));
    }

    private async Task AlignOrganizationTaxConcernsAsync(
        Organization organization,
        Subscription subscription,
        Customer customer,
        string eventId)
    {
        var nonUSBusinessUse =
            organization.PlanType.GetProductTier() != ProductTierType.Families &&
            customer.Address.Country != Core.Constants.CountryAbbreviations.UnitedStates;

        if (nonUSBusinessUse && customer.TaxExempt != TaxExempt.Reverse)
        {
            try
            {
                await stripeFacade.UpdateCustomer(subscription.CustomerId,
                    new CustomerUpdateOptions { TaxExempt = TaxExempt.Reverse });
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

    private async Task AlignOrganizationSubscriptionConcernsAsync(
        Organization organization,
        Event @event,
        Subscription subscription,
        Plan plan,
        bool milestone3)
    {
        // currently these are the only plans that need aligned and both require the same flag and share most of the logic
        if (!milestone3 || plan.Type is not (PlanType.FamiliesAnnually2019 or PlanType.FamiliesAnnually2025))
        {
            return;
        }

        var passwordManagerItem =
            subscription.Items.FirstOrDefault(item => item.Price.Id == plan.PasswordManager.StripePlanId);

        if (passwordManagerItem == null)
        {
            logger.LogWarning("Could not find Organization's ({OrganizationId}) password manager item while processing '{EventType}' event ({EventID})",
                organization.Id, @event.Type, @event.Id);
            return;
        }

        var families = await pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually);

        organization.PlanType = families.Type;
        organization.Plan = families.Name;
        organization.UsersGetPremium = families.UsersGetPremium;
        organization.Seats = families.PasswordManager.BaseSeats;

        var options = new SubscriptionUpdateOptions
        {
            Items =
            [
                new SubscriptionItemOptions
                {
                    Id = passwordManagerItem.Id,
                        Price = families.PasswordManager.StripePlanId
                }
            ],
            ProrationBehavior = ProrationBehavior.None
        };

        if (plan.Type == PlanType.FamiliesAnnually2019)
        {
            options.Discounts =
            [
                new SubscriptionDiscountOptions { Coupon = CouponIDs.Milestone3SubscriptionDiscount }
            ];

            var premiumAccessAddOnItem = subscription.Items.FirstOrDefault(item =>
                item.Price.Id == plan.PasswordManager.StripePremiumAccessPlanId);

            if (premiumAccessAddOnItem != null)
            {
                options.Items.Add(new SubscriptionItemOptions
                {
                    Id = premiumAccessAddOnItem.Id,
                    Deleted = true
                });
            }

            var seatAddOnItem = subscription.Items.FirstOrDefault(item => item.Price.Id == "personal-org-seat-annually");

            if (seatAddOnItem != null)
            {
                options.Items.Add(new SubscriptionItemOptions
                {
                    Id = seatAddOnItem.Id,
                    Deleted = true
                });
            }
        }

        try
        {
            await organizationRepository.ReplaceAsync(organization);
            await stripeFacade.UpdateSubscription(subscription.Id, options);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to align subscription concerns for Organization ({OrganizationID}) while processing '{EventType}' event ({EventID})",
                organization.Id,
                @event.Type,
                @event.Id);
        }
    }

    #endregion

    #region Premium Users

    private async Task HandlePremiumUsersUpcomingInvoiceAsync(
        Guid userId,
        Event @event,
        Invoice invoice,
        Customer customer,
        Subscription subscription)
    {
        var user = await userRepository.GetByIdAsync(userId);

        if (user == null)
        {
            logger.LogWarning("Could not find User ({UserID}) for '{EventType}' event ({EventID})",
                userId, @event.Type, @event.Id);
            return;
        }

        await AlignPremiumUsersTaxConcernsAsync(user, @event, customer, subscription);

        var milestone2Feature = featureService.IsEnabled(FeatureFlagKeys.PM23341_Milestone_2);
        if (milestone2Feature)
        {
            await AlignPremiumUsersSubscriptionConcernsAsync(user, @event, subscription);
        }

        if (user.Premium)
        {
            await (milestone2Feature
                ? SendUpdatedUpcomingInvoiceEmailsAsync(new List<string> { user.Email })
                : SendUpcomingInvoiceEmailsAsync(new List<string> { user.Email }, invoice));
        }
    }

    private async Task AlignPremiumUsersTaxConcernsAsync(
        User user,
        Event @event,
        Customer customer,
        Subscription subscription)
    {
        if (!subscription.AutomaticTax.Enabled && customer.HasRecognizedTaxLocation())
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
                    @event.Id);
            }
        }
    }

    private async Task AlignPremiumUsersSubscriptionConcernsAsync(
        User user,
        Event @event,
        Subscription subscription)
    {
        var premiumItem = subscription.Items.FirstOrDefault(i => i.Price.Id == Prices.PremiumAnnually);

        if (premiumItem == null)
        {
            logger.LogWarning("Could not find User's ({UserID}) premium subscription item while processing '{EventType}' event ({EventID})",
                user.Id, @event.Type, @event.Id);
            return;
        }

        try
        {
            var plan = await pricingClient.GetAvailablePremiumPlan();
            await stripeFacade.UpdateSubscription(subscription.Id,
                new SubscriptionUpdateOptions
                {
                    Items =
                    [
                        new SubscriptionItemOptions { Id = premiumItem.Id, Price = plan.Seat.StripePriceId }
                    ],
                    Discounts =
                    [
                        new SubscriptionDiscountOptions { Coupon = CouponIDs.Milestone2SubscriptionDiscount }
                    ],
                    ProrationBehavior = ProrationBehavior.None
                });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to update user's ({UserID}) subscription price id while processing event with ID {EventID}",
                user.Id,
                @event.Id);
        }
    }

    #endregion

    #region Providers

    private async Task HandleProviderUpcomingInvoiceAsync(
        Guid providerId,
        Event @event,
        Invoice invoice,
        Customer customer,
        Subscription subscription)
    {
        var provider = await providerRepository.GetByIdAsync(providerId);

        if (provider == null)
        {
            logger.LogWarning("Could not find Provider ({ProviderID}) for '{EventType}' event ({EventID})",
                providerId, @event.Type, @event.Id);
            return;
        }

        await AlignProviderTaxConcernsAsync(provider, subscription, customer, @event.Id);

        if (!string.IsNullOrEmpty(provider.BillingEmail))
        {
            await SendProviderUpcomingInvoiceEmailsAsync(new List<string> { provider.BillingEmail }, invoice, subscription, providerId);
        }
    }

    private async Task AlignProviderTaxConcernsAsync(
        Provider provider,
        Subscription subscription,
        Customer customer,
        string eventId)
    {
        if (customer.Address.Country != Core.Constants.CountryAbbreviations.UnitedStates &&
            customer.TaxExempt != TaxExempt.Reverse)
        {
            try
            {
                await stripeFacade.UpdateCustomer(subscription.CustomerId,
                    new CustomerUpdateOptions { TaxExempt = TaxExempt.Reverse });
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

    private async Task SendProviderUpcomingInvoiceEmailsAsync(IEnumerable<string> emails, Invoice invoice,
        Subscription subscription, Guid providerId)
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

    #endregion

    #region Shared

    private async Task SendUpcomingInvoiceEmailsAsync(IEnumerable<string> emails, Invoice invoice)
    {
        var validEmails = emails.Where(e => !string.IsNullOrEmpty(e));

        var items = invoice.Lines.Select(i => i.Description).ToList();

        if (invoice is { NextPaymentAttempt: not null, AmountDue: > 0 })
        {
            await mailService.SendInvoiceUpcoming(
                validEmails,
                invoice.AmountDue / 100M,
                invoice.NextPaymentAttempt.Value,
                items,
                true);
        }
    }

    private async Task SendUpdatedUpcomingInvoiceEmailsAsync(IEnumerable<string> emails)
    {
        var validEmails = emails.Where(e => !string.IsNullOrEmpty(e));
        var updatedUpcomingEmail = new UpdatedInvoiceUpcomingMail
        {
            ToEmails = validEmails,
            View = new UpdatedInvoiceUpcomingView()
        };
        await mailer.SendEmail(updatedUpcomingEmail);
    }

    #endregion
}
