using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;

/// <summary>
/// The request to update an organization user: the loaded current state, the requested <c>New*</c> changes,
/// and the acting user. Collection and group access are trusted as already-authorized by the API layer.
/// </summary>
/// <param name="NewPermissions">The requested custom permissions (used when <paramref name="NewType"/> is Custom).</param>
/// <param name="Collections"><c>collectionsToSave</c>: posted plus currently-held collection entities for validation lookups. <c>collectionAccessToSave</c>: the access to persist; null removes all.</param>
/// <param name="NewGroups">The updated group access; null leaves groups unchanged.</param>
/// <param name="NewEmail">The requested new email address; null or blank leaves the member's email unchanged.</param>
/// <param name="NewName">The requested new account name; null leaves the member's name unchanged, blank clears it.</param>
/// <param name="DefaultUserCollectionName">Default collection name used when applicable</param>
/// <param name="PerformedByOrganizationUser">The actor's own membership; null when the actor is not an organization member (e.g. a provider).</param>
/// <param name="UserToUpdate">The member's loaded account; populated by the command before validation, null when no email change is requested or the member has no account.</param>
public record UpdateOrganizationUserRequest(
    OrganizationUser OrganizationUserToUpdate,
    Organization Organization,
    OrganizationUserType NewType,
    Permissions? NewPermissions,
    bool NewAccessSecretsManager,
    (ICollection<Collection> collectionsToSave, List<CollectionAccessSelection>? collectionAccessToSave) Collections,
    IEnumerable<Guid>? NewGroups,
    string? NewEmail,
    string? NewName,
    string? DefaultUserCollectionName,
    IActingUser PerformedBy,
    OrganizationUser? PerformedByOrganizationUser,
    User? UserToUpdate = null)
{
    public bool IsDemotedFromPrivilegedRole() =>
        _existingOrganizationUserType is OrganizationUserType.Admin or OrganizationUserType.Owner
        && NewType is not (OrganizationUserType.Admin or OrganizationUserType.Owner);

    public bool IsEnablingSecretsManager() => !_existingAccessSecretsManager && NewAccessSecretsManager;

    private readonly OrganizationUserType _existingOrganizationUserType = OrganizationUserToUpdate.Type;
    private readonly bool _existingAccessSecretsManager = OrganizationUserToUpdate.AccessSecretsManager;
};
