using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Providers.Models;
using Bit.Core.Billing.Providers.Queries;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Services;
using Stripe;
using Stripe.Tax;

namespace Bit.Commercial.Core.Billing.Providers.Queries;

using static Bit.Core.Constants;
using static StripeConstants;
using SuspensionWarning = ProviderWarnings.SuspensionWarning;
using TaxIdWarning = ProviderWarnings.TaxIdWarning;

public class GetProviderWarningsQuery(
    ICurrentContext currentContext,
    IStripeAdapter stripeAdapter,
    ISubscriberService subscriberService) : IGetProviderWarningsQuery
{
    public async Task<ProviderWarnings?> Run(Provider provider)
    {
        var warnings = new ProviderWarnings();

        var subscription =
            await subscriberService.GetSubscription(provider,
                new SubscriptionGetOptions { Expand = ["customer.tax_ids"] });

        if (subscription == null)
        {
            return warnings;
        }

        warnings.Suspension = GetSuspensionWarning(provider, subscription);

        warnings.TaxId = await GetTaxIdWarningAsync(provider, subscription.Customer);

        return warnings;
    }

    private SuspensionWarning? GetSuspensionWarning(
        Provider provider,
        Subscription subscription)
    {
        if (provider.Enabled)
        {
            return null;
        }

        return subscription.Status switch
        {
            SubscriptionStatus.Unpaid => currentContext.ProviderProviderAdmin(provider.Id)
                ? new SuspensionWarning { Resolution = "add_payment_method", SubscriptionCancelsAt = subscription.CancelAt }
                : new SuspensionWarning { Resolution = "contact_administrator" },
            _ => new SuspensionWarning { Resolution = "contact_support" }
        };
    }

    private async Task<TaxIdWarning?> GetTaxIdWarningAsync(
        Provider provider,
        Customer customer)
    {
        if (customer.Address?.Country == CountryAbbreviations.UnitedStates)
        {
            return null;
        }

        if (!currentContext.ProviderProviderAdmin(provider.Id))
        {
            return null;
        }

        // TODO: Potentially DRY this out with the GetOrganizationWarningsQuery

        // Get active and scheduled registrations
        var registrations = (await Task.WhenAll(
                stripeAdapter.TaxRegistrationsListAsync(new RegistrationListOptions { Status = TaxRegistrationStatus.Active }),
                stripeAdapter.TaxRegistrationsListAsync(new RegistrationListOptions { Status = TaxRegistrationStatus.Scheduled })))
            .SelectMany(registrations => registrations.Data);

        // Find the matching registration for the customer
        var registration = registrations.FirstOrDefault(registration => registration.Country == customer.Address?.Country);

        // If we're not registered in their country, we don't need a warning
        if (registration == null)
        {
            return null;
        }

        var taxId = customer.TaxIds.FirstOrDefault();

        return taxId switch
        {
            // Customer's tax ID is missing
            null => new TaxIdWarning { Type = "tax_id_missing" },
            // Not sure if this case is valid, but Stripe says this property is nullable
            not null when taxId.Verification == null => null,
            // Customer's tax ID is pending verification
            not null when taxId.Verification.Status == TaxIdVerificationStatus.Pending => new TaxIdWarning { Type = "tax_id_pending_verification" },
            // Customer's tax ID failed verification
            not null when taxId.Verification.Status == TaxIdVerificationStatus.Unverified => new TaxIdWarning { Type = "tax_id_failed_verification" },
            _ => null
        };
    }
}
