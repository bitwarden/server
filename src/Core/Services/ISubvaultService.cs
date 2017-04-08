using System.Threading.Tasks;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public interface ISubvaultService
    {
        Task SaveAsync(Subvault subvault);
    }
}
