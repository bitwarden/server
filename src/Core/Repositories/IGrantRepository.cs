using Bit.Core.Entities;

namespace Bit.Core.Repositories;

public interface IGrantRepository
{
    Task<Grant> GetByKeyAsync(string key);
    Task<ICollection<Grant>> GetManyAsync(string subjectId, string sessionId, string clientId, string type);
    Task SaveAsync(Grant obj);
    Task DeleteByKeyAsync(string key);
    Task DeleteManyAsync(string subjectId, string sessionId, string clientId, string type);
}
