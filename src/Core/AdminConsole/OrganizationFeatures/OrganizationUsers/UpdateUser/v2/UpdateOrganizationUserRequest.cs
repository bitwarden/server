using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;

/// <summary>
/// The request to update an organization user. Carries the loaded database copy (its current, unmodified
/// state) together with the requested field changes — the two are kept separate, and the command applies
/// the changes onto the copy. Collection and group access are trusted as already-authorized by the caller
/// (the API layer resolves collection resource authorization).
/// </summary>
/// <param name="OrganizationUser">The loaded organization user in its current (unmodified) state.</param>
/// <param name="Organization">The loaded organization the user belongs to.</param>
/// <param name="OrganizationAbility">The loaded organization ability for the user's organization.</param>
/// <param name="Type">The requested member role.</param>
/// <param name="Permissions">The requested custom permissions (used when <paramref name="Type"/> is Custom).</param>
/// <param name="AccessSecretsManager">Whether the user should have Secrets Manager access.</param>
/// <param name="PerformedBy">The actor making the change.</param>
/// <param name="PerformedByOrganizationUser">The acting user's own organization membership (its role and
/// permissions), used to prevent granting a role higher than their own. Null when the actor is not an
/// organization member (e.g. a provider), whose authority is instead governed by
/// <see cref="IActingUser.IsOrganizationOwnerOrProvider"/>.</param>
/// <param name="CollectionsToSave">The user's updated collection access. Null removes all collection access.</param>
/// <param name="GroupsToSave">The user's updated group access. Null means groups are not updated.</param>
/// <param name="CurrentAccessIds">The collection ids the user currently has access to (used for the self-add check).</param>
public record UpdateOrganizationUserRequest(
    OrganizationUser OrganizationUser,
    Organization Organization,
    OrganizationAbility OrganizationAbility,
    OrganizationUserType Type,
    Permissions? Permissions,
    bool AccessSecretsManager,
    IActingUser PerformedBy,
    OrganizationUser? PerformedByOrganizationUser,
    List<CollectionAccessSelection>? CollectionsToSave,
    IEnumerable<Guid>? GroupsToSave,
    HashSet<Guid> CurrentAccessIds);

/// <summary>
/// The input to the validator: the <see cref="OrganizationUser"/> in its current (unmodified) database state,
/// the requested <see cref="NewType"/> it would be changed to, and the loaded <see cref="Organization"/> and
/// <see cref="OrganizationAbility"/>. The validator checks the current state against the requested change; the
/// command only applies the change after validation succeeds.
/// </summary>
public record UpdateOrganizationUserValidationRequest(
    OrganizationUser OrganizationUser,
    OrganizationUserType NewType,
    IActingUser PerformedBy,
    OrganizationUser? PerformedByOrganizationUser,
    List<CollectionAccessSelection> CollectionsToSave,
    IEnumerable<Guid>? GroupsToSave,
    HashSet<Guid> CurrentAccessIds,
    Organization Organization,
    OrganizationAbility OrganizationAbility);
