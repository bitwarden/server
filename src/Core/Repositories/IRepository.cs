using System.Threading.Tasks;

namespace Bit.Core.Repositories
{
    public interface IRepository<T> where T : IDataObject
    {
        Task<T> GetByIdAsync(string id);
        Task CreateAsync(T obj);
        Task ReplaceAsync(T obj);
        Task UpsertAsync(T obj);
        Task DeleteByIdAsync(string id);
        Task DeleteAsync(T obj);
    }
}
