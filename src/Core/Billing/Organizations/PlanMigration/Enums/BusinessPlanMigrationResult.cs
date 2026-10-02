namespace Bit.Core.Billing.Organizations.PlanMigration.Enums;

/// <summary>
/// The result of running the business-plan migration flow for a single organization.
/// </summary>
public enum BusinessPlanMigrationResult
{
    /// <summary>The organization has no cohort assignment — it isn't in the migration program.</summary>
    NotAssigned,

    /// <summary>The organization has already been migrated (MigratedDate is set).</summary>
    AlreadyMigrated,

    /// <summary>Enrolled and not migrated, but no active schedule resulted this run (scheduler declined).</summary>
    NotScheduled,

    /// <summary>
    /// The schedule is in effect (created this run or on a prior run) and the renewal notification has
    /// been sent.
    /// </summary>
    Completed,

    /// <summary>Schedule is in effect, but the renewal notification did not go out; it will retry on a later run.</summary>
    CompletedWithoutNotification
}
