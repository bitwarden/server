using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Bit.Api.IntegrationTest.Public.Controllers
{
    public class OrganizationControllerTests : IClassFixture<ApiApplicationFactory>
    {
        private readonly ApiApplicationFactory _factory;

        public OrganizationControllerTests(ApiApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Import_Sucess()
        {
            var json = "{ \"groups\": [], \"members\": []}";

            var response = await _factory.Server.SendAsync().PostAsync("public/organization/import", new StringContent(json, null, "application/json"));
            response.EnsureSuccessStatusCode();
        }
    }
}
