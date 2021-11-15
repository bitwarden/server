using System.Threading.Tasks;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public interface ISsoConfigService
    {
        Task SaveAsync(SsoConfig config, Organization organization);
    }
}
