using System.Net;
using System.Net.Http.Headers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Seeder.Recipes;
using Xunit;
using Xunit.Abstractions;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUsersControllerPerformanceTest(ITestOutputHelper testOutputHelper)
{
    [Theory(Skip = "Performance test")]
    [InlineData(100)]
    [InlineData(60000)]
    public async Task GetAsync(int seats)
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var seeder = new OrganizationWithUsersRecipe(db);

        var orgId = seeder.Seed("Org", seats, "large.test");

        var tokens = await factory.LoginAsync("admin@large.test", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await client.GetAsync($"/organizations/{orgId}/users?includeCollections=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(result);

        stopwatch.Stop();
        testOutputHelper.WriteLine($"Seed: {seats}; Request duration: {stopwatch.ElapsedMilliseconds} ms");
    }
}
