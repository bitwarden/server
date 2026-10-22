namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteReminders;

public interface ISendInviteRemindersCommand
{
    /// <summary>
    /// Claims the invited members whose reminder is due, up to the configured batch size, and sends each one the
    /// matching reminder email with a new invite link. Does nothing while the feature flag is off.
    /// </summary>
    Task<SendInviteRemindersResult> SendInviteRemindersAsync();
}
