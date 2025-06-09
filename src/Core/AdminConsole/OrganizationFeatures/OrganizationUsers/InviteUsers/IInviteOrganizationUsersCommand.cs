using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.Utilities.Commands;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;

/// <summary>
/// Defines the contract for inviting organization users via SCIM (System for Cross-domain Identity Management).
/// Provides functionality for handling single email invitation requests within an organization context.
/// </summary>
public interface IInviteOrganizationUsersCommand
{
    /// <summary>
    /// Sends an invitation to add an organization user via SCIM (System for Cross-domain Identity Management) system.
    /// This can be a Success or a Failure. Failure will contain the Error along with a representation of the errored value.
    /// Success will be the successful return object.
    /// </summary>
    /// <param name="request">
    /// Contains the details for inviting a single organization user via email.
    /// </param>
    /// <returns>Response from InviteScimOrganiation<see cref="ScimInviteOrganizationUsersResponse"/></returns>
    Task<CommandResult<ScimInviteOrganizationUsersResponse>> InviteScimOrganizationUserAsync(InviteOrganizationUsersRequest request);
}
