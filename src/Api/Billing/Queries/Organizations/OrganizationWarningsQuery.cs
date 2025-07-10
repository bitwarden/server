// ReSharper disable InconsistentNaming

#nullable enable

using Bit.Api.Billing.Models.Responses.Organizations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Services;
using Stripe;
using FreeTrialWarning = Bit.Api.Billing.Models.Responses.Organizations.OrganizationWarningsResponse.FreeTrialWarning;
using InactiveSubscriptionWarning =
    Bit.Api.Billing.Models.Responses.Organizations.OrganizationWarningsResponse.InactiveSubscriptionWarning;
using ResellerRenewalWarning =
    Bit.Api.Billing.Models.Responses.Organizations.OrganizationWarningsResponse.ResellerRenewalWarning;

namespace Bit.Api.Billing.Queries.Organizations;

public interface IOrganizationWarningsQuery
{
    Task<OrganizationWarningsResponse> Run(
        Organization organization);
}

public class OrganizationWarningsQuery(
    ICurrentContext currentContext,
    IProviderRepository providerRepository,
    IStripeAdapter stripeAdapter,
    ISubscriberService subscriberService) : IOrganizationWarningsQuery
{
    public async Task<OrganizationWarningsResponse> Run(
        Organization organization)
    {
        var response = new OrganizationWarningsResponse();

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
                Status: StripeConstants.SubscriptionStatus.Trialing,
                TrialEnd: not null,
                Customer: not null
            })
        {
            return null;
        }

        var customer = subscription.Customer;

        var hasPaymentMethod =
            !string.IsNullOrEmpty(customer.InvoiceSettings.DefaultPaymentMethodId) ||
            !string.IsNullOrEmpty(customer.DefaultSourceId) ||
            customer.Metadata.ContainsKey(StripeConstants.MetadataKeys.BraintreeCustomerId);

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
        if (organization.Enabled ||
            subscription.Status is not StripeConstants.SubscriptionStatus.Unpaid
                and not StripeConstants.SubscriptionStatus.Canceled)
        {
            return null;
        }

        if (provider != null)
        {
            return new InactiveSubscriptionWarning { Resolution = "contact_provider" };
        }

        if (await currentContext.OrganizationOwner(organization.Id))
        {
            return subscription.Status switch
            {
                StripeConstants.SubscriptionStatus.Unpaid => new InactiveSubscriptionWarning
                {
                    Resolution = "add_payment_method"
                },
                StripeConstants.SubscriptionStatus.Canceled => new InactiveSubscriptionWarning
                {
                    Resolution = "resubscribe"
                },
                _ => null
            };
        }

        return new InactiveSubscriptionWarning { Resolution = "contact_owner" };
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

        if (subscription.CollectionMethod != StripeConstants.CollectionMethod.SendInvoice)
        {
            return null;
        }

        var now = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;

        // ReSharper disable once ConvertIfStatementToSwitchStatement
        if (subscription is
            {
                Status: StripeConstants.SubscriptionStatus.Trialing or StripeConstants.SubscriptionStatus.Active,
                LatestInvoice: null or { Status: StripeConstants.InvoiceStatus.Paid }
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
                Status: StripeConstants.SubscriptionStatus.Active,
                LatestInvoice: { Status: StripeConstants.InvoiceStatus.Open, DueDate: not null }
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
        if (subscription.Status == StripeConstants.SubscriptionStatus.PastDue)
        {
            var openInvoices = await stripeAdapter.InvoiceSearchAsync(new InvoiceSearchOptions
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
}
