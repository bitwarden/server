using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Bit.Test.Common.ApplicationFactories;
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
            // TODO: Add stuff
            await Task.Delay(1);
        }
    }
}
