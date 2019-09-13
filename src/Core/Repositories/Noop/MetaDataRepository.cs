using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bit.Core.Repositories.Noop
{
    public class MetaDataRepository : IMetaDataRespository
    {
        public Task DeleteAsync(string id)
        {
            return Task.FromResult(0);
        }

        public Task<IDictionary<string, string>> GetAsync(string id)
        {
            return Task.FromResult(null as IDictionary<string, string>);
        }

        public Task<string> GetAsync(string id, string prop)
        {
            return Task.FromResult(null as string);
        }

        public Task UpsertAsync(string id, IDictionary<string, string> dict)
        {
            return Task.FromResult(0);
        }

        public Task UpsertAsync(string id, KeyValuePair<string, string> keyValuePair)
        {
            return Task.FromResult(0);
        }
    }
}
