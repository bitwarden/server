using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.KeyManagement.UserKey;

namespace Bit.Core.Repositories;

public interface IEmergencyAccessRepository : IRepository<EmergencyAccess, Guid>
{
    Task<int> GetCountByGrantorIdEmailAsync(Guid grantorId, string email, bool onlyRegisteredUsers);
    Task<ICollection<EmergencyAccessDetails>> GetManyDetailsByGrantorIdAsync(Guid grantorId);
    Task<ICollection<EmergencyAccessDetails>> GetManyDetailsByGranteeIdAsync(Guid granteeId);
    /// <summary>
    /// Fetches emergency access details by EmergencyAccess id and grantor id
    /// </summary>
    /// <param name="id">Emergency Access Id</param>
    /// <param name="grantorId">Grantor Id</param>
    /// <returns>EmergencyAccessDetails or null</returns>
    Task<EmergencyAccessDetails?> GetDetailsByIdGrantorIdAsync(Guid id, Guid grantorId);
    /// <summary>
    /// Database call to fetch emergency accesses that need notification emails sent through a Job
    /// </summary>
    /// <returns>collection of EmergencyAccessNotify objects that require notification</returns>
    Task<ICollection<EmergencyAccessNotify>> GetManyToNotifyAsync();
    Task<ICollection<EmergencyAccessDetails>> GetExpiredRecoveriesAsync();

    /// <summary>
    /// Updates encrypted data for emergency access during a key rotation
    /// </summary>
    /// <param name="grantorId">The grantor that initiated the key rotation</param>
    /// <param name="emergencyAccessKeys">A list of emergency access with updated keys</param>
    UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(Guid grantorId,
        IEnumerable<EmergencyAccess> emergencyAccessKeys);
}
