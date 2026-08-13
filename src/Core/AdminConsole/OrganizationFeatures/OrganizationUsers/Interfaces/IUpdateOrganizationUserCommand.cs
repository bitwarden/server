#nullable enable
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IUpdateOrganizationUserCommand
{
    /// <param name="organizationUser">The modified organization user to save.</param>
    /// <param name="existingUserType">The current type (member role) of the user.</param>
    /// <param name="savingUserId">
    /// The userId of the currently logged in user who is making the change, or <see langword="null"/> when the
    /// request is authenticated via an organization API key (Public API) or SCIM. Those callers are intentionally
    /// granted full authority over the organization, so passing <see langword="null"/> skips the per-target
    /// authorization check rather than denying the request.
    /// </param>
    /// <param name="collectionAccess">The user's updated collection access. If set to null, this removes all collection access.</param>
    /// <param name="groupAccess">The user's updated group access. If set to null, groups are not updated.</param>
    Task UpdateUserAsync(OrganizationUser organizationUser, OrganizationUserType existingUserType, Guid? savingUserId,
        List<CollectionAccessSelection>? collectionAccess, IEnumerable<Guid>? groupAccess,
        string? defaultUserCollectionName = null);
}
