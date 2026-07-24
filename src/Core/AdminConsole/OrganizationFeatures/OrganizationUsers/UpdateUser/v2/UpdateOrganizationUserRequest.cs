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
/// <param name="CollectionsToSave">The collection access to persist; null removes all collection access.</param>
/// <param name="NewGroups">The updated group access; null leaves groups unchanged.</param>
/// <param name="DefaultUserCollectionName">Default collection name used when applicable</param>
public record UpdateOrganizationUserRequest(
    OrganizationUser OrganizationUserToUpdate,
    Organization Organization,
    OrganizationUserType NewType,
    Permissions? NewPermissions,
    bool NewAccessSecretsManager,
    List<CollectionAccessSelection>? CollectionsToSave,
    IEnumerable<Guid>? NewGroups,
    IActingUser PerformedBy,
    string? DefaultUserCollectionName)
{
    public bool IsDemotedFromPrivilegedRole() =>
        _existingOrganizationUserType is OrganizationUserType.Admin or OrganizationUserType.Owner
        && NewType is not (OrganizationUserType.Admin or OrganizationUserType.Owner);

    public bool IsEnablingSecretsManager() => !_existingAccessSecretsManager && NewAccessSecretsManager;

    private readonly OrganizationUserType _existingOrganizationUserType = OrganizationUserToUpdate.Type;
    private readonly bool _existingAccessSecretsManager = OrganizationUserToUpdate.AccessSecretsManager;
};
