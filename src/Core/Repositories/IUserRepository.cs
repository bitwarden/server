using Bit.Core.Auth.Entities;
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
    Task UpdateUserKeyAndEncryptedDataAsync(User user, IEnumerable<Cipher> ciphers, IEnumerable<Folder> folders, IEnumerable<Send> sends, IEnumerable<EmergencyAccess> emergencyAccessKeys, IEnumerable<OrganizationUser> accountRecoveryKeys);
    Task<IEnumerable<User>> GetManyAsync(IEnumerable<Guid> ids);
}
