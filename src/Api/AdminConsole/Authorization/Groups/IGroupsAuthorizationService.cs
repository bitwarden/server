namespace Bit.Api.AdminConsole.Authorization.Groups;

/// <summary>
/// Decides what the caller can change when they save a group.
/// </summary>
public interface IGroupsAuthorizationService
{
    /// <summary>
    /// Determines if the caller can save the group's collection access and its members.
    /// </summary>
    /// <param name="organizationId">The ID of the organization that owns the group.</param>
    /// <param name="groupId">The ID of the group to update, or null when the group is being created.</param>
    /// <param name="postedCollectionIds">The IDs of the collections the client sent.</param>
    /// <param name="currentCollectionIds">
    /// The IDs of the collections the group can already access. Empty when the group is being created.
    /// </param>
    /// <param name="postedUserIds">The organization user IDs the client sent.</param>
    Task<GroupsAuthorizationResult> AuthorizeSaveAsync(
        Guid organizationId,
        Guid? groupId,
        IReadOnlyCollection<Guid> postedCollectionIds,
        IReadOnlyCollection<Guid> currentCollectionIds,
        IReadOnlyCollection<Guid> postedUserIds);
}
