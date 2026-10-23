namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteReminders;

public static class InviteReminderConstants
{
    /// <summary>
    /// Days between an invite and the first reminder, and between each reminder after that.
    /// </summary>
    public const int ReminderIntervalDays = 5;

    /// <summary>
    /// The most days a reminder can be late and still be sent. A later reminder is skipped, and the member gets no
    /// more reminders. This stops the first run after an outage from sending reminders for old invites.
    /// </summary>
    public const int MaxLatenessDays = 4;

    /// <summary>
    /// The batch size used when the configured value is below 0.
    /// </summary>
    public const int DefaultBatchSize = 500;
}
