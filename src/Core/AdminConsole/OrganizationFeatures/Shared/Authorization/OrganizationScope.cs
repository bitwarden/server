#nullable enable

namespace Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;

/// <summary>
/// A typed wrapper for an organization Guid. This is used for authorization checks
/// scoped to an organization's resources (e.g. all users for an organization).
/// In these cases, AuthorizationService needs more than just a Guid, but we also don't want to fetch the
/// Organization object from the database each time when it's usually not needed.
/// This should not be used for operations on the organization itself.
/// It implicitly converts to a regular Guid.
/// </summary>
public record OrganizationScope
{
    public OrganizationScope(Guid id)
    {
        Id = id;
    }
    private Guid Id { get; }
    public static implicit operator Guid(OrganizationScope organizationScope) =>
        organizationScope.Id;
    public override string ToString() => Id.ToString();
}
