using System;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Web;
using Bit.Core.Models.Api.Request.Accounts;
using Bit.Identity;
using IdentityServer4.Models;
using IdentityServer4.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Test.Common.ApplicationFactories
{
    public class IdentityApplicationFactory : WebApplicationFactoryBase<Startup>
    {
        public async Task<HttpContext> RegisterAsync(RegisterRequestModel model)
        {
            return await Server.PostAsync("/accounts/register", JsonContent.Create(model));
        }
    }
}
