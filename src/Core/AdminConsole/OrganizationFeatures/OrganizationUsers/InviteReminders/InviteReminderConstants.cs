namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteReminders;

public static class InviteReminderConstants
{
    /// <summary>
    /// Days between an invite and the first reminder, and between each reminder after that.
    /// </summary>
    public const int ReminderIntervalDays = 5;

    /// <summary>
    /// How many days late a reminder can still go out. An older reminder is skipped and the sequence stops, so the
    /// first run after a long outage does not send reminders for stale invites.
    /// </summary>
    public const int MaxLatenessDays = 4;

    /// <summary>
    /// The batch size used when the configured value is below 0.
    /// </summary>
    public const int DefaultBatchSize = 500;
}
