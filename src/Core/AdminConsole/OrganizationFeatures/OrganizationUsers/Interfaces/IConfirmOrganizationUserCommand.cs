﻿using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

/// <summary>
/// Command to confirm organization users who have accepted their invitations.
/// </summary>
public interface IConfirmOrganizationUserCommand
{
    /// <summary>
    /// Confirms a single organization user who has accepted their invitation.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="organizationUserId">The ID of the organization user to confirm.</param>
    /// <param name="key">The encrypted organization key for the user.</param>
    /// <param name="confirmingUserId">The ID of the user performing the confirmation.</param>
    /// <returns>The confirmed organization user.</returns>
    /// <exception cref="BadRequestException">Thrown when the user is not valid or cannot be confirmed.</exception>
    Task<OrganizationUser> ConfirmUserAsync(Guid organizationId, Guid organizationUserId, string key, Guid confirmingUserId);

    /// <summary>
    /// Confirms multiple organization users who have accepted their invitations.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="keys">A dictionary mapping organization user IDs to their encrypted organization keys.</param>
    /// <param name="confirmingUserId">The ID of the user performing the confirmation.</param>
    /// <returns>A list of tuples containing the organization user and an error message (if any).</returns>
    Task<List<Tuple<OrganizationUser, string>>> ConfirmUsersAsync(Guid organizationId, Dictionary<Guid, string> keys,
        Guid confirmingUserId);
}
