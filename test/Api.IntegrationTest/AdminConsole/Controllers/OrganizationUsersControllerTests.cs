using System.Net;
using System.Net.Http.Headers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Seeder.Recipes;
using Xunit;
using Xunit.Abstractions;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUsersControllerTest : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly ITestOutputHelper _testOutputHelper;

    public OrganizationUsersControllerTest(ApiApplicationFactory factory, ITestOutputHelper testOutputHelper)
    {
        _factory = factory;
        _testOutputHelper = testOutputHelper;
        _client = _factory.CreateClient();
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Get_SmallOrg()
    {
        var db = _factory.GetDatabaseContext();
        var seeder = new OrganizationWithUsersRecipe(db);

        var orgId = seeder.Seed("Org", 100, "large.test");

        var tokens = await _factory.LoginAsync("admin@large.test", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await _client.GetAsync($"/organizations/{orgId}/users?includeCollections=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(result);

        stopwatch.Stop();
        _testOutputHelper.WriteLine($"Request duration: {stopwatch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task Get_LargeOrg()
    {
        var db = _factory.GetDatabaseContext();
        var seeder = new OrganizationWithUsersRecipe(db);

        var orgId = seeder.Seed("Org", 60000, "large.test");

        var tokens = await _factory.LoginAsync("admin@large.test", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await _client.GetAsync($"/organizations/{orgId}/users?includeCollections=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(result);

        stopwatch.Stop();
        _testOutputHelper.WriteLine($"Request duration: {stopwatch.ElapsedMilliseconds} ms");

        // var result = await response.Content.ReadFromJsonAsync<ListResponseModel<OrganizationUserUserDetailsResponseModel>>();
        // Assert.NotNull(result?.Data);
        // Assert.Equal(600001, result.Data.Count());
    }

}
