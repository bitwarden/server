using IdentityServer4.Services;
using System.Threading.Tasks;

namespace Bit.Core.IdentityServer
{
    public class VaultCorsPolicyService : ICorsPolicyService
    {
        private readonly GlobalSettings _globalSettings;

        public VaultCorsPolicyService(GlobalSettings globalSettings)
        {
            _globalSettings = globalSettings;
        }

        public Task<bool> IsOriginAllowedAsync(string origin)
        {
            return Task.FromResult(origin == _globalSettings.BaseServiceUri.Vault);
        }
    }
}
