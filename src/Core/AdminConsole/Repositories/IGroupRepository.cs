using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;

#nullable enable

namespace Bit.Core.AdminConsole.Repositories;

public interface IGroupRepository : IRepository<Group, Guid>
{
    Task<Tuple<Group?, ICollection<CollectionAccessSelection>>> GetByIdWithCollectionsAsync(Guid id);
    Task<ICollection<Group>> GetManyByOrganizationIdAsync(Guid organizationId);
    Task<ICollection<Tuple<Group, ICollection<CollectionAccessSelection>>>> GetManyWithCollectionsByOrganizationIdAsync(
        Guid organizationId);
    Task<ICollection<Group>> GetManyByManyIds(IEnumerable<Guid> groupIds);
    Task<ICollection<Guid>> GetManyIdsByUserIdAsync(Guid organizationUserId);
    /// <summary>
    /// Query all OrganizationUserIds who are a member of the specified group.
    /// </summary>
    /// <param name="id">The group id.</param>
    /// <param name="useReadOnlyReplica">
    /// Whether to use the high-availability database replica. This is for paths with high traffic where immediate data
    /// consistency is not required. You generally do not want this.
    /// </param>
    /// <returns></returns>
    Task<ICollection<Guid>> GetManyUserIdsByIdAsync(Guid id, bool useReadOnlyReplica = false);
    Task<ICollection<GroupUser>> GetManyGroupUsersByOrganizationIdAsync(Guid organizationId);
    Task CreateAsync(Group obj, IEnumerable<CollectionAccessSelection> collections);
    Task ReplaceAsync(Group obj, IEnumerable<CollectionAccessSelection> collections);
    /// <summary>
    /// Remove a user from a group.
    /// </summary>
    /// <param name="groupId">The group to remove the user from.</param>
    /// <param name="organizationUserId">The organization user to remove.</param>
    /// <param name="revisionDate">The timestamp to set as the group's new <see cref="Group.RevisionDate"/>.</param>
    Task DeleteUserAsync(Guid groupId, Guid organizationUserId, DateTime revisionDate);
    /// <summary>
    /// Update a group's members. Replaces all members currently in the group.
    /// Ignores members that do not belong to the same organization as the group.
    /// </summary>
    /// <param name="groupId">The group whose members will be replaced.</param>
    /// <param name="organizationUserIds">The full set of organization user ids that should be in the group.</param>
    /// <param name="revisionDate">The timestamp to set as the group's new <see cref="Group.RevisionDate"/>.</param>
    Task UpdateUsersAsync(Guid groupId, IEnumerable<Guid> organizationUserIds, DateTime revisionDate);
    /// <summary>
    /// Add members to a group. Gracefully ignores members that are already in the group,
    /// duplicate organizationUserIds, and organizationUsers who are not part of the organization.
    /// </summary>
    /// <param name="groupId">The group to add members to.</param>
    /// <param name="organizationUserIds">The organization user ids to add.</param>
    /// <param name="revisionDate">The timestamp to set as the group's new <see cref="Group.RevisionDate"/>.</param>
    Task AddGroupUsersByIdAsync(Guid groupId, IEnumerable<Guid> organizationUserIds, DateTime revisionDate);
    Task DeleteManyAsync(IEnumerable<Guid> groupIds);
}
