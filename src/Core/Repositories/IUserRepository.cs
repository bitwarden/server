using Bit.Core.Auth.UserFeatures.UserKey;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Tools.Entities;
using Bit.Core.Vault.Entities;

namespace Bit.Core.Repositories;

public interface IUserRepository : IRepository<User, Guid>
{
    Task<User> GetByEmailAsync(string email);
    Task<User> GetBySsoUserAsync(string externalId, Guid? organizationId);
    Task<UserKdfInformation> GetKdfInformationByEmailAsync(string email);
    Task<ICollection<User>> SearchAsync(string email, int skip, int take);
    Task<ICollection<User>> GetManyByPremiumAsync(bool premium);
    Task<string> GetPublicKeyAsync(Guid id);
    Task<DateTime> GetAccountRevisionDateAsync(Guid id);
    Task UpdateStorageAsync(Guid id);
    Task UpdateRenewalReminderDateAsync(Guid id, DateTime renewalReminderDate);

    /// <summary>
    /// Sets a new user key and updates all encrypted data.
    /// <para>Warning: Any user key encrypted data not included will be lost.</para>
    /// </summary>
    /// <param name="user">The user to update</param>
    /// <param name="ciphers">All user ciphers with re-encrypted data</param>
    /// <param name="folders">All user folders with re-encrypted data</param>
    /// <param name="sends">All user sends with re-encrypted data</param>
    /// <param name="emergencyAccessKeys">All user emergency access with re-encrypted keys</param>
    /// <param name="resetPasswordKeys">All organization users with re-encrypted reset password keys</param>
    [Obsolete("Intended for future improvements to key rotation. Do not use.")]
    Task UpdateUserKeyAndEncryptedDataAsync(User user, IEnumerable<Cipher> ciphers, IEnumerable<Folder> folders,
        IEnumerable<Send> sends, IEnumerable<EmergencyAccess> emergencyAccessKeys,
        IEnumerable<OrganizationUser> resetPasswordKeys);
    Task<IEnumerable<User>> GetManyAsync(IEnumerable<Guid> ids);
    /// <summary>
    /// Sets a new user key and updates all encrypted data.
    /// <para>Warning: Any user key encrypted data not included will be lost.</para>
    /// </summary>
    /// <param name="user">The user to update</param>
    /// <param name="updateDataActions">Registered database calls to update re-encrypted data.</param>
    [Obsolete("Intended for future improvements to key rotation. Do not use.")]
    Task UpdateUserKeyAndEncryptedDataAsync(User user,
        IEnumerable<UpdateEncryptedDataForKeyRotation> updateDataActions);
}
