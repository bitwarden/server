namespace Bit.Core.Repositories.Noop;

public class MetaDataRepository : IMetaDataRepository
{
    public Task DeleteAsync(string objectName, string id)
    {
        return Task.FromResult(0);
    }

    public Task<IDictionary<string, string>> GetAsync(string objectName, string id)
    {
        return Task.FromResult(null as IDictionary<string, string>);
    }

    public Task<string> GetAsync(string objectName, string id, string prop)
    {
        return Task.FromResult(null as string);
    }

    public Task UpsertAsync(string objectName, string id, IDictionary<string, string> dict)
    {
        return Task.FromResult(0);
    }

    public Task UpsertAsync(string objectName, string id, KeyValuePair<string, string> keyValuePair)
    {
        return Task.FromResult(0);
    }
}
