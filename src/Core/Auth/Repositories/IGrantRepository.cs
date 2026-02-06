using Bit.Core.Auth.Models.Data;

#nullable enable

namespace Bit.Core.Auth.Repositories;

public interface IGrantRepository
{
    Task<IGrant?> GetByKeyAsync(string key);
    Task<ICollection<IGrant>> GetManyAsync(string subjectId, string sessionId, string clientId, string type);
    Task SaveAsync(IGrant obj);
    Task DeleteByKeyAsync(string key);
    Task DeleteManyAsync(string subjectId, string sessionId, string clientId, string type);
}
