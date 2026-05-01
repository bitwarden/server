using Bit.Core.Entities;

namespace Bit.Api.AdminConsole.Authorization.Collections;

/// <summary>
/// Carries the collections and target group for a collection group-access authorization check.
/// </summary>
public record CollectionGroupAccessResource(
    ICollection<Collection> Collections,
    Guid TargetGroupId);
