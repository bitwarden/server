using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Services;
using Stripe;
using Stripe.Tax;

namespace Bit.Core.Billing.Organizations.Queries;

using static StripeConstants;
using CountryAbbreviations = Bit.Core.Constants.CountryAbbreviations;
using FreeTrialWarning = OrganizationWarnings.FreeTrialWarning;
using InactiveSubscriptionWarning = OrganizationWarnings.InactiveSubscriptionWarning;
using ResellerRenewalWarning = OrganizationWarnings.ResellerRenewalWarning;
using ScheduledPriceIncreaseWarning = OrganizationWarnings.ScheduledPriceIncreaseWarning;
using TaxIdWarning = OrganizationWarnings.TaxIdWarning;

public interface IGetOrganizationWarningsQuery
{
    Task<OrganizationWarnings> Run(
        Organization organization);
}

public class GetOrganizationWarningsQuery(
    IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository,
    IOrganizationPlanMigrationCohortRepository cohortRepository,
    ICurrentContext currentContext,
    IFeatureService featureService,
    IHasPaymentMethodQuery hasPaymentMethodQuery,
    IPricingClient pricingClient,
    IProviderRepository providerRepository,
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

        warnings.ResellerRenewal = await GetResellerRenewalWarningAsync(organization, provider, subscription);

        warnings.TaxId = await GetTaxIdWarningAsync(organization, subscription.Customer, provider);

        warnings.ScheduledPriceIncrease = await GetScheduledPriceIncreaseWarningAsync(organization, subscription);

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

        var hasPaymentMethod = await hasPaymentMethodQuery.Run(organization);

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
        if (organization.ExemptFromBillingAutomation)
        {
            return null;
        }

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
        Organization organization,
        Provider? provider,
        Subscription subscription)
    {
        if (organization.ExemptFromBillingAutomation)
        {
            return null;
        }

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
        if (customer.TaxExempt != TaxExempt.None)
        {
            return null;
        }

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

    private async Task<ScheduledPriceIncreaseWarning?> GetScheduledPriceIncreaseWarningAsync(
        Organization organization,
        Subscription subscription)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration))
        {
            return null;
        }

        if (!await currentContext.EditSubscription(organization.Id))
        {
            return null;
        }

        if (subscription.Status != SubscriptionStatus.Active)
        {
            return null;
        }

        var assignment = await assignmentRepository.GetByOrganizationIdAsync(organization.Id);

        if (assignment == null)
        {
            return null;
        }

        var cohort = await cohortRepository.GetByIdAsync(assignment.CohortId);

        // A null MigrationPathId is a churn-only cohort, which never schedules a price increase.
        if (cohort?.MigrationPathId == null)
        {
            return null;
        }

        var migrationPath = MigrationPaths.FromId(cohort.MigrationPathId.Value);

        if (migrationPath == null)
        {
            return null;
        }

        var schedules = await stripeAdapter.ListSubscriptionSchedulesAsync(
            new SubscriptionScheduleListOptions { Customer = subscription.CustomerId });

        var activeSchedule = schedules.Data.FirstOrDefault(schedule =>
            schedule.Status == SubscriptionScheduleStatus.Active && schedule.SubscriptionId == subscription.Id);

        if (activeSchedule is not { Phases.Count: > 0 })
        {
            return null;
        }

        var now = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;

        // Select by StartDate > now (earliest future phase), not EndDate > now: with EndBehavior=Release
        // the schedule stays Active through the post-renewal period, and an EndDate filter would keep
        // surfacing the past renewal date as the "effective date" until the period closed.
        var upcomingPhase = activeSchedule.Phases
            .Where(phase => phase.StartDate > now)
            .MinBy(phase => phase.StartDate);

        if (upcomingPhase == null)
        {
            return null;
        }

        var targetPlan = await pricingClient.GetPlanOrThrow(migrationPath.ToPlan);

        // SeatPrice is per-year on annual plans and per-month on monthly plans.
        var seatPrice = targetPlan.IsAnnual
            ? targetPlan.PasswordManager.SeatPrice / 12
            : targetPlan.PasswordManager.SeatPrice;

        return new ScheduledPriceIncreaseWarning
        {
            SeatPrice = seatPrice,
            EffectiveDate = upcomingPhase.StartDate,
            Cadence = targetPlan.IsAnnual ? "annually" : "monthly"
        };
    }
}
