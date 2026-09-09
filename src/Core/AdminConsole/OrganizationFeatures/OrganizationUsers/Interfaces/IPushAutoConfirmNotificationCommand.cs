namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IPushAutoConfirmNotificationCommand
{
    /// <summary>
    /// Sends auto-confirm push notifications to all admins and custom users with ManageUsers permission
    /// for the given organization, prompting them to confirm the newly accepted user.
    /// </summary>
    /// <remarks>
    /// No notifications are sent if any of the following conditions are not met:
    /// <list type="bullet">
    /// <item>The organization has the <c>UseAutomaticUserConfirmation</c> ability enabled.</item>
    /// <item>The organization has the <c>AutomaticUserConfirmation</c> policy enabled.</item>
    /// <item>The user being confirmed has the <c>User</c> role (owners, admins, and custom are excluded).</item>
    /// </list>
    /// </remarks>
    Task PushAsync(Guid userId, Guid organizationId);
}
