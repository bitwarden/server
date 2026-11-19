using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface INotifyAdminsOfAcceptedUserCommand
{
    /// <summary>
    /// Notifies organization admins that a user has accepted their invitation.
    /// </summary>
    /// <remarks>
    /// When the user qualifies for automatic confirmation, admins and custom users with the ManageUsers permission
    /// receive a push notification prompting them to confirm the user, and the accepted email is delayed so that it
    /// is only sent if the user is still awaiting confirmation. Otherwise, the accepted email is sent immediately.
    /// A user qualifies for automatic confirmation when:
    /// <list type="bullet">
    /// <item>The organization has the <c>UseAutomaticUserConfirmation</c> ability enabled.</item>
    /// <item>The organization has the <c>AutomaticUserConfirmation</c> policy enabled.</item>
    /// <item>The user has the <c>User</c> role (owners, admins, and custom are excluded).</item>
    /// </list>
    /// </remarks>
    Task NotifyAsync(Organization organization, OrganizationUser organizationUser, User user);
}
