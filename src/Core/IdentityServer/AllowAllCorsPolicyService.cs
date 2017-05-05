using IdentityServer4.Services;
using System.Threading.Tasks;

namespace Bit.Core.IdentityServer
{
    public class AllowAllCorsPolicyService : ICorsPolicyService
    {
        public Task<bool> IsOriginAllowedAsync(string origin)
        {
            return Task.FromResult(true);
        }
    }
}
