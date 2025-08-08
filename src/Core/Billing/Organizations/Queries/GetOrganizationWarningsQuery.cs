// ReSharper disable InconsistentNaming

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Services;
using Stripe;
using FreeTrialWarning = Bit.Core.Billing.Organizations.Models.OrganizationWarnings.FreeTrialWarning;
using InactiveSubscriptionWarning =
    Bit.Core.Billing.Organizations.Models.OrganizationWarnings.InactiveSubscriptionWarning;
using ResellerRenewalWarning =
    Bit.Core.Billing.Organizations.Models.OrganizationWarnings.ResellerRenewalWarning;

namespace Bit.Core.Billing.Organizations.Queries;

using static StripeConstants;

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
        var response = new OrganizationWarnings();

        var subscription =
            await subscriberService.GetSubscription(organization,
                new SubscriptionGetOptions { Expand = ["customer", "latest_invoice", "test_clock"] });

        if (subscription == null)
        {
            return response;
        }

        response.FreeTrial = await GetFreeTrialWarning(organization, subscription);

        var provider = await providerRepository.GetByOrganizationIdAsync(organization.Id);

        response.InactiveSubscription = await GetInactiveSubscriptionWarning(organization, provider, subscription);

        response.ResellerRenewal = await GetResellerRenewalWarning(provider, subscription);

        return response;
    }

    private async Task<FreeTrialWarning?> GetFreeTrialWarning(
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

        var hasUnverifiedBankAccount = await HasUnverifiedBankAccount(organization);

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

    private async Task<InactiveSubscriptionWarning?> GetInactiveSubscriptionWarning(
        Organization organization,
        Provider? provider,
        Subscription subscription)
    {
        var isOrganizationOwner = await currentContext.OrganizationOwner(organization.Id);

        switch (organization.Enabled)
        {
            // Member of an enabled, trialing organization.
            case true when subscription.Status is SubscriptionStatus.Trialing:
                {
                    var hasUnverifiedBankAccount = await HasUnverifiedBankAccount(organization);

                    var hasPaymentMethod =
                        !string.IsNullOrEmpty(subscription.Customer.InvoiceSettings.DefaultPaymentMethodId) ||
                        !string.IsNullOrEmpty(subscription.Customer.DefaultSourceId) ||
                        hasUnverifiedBankAccount ||
                        subscription.Customer.Metadata.ContainsKey(MetadataKeys.BraintreeCustomerId);

                    // If this member is the owner and there's no payment method on file, ask them to add one.
                    return isOrganizationOwner && !hasPaymentMethod
                        ? new InactiveSubscriptionWarning { Resolution = "add_payment_method_optional_trial" }
                        : null;
                }
            // Member of disabled and unpaid or canceled organization.
            case false when subscription.Status is SubscriptionStatus.Unpaid or SubscriptionStatus.Canceled:
                {
                    // If the organization is managed by a provider, return a warning asking them to contact the provider.
                    if (provider != null)
                    {
                        return new InactiveSubscriptionWarning { Resolution = "contact_provider" };
                    }

                    /* If the organization is not managed by a provider and this user is the owner, return an action warning based
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

                    // Otherwise, this member is not the owner, and we need to ask them to contact the owner.
                    return new InactiveSubscriptionWarning { Resolution = "contact_owner" };
                }
            default: return null;
        }
    }

    private async Task<ResellerRenewalWarning?> GetResellerRenewalWarning(
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
                LatestInvoice: null or { Status: InvoiceStatus.Paid }
            } && (subscription.CurrentPeriodEnd - now).TotalDays <= 14)
        {
            return new ResellerRenewalWarning
            {
                Type = "upcoming",
                Upcoming = new ResellerRenewalWarning.UpcomingRenewal
                {
                    RenewalDate = subscription.CurrentPeriodEnd
                }
            };
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

    private async Task<bool> HasUnverifiedBankAccount(
        Organization organization)
    {
        var setupIntentId = await setupIntentCache.Get(organization.Id);

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
