using System.Threading.Tasks;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public interface ICollectionService
    {
        Task SaveAsync(Collection collection);
    }
}
