﻿using Bit.Core.Entities;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Queries;


namespace Bit.Core.Vault.Repositories;

public interface ICipherRepository : IRepository<Cipher, Guid>
{
    Task<CipherDetails> GetByIdAsync(Guid id, Guid userId);
    Task<CipherOrganizationDetails> GetOrganizationDetailsByIdAsync(Guid id);
    Task<ICollection<CipherOrganizationDetails>> GetManyOrganizationDetailsByOrganizationIdAsync(Guid organizationId);
    Task<bool> GetCanEditByIdAsync(Guid userId, Guid cipherId);
    Task<ICollection<CipherDetails>> GetManyByUserIdAsync(Guid userId, bool withOrganizations = true);
    Task<ICollection<Cipher>> GetManyByOrganizationIdAsync(Guid organizationId);
    Task<ICollection<CipherOrganizationDetails>> GetManyUnassignedOrganizationDetailsByOrganizationIdAsync(Guid organizationId);
    Task CreateAsync(Cipher cipher, IEnumerable<Guid> collectionIds);
    Task CreateAsync(CipherDetails cipher);
    Task CreateAsync(CipherDetails cipher, IEnumerable<Guid> collectionIds);
    Task ReplaceAsync(CipherDetails cipher);
    Task UpsertAsync(CipherDetails cipher);
    Task<bool> ReplaceAsync(Cipher obj, IEnumerable<Guid> collectionIds);
    Task UpdatePartialAsync(Guid id, Guid userId, Guid? folderId, bool favorite);
    Task UpdateAttachmentAsync(CipherAttachment attachment);
    Task DeleteAttachmentAsync(Guid cipherId, string attachmentId);
    Task DeleteAsync(IEnumerable<Guid> ids, Guid userId);
    Task DeleteByIdsOrganizationIdAsync(IEnumerable<Guid> ids, Guid organizationId);
    Task MoveAsync(IEnumerable<Guid> ids, Guid? folderId, Guid userId);
    Task DeleteByUserIdAsync(Guid userId);
    Task DeleteByOrganizationIdAsync(Guid organizationId);
    Task UpdateCiphersAsync(Guid userId, IEnumerable<Cipher> ciphers);
    /// <summary>
    /// Create ciphers and folders for the specified UserId. Must not be used to create organization owned items.
    /// </summary>
    Task CreateAsync(Guid userId, IEnumerable<Cipher> ciphers, IEnumerable<Folder> folders);
    Task CreateAsync(IEnumerable<Cipher> ciphers, IEnumerable<Collection> collections,
        IEnumerable<CollectionCipher> collectionCiphers, IEnumerable<CollectionUser> collectionUsers);
    Task SoftDeleteAsync(IEnumerable<Guid> ids, Guid userId);
    Task SoftDeleteByIdsOrganizationIdAsync(IEnumerable<Guid> ids, Guid organizationId);
    Task<DateTime> RestoreAsync(IEnumerable<Guid> ids, Guid userId);
    Task<DateTime> RestoreByIdsOrganizationIdAsync(IEnumerable<Guid> ids, Guid organizationId);
    Task DeleteDeletedAsync(DateTime deletedDateBefore);

    /// <summary>
    /// Low-level query to get all cipher permissions for a user in an organization. DOES NOT consider the user's
    /// organization role, any collection management settings on the organization, or special unassigned cipher
    /// permissions.
    ///
    /// Recommended to use <see cref="IGetCipherPermissionsForUserQuery"/> instead to handle those cases.
    /// </summary>
    Task<ICollection<OrganizationCipherPermission>> GetCipherPermissionsForOrganizationAsync(Guid organizationId,
        Guid userId);

    /// <summary>
    /// Returns the users and the cipher ids for security tasks that are applicable to them.
    ///
    /// Security tasks are actionable when a user has manage access to the associated cipher.
    /// </summary>
    Task<ICollection<UserSecurityTaskCipher>> GetUserSecurityTasksByCipherIdsAsync(Guid organizationId, IEnumerable<SecurityTask> tasks);

    /// <summary>
    /// Updates encrypted data for ciphers during a key rotation
    /// </summary>
    /// <param name="userId">The user that initiated the key rotation</param>
    /// <param name="ciphers">A list of ciphers with updated data</param>
    UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(Guid userId,
        IEnumerable<Cipher> ciphers);
}
