using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;

/// <summary>
/// The request to update an organization user: the loaded current state, the requested changes (the
/// <c>New*</c> values), and the acting user. Collection and group access are trusted as already-authorized
/// by the API layer.
/// </summary>
/// <param name="OrganizationUserToUpdate">The organization user being updated, in its current state.</param>
/// <param name="Organization">The organization the user belongs to.</param>
/// <param name="OrganizationAbility">The organization's ability.</param>
/// <param name="CurrentAccessIds">The collection ids the user currently has access to.</param>
/// <param name="ReferencedCollections">The hydrated collections referenced by this update — posted plus
/// currently-held, deduplicated — so the validator can check existence and reject default collections
/// without re-querying.</param>
/// <param name="NewType">The requested member role.</param>
/// <param name="NewPermissions">The requested custom permissions (used when <paramref name="NewType"/> is Custom).</param>
/// <param name="NewAccessSecretsManager">Whether the user should have Secrets Manager access.</param>
/// <param name="NewCollections">The updated collection access. Null removes all collection access.</param>
/// <param name="NewGroups">The updated group access. Null leaves groups unchanged.</param>
/// <param name="PerformedBy">The actor making the change.</param>
/// <param name="PerformedByOrganizationUser">The actor's own membership, used to prevent granting a role
/// higher than their own. Null when the actor is not an organization member (e.g. a provider).</param>
public record UpdateOrganizationUserRequest(
    OrganizationUser OrganizationUserToUpdate,
    Organization Organization,
    OrganizationAbility OrganizationAbility,
    HashSet<Guid> CurrentAccessIds,
    ICollection<Collection> ReferencedCollections,
    OrganizationUserType NewType,
    Permissions? NewPermissions,
    bool NewAccessSecretsManager,
    List<CollectionAccessSelection>? NewCollections,
    IEnumerable<Guid>? NewGroups,
    IActingUser PerformedBy,
    OrganizationUser? PerformedByOrganizationUser);
