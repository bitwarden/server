using System.Net;
using System.Net.Http.Headers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Core;
using Bit.Core.Services;
using Bit.Seeder.Recipes;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUsersControllerPerformanceTest(ITestOutputHelper testOutputHelper)
{
    [Theory]
    //[InlineData(100)]
    //[InlineData(30000)]
    [InlineData(60000)]
    public async Task GetAsync(int seats)
    {
        await using var factory = new SqlServerApiApplicationFactory();

        var optimizationEnabled = false;

        // Mock feature service with changeable state
        factory.SubstituteService<IFeatureService>(featureService =>
        {
            featureService.IsEnabled(FeatureFlagKeys.MembersGetEndpointOptimization)
                .Returns(_ => optimizationEnabled);
            // Enable other flags
            featureService.IsEnabled(Arg.Is<string>(key => key != FeatureFlagKeys.MembersGetEndpointOptimization))
                .Returns(true);
        });

        var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5);

        var db = factory.GetDatabaseContext();
        var seeder = new OrganizationWithUsersRecipe(db);
        var orgId = seeder.Seed("Org", seats, "large.test");

        var tokens = await factory.LoginAsync("admin@large.test", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

        // Test unoptimized version
        optimizationEnabled = false;
        var stopwatch1 = System.Diagnostics.Stopwatch.StartNew();
        var response1 = await client.GetAsync($"/organizations/{orgId}/users?includeCollections=true&includeGroups=true");
        stopwatch1.Stop();

        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        var result1 = await response1.Content.ReadAsStringAsync();
        Assert.NotEmpty(result1);

        // Test optimized version
        optimizationEnabled = true;
        var stopwatch2 = System.Diagnostics.Stopwatch.StartNew();
        var response2 = await client.GetAsync($"/organizations/{orgId}/users?includeCollections=true&includeGroups=true");
        stopwatch2.Stop();

        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        var result2 = await response2.Content.ReadAsStringAsync();
        Assert.NotEmpty(result2);

        // Output comparison
        testOutputHelper.WriteLine($"Seed: {seats}");
        testOutputHelper.WriteLine($"Unoptimized: {stopwatch1.ElapsedMilliseconds} ms");
        testOutputHelper.WriteLine($"Optimized:   {stopwatch2.ElapsedMilliseconds} ms");
        testOutputHelper.WriteLine($"Improvement: {(double)(stopwatch1.ElapsedMilliseconds - stopwatch2.ElapsedMilliseconds) / stopwatch1.ElapsedMilliseconds * 100:F1}%");
    }
}
