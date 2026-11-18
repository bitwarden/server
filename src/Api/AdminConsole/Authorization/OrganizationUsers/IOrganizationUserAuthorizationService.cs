namespace Bit.Api.AdminConsole.Authorization.OrganizationUsers;

/// <summary>
/// Decides what the caller can change when they save an organization user.
/// </summary>
public interface IOrganizationUserAuthorizationService
{
    /// <summary>
    /// Determines if the caller can save the organization user's collection access and their groups.
    /// </summary>
    /// <param name="organizationId">The ID of the organization that the user belongs to.</param>
    /// <param name="organizationUserId">
    /// The ID of the organization user to update, or null when the user is being invited.
    /// </param>
    /// <param name="postedCollectionIds">The IDs of the collections the client sent.</param>
    /// <param name="currentCollectionIds">
    /// The IDs of the collections the organization user can already reach. Empty when the user is being invited.
    /// </param>
    Task<OrganizationUserAuthorizationResult> AuthorizeSaveAsync(
        Guid organizationId,
        Guid? organizationUserId,
        IReadOnlyCollection<Guid> postedCollectionIds,
        IReadOnlyCollection<Guid> currentCollectionIds);
}
