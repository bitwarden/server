using Bit.Core.Entities;
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
    Task<IEnumerable<UserWithCalculatedPremium>> GetManyWithCalculatedPremiumAsync(IEnumerable<Guid> ids);
    /// <summary>
    /// Sets a new user key and updates all encrypted data.
    /// <para>Warning: Any user key encrypted data not included will be lost.</para>
    /// </summary>
    /// <param name="user">The user to update</param>
    /// <param name="updateDataActions">Registered database calls to update re-encrypted data.</param>
    Task UpdateUserKeyAndEncryptedDataAsync(User user,
        IEnumerable<UpdateEncryptedDataForKeyRotation> updateDataActions);
    Task DeleteManyAsync(IEnumerable<User> users);
}
