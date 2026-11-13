using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteReminders;

public static class InviteReminderSchedule
{
    /// <summary>
    /// Returns the configured hours between invite reminders, or the default if the setting is negative.
    /// </summary>
    public static int GetIntervalHours(IGlobalSettings globalSettings, ILogger logger)
    {
        var intervalHours = globalSettings.OrganizationInviteReminderIntervalHours;
        if (intervalHours >= 0)
        {
            return intervalHours;
        }

        logger.LogWarning(
            "Invite reminder interval {ConfiguredIntervalHours} hours is below 0. Using the default of {DefaultIntervalHours} hours.",
            intervalHours, GlobalSettings.DefaultOrganizationInviteReminderIntervalHours);
        return GlobalSettings.DefaultOrganizationInviteReminderIntervalHours;
    }
}
