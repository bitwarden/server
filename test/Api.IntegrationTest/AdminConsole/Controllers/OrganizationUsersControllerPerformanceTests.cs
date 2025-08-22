using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.Models.Request;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Seeder.Recipes;
using Xunit;
using Xunit.Abstractions;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUsersControllerPerformanceTest(ITestOutputHelper testOutputHelper)
{
    [Theory]
    [InlineData(100)]
    //[InlineData(60000)]
    public async Task GetAsync(int seats)
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);
        var collectionsSeeder = new CollectionsRecipe(db);
        var groupsSeeder = new GroupsRecipe(db);

        var domain = $"large.test.{Guid.NewGuid():N}";

        var orgId = orgSeeder.Seed(name: "Org", domain: domain, users: seats);

        var orgUserIds = db.OrganizationUsers.Select(ou => ou.Id).ToList();
        collectionsSeeder.AddToOrganization(orgId, 10, orgUserIds);
        groupsSeeder.AddToOrganization(orgId, 5, orgUserIds);

        var tokens = await factory.LoginAsync($"owner@{domain}", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
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
    [InlineData(100)]
    //[InlineData(60000)]
    public async Task GetMiniDetailsAsync(int seats)
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);
        var collectionsSeeder = new CollectionsRecipe(db);
        var groupsSeeder = new GroupsRecipe(db);

        var domain = $"large.test.{Guid.NewGuid():N}";
        var orgId = orgSeeder.Seed(name: "Org", domain: domain, users: seats);

        var orgUserIds = db.OrganizationUsers.Select(ou => ou.Id).ToList();
        collectionsSeeder.AddToOrganization(orgId, 10, orgUserIds);
        groupsSeeder.AddToOrganization(orgId, 5, orgUserIds);

        var tokens = await factory.LoginAsync($"owner@{domain}", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
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

        var domain = $"single.test.{Guid.NewGuid():N}";
        var orgId = orgSeeder.Seed(name: "Org", domain: domain, users: 1);

        var orgUserId = db.OrganizationUsers.Select(ou => ou.Id).FirstOrDefault();
        groupsSeeder.AddToOrganization(orgId, 2, [orgUserId]);

        var tokens = await factory.LoginAsync($"owner@{domain}", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
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

        var domain = $"reset.test.{Guid.NewGuid():N}";
        var orgId = orgSeeder.Seed(name: "Org", domain: domain, users: 1);

        var orgUserId = db.OrganizationUsers.Select(ou => ou.Id).FirstOrDefault();

        var tokens = await factory.LoginAsync($"owner@{domain}", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await client.GetAsync($"/organizations/{orgId}/users/{orgUserId}/reset-password-details");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(result);

        stopwatch.Stop();
        testOutputHelper.WriteLine($"GET /users/{{id}}/reset-password-details - Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");
    }

    [Theory]
    [InlineData(100)]
    //[InlineData(1000)]
    public async Task BulkConfirmAsync(int userCount)
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);

        var domain = $"bulkconfirm.test.{Guid.NewGuid():N}";
        var orgId = orgSeeder.Seed(
            name: "Org",
            domain: domain,
            users: userCount,
            usersStatus: OrganizationUserStatusType.Accepted);

        var tokens = await factory.LoginAsync($"owner@{domain}", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

        var acceptedUserIds = db.OrganizationUsers
            .Where(ou => ou.OrganizationId == orgId && ou.Status == OrganizationUserStatusType.Accepted)
            .Select(ou => ou.Id)
            .ToList();

        var confirmRequest = new OrganizationUserBulkConfirmRequestModel
        {
            Keys = acceptedUserIds.Select(id => new OrganizationUserBulkConfirmRequestModelEntry { Id = id, Key = "test-key-" + id }),
            DefaultUserCollectionName = "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk="
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var requestContent = new StringContent(JsonSerializer.Serialize(confirmRequest), Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"/organizations/{orgId}/users/confirm", requestContent);

        stopwatch.Stop();
        testOutputHelper.WriteLine($"POST /users/confirm - Users: {acceptedUserIds.Count}; Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Theory]
    [InlineData(100)]
    //[InlineData(1000)]
    public async Task BulkRemoveAsync(int userCount)
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);

        var domain = $"bulkremove.test.{Guid.NewGuid():N}";
        var orgId = orgSeeder.Seed(name: "Org", domain: domain, users: userCount);

        var tokens = await factory.LoginAsync($"owner@{domain}", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

        var usersToRemove = db.OrganizationUsers
            .Where(ou => ou.OrganizationId == orgId && ou.Type == OrganizationUserType.User)
            .Select(ou => ou.Id)
            .ToList();

        var removeRequest = new OrganizationUserBulkRequestModel { Ids = usersToRemove };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var requestContent = new StringContent(JsonSerializer.Serialize(removeRequest), Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"/organizations/{orgId}/users/remove", requestContent);

        stopwatch.Stop();
        testOutputHelper.WriteLine($"POST /users/remove - Users: {usersToRemove.Count}; Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Theory]
    [InlineData(100)]
    //[InlineData(1000)]
    public async Task BulkRevokeAsync(int userCount)
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);

        var domain = $"bulkrevoke.test.{Guid.NewGuid():N}";
        var orgId = orgSeeder.Seed(
            name: "Org",
            domain: domain,
            users: userCount,
            usersStatus: OrganizationUserStatusType.Confirmed);

        var tokens = await factory.LoginAsync($"owner@{domain}", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

        var usersToRevoke = db.OrganizationUsers
            .Where(ou => ou.OrganizationId == orgId && ou.Type == OrganizationUserType.User)
            .Select(ou => ou.Id)
            .ToList();

        var revokeRequest = new OrganizationUserBulkRequestModel { Ids = usersToRevoke };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var requestContent = new StringContent(JsonSerializer.Serialize(revokeRequest), Encoding.UTF8, "application/json");
        var response = await client.PutAsync($"/organizations/{orgId}/users/revoke", requestContent);

        stopwatch.Stop();
        testOutputHelper.WriteLine($"PUT /users/revoke - Users: {usersToRevoke.Count}; Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Theory]
    [InlineData(100)]
    //[InlineData(1000)]
    public async Task BulkRestoreAsync(int userCount)
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);

        var domain = $"bulkrestore.test.{Guid.NewGuid():N}";
        var orgId = orgSeeder.Seed(
            name: "Org",
            domain: domain,
            users: userCount,
            usersStatus: OrganizationUserStatusType.Revoked);

        var tokens = await factory.LoginAsync($"owner@{domain}", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

        var usersToRestore = db.OrganizationUsers
            .Where(ou => ou.OrganizationId == orgId && ou.Type == OrganizationUserType.User)
            .Select(ou => ou.Id)
            .ToList();

        var restoreRequest = new OrganizationUserBulkRequestModel { Ids = usersToRestore };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var requestContent = new StringContent(JsonSerializer.Serialize(restoreRequest), Encoding.UTF8, "application/json");
        var response = await client.PutAsync($"/organizations/{orgId}/users/restore", requestContent);

        stopwatch.Stop();
        testOutputHelper.WriteLine($"PUT /users/restore - Users: {usersToRestore.Count}; Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Theory]
    [InlineData(100)]
    //[InlineData(1000)]
    public async Task BulkDeleteAccountAsync(int userCount)
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);
        var domainSeeder = new OrganizationDomainRecipe(db);

        var domain = $"bulkdeleteaccount.test.{Guid.NewGuid():N}";

        var orgId = orgSeeder.Seed(
            name: "Org",
            domain: domain,
            users: userCount,
            usersStatus: OrganizationUserStatusType.Confirmed);

        domainSeeder.AddVerifiedDomainToOrganization(orgId, domain);

        var tokens = await factory.LoginAsync($"owner@{domain}", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

        var usersToDelete = db.OrganizationUsers
            .Where(ou => ou.OrganizationId == orgId && ou.Type == OrganizationUserType.User)
            .Select(ou => ou.Id)
            .ToList();

        var deleteRequest = new OrganizationUserBulkRequestModel { Ids = usersToDelete };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var requestContent = new StringContent(JsonSerializer.Serialize(deleteRequest), Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"/organizations/{orgId}/users/delete-account", requestContent);

        stopwatch.Stop();
        testOutputHelper.WriteLine($"POST /users/delete-account - Users: {usersToDelete.Count}; Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task UpdateUserAsync()
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);
        var collectionsSeeder = new CollectionsRecipe(db);
        var groupsSeeder = new GroupsRecipe(db);

        var domain = $"updateuser.test.{Guid.NewGuid():N}";
        var orgId = orgSeeder.Seed(name: "Org", domain: domain, users: 1);

        var orgUserIds = db.OrganizationUsers.Where(ou => ou.OrganizationId == orgId).Select(ou => ou.Id).ToList();
        var collectionIds = collectionsSeeder.AddToOrganization(orgId, 3, orgUserIds, 0);
        var groupIds = groupsSeeder.AddToOrganization(orgId, 2, orgUserIds, 0);

        var tokens = await factory.LoginAsync($"owner@{domain}", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

        var userToUpdate = db.OrganizationUsers
            .FirstOrDefault(ou => ou.OrganizationId == orgId && ou.Type == OrganizationUserType.User);

        var updateRequest = new OrganizationUserUpdateRequestModel
        {
            Type = OrganizationUserType.Custom,
            Collections = collectionIds.Select(c => new SelectionReadOnlyRequestModel { Id = c, ReadOnly = false, HidePasswords = false, Manage = false }),
            Groups = groupIds,
            AccessSecretsManager = false,
            Permissions = new Permissions { AccessEventLogs = true }
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await client.PutAsync($"/organizations/{orgId}/users/{userToUpdate.Id}",
            new StringContent(JsonSerializer.Serialize(updateRequest), Encoding.UTF8, "application/json"));

        stopwatch.Stop();
        testOutputHelper.WriteLine($"PUT /users/{{id}} - Collections: {collectionIds.Count}; Groups: {groupIds.Count}; Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Theory]
    [InlineData(100)]
    //[InlineData(1000)]
    public async Task BulkEnableSecretsManagerAsync(int userCount)
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);

        var domain = $"bulksm.test.{Guid.NewGuid():N}";
        var orgId = orgSeeder.Seed(name: "Org", domain: domain, users: userCount);

        var tokens = await factory.LoginAsync($"owner@{domain}", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

        var usersToEnable = db.OrganizationUsers
            .Where(ou => ou.OrganizationId == orgId && ou.Type == OrganizationUserType.User)
            .Select(ou => ou.Id)
            .ToList();

        var enableRequest = new OrganizationUserBulkRequestModel { Ids = usersToEnable };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var requestContent = new StringContent(JsonSerializer.Serialize(enableRequest), Encoding.UTF8, "application/json");
        var response = await client.PutAsync($"/organizations/{orgId}/users/enable-secrets-manager", requestContent);

        stopwatch.Stop();
        testOutputHelper.WriteLine($"PUT /users/enable-secrets-manager - Users: {usersToEnable.Count}; Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.True(response.IsSuccessStatusCode);
    }

}
