using Bit.Api.IntegrationTest.Factories;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole;

public class IntegrationTestBase(ApiApplicationFactory apiApplicationFactory)
    : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    protected ApiApplicationFactory Factory;
    protected HttpClient Client;

    public virtual Task InitializeAsync()
    {
        Factory = apiApplicationFactory;
        Client = Factory.CreateClient();

        return Task.CompletedTask;
    }

    public virtual Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }

}
