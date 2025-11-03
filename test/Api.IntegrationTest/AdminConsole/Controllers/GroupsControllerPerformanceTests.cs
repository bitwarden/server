using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.Models.Request;
using Bit.Seeder.Recipes;
using Xunit;
using Xunit.Abstractions;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class GroupsControllerPerformanceTests(ITestOutputHelper testOutputHelper)
{
    [Theory(Skip = "Performance test")]
    [InlineData(10, 5)]
    //[InlineData(100, 10)]
    //[InlineData(1000, 20)]
    public async Task PutGroupAsync(int userCount, int collectionCount)
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);
        var collectionsSeeder = new CollectionsRecipe(db);
        var groupsSeeder = new GroupsRecipe(db);

        var domain = $"{Guid.NewGuid().ToString("N").Substring(0, 8)}.com";
        var orgId = orgSeeder.Seed(name: "Org", domain: domain, users: userCount);

        var orgUserIds = db.OrganizationUsers.Where(ou => ou.OrganizationId == orgId).Select(ou => ou.Id).ToList();
        var collectionIds = collectionsSeeder.AddToOrganization(orgId, collectionCount, orgUserIds, 0);
        var groupIds = groupsSeeder.AddToOrganization(orgId, 1, orgUserIds, 0);

        var groupId = groupIds.First();

        var tokens = await factory.LoginAsync($"owner@{domain}", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

        var updateRequest = new GroupRequestModel
        {
            Name = "Updated Group Name",
            Collections = collectionIds.Select(c => new SelectionReadOnlyRequestModel { Id = c, ReadOnly = false, HidePasswords = false, Manage = false }),
            Users = orgUserIds
        };

        var requestContent = new StringContent(JsonSerializer.Serialize(updateRequest), Encoding.UTF8, "application/json");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await client.PutAsync($"/organizations/{orgId}/groups/{groupId}", requestContent);

        stopwatch.Stop();

        testOutputHelper.WriteLine($"PUT /organizations/{{orgId}}/groups/{{id}} - Users: {orgUserIds.Count}; Collections: {collectionIds.Count}; Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
