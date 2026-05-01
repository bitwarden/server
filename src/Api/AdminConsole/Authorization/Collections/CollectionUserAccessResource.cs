using Bit.Core.Entities;

namespace Bit.Api.AdminConsole.Authorization.Collections;

/// <summary>
/// Carries the collections and target user for a collection user-access authorization check.
/// </summary>
public record CollectionUserAccessResource(
    ICollection<Collection> Collections,
    Guid TargetOrganizationUserId);
