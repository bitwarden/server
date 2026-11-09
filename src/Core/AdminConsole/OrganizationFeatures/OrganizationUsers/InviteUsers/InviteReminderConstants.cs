namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;

public static class InviteReminderConstants
{
    /// <summary>
    /// The number of days between invite reminders, and the delay before the first one.
    /// </summary>
    public const int ReminderIntervalDays = 5;

    /// <summary>
    /// How many days past its due date a reminder can still be sent before it is skipped.
    /// </summary>
    public const int MaxLatenessDays = 4;
}
