using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

#nullable enable

namespace Bit.Core.Repositories;

public interface IOrganizationUserRepository : IRepository<OrganizationUser, Guid>
{
    Task<int> GetCountByOrganizationIdAsync(Guid organizationId);
    Task<int> GetCountByFreeOrganizationAdminUserAsync(Guid userId);
    Task<int> GetCountByOnlyOwnerAsync(Guid userId);
    Task<ICollection<OrganizationUser>> GetManyByUserAsync(Guid userId);
    Task<ICollection<OrganizationUser>> GetManyByOrganizationAsync(Guid organizationId, OrganizationUserType? type);
    Task<int> GetCountByOrganizationAsync(Guid organizationId, string email, bool onlyRegisteredUsers);
    Task<ICollection<string>> SelectKnownEmailsAsync(Guid organizationId, IEnumerable<string> emails, bool onlyRegisteredUsers);
    Task<OrganizationUser?> GetByOrganizationAsync(Guid organizationId, Guid userId);
    Task<Tuple<OrganizationUser?, ICollection<CollectionAccessSelection>>> GetByIdWithCollectionsAsync(Guid id);
    Task<OrganizationUserUserDetails?> GetDetailsByIdAsync(Guid id);
    /// <summary>
    /// Returns the OrganizationUser and its associated collections (excluding DefaultUserCollections).
    /// </summary>
    /// <param name="id">The id of the OrganizationUser</param>
    /// <returns>A tuple containing the OrganizationUser and its associated collections</returns>
    Task<(OrganizationUserUserDetails? OrganizationUser, ICollection<CollectionAccessSelection> Collections)> GetDetailsByIdWithCollectionsAsync(Guid id);
    /// <summary>
    /// Returns the OrganizationUsers and their associated collections (excluding DefaultUserCollections).
    /// </summary>
    /// <param name="organizationId">The id of the organization</param>
    /// <param name="includeGroups">Whether to include groups</param>
    /// <param name="includeCollections">Whether to include collections</param>
    /// <returns>A list of OrganizationUserUserDetails</returns>
    Task<ICollection<OrganizationUserUserDetails>> GetManyDetailsByOrganizationAsync(Guid organizationId, bool includeGroups = false, bool includeCollections = false);
    /// <inheritdoc cref="GetManyDetailsByOrganizationAsync"/>
    /// <remarks>
    /// This method is optimized for performance.
    /// Reduces database round trips by fetching all data in fewer queries.
    /// </remarks>
    Task<ICollection<OrganizationUserUserDetails>> GetManyDetailsByOrganizationAsync_vNext(Guid organizationId, bool includeGroups = false, bool includeCollections = false);
    Task<ICollection<OrganizationUserOrganizationDetails>> GetManyDetailsByUserAsync(Guid userId,
        OrganizationUserStatusType? status = null);
    Task<OrganizationUserOrganizationDetails?> GetDetailsByUserAsync(Guid userId, Guid organizationId,
        OrganizationUserStatusType? status = null);
    Task UpdateGroupsAsync(Guid orgUserId, IEnumerable<Guid> groupIds);
    Task UpsertManyAsync(IEnumerable<OrganizationUser> organizationUsers);
    Task<Guid> CreateAsync(OrganizationUser obj, IEnumerable<CollectionAccessSelection> collections);
    Task<ICollection<Guid>?> CreateManyAsync(IEnumerable<OrganizationUser> organizationIdUsers);
    Task ReplaceAsync(OrganizationUser obj, IEnumerable<CollectionAccessSelection> collections);
    Task ReplaceManyAsync(IEnumerable<OrganizationUser> organizationUsers);
    Task<ICollection<OrganizationUser>> GetManyByManyUsersAsync(IEnumerable<Guid> userIds);
    Task<ICollection<OrganizationUser>> GetManyAsync(IEnumerable<Guid> Ids);
    Task DeleteManyAsync(IEnumerable<Guid> userIds);
    Task<OrganizationUser?> GetByOrganizationEmailAsync(Guid organizationId, string email);
    Task<IEnumerable<OrganizationUserPublicKey>> GetManyPublicKeysByOrganizationUserAsync(Guid organizationId, IEnumerable<Guid> Ids);
    Task<IEnumerable<OrganizationUserUserDetails>> GetManyByMinimumRoleAsync(Guid organizationId, OrganizationUserType minRole);
    Task RevokeAsync(Guid id);
    Task RestoreAsync(Guid id, OrganizationUserStatusType status);
    Task<IEnumerable<OrganizationUserPolicyDetails>> GetByUserIdWithPolicyDetailsAsync(Guid userId, PolicyType policyType);
    Task<int> GetOccupiedSmSeatCountByOrganizationIdAsync(Guid organizationId);
    Task<IEnumerable<OrganizationUserResetPasswordDetails>> GetManyAccountRecoveryDetailsByOrganizationUserAsync(Guid organizationId, IEnumerable<Guid> organizationUserIds);

    /// <summary>
    /// Updates encrypted data for organization users during a key rotation
    /// </summary>
    /// <param name="userId">The user that initiated the key rotation</param>
    /// <param name="resetPasswordKeys">A list of organization users with updated reset password keys</param>
    UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(Guid userId,
        IEnumerable<OrganizationUser> resetPasswordKeys);

    /// <summary>
    /// Returns a list of OrganizationUsers with email domains that match one of the Organization's claimed domains.
    /// </summary>
    Task<ICollection<OrganizationUser>> GetManyByOrganizationWithClaimedDomainsAsync(Guid organizationId);
    Task RevokeManyByIdAsync(IEnumerable<Guid> organizationUserIds);

    /// <summary>
    /// Returns a list of OrganizationUsersUserDetails with the specified role.
    /// </summary>
    /// <param name="organizationId">The organization to search within</param>
    /// <param name="role">The role to search for</param>
    /// <returns>A list of OrganizationUsersUserDetails with the specified role</returns>
    Task<IEnumerable<OrganizationUserUserDetails>> GetManyDetailsByRoleAsync(Guid organizationId, OrganizationUserType role);

    Task CreateManyAsync(IEnumerable<CreateOrganizationUser> organizationUserCollection);

    /// <summary>
    /// It will only confirm if the user is in the `Accepted` state.
    ///
    /// This is an idempotent operation.
    /// </summary>
    /// <param name="organizationUserToConfirm">Accepted OrganizationUser to confirm</param>
    /// <returns>True, if the user was updated. False, if not performed.</returns>
    Task<bool> ConfirmOrganizationUserAsync(AcceptedOrganizationUserToConfirm organizationUserToConfirm);
}
