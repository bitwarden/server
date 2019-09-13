using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bit.Core.Repositories
{
    public interface IMetaDataRespository
    {
        Task DeleteAsync(string id);
        Task<IDictionary<string, string>> GetAsync(string id);
        Task<string> GetAsync(string id, string prop);
        Task UpsertAsync(string id, IDictionary<string, string> dict);
        Task UpsertAsync(string id, KeyValuePair<string, string> keyValuePair);
    }
}
