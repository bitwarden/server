using System.Net;
using System.Net.Http.Headers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Seeder.Recipes;
using Xunit;
using Xunit.Abstractions;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUsersControllerPerformanceTest
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly ITestOutputHelper _testOutputHelper;

    public OrganizationUsersControllerPerformanceTest(ITestOutputHelper testOutputHelper)
    {
        _factory = new ApiApplicationFactory();
        _testOutputHelper = testOutputHelper;
        _client = _factory.CreateClient();
    }

    [Theory(Skip = "Performance test")]
    [InlineData(100)]
    [InlineData(60000)]
    public async Task GetAsync(int seats)
    {
        var db = _factory.GetDatabaseContext();
        var seeder = new OrganizationWithUsersRecipe(db);

        var orgId = seeder.Seed("Org", seats, "large.test");

        var tokens = await _factory.LoginAsync("admin@large.test", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await _client.GetAsync($"/organizations/{orgId}/users?includeCollections=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(result);

        stopwatch.Stop();
        _testOutputHelper.WriteLine($"Seed: {seats}; Request duration: {stopwatch.ElapsedMilliseconds} ms");
    }
}
