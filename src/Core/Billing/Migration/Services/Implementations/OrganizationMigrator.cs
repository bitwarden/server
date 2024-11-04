using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Migration.Models;
using Bit.Core.Billing.Repositories;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;
using Stripe;
using Plan = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Core.Billing.Migration.Services.Implementations;

public class OrganizationMigrator(
    IClientOrganizationMigrationRecordRepository clientOrganizationMigrationRecordRepository,
    ILogger<OrganizationMigrator> logger,
    IMigrationTrackerCache migrationTrackerCache,
    IOrganizationRepository organizationRepository,
    IStripeAdapter stripeAdapter) : IOrganizationMigrator
{
    private const string _cancellationComment = "Cancelled as part of provider migration to Consolidated Billing";

    public async Task Migrate(Guid providerId, Organization organization)
    {
        logger.LogInformation("CB: Starting migration for organization ({OrganizationID})", organization.Id);

        await migrationTrackerCache.StartTracker(providerId, organization);

        await CreateMigrationRecordAsync(providerId, organization);

        await CancelSubscriptionAsync(providerId, organization);

        await UpdateOrganizationAsync(providerId, organization);
    }

    #region Steps

    private async Task CreateMigrationRecordAsync(Guid providerId, Organization organization)
    {
        logger.LogInformation("CB: Creating ClientOrganizationMigrationRecord for organization ({OrganizationID})", organization.Id);

        var migrationRecord = await clientOrganizationMigrationRecordRepository.GetByOrganizationId(organization.Id);

        if (migrationRecord != null)
        {
            logger.LogInformation(
                "CB: ClientOrganizationMigrationRecord already exists for organization ({OrganizationID}), deleting record",
                organization.Id);

            await clientOrganizationMigrationRecordRepository.DeleteAsync(migrationRecord);
        }

        await clientOrganizationMigrationRecordRepository.CreateAsync(new ClientOrganizationMigrationRecord
        {
            OrganizationId = organization.Id,
            ProviderId = providerId,
            PlanType = organization.PlanType,
            Seats = organization.Seats ?? 0,
            MaxStorageGb = organization.MaxStorageGb,
            GatewayCustomerId = organization.GatewayCustomerId!,
            GatewaySubscriptionId = organization.GatewaySubscriptionId!,
            ExpirationDate = organization.ExpirationDate,
            MaxAutoscaleSeats = organization.MaxAutoscaleSeats,
            Status = organization.Status
        });

        logger.LogInformation("CB: Created migration record for organization ({OrganizationID})", organization.Id);

        await migrationTrackerCache.UpdateTrackingStatus(providerId, organization.Id,
            ClientMigrationProgress.MigrationRecordCreated);
    }

    private async Task CancelSubscriptionAsync(Guid providerId, Organization organization)
    {
        logger.LogInformation("CB: Cancelling subscription for organization ({OrganizationID})", organization.Id);

        var subscription = await stripeAdapter.SubscriptionGetAsync(organization.GatewaySubscriptionId);

        if (subscription is
            {
                Status:
                    StripeConstants.SubscriptionStatus.Active or
                    StripeConstants.SubscriptionStatus.PastDue or
                    StripeConstants.SubscriptionStatus.Trialing
            })
        {
            await stripeAdapter.SubscriptionUpdateAsync(organization.GatewaySubscriptionId,
                new SubscriptionUpdateOptions { CancelAtPeriodEnd = false });

            subscription = await stripeAdapter.SubscriptionCancelAsync(organization.GatewaySubscriptionId,
                new SubscriptionCancelOptions
                {
                    CancellationDetails = new SubscriptionCancellationDetailsOptions
                    {
                        Comment = _cancellationComment
                    },
                    InvoiceNow = true,
                    Prorate = true,
                    Expand = ["latest_invoice", "test_clock"]
                });

            logger.LogInformation("CB: Cancelled subscription for organization ({OrganizationID})", organization.Id);

            var now = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;

            var trialing = subscription.TrialEnd.HasValue && subscription.TrialEnd.Value > now;

            if (!trialing && subscription is { Status: StripeConstants.SubscriptionStatus.Canceled, CancellationDetails.Comment: _cancellationComment })
            {
                var latestInvoice = subscription.LatestInvoice;

                if (latestInvoice.Status == "draft")
                {
                    await stripeAdapter.InvoiceFinalizeInvoiceAsync(latestInvoice.Id,
                        new InvoiceFinalizeOptions { AutoAdvance = true });

                    logger.LogInformation("CB: Finalized prorated invoice for organization ({OrganizationID})", organization.Id);
                }
            }
        }
        else
        {
            logger.LogInformation(
                "CB: Did not need to cancel subscription for organization ({OrganizationID}) as it was inactive",
                organization.Id);
        }

        await migrationTrackerCache.UpdateTrackingStatus(providerId, organization.Id,
            ClientMigrationProgress.SubscriptionEnded);
    }

    private async Task UpdateOrganizationAsync(Guid providerId, Organization organization)
    {
        logger.LogInformation("CB: Bringing organization ({OrganizationID}) under provider management",
            organization.Id);

        var plan = StaticStore.GetPlan(organization.Plan.Contains("Teams") ? PlanType.TeamsMonthly : PlanType.EnterpriseMonthly);

        ResetOrganizationPlan(organization, plan);
        organization.MaxStorageGb = plan.PasswordManager.BaseStorageGb;
        organization.GatewaySubscriptionId = null;
        organization.ExpirationDate = null;
        organization.MaxAutoscaleSeats = null;
        organization.Status = OrganizationStatusType.Managed;

        await organizationRepository.ReplaceAsync(organization);

        logger.LogInformation("CB: Brought organization ({OrganizationID}) under provider management",
            organization.Id);

        await migrationTrackerCache.UpdateTrackingStatus(providerId, organization.Id,
            ClientMigrationProgress.Completed);
    }

    #endregion

    #region Reverse

    private async Task RemoveMigrationRecordAsync(Guid providerId, Organization organization)
    {
        logger.LogInformation("CB: Removing migration record for organization ({OrganizationID})", organization.Id);

        var migrationRecord = await clientOrganizationMigrationRecordRepository.GetByOrganizationId(organization.Id);

        if (migrationRecord != null)
        {
            await clientOrganizationMigrationRecordRepository.DeleteAsync(migrationRecord);

            logger.LogInformation(
                "CB: Removed migration record for organization ({OrganizationID})",
                organization.Id);
        }
        else
        {
            logger.LogInformation("CB: Did not remove migration record for organization ({OrganizationID}) as it does not exist", organization.Id);
        }

        await migrationTrackerCache.UpdateTrackingStatus(providerId, organization.Id, ClientMigrationProgress.Reversed);
    }

    private async Task RecreateSubscriptionAsync(Guid providerId, Organization organization)
    {
        logger.LogInformation("CB: Recreating subscription for organization ({OrganizationID})", organization.Id);

        if (!string.IsNullOrEmpty(organization.GatewaySubscriptionId))
        {
            if (string.IsNullOrEmpty(organization.GatewayCustomerId))
            {
                logger.LogError(
                    "CB: Cannot recreate subscription for organization ({OrganizationID}) as it does not have a Stripe customer",
                    organization.Id);

                throw new Exception();
            }

            var customer = await stripeAdapter.CustomerGetAsync(organization.GatewayCustomerId,
                new CustomerGetOptions { Expand = ["default_source", "invoice_settings.default_payment_method"] });

            var collectionMethod =
                customer.DefaultSource != null ||
                customer.InvoiceSettings?.DefaultPaymentMethod != null ||
                customer.Metadata.ContainsKey(Utilities.BraintreeCustomerIdKey)
                    ? StripeConstants.CollectionMethod.ChargeAutomatically
                    : StripeConstants.CollectionMethod.SendInvoice;

            var plan = StaticStore.GetPlan(organization.PlanType);

            var items = new List<SubscriptionItemOptions>
            {
                new ()
                {
                    Price = plan.PasswordManager.StripeSeatPlanId,
                    Quantity = organization.Seats
                }
            };

            if (organization.MaxStorageGb.HasValue && plan.PasswordManager.BaseStorageGb.HasValue && organization.MaxStorageGb.Value > plan.PasswordManager.BaseStorageGb.Value)
            {
                var additionalStorage = organization.MaxStorageGb.Value - plan.PasswordManager.BaseStorageGb.Value;

                items.Add(new SubscriptionItemOptions
                {
                    Price = plan.PasswordManager.StripeStoragePlanId,
                    Quantity = additionalStorage
                });
            }

            var subscriptionCreateOptions = new SubscriptionCreateOptions
            {
                AutomaticTax = new SubscriptionAutomaticTaxOptions
                {
                    Enabled = true
                },
                Customer = customer.Id,
                CollectionMethod = collectionMethod,
                DaysUntilDue = collectionMethod == StripeConstants.CollectionMethod.SendInvoice ? 30 : null,
                Items = items,
                Metadata = new Dictionary<string, string>
                {
                    [organization.GatewayIdField()] = organization.Id.ToString()
                },
                OffSession = true,
                ProrationBehavior = StripeConstants.ProrationBehavior.CreateProrations,
                TrialPeriodDays = plan.TrialPeriodDays
            };

            var subscription = await stripeAdapter.SubscriptionCreateAsync(subscriptionCreateOptions);

            organization.GatewaySubscriptionId = subscription.Id;

            await organizationRepository.ReplaceAsync(organization);

            logger.LogInformation("CB: Recreated subscription for organization ({OrganizationID})", organization.Id);
        }
        else
        {
            logger.LogInformation(
                "CB: Did not recreate subscription for organization ({OrganizationID}) as it already exists",
                organization.Id);
        }

        await migrationTrackerCache.UpdateTrackingStatus(providerId, organization.Id,
            ClientMigrationProgress.RecreatedSubscription);
    }

    private async Task ReverseOrganizationUpdateAsync(Guid providerId, Organization organization)
    {
        var migrationRecord = await clientOrganizationMigrationRecordRepository.GetByOrganizationId(organization.Id);

        if (migrationRecord == null)
        {
            logger.LogError(
                "CB: Cannot reverse migration for organization ({OrganizationID}) as it does not have a migration record",
                organization.Id);

            throw new Exception();
        }

        var plan = StaticStore.GetPlan(migrationRecord.PlanType);

        ResetOrganizationPlan(organization, plan);
        organization.MaxStorageGb = migrationRecord.MaxStorageGb;
        organization.ExpirationDate = migrationRecord.ExpirationDate;
        organization.MaxAutoscaleSeats = migrationRecord.MaxAutoscaleSeats;
        organization.Status = migrationRecord.Status;

        await organizationRepository.ReplaceAsync(organization);

        logger.LogInformation("CB: Reversed organization ({OrganizationID}) updates",
            organization.Id);

        await migrationTrackerCache.UpdateTrackingStatus(providerId, organization.Id,
            ClientMigrationProgress.ResetOrganization);
    }

    #endregion

    #region Shared

    private static void ResetOrganizationPlan(Organization organization, Plan plan)
    {
        organization.Plan = plan.Name;
        organization.PlanType = plan.Type;
        organization.MaxCollections = plan.PasswordManager.MaxCollections;
        organization.MaxStorageGb = plan.PasswordManager.BaseStorageGb;
        organization.UsePolicies = plan.HasPolicies;
        organization.UseSso = plan.HasSso;
        organization.UseGroups = plan.HasGroups;
        organization.UseEvents = plan.HasEvents;
        organization.UseDirectory = plan.HasDirectory;
        organization.UseTotp = plan.HasTotp;
        organization.Use2fa = plan.Has2fa;
        organization.UseApi = plan.HasApi;
        organization.UseResetPassword = plan.HasResetPassword;
        organization.SelfHost = plan.HasSelfHost;
        organization.UsersGetPremium = plan.UsersGetPremium;
        organization.UseCustomPermissions = plan.HasCustomPermissions;
        organization.UseScim = plan.HasScim;
        organization.UseKeyConnector = plan.HasKeyConnector;
    }

    #endregion
}
