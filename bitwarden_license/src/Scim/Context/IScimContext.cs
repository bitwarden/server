using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Http;

namespace Bit.Scim.Context
{
    public interface IScimContext
    {
        HttpContext HttpContext { get; set; }
        ScimProviderType? ScimProvider { get; set; }
        Task BuildAsync(HttpContext httpContext, GlobalSettings globalSettings);
    }
}
