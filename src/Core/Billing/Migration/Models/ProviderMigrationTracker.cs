namespace Bit.Core.Billing.Migration.Models;

public enum ProviderMigrationProgress
{
    Started = 1,
    NoClients = 2,
    ClientsMigrated = 3,
    TeamsPlanConfigured = 4,
    EnterprisePlanConfigured = 5,
    CustomerSetup = 6,
    SubscriptionSetup = 7,
    CreditApplied = 8,
    Completed = 9,
}

public class ProviderMigrationTracker
{
    public Guid ProviderId { get; set; }
    public string ProviderName { get; set; }
    public List<Guid> OrganizationIds { get; set; }
    public ProviderMigrationProgress Progress { get; set; } = ProviderMigrationProgress.Started;
}
