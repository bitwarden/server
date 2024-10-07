namespace Bit.Core.Billing.Migration.Models;

public enum ProviderMigrationProgress
{
    Started = 1,
    ClientsMigrated = 2,
    TeamsPlanConfigured = 3,
    EnterprisePlanConfigured = 4,
    CustomerSetup = 5,
    SubscriptionSetup = 6,
    CreditApplied = 7,
    Completed = 8,

    Reversing = 9,
    ReversedClientMigrations = 10,
    RemovedProviderPlans = 11
}

public class ProviderMigrationTracker
{
    public Guid ProviderId { get; set; }
    public string ProviderName { get; set; }
    public List<Guid> OrganizationIds { get; set; }
    public ProviderMigrationProgress Progress { get; set; } = ProviderMigrationProgress.Started;
}
