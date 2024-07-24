using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Repositories;
using Bit.Core.Billing.Services;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Commercial.Core.Billing;

public class ProviderMigrationTracker
{
    private ProviderMigrationStep Progress { get; set; } = ProviderMigrationStep.None;
    private Dictionary<Guid, OrganizationMigrationTracker> OrganizationMigrations { get; } = new ();

    public void Started() => Progress = ProviderMigrationStep.Started;

    public void MigratingOrganizations() => Progress = ProviderMigrationStep.MigratingOrganizations;

    public void StartedOrganizationMigration(Guid organizationId) =>
        OrganizationMigrations[organizationId] = new OrganizationMigrationTracker();

    public void CancelledOrganizationSubscription(Guid organizationId) =>
        OrganizationMigrations[organizationId].CancelledSubscription();

    public void FinalizedOrganizationInvoice(Guid organizationId) =>
        OrganizationMigrations[organizationId].FinalizedInvoice();

    public void CreatedClientMigrationRecordForOrganization(Guid organizationId) =>
        OrganizationMigrations[organizationId].CreatedMigrationRecord();

    public void UpdatedOrganizationRecord(Guid organizationId) =>
        OrganizationMigrations[organizationId].UpdatedOrganization();

    public void CompletedOrganizationMigration(Guid organizationId) =>
        OrganizationMigrations[organizationId].Completed();

    public void OrganizationsMigrated() => Progress = ProviderMigrationStep.OrganizationsMigrated;

    public void CreatedTeamsPlan() => Progress = ProviderMigrationStep.CreatedTeamsPlan;

    public void CreatedEnterprisePlan() => Progress = ProviderMigrationStep.CreatedEnterprisePlan;

    public void CreatedStripeCustomer() => Progress = ProviderMigrationStep.CreatedStripeCustomer;

    public void StartedStripeSubscription() => Progress = ProviderMigrationStep.StartedStripeSubscription;

    public void CreditedProvider() => Progress = ProviderMigrationStep.CreditedProvider;

    public void Completed() => Progress = ProviderMigrationStep.Completed;

    private enum ProviderMigrationStep
    {
        None,
        Started,
        MigratingOrganizations,
        OrganizationsMigrated,
        CreatedTeamsPlan,
        CreatedEnterprisePlan,
        CreatedStripeCustomer,
        StartedStripeSubscription,
        CreditedProvider,
        Completed
    }

    private class OrganizationMigrationTracker
    {
        private OrganizationMigrationStep Progress { get; set; } = OrganizationMigrationStep.Started;

        public void CancelledSubscription() => Progress = OrganizationMigrationStep.CancelledSubscription;

        public void FinalizedInvoice() => Progress = OrganizationMigrationStep.FinalizedInvoice;

        public void CreatedMigrationRecord() => Progress = OrganizationMigrationStep.CreatedMigrationRecord;

        public void UpdatedOrganization() => Progress = OrganizationMigrationStep.UpdatedOrganization;

        public void Completed() => Progress = OrganizationMigrationStep.Completed;

        private enum OrganizationMigrationStep
        {
            Started = 0,
            CancelledSubscription = 1,
            FinalizedInvoice = 2,
            CreatedMigrationRecord = 3,
            UpdatedOrganization = 4,
            Completed = 5,
        }
    }
}

public class MigrateProviderCommand(
    IClientOrganizationMigrationRecordRepository clientOrganizationMigrationRecordRepository,
    ILogger<MigrateProviderCommand> logger,
    IOrganizationRepository organizationRepository,
    IPaymentService paymentService,
    IProviderBillingService providerBillingService,
    IProviderOrganizationRepository providerOrganizationRepository,
    IProviderPlanRepository providerPlanRepository,
    IProviderRepository providerRepository,
    IStripeAdapter stripeAdapter) : IMigrateProviderCommand
{
    private readonly ProviderMigrationTracker _providerMigrationTracker = new ();

    public async Task MigrateProvider(Guid providerId)
    {
        logger.LogInformation("CB: Starting migration for provider {ProviderID}", providerId);

        _providerMigrationTracker.Started();

        var provider = await GetValidProviderAsync(providerId);

        var organizations = (await GetValidOrganizationsAsync(provider.Id))
            .ToList();

        await MigrateOrganizationsAsync(provider.Id, organizations);

        await CreateTeamsProviderPlanAsync(provider.Id, organizations);

        await CreateEnterpriseProviderPlanAsync(provider.Id, organizations);

        await SetupStripeResourcesAsync(provider, organizations);

        await CreditProviderAsync(provider, organizations);

        provider.Status = ProviderStatusType.Billable;

        await providerRepository.ReplaceAsync(provider);

        _providerMigrationTracker.Completed();
    }

    private async Task<Provider> GetValidProviderAsync(Guid providerId)



    
    {
        var provider = await providerRepository.GetByIdAsync(providerId);

        if (provider == null)
        {
            logger.LogWarning("CB: Cannot migrate provider ({ProviderID}) as it does not exist", providerId);

            return null;
        }

        if (provider.Type != ProviderType.Msp)
        {
            logger.LogWarning("CB: Cannot migrate provider ({ProviderID}) as it is not an MSP", providerId);

            return null;
        }

        if (provider.Status == ProviderStatusType.Created)
        {
            return provider;
        }

        logger.LogWarning("CB: Cannot migrate provider ({ProviderID}) as it is not in the 'Created' state", providerId);

        return null;
    }

    private async Task<IEnumerable<Organization>> GetValidOrganizationsAsync(Guid providerId)
    {
        var organizations = await Task.WhenAll(
            (await providerOrganizationRepository.GetManyDetailsByProviderAsync(providerId))
            .Select(details => organizationRepository.GetByIdAsync(details.OrganizationId)));

        return organizations.Where(organization => organization.Enabled && (HasTeams(organization) || HasEnterprise(organization)));
    }

    private async Task MigrateOrganizationsAsync(Guid providerId, List<Organization> organizations)
    {
        _providerMigrationTracker.MigratingOrganizations();

        foreach (var organization in organizations)
        {
            await MigrateOrganizationAsync(providerId, organization);
        }

        _providerMigrationTracker.OrganizationsMigrated();
    }

    private async Task MigrateOrganizationAsync(Guid providerId, Organization organization)
    {
        logger.LogInformation("CB: Started migration for organization ({OrganizationID})", organization.Id);

        _providerMigrationTracker.StartedOrganizationMigration(organization.Id);

        if (string.IsNullOrEmpty(organization.GatewayCustomerId) ||
            string.IsNullOrEmpty(organization.GatewaySubscriptionId))
        {
            return;
        }

        await clientOrganizationMigrationRecordRepository.CreateAsync(new ClientOrganizationMigrationRecord
        {
            OrganizationId = organization.Id,
            ProviderId = providerId,
            PlanType = organization.PlanType,
            Seats = organization.Seats ?? 0,
            MaxStorageGb = organization.MaxStorageGb,
            GatewayCustomerId = organization.GatewayCustomerId,
            GatewaySubscriptionId = organization.GatewaySubscriptionId,
            ExpirationDate = organization.ExpirationDate,
            MaxAutoscaleSeats = organization.MaxAutoscaleSeats,
            UseSecretsManager = organization.UseSecretsManager,
            SmSeats = organization.SmSeats,
            SmServiceAccounts = organization.SmServiceAccounts,
            Status = organization.Status
        });

        logger.LogInformation("CB: Created migration record for organization ({OrganizationID})", organization.Id);

        _providerMigrationTracker.CreatedClientMigrationRecordForOrganization(organization.Id);

        var customer = await stripeAdapter.CustomerGetAsync(organization.GatewayCustomerId,
            new CustomerGetOptions { Expand = [ "subscriptions.data.test_clock" ] });

        var subscription = customer.Subscriptions.First(subscription => subscription.Id == organization.GatewaySubscriptionId);

        var now = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;

        var trialing = subscription.TrialEnd.HasValue && subscription.TrialEnd.Value > now;

        await stripeAdapter.SubscriptionUpdateAsync(subscription.Id, new SubscriptionUpdateOptions
        {
            CancelAtPeriodEnd = false
        });

        await stripeAdapter.SubscriptionCancelAsync(subscription.Id, new SubscriptionCancelOptions
        {
            CancellationDetails = new SubscriptionCancellationDetailsOptions
            {
                Comment = "Cancelled as part of provider migration to Consolidated Billing"
            },
            InvoiceNow = true,
            Prorate = true
        });

        logger.LogInformation("CB: Cancelled subscription for organization ({OrganizationID})", organization.Id);

        _providerMigrationTracker.CancelledOrganizationSubscription(organization.Id);

        if (!trialing)
        {
            var latestInvoice =
                (await stripeAdapter.SubscriptionGetAsync(subscription.Id,
                    new SubscriptionGetOptions { Expand = [ "latest_invoice" ] })).LatestInvoice;

            await stripeAdapter.InvoiceFinalizeInvoiceAsync(latestInvoice.Id,
                new InvoiceFinalizeOptions { AutoAdvance = true });

            logger.LogInformation("CB: Finalized prorated invoice for organization ({OrganizationID})", organization.Id);

            _providerMigrationTracker.FinalizedOrganizationInvoice(organization.Id);
        }

        var plan = StaticStore.GetPlan(HasTeams(organization) ? PlanType.TeamsMonthly : PlanType.EnterpriseMonthly);

        organization.Plan = plan.Name;
        organization.PlanType = plan.Type;
        organization.MaxStorageGb = plan.PasswordManager.BaseStorageGb;
        organization.GatewaySubscriptionId = null;
        organization.ExpirationDate = null;
        organization.MaxAutoscaleSeats = null;
        organization.UseSecretsManager = false;
        organization.SmSeats = null;
        organization.SmServiceAccounts = null;
        organization.Status = OrganizationStatusType.Managed;

        await organizationRepository.ReplaceAsync(organization);

        logger.LogInformation("CB: Brought organization ({OrganizationID}) under provider management", organization.Id);

        _providerMigrationTracker.UpdatedOrganizationRecord(organization.Id);

        _providerMigrationTracker.CompletedOrganizationMigration(organization.Id);
    }

    private async Task CreateTeamsProviderPlanAsync(Guid providerId, List<Organization> organizations)
    {
        var teamsSeats = organizations
            .Where(HasTeams)
            .Sum(client => client.Seats) ?? 0;

        await providerPlanRepository.CreateAsync(new ProviderPlan
        {
            ProviderId = providerId,
            PlanType = PlanType.TeamsMonthly,
            SeatMinimum = teamsSeats,
            PurchasedSeats = 0,
            AllocatedSeats = 0
        });

        logger.LogInformation("Created Teams plan for provider ({ProviderID}) with a seat minimum of {Seats}",
            providerId, teamsSeats);

        _providerMigrationTracker.CreatedTeamsPlan();
    }

    private async Task CreateEnterpriseProviderPlanAsync(Guid providerId, List<Organization> organizations)
    {
        var enterpriseSeats = organizations
            .Where(HasEnterprise)
            .Sum(client => client.Seats) ?? 0;

        await providerPlanRepository.CreateAsync(new ProviderPlan
        {
            ProviderId = providerId,
            PlanType = PlanType.EnterpriseMonthly,
            SeatMinimum = enterpriseSeats,
            PurchasedSeats = 0,
            AllocatedSeats = 0
        });

        logger.LogInformation("Created Enterprise plan for provider ({ProviderID}) with a seat minimum of {Seats}",
            providerId, enterpriseSeats);

        _providerMigrationTracker.CreatedEnterprisePlan();
    }

    private async Task SetupStripeResourcesAsync(Provider provider, List<Organization> organizations)
    {
        var sampleOrganization = organizations.FirstOrDefault();

        var taxInfo = await paymentService.GetTaxInfoAsync(sampleOrganization);

        await providerBillingService.CreateCustomer(provider, taxInfo);

        _providerMigrationTracker.CreatedStripeCustomer();

        await providerBillingService.StartSubscription(provider);

        _providerMigrationTracker.StartedStripeSubscription();
    }

    private async Task CreditProviderAsync(Provider provider, List<Organization> organizations)
    {
        var customers =
            await Task.WhenAll(organizations.Select(organization => stripeAdapter.CustomerGetAsync(organization.GatewayCustomerId)));

        var credit = customers.Sum(customer => customer.Balance);

        await stripeAdapter.CustomerUpdateAsync(provider.GatewayCustomerId, new CustomerUpdateOptions
        {
            Balance = credit
        });

        _providerMigrationTracker.CreditedProvider();
    }

    private static bool HasTeams(Organization organization) => organization.Plan.Contains("Teams");
    private static bool HasEnterprise(Organization organization) => organization.Plan.Contains("Enterprise");
}
