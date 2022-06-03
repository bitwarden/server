using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Entities.Provider;

namespace Bit.CommCore.ProviderFeatures.Interfaces
{
    public interface IProviderMailer
    {
        Task SendProviderSetupInviteEmailAsync(Provider provider, string token, string email);
        Task SendProviderInviteEmailAsync(Provider provider, ProviderUser providerUser, string token);
        Task SendProviderConfirmedEmailAsync(Provider providerName, User user);
        Task SendProviderUserRemoved(Provider provider, string email);
    }
}
