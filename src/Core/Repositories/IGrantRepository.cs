using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using System.Collections.Generic;

namespace Bit.Core.Repositories
{
    public interface IGrantRepository
    {
        Task<Grant> GetByKeyAsync(string key);
        Task<ICollection<Grant>> GetManyAsync(string subjectId);
        Task SaveAsync(Grant obj);
        Task DeleteAsync(string key);
        Task DeleteAsync(string subjectId, string clientId);
        Task DeleteAsync(string subjectId, string clientId, string type);
    }
}
