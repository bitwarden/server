using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Stripe;
using Stripe.Tax;

namespace Bit.Core.Billing.Organizations.Queries;

using static Core.Constants;
using static StripeConstants;
using FreeTrialWarning = OrganizationWarnings.FreeTrialWarning;
using InactiveSubscriptionWarning = OrganizationWarnings.InactiveSubscriptionWarning;
using ResellerRenewalWarning = OrganizationWarnings.ResellerRenewalWarning;
using TaxIdWarning = OrganizationWarnings.TaxIdWarning;

public interface IGetOrganizationWarningsQuery
{
    Task<OrganizationWarnings> Run(
        Organization organization);
}

public class GetOrganizationWarningsQuery(
    ICurrentContext currentContext,
    IProviderRepository providerRepository,
    ISetupIntentCache setupIntentCache,
    IStripeAdapter stripeAdapter,
    ISubscriberService subscriberService) : IGetOrganizationWarningsQuery
{
    public async Task<OrganizationWarnings> Run(
        Organization organization)
    {
        var warnings = new OrganizationWarnings();

        var subscription =
            await subscriberService.GetSubscription(organization,
                new SubscriptionGetOptions { Expand = ["customer.tax_ids", "latest_invoice", "test_clock"] });

        if (subscription == null)
        {
            return warnings;
        }

        warnings.FreeTrial = await GetFreeTrialWarningAsync(organization, subscription);

        var provider = await providerRepository.GetByOrganizationIdAsync(organization.Id);

        warnings.InactiveSubscription = await GetInactiveSubscriptionWarningAsync(organization, provider, subscription);

        warnings.ResellerRenewal = await GetResellerRenewalWarningAsync(provider, subscription);

        warnings.TaxId = await GetTaxIdWarningAsync(organization, subscription.Customer, provider);

        return warnings;
    }

    private async Task<FreeTrialWarning?> GetFreeTrialWarningAsync(
        Organization organization,
        Subscription subscription)
    {
        if (!await currentContext.EditSubscription(organization.Id))
        {
            return null;
        }

        if (subscription is not
            {
                Status: SubscriptionStatus.Trialing,
                TrialEnd: not null,
                Customer: not null
            })
        {
            return null;
        }

        var customer = subscription.Customer;

        var hasUnverifiedBankAccount = await HasUnverifiedBankAccountAsync(organization);

        var hasPaymentMethod =
            !string.IsNullOrEmpty(customer.InvoiceSettings.DefaultPaymentMethodId) ||
            !string.IsNullOrEmpty(customer.DefaultSourceId) ||
            hasUnverifiedBankAccount ||
            customer.Metadata.ContainsKey(MetadataKeys.BraintreeCustomerId);

        if (hasPaymentMethod)
        {
            return null;
        }

        var now = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;

        var remainingTrialDays = (int)Math.Ceiling((subscription.TrialEnd.Value - now).TotalDays);

        return new FreeTrialWarning { RemainingTrialDays = remainingTrialDays };
    }

    private async Task<InactiveSubscriptionWarning?> GetInactiveSubscriptionWarningAsync(
        Organization organization,
        Provider? provider,
        Subscription subscription)
    {
        // If the organization is enabled or the subscription is active, don't return a warning.
        if (organization.Enabled || subscription is not
            {
                Status: SubscriptionStatus.Unpaid or SubscriptionStatus.Canceled
            })
        {
            return null;
        }

        // If the organization is managed by a provider, return a warning asking them to contact the provider.
        if (provider != null)
        {
            return new InactiveSubscriptionWarning { Resolution = "contact_provider" };
        }

        var isOrganizationOwner = await currentContext.OrganizationOwner(organization.Id);

        /* If the organization is not managed by a provider and this user is the owner, return a warning based
           on the subscription status. */
        if (isOrganizationOwner)
        {
            return subscription.Status switch
            {
                SubscriptionStatus.Unpaid => new InactiveSubscriptionWarning
                {
                    Resolution = "add_payment_method"
                },
                SubscriptionStatus.Canceled => new InactiveSubscriptionWarning
                {
                    Resolution = "resubscribe"
                },
                _ => null
            };
        }

        // Otherwise, return a warning asking them to contact the owner.
        return new InactiveSubscriptionWarning { Resolution = "contact_owner" };
    }

    private async Task<ResellerRenewalWarning?> GetResellerRenewalWarningAsync(
        Provider? provider,
        Subscription subscription)
    {
        if (provider is not
            {
                Type: ProviderType.Reseller
            })
        {
            return null;
        }

        if (subscription.CollectionMethod != CollectionMethod.SendInvoice)
        {
            return null;
        }

        var now = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;

        // ReSharper disable once ConvertIfStatementToSwitchStatement
        if (subscription is
            {
                Status: SubscriptionStatus.Trialing or SubscriptionStatus.Active,
                LatestInvoice: null or { Status: InvoiceStatus.Paid },
                Items.Data.Count: > 0
            })
        {
            var currentPeriodEnd = subscription.GetCurrentPeriodEnd();

            if (currentPeriodEnd != null && (currentPeriodEnd.Value - now).TotalDays <= 14)
            {
                return new ResellerRenewalWarning
                {
                    Type = "upcoming",
                    Upcoming = new ResellerRenewalWarning.UpcomingRenewal
                    {
                        RenewalDate = currentPeriodEnd.Value
                    }
                };
            }
        }

        if (subscription is
            {
                Status: SubscriptionStatus.Active,
                LatestInvoice: { Status: InvoiceStatus.Open, DueDate: not null }
            } && subscription.LatestInvoice.DueDate > now)
        {
            return new ResellerRenewalWarning
            {
                Type = "issued",
                Issued = new ResellerRenewalWarning.IssuedRenewal
                {
                    IssuedDate = subscription.LatestInvoice.Created,
                    DueDate = subscription.LatestInvoice.DueDate.Value
                }
            };
        }

        // ReSharper disable once InvertIf
        if (subscription.Status == SubscriptionStatus.PastDue)
        {
            var openInvoices = await stripeAdapter.SearchInvoiceAsync(new InvoiceSearchOptions
            {
                Query = $"subscription:'{subscription.Id}' status:'open'"
            });

            var earliestOverdueInvoice = openInvoices
                .Where(invoice => invoice.DueDate != null && invoice.DueDate < now)
                .MinBy(invoice => invoice.Created);

            if (earliestOverdueInvoice != null)
            {
                return new ResellerRenewalWarning
                {
                    Type = "past_due",
                    PastDue = new ResellerRenewalWarning.PastDueRenewal
                    {
                        SuspensionDate = earliestOverdueInvoice.DueDate!.Value.AddDays(30)
                    }
                };
            }
        }

        return null;
    }

    private async Task<TaxIdWarning?> GetTaxIdWarningAsync(
        Organization organization,
        Customer customer,
        Provider? provider)
    {
        if (customer.Address?.Country == CountryAbbreviations.UnitedStates)
        {
            return null;
        }

        var productTier = organization.PlanType.GetProductTier();

        // Only business tier customers can have tax IDs
        if (productTier is not ProductTierType.Teams and not ProductTierType.Enterprise)
        {
            return null;
        }

        // Only an organization owner can update a tax ID
        if (!await currentContext.OrganizationOwner(organization.Id))
        {
            return null;
        }

        if (provider != null)
        {
            return null;
        }

        // Get active and scheduled registrations
        var registrations = (await Task.WhenAll(
                stripeAdapter.ListTaxRegistrationsAsync(new RegistrationListOptions { Status = TaxRegistrationStatus.Active }),
                stripeAdapter.ListTaxRegistrationsAsync(new RegistrationListOptions { Status = TaxRegistrationStatus.Scheduled })))
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

    private async Task<bool> HasUnverifiedBankAccountAsync(
        Organization organization)
    {
        var setupIntentId = await setupIntentCache.GetSetupIntentIdForSubscriber(organization.Id);

        if (string.IsNullOrEmpty(setupIntentId))
        {
            return false;
        }

        var setupIntent = await stripeAdapter.GetSetupIntentAsync(setupIntentId, new SetupIntentGetOptions
        {
            Expand = ["payment_method"]
        });

        return setupIntent.IsUnverifiedBankAccount();
    }
}
