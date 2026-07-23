using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;

public interface IUpdateOrganizationUserCommand
{
    /// <summary>
    /// Validates and applies the requested changes to an organization user, including type, permissions,
    /// Secrets Manager access, and collection and group access.
    /// <para>Side effects on success:</para>
    /// <list type="bullet">
    /// <item>Autoscales the organization's Secrets Manager seats when enabling Secrets Manager requires more seats.</item>
    /// <item>Updates organization user along with collection access and groups</item>
    /// <item>Creates a default collection when the user is demoted from a privileged role and organization data ownership requires it.</item>
    /// <item>Logs an <c>OrganizationUser_Updated</c> event.</item>
    /// </list>
    /// </summary>
    /// <param name="request">The user's current state, the requested changes, and the acting user.</param>
    /// <returns>A <see cref="CommandResult"/> indicating success or the validation errors that prevented the update.</returns>
    Task<CommandResult> UpdateUserAsync(UpdateOrganizationUserRequest request);
}
