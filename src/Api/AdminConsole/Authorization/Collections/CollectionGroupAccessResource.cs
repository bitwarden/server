using Bit.Core.Entities;

namespace Bit.Api.AdminConsole.Authorization.Collections;

/// <summary>
/// Resource for authorizing group access to collections.
/// Use <see cref="Guid.Empty"/> for <c>TargetGroupId</c> when the group does not exist yet (e.g. on group create).
/// </summary>
public record CollectionGroupAccessResource(
    ICollection<Collection> Collections,
    Guid TargetGroupId);
