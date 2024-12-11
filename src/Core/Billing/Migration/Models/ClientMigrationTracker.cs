namespace Bit.Core.Billing.Migration.Models;

public enum ClientMigrationProgress
{
    Started = 1,
    MigrationRecordCreated = 2,
    SubscriptionEnded = 3,
    Completed = 4,

    Reversing = 5,
    ResetOrganization = 6,
    RecreatedSubscription = 7,
    RemovedMigrationRecord = 8,
    Reversed = 9,
}

public class ClientMigrationTracker
{
    public Guid ProviderId { get; set; }
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; }
    public ClientMigrationProgress Progress { get; set; } = ClientMigrationProgress.Started;
}
