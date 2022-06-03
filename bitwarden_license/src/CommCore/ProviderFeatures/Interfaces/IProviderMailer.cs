using System.Threading.Tasks;
using Bit.Core.Entities.Provider;

namespace Bit.CommCore.ProviderFeatures.Interfaces
{
    public interface IProviderMailer
    {
        Task SendProviderSetupInviteEmailAsync(Provider provider, string token, string email);
        Task SendProviderInviteEmailAsync(string providerName, ProviderUser providerUser, string token, string email);
        Task SendProviderConfirmedEmailAsync(string providerName, string email);
        Task SendProviderUserRemoved(string providerName, string email);
    }
}
