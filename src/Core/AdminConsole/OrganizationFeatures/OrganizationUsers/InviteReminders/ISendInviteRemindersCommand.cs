namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteReminders;

public interface ISendInviteRemindersCommand
{
    /// <summary>
    /// Claims the invited members whose reminder is due, up to the batch size. Sends each member the reminder email
    /// with a new invite link. Does nothing when the feature flag is off.
    /// </summary>
    Task<SendInviteRemindersResult> SendInviteRemindersAsync();
}
