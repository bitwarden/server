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
    [InlineData(60000)]
    public async Task GetAsync(int seats)
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);
        var collectionsSeeder = new CollectionsRecipe(db);
        var groupsSeeder = new GroupsRecipe(db);

        var orgId = orgSeeder.Seed("Org", seats, "large.test");

        var orgUserIds = db.OrganizationUsers.Select(ou => ou.Id).ToList();
        collectionsSeeder.AddToOrganization(orgId, 10, orgUserIds);
        groupsSeeder.AddToOrganization(orgId, 5, orgUserIds);

        var tokens = await factory.LoginAsync("admin@large.test", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await client.GetAsync($"/organizations/{orgId}/users?includeCollections=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(result);

        stopwatch.Stop();
        testOutputHelper.WriteLine($"GET /users - Seats: {seats}; Request duration: {stopwatch.ElapsedMilliseconds} ms");
    }

    [Theory]
    //[InlineData(100)]
    [InlineData(60000)]
    public async Task GetMiniDetailsAsync(int seats)
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);
        var collectionsSeeder = new CollectionsRecipe(db);
        var groupsSeeder = new GroupsRecipe(db);

        var orgId = orgSeeder.Seed("Org", seats, "large.test");

        var orgUserIds = db.OrganizationUsers.Select(ou => ou.Id).ToList();
        collectionsSeeder.AddToOrganization(orgId, 10, orgUserIds);
        groupsSeeder.AddToOrganization(orgId, 5, orgUserIds);

        var tokens = await factory.LoginAsync("admin@large.test", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await client.GetAsync($"/organizations/{orgId}/users/mini-details");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(result);

        stopwatch.Stop();
        testOutputHelper.WriteLine($"GET /users/mini-details - Seats: {seats}; Request duration: {stopwatch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task GetSingleUserAsync()
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);
        var groupsSeeder = new GroupsRecipe(db);

        var orgId = orgSeeder.Seed("Org", 1, "single.test");

        var orgUserId = db.OrganizationUsers.Select(ou => ou.Id).FirstOrDefault();
        groupsSeeder.AddToOrganization(orgId, 2, new List<Guid> { orgUserId });

        var tokens = await factory.LoginAsync("admin@single.test", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await client.GetAsync($"/organizations/{orgId}/users/{orgUserId}?includeGroups=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(result);

        stopwatch.Stop();
        testOutputHelper.WriteLine($"GET /users/{{id}} - Request duration: {stopwatch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task GetResetPasswordDetailsAsync()
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);

        var orgId = orgSeeder.Seed("Org", 1, "reset.test");

        var orgUserId = db.OrganizationUsers.Select(ou => ou.Id).FirstOrDefault();

        var tokens = await factory.LoginAsync("admin@reset.test", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await client.GetAsync($"/organizations/{orgId}/users/{orgUserId}/reset-password-details");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(result);

        stopwatch.Stop();
        testOutputHelper.WriteLine($"GET /users/{{id}}/reset-password-details - Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");
    }
}
