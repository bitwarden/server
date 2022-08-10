using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Bit.Scim;
using Bit.Scim.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.IntegrationTestCommon.Factories
{
    public class ScimApplicationFactory : WebApplicationFactoryBase<Startup>
    {
        public readonly new TestServer Server;

        public ScimApplicationFactory()
        {
            WebApplicationFactory<Startup> webApplicationFactory = WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Override to bypass SCIM authorization
                    services.AddAuthorization(config =>
                    {
                        config.AddPolicy("Scim", policy =>
                        {
                            policy.RequireAssertion(a => true);
                        });
                    });
                });
            });

            Server = webApplicationFactory.Server;
        }

        public async Task<HttpContext> GetAsync(Guid organizationId, Guid id)
        {
            return await Server.GetAsync($"/v2/{organizationId}/groups/{id}");
        }

        public async Task<HttpContext> GetListAsync(Guid organizationId, string filter, int? count, int? startIndex)
        {
            var queryString = new QueryString("?");

            if (!string.IsNullOrWhiteSpace(filter))
            {
                queryString = queryString.Add("filter", filter);
            }

            if (count.HasValue)
            {
                queryString = queryString.Add("count", count.ToString());
            }

            if (startIndex.HasValue)
            {
                queryString = queryString.Add("startIndex", startIndex.ToString());
            }

            return await Server.GetAsync($"/v2/{organizationId}/groups", httpContext => httpContext.Request.QueryString = queryString);
        }

        public async Task<HttpContext> PostAsync(Guid organizationId, ScimGroupRequestModel model)
        {
            return await Server.PostAsync($"/v2/{organizationId}/groups", GetStringContent(model));
        }

        public async Task<HttpContext> PutAsync(Guid organizationId, Guid id, ScimGroupRequestModel model)
        {
            return await Server.PutAsync($"/v2/{organizationId}/groups/{id}", GetStringContent(model));
        }

        public async Task<HttpContext> PatchAsync(Guid organizationId, Guid id, ScimPatchModel model)
        {
            return await Server.PatchAsync($"/v2/{organizationId}/groups/{id}", GetStringContent(model));
        }

        public async Task<HttpContext> DeleteAsync(Guid organizationId, Guid id)
        {
            return await Server.DeleteAsync($"/v2/{organizationId}/groups/{id}");
        }

        private static StringContent GetStringContent(object obj) => new(JsonSerializer.Serialize(obj), Encoding.Default, MediaTypeNames.Application.Json);
    }
}
