using Bit.Core.Entities;

namespace Bit.Api.AdminConsole.Authorization.Collections;

/// <summary>
/// Resource for authorizing user access to collections.
/// Use <see cref="Guid.Empty"/> for <c>TargetOrganizationUserId</c> when the user does not exist yet (e.g. on invite).
/// </summary>
public record CollectionUserAccessResource(
    ICollection<Collection> Collections,
    Guid TargetOrganizationUserId);
