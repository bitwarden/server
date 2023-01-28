namespace Bit.Core.Repositories;

public interface IMetaDataRepository
{
    Task DeleteAsync(string objectName, string id);
    Task<IDictionary<string, string>> GetAsync(string objectName, string id);
    Task<string> GetAsync(string objectName, string id, string prop);
    Task UpsertAsync(string objectName, string id, IDictionary<string, string> dict);
    Task UpsertAsync(string objectName, string id, KeyValuePair<string, string> keyValuePair);
}
