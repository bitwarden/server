using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Collections;

/// <summary>
/// The groups and the members for a collection. The validator ignores a null list.
/// </summary>
public record CollectionAccessValidationRequest(
    Guid OrganizationId,
    IReadOnlyCollection<CollectionAccessSelection>? Groups,
    IReadOnlyCollection<CollectionAccessSelection>? Users);
