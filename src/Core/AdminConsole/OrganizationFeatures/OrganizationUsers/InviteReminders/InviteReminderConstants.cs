using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteReminders;

public static class InviteReminderConstants
{
    /// <summary>
    /// How many days after the invite the first reminder goes out. Later reminders go out at the same interval.
    /// </summary>
    public const int ReminderIntervalDays = 5;

    /// <summary>
    /// How many days late a reminder can be and still go out. If it is later than that, it is skipped and the member
    /// gets no more reminders. This keeps the first run after an outage from emailing people about old invites.
    /// </summary>
    public const int MaxLatenessDays = 4;

    /// <summary>
    /// The default batch size, used when <see cref="IGlobalSettings.OrganizationInviteReminderBatchSize"/> is missing
    /// from the config or set to a negative number. Keep it in sync with the default in <see cref="GlobalSettings"/>.
    /// </summary>
    public const int DefaultBatchSize = 500;
}
