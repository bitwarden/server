using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Bit.Api.IntegrationTest.Public.Controllers
{
    public class OrganizationControllerTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> _factory;

        public OrganizationControllerTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
            _factory.WithWebHostBuilder(b => b)
        }

        [Fact]
        public async Task Import_Sucess()
        {
            var client = _factory.CreateClient();

            var json = "{ \"groups\": [], \"members\": []}";

            var response = await client.PostAsync("public/organization/import", new StringContent(json, null, "application/json"));
            response.EnsureSuccessStatusCode();
        }
    }
}
