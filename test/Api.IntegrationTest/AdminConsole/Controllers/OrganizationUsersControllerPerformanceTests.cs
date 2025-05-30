using System.Net;
using System.Net.Http.Headers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Seeder.Recipes;
using Xunit;
using Xunit.Abstractions;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUsersControllerPerformanceTest(ITestOutputHelper testOutputHelper)
{
    [Theory]
    //[InlineData(100)]
    //[InlineData(60000)]
    [InlineData(10000)]
    public async Task GetAsync(int seats)
    {
        await using var factory = new ApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var seeder = new OrganizationWithUsersRecipe(db);

        var orgId = seeder.Seed("Org", seats, "large.test");

        var tokens = await factory.LoginAsync("admin@large.test", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

        // Test unoptimized version
        var stopwatch1 = System.Diagnostics.Stopwatch.StartNew();

        var response1 = await client.GetAsync($"/organizations/{orgId}/users?includeCollections=true&includeGroups=true");

        stopwatch1.Stop();

        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        var result1 = await response1.Content.ReadAsStringAsync();
        Assert.NotEmpty(result1);

        // Test optimized version
        var stopwatch2 = System.Diagnostics.Stopwatch.StartNew();

        var response2 = await client.GetAsync($"/organizations/{orgId}/users?includeCollections=true&includeGroups=true&queryOptimization=true");

        stopwatch2.Stop();

        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        var result2 = await response2.Content.ReadAsStringAsync();
        Assert.NotEmpty(result2);

        // Validate that both versions return identical data
        Assert.Equal(result1, result2);

        // Output comparison
        testOutputHelper.WriteLine($"Seed: {seats}");
        testOutputHelper.WriteLine($"Unoptimized: {stopwatch1.ElapsedMilliseconds} ms");
        testOutputHelper.WriteLine($"Optimized:   {stopwatch2.ElapsedMilliseconds} ms");
        testOutputHelper.WriteLine($"Improvement: {((double)(stopwatch1.ElapsedMilliseconds - stopwatch2.ElapsedMilliseconds) / stopwatch1.ElapsedMilliseconds * 100):F1}%");
        testOutputHelper.WriteLine($"Data identical: ✅");
    }
}
