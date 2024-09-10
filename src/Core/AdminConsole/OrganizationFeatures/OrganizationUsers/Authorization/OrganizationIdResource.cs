#nullable enable

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization;

/// <summary>
/// A typed wrapper for an organization Guid. This is used for authorization checks because
/// AuthorizationService needs more than just a Guid, but we also don't want to fetch the
/// Organization object from the database each time when it's usually not needed.
/// It implicitly converts to a regular Guid.
/// </summary>
public class OrganizationIdResource
{
    public OrganizationIdResource(Guid id)
    {
        Id = id;
    }
    private Guid Id { get; }
    public static implicit operator Guid(OrganizationIdResource organizationIdResource) =>
        organizationIdResource.Id;
    public override string ToString() => Id.ToString();
}
