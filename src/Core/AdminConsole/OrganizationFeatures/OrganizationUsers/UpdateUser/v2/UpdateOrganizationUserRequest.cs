using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;

/// <summary>
/// The request to update an organization user: the loaded current state, the requested <c>New*</c> changes,
/// and the acting user. Collection and group access are trusted as already-authorized by the API layer.
/// </summary>
/// <param name="CurrentCollectionsIds">Collections the user to update currently has access to.</param>
/// <param name="ReferencedCollections">Posted plus currently-held collections</param>
/// <param name="NewPermissions">The requested custom permissions (used when <paramref name="NewType"/> is Custom).</param>
/// <param name="NewCollections">The updated collection access; null removes all collection access.</param>
/// <param name="NewGroups">The updated group access; null leaves groups unchanged.</param>
/// <param name="PerformedByOrganizationUser">The actor's own membership; null when the actor is not an organization member (e.g. a provider).</param>
/// <param name="DefaultUserCollectionName">Default collection name used when applicable</param>
public record UpdateOrganizationUserRequest(
    OrganizationUser OrganizationUserToUpdate,
    Organization Organization,
    OrganizationAbility OrganizationAbility,
    HashSet<Guid> CurrentCollectionsIds,
    ICollection<Collection> ReferencedCollections,
    OrganizationUserType NewType,
    Permissions? NewPermissions,
    bool NewAccessSecretsManager,
    List<CollectionAccessSelection>? NewCollections,
    IEnumerable<Guid>? NewGroups,
    IActingUser PerformedBy,
    OrganizationUser? PerformedByOrganizationUser,
    string? DefaultUserCollectionName)
{
    public bool IsDemotedFromPrivilegedRole() =>
        _existingOrganizationUserType is OrganizationUserType.Admin or OrganizationUserType.Owner
        && NewType is not (OrganizationUserType.Admin or OrganizationUserType.Owner);

    public bool IsEnablingSecretsManager() => !_existingAccessSecretsManager && NewAccessSecretsManager;

    private readonly OrganizationUserType _existingOrganizationUserType = OrganizationUserToUpdate.Type;
    private readonly bool _existingAccessSecretsManager = OrganizationUserToUpdate.AccessSecretsManager;
};
