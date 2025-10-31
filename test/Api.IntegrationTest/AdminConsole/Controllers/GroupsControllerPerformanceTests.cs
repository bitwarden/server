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

public class GroupsControllerPerformanceTest(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task PutGroupAsync()
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);
        var collectionsSeeder = new CollectionsRecipe(db);
        var groupsSeeder = new GroupsRecipe(db);

        var domain = $"updategroup.test.{Guid.NewGuid():N}";
        var orgId = orgSeeder.Seed(name: "Org", domain: domain, users: 5);

        var orgUserIds = db.OrganizationUsers.Where(ou => ou.OrganizationId == orgId).Select(ou => ou.Id).ToList();
        var collectionIds = collectionsSeeder.AddToOrganization(orgId, 3, orgUserIds, 0);
        var groupIds = groupsSeeder.AddToOrganization(orgId, 2, orgUserIds, 0);

        var groupId = groupIds.First();

        var tokens = await factory.LoginAsync($"owner@{domain}", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

        var updateRequest = new GroupRequestModel
        {
            Name = "Updated Group Name",
            Collections = collectionIds.Select(c => new SelectionReadOnlyRequestModel { Id = c, ReadOnly = false, HidePasswords = false, Manage = false }),
            Users = new List<Guid>() // Empty users list to avoid authorization issues
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var requestContent = new StringContent(JsonSerializer.Serialize(updateRequest), Encoding.UTF8, "application/json");
        var response = await client.PutAsync($"/organizations/{orgId}/groups/{groupId}", requestContent);

        stopwatch.Stop();
        testOutputHelper.WriteLine($"PUT /organizations/{{orgid}}/groups/{{id}} - Collections: {collectionIds.Count}; Users: {updateRequest.Users.Count()}; Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(result);
    }
}

