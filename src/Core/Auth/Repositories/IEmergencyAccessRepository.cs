using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;

namespace Bit.Core.Repositories;

public interface IEmergencyAccessRepository : IRepository<EmergencyAccess, Guid>
{
    Task<int> GetCountByGrantorIdEmailAsync(Guid grantorId, string email, bool onlyRegisteredUsers);
    Task<ICollection<EmergencyAccessDetails>> GetManyDetailsByGrantorIdAsync(Guid grantorId);
    Task<ICollection<EmergencyAccessDetails>> GetManyDetailsByGranteeIdAsync(Guid granteeId);
    Task<EmergencyAccessDetails> GetDetailsByIdGrantorIdAsync(Guid id, Guid grantorId);
    Task<ICollection<EmergencyAccessNotify>> GetManyToNotifyAsync();
    Task<ICollection<EmergencyAccessDetails>> GetExpiredRecoveriesAsync();
}
