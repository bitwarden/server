using System.Threading.Tasks;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public interface IPolicyService
    {
        Task SaveAsync(Policy policy);
        Task DeleteAsync(Policy policy);
    }
}
