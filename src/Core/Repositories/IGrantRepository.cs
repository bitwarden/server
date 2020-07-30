using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using System.Collections.Generic;

namespace Bit.Core.Repositories
{
    public interface IGrantRepository
    {
        Task<Grant> GetByKeyAsync(string key);
        Task<ICollection<Grant>> GetManyAsync(string subjectId, string sessionId, string clientId, string type);
        Task SaveAsync(Grant obj);
        Task DeleteByKeyAsync(string key);
        Task DeleteManyAsync(string subjectId, string sessionId, string clientId, string type);
    }
}
