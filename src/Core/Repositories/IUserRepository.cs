using Bit.Core.Entities;
using Bit.Core.Models.Data;

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
    Task<IEnumerable<User>> GetManyAsync(IEnumerable<Guid> ids);
}
