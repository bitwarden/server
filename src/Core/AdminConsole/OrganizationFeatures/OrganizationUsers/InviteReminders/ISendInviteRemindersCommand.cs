namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteReminders;

public interface ISendInviteRemindersCommand
{
    /// <summary>
    /// Finds the invited members who are due a reminder, up to the batch size, and claims them so a second run can't
    /// pick them up. Then emails each one a reminder with a new invite link. Does nothing if the feature flag is off.
    /// </summary>
    Task<SendInviteRemindersResult> SendInviteRemindersAsync();
}
