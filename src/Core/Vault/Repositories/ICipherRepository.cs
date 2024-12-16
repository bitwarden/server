﻿using Bit.Core.Entities;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;

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
    Task CreateAsync(IEnumerable<Cipher> ciphers, IEnumerable<Folder> folders);
    Task CreateAsync(IEnumerable<Cipher> ciphers, IEnumerable<Collection> collections,
        IEnumerable<CollectionCipher> collectionCiphers, IEnumerable<CollectionUser> collectionUsers);
    Task SoftDeleteAsync(IEnumerable<Guid> ids, Guid userId);
    Task SoftDeleteByIdsOrganizationIdAsync(IEnumerable<Guid> ids, Guid organizationId);
    Task<DateTime> RestoreAsync(IEnumerable<Guid> ids, Guid userId);
    Task<DateTime> RestoreByIdsOrganizationIdAsync(IEnumerable<Guid> ids, Guid organizationId);
    Task DeleteDeletedAsync(DateTime deletedDateBefore);

    /// <summary>
    /// Updates encrypted data for ciphers during a key rotation
    /// </summary>
    /// <param name="userId">The user that initiated the key rotation</param>
    /// <param name="ciphers">A list of ciphers with updated data</param>
    UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(Guid userId,
        IEnumerable<Cipher> ciphers);
}
