using Bit.Core.Billing.Premium.Models;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Models.Data;

#nullable enable

namespace Bit.Core.Repositories;

public interface IUserRepository : IRepository<User, Guid>
{
    Task<User?> GetByEmailAsync(string email);
    Task<IEnumerable<User>> GetManyByEmailsAsync(IEnumerable<string> emails);
    Task<User?> GetBySsoUserAsync(string externalId, Guid? organizationId);
    Task<UserKdfInformation?> GetKdfInformationByEmailAsync(string email);
    Task<ICollection<User>> SearchAsync(string email, int skip, int take);
    Task<ICollection<User>> GetManyByPremiumAsync(bool premium);
    Task<string?> GetPublicKeyAsync(Guid id);
    Task<DateTime> GetAccountRevisionDateAsync(Guid id);
    Task UpdateStorageAsync(Guid id);
    Task UpdateRenewalReminderDateAsync(Guid id, DateTime renewalReminderDate);
    Task<IEnumerable<User>> GetManyAsync(IEnumerable<Guid> ids);
    /// <summary>
    /// Retrieves the data for the requested user IDs and includes an additional property indicating
    /// whether the user has premium access directly or through an organization.
    /// </summary>
    [Obsolete("Use GetPremiumAccessByIdsAsync instead. This method will be removed in a future version.")]
    Task<IEnumerable<UserWithCalculatedPremium>> GetManyWithCalculatedPremiumAsync(IEnumerable<Guid> ids);
    /// <summary>
    /// Retrieves the data for the requested user ID and includes additional property indicating
    /// whether the user has premium access directly or through an organization.
    ///
    /// Calls the same stored procedure as GetManyWithCalculatedPremiumAsync but handles the query
    /// for a single user.
    /// </summary>
    /// <param name="userId">The user ID to retrieve data for.</param>
    /// <returns>User data with calculated premium access; null if nothing is found</returns>
    [Obsolete("Use GetPremiumAccessAsync instead. This method will be removed in a future version.")]
    Task<UserWithCalculatedPremium?> GetCalculatedPremiumAsync(Guid userId);
    /// <summary>
    /// Retrieves premium access status for multiple users.
    /// For internal use - consumers should use IHasPremiumAccessQuery instead.
    /// </summary>
    /// <param name="ids">The user IDs to check</param>
    /// <returns>Collection of UserPremiumAccess objects containing premium status information</returns>
    Task<IEnumerable<UserPremiumAccess>> GetPremiumAccessByIdsAsync(IEnumerable<Guid> ids);
    /// <summary>
    /// Retrieves premium access status for a single user.
    /// For internal use - consumers should use IHasPremiumAccessQuery instead.
    /// </summary>
    /// <param name="userId">The user ID to check</param>
    /// <returns>UserPremiumAccess object containing premium status information, or null if user not found</returns>
    Task<UserPremiumAccess?> GetPremiumAccessAsync(Guid userId);
    /// <summary>
    /// Sets a new user key and updates all encrypted data.
    /// <para>Warning: Any user key encrypted data not included will be lost.</para>
    /// </summary>
    /// <param name="user">The user to update</param>
    /// <param name="updateDataActions">Registered database calls to update re-encrypted data.</param>
    Task UpdateUserKeyAndEncryptedDataAsync(User user,
        IEnumerable<UpdateEncryptedDataForKeyRotation> updateDataActions);
    Task UpdateUserKeyAndEncryptedDataV2Async(User user,
        IEnumerable<UpdateEncryptedDataForKeyRotation> updateDataActions);
    /// <summary>
    /// Sets the account cryptographic state to a user in a single transaction. The provided
    /// MUST be a V2 encryption state. Passing in a V1 encryption state will throw.
    /// Extra actions can be passed in case other user data needs to be updated in the same transaction.
    /// </summary>
    Task SetV2AccountCryptographicStateAsync(
        Guid userId,
        UserAccountKeysData accountKeysData,
        IEnumerable<UpdateUserData>? updateUserDataActions = null);
    Task DeleteManyAsync(IEnumerable<User> users);

    UpdateUserData SetKeyConnectorUserKey(Guid userId, string keyConnectorWrappedUserKey);

    /// <summary>
    /// Sets the master password and KDF for a user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="masterPasswordUnlockData">Data for unlocking with the master password.</param>
    /// <param name="serverSideHashedMasterPasswordAuthenticationHash">Server side hash of the user master authentication password hash</param>
    /// <param name="masterPasswordHint">Optional hint for the master password.</param>
    /// <returns>A task to complete the operation.</returns>
    UpdateUserData SetMasterPassword(Guid userId, MasterPasswordUnlockData masterPasswordUnlockData,
        string serverSideHashedMasterPasswordAuthenticationHash, string? masterPasswordHint);

    /// <summary>
    /// Updates multiple user data properties in a single transaction.
    /// </summary>
    /// <param name="updateUserDataActions">Actions to update user data.</param>
    /// <returns>On success</returns>
    Task UpdateUserDataAsync(IEnumerable<UpdateUserData> updateUserDataActions);
    
    UpdateUserData SetRegisterFinishUserData(Guid userId, RegisterFinishData registerFinishData);
}

public delegate Task UpdateUserData(Microsoft.Data.SqlClient.SqlConnection? connection = null,
    Microsoft.Data.SqlClient.SqlTransaction? transaction = null);
