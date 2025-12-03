using System.Net;
using System.Text;
using System.Text.Json;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Models.Request;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Seeder.Recipes;
using Xunit;
using Xunit.Abstractions;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUsersControllerPerformanceTests(ITestOutputHelper testOutputHelper)
{
    /// <summary>
    /// Tests GET /organizations/{orgId}/users?includeCollections=true
    /// </summary>
    [Theory(Skip = "Performance test")]
    [InlineData(10)]
    //[InlineData(100)]
    //[InlineData(1000)]
    public async Task GetAllUsers_WithCollections(int seats)
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);
        var collectionsSeeder = new CollectionsRecipe(db);
        var groupsSeeder = new GroupsRecipe(db);

        var domain = OrganizationTestHelpers.GenerateRandomDomain();

        var orgId = orgSeeder.Seed(name: "Org", domain: domain, users: seats);

        var orgUserIds = db.OrganizationUsers.Select(ou => ou.Id).ToList();
        collectionsSeeder.AddToOrganization(orgId, 10, orgUserIds);
        groupsSeeder.AddToOrganization(orgId, 5, orgUserIds);

        await PerformanceTestHelpers.AuthenticateClientAsync(factory, client, $"owner@{domain}");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await client.GetAsync($"/organizations/{orgId}/users?includeCollections=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        stopwatch.Stop();
        testOutputHelper.WriteLine($"GET /users - Seats: {seats}; Request duration: {stopwatch.ElapsedMilliseconds} ms");
    }

    /// <summary>
    /// Tests GET /organizations/{orgId}/users/mini-details
    /// </summary>
    [Theory(Skip = "Performance test")]
    [InlineData(10)]
    //[InlineData(100)]
    //[InlineData(1000)]
    public async Task GetAllUsers_MiniDetails(int seats)
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);
        var collectionsSeeder = new CollectionsRecipe(db);
        var groupsSeeder = new GroupsRecipe(db);

        var domain = OrganizationTestHelpers.GenerateRandomDomain();
        var orgId = orgSeeder.Seed(name: "Org", domain: domain, users: seats);

        var orgUserIds = db.OrganizationUsers.Select(ou => ou.Id).ToList();
        collectionsSeeder.AddToOrganization(orgId, 10, orgUserIds);
        groupsSeeder.AddToOrganization(orgId, 5, orgUserIds);

        await PerformanceTestHelpers.AuthenticateClientAsync(factory, client, $"owner@{domain}");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await client.GetAsync($"/organizations/{orgId}/users/mini-details");

        stopwatch.Stop();

        testOutputHelper.WriteLine($"GET /users/mini-details - Seats: {seats}; Request duration: {stopwatch.ElapsedMilliseconds} ms");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Tests GET /organizations/{orgId}/users/{id}?includeGroups=true
    /// </summary>
    [Fact(Skip = "Performance test")]
    public async Task GetSingleUser_WithGroups()
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);
        var groupsSeeder = new GroupsRecipe(db);

        var domain = OrganizationTestHelpers.GenerateRandomDomain();
        var orgId = orgSeeder.Seed(name: "Org", domain: domain, users: 1);

        var orgUserId = db.OrganizationUsers.Select(ou => ou.Id).FirstOrDefault();
        groupsSeeder.AddToOrganization(orgId, 2, [orgUserId]);

        await PerformanceTestHelpers.AuthenticateClientAsync(factory, client, $"owner@{domain}");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await client.GetAsync($"/organizations/{orgId}/users/{orgUserId}?includeGroups=true");

        stopwatch.Stop();

        testOutputHelper.WriteLine($"GET /users/{{id}} - Request duration: {stopwatch.ElapsedMilliseconds} ms");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Tests GET /organizations/{orgId}/users/{id}/reset-password-details
    /// </summary>
    [Fact(Skip = "Performance test")]
    public async Task GetResetPasswordDetails_ForSingleUser()
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);

        var domain = OrganizationTestHelpers.GenerateRandomDomain();
        var orgId = orgSeeder.Seed(name: "Org", domain: domain, users: 1);

        var orgUserId = db.OrganizationUsers.Select(ou => ou.Id).FirstOrDefault();

        await PerformanceTestHelpers.AuthenticateClientAsync(factory, client, $"owner@{domain}");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await client.GetAsync($"/organizations/{orgId}/users/{orgUserId}/reset-password-details");

        stopwatch.Stop();

        testOutputHelper.WriteLine($"GET /users/{{id}}/reset-password-details - Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Tests POST /organizations/{orgId}/users/confirm
    /// </summary>
    [Theory(Skip = "Performance test")]
    [InlineData(10)]
    //[InlineData(100)]
    //[InlineData(1000)]
    public async Task BulkConfirmUsers(int userCount)
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);

        var domain = OrganizationTestHelpers.GenerateRandomDomain();
        var orgId = orgSeeder.Seed(
            name: "Org",
            domain: domain,
            users: userCount,
            usersStatus: OrganizationUserStatusType.Accepted);

        await PerformanceTestHelpers.AuthenticateClientAsync(factory, client, $"owner@{domain}");

        var acceptedUserIds = db.OrganizationUsers
            .Where(ou => ou.OrganizationId == orgId && ou.Status == OrganizationUserStatusType.Accepted)
            .Select(ou => ou.Id)
            .ToList();

        var confirmRequest = new OrganizationUserBulkConfirmRequestModel
        {
            Keys = acceptedUserIds.Select(id => new OrganizationUserBulkConfirmRequestModelEntry { Id = id, Key = "test-key-" + id }),
            DefaultUserCollectionName = "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk="
        };

        var requestContent = new StringContent(JsonSerializer.Serialize(confirmRequest), Encoding.UTF8, "application/json");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await client.PostAsync($"/organizations/{orgId}/users/confirm", requestContent);

        stopwatch.Stop();

        testOutputHelper.WriteLine($"POST /users/confirm - Users: {acceptedUserIds.Count}; Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.True(response.IsSuccessStatusCode);
    }

    /// <summary>
    /// Tests POST /organizations/{orgId}/users/remove
    /// </summary>
    [Theory(Skip = "Performance test")]
    [InlineData(10)]
    //[InlineData(100)]
    //[InlineData(1000)]
    public async Task BulkRemoveUsers(int userCount)
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);

        var domain = OrganizationTestHelpers.GenerateRandomDomain();
        var orgId = orgSeeder.Seed(name: "Org", domain: domain, users: userCount);

        await PerformanceTestHelpers.AuthenticateClientAsync(factory, client, $"owner@{domain}");

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

    /// <summary>
    /// Tests PUT /organizations/{orgId}/users/revoke
    /// </summary>
    [Theory(Skip = "Performance test")]
    [InlineData(10)]
    //[InlineData(100)]
    //[InlineData(1000)]
    public async Task BulkRevokeUsers(int userCount)
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);

        var domain = OrganizationTestHelpers.GenerateRandomDomain();
        var orgId = orgSeeder.Seed(
            name: "Org",
            domain: domain,
            users: userCount,
            usersStatus: OrganizationUserStatusType.Confirmed);

        await PerformanceTestHelpers.AuthenticateClientAsync(factory, client, $"owner@{domain}");

        var usersToRevoke = db.OrganizationUsers
            .Where(ou => ou.OrganizationId == orgId && ou.Type == OrganizationUserType.User)
            .Select(ou => ou.Id)
            .ToList();

        var revokeRequest = new OrganizationUserBulkRequestModel { Ids = usersToRevoke };

        var requestContent = new StringContent(JsonSerializer.Serialize(revokeRequest), Encoding.UTF8, "application/json");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await client.PutAsync($"/organizations/{orgId}/users/revoke", requestContent);

        stopwatch.Stop();

        testOutputHelper.WriteLine($"PUT /users/revoke - Users: {usersToRevoke.Count}; Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.True(response.IsSuccessStatusCode);
    }

    /// <summary>
    /// Tests PUT /organizations/{orgId}/users/restore
    /// </summary>
    [Theory(Skip = "Performance test")]
    [InlineData(10)]
    //[InlineData(100)]
    //[InlineData(1000)]
    public async Task BulkRestoreUsers(int userCount)
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);

        var domain = OrganizationTestHelpers.GenerateRandomDomain();
        var orgId = orgSeeder.Seed(
            name: "Org",
            domain: domain,
            users: userCount,
            usersStatus: OrganizationUserStatusType.Revoked);

        await PerformanceTestHelpers.AuthenticateClientAsync(factory, client, $"owner@{domain}");

        var usersToRestore = db.OrganizationUsers
            .Where(ou => ou.OrganizationId == orgId && ou.Type == OrganizationUserType.User)
            .Select(ou => ou.Id)
            .ToList();

        var restoreRequest = new OrganizationUserBulkRequestModel { Ids = usersToRestore };

        var requestContent = new StringContent(JsonSerializer.Serialize(restoreRequest), Encoding.UTF8, "application/json");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await client.PutAsync($"/organizations/{orgId}/users/restore", requestContent);

        stopwatch.Stop();

        testOutputHelper.WriteLine($"PUT /users/restore - Users: {usersToRestore.Count}; Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.True(response.IsSuccessStatusCode);
    }

    /// <summary>
    /// Tests POST /organizations/{orgId}/users/delete-account
    /// </summary>
    [Theory(Skip = "Performance test")]
    [InlineData(10)]
    //[InlineData(100)]
    //[InlineData(1000)]
    public async Task BulkDeleteAccounts(int userCount)
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);
        var domainSeeder = new OrganizationDomainRecipe(db);

        var domain = OrganizationTestHelpers.GenerateRandomDomain();

        var orgId = orgSeeder.Seed(
            name: "Org",
            domain: domain,
            users: userCount,
            usersStatus: OrganizationUserStatusType.Confirmed);

        domainSeeder.AddVerifiedDomainToOrganization(orgId, domain);

        await PerformanceTestHelpers.AuthenticateClientAsync(factory, client, $"owner@{domain}");

        var usersToDelete = db.OrganizationUsers
            .Where(ou => ou.OrganizationId == orgId && ou.Type == OrganizationUserType.User)
            .Select(ou => ou.Id)
            .ToList();

        var deleteRequest = new OrganizationUserBulkRequestModel { Ids = usersToDelete };

        var requestContent = new StringContent(JsonSerializer.Serialize(deleteRequest), Encoding.UTF8, "application/json");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await client.PostAsync($"/organizations/{orgId}/users/delete-account", requestContent);

        stopwatch.Stop();

        testOutputHelper.WriteLine($"POST /users/delete-account - Users: {usersToDelete.Count}; Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.True(response.IsSuccessStatusCode);
    }

    /// <summary>
    /// Tests PUT /organizations/{orgId}/users/{id}
    /// </summary>
    [Fact(Skip = "Performance test")]
    public async Task UpdateSingleUser_WithCollectionsAndGroups()
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);
        var collectionsSeeder = new CollectionsRecipe(db);
        var groupsSeeder = new GroupsRecipe(db);

        var domain = OrganizationTestHelpers.GenerateRandomDomain();
        var orgId = orgSeeder.Seed(name: "Org", domain: domain, users: 1);

        var orgUserIds = db.OrganizationUsers.Where(ou => ou.OrganizationId == orgId).Select(ou => ou.Id).ToList();
        var collectionIds = collectionsSeeder.AddToOrganization(orgId, 3, orgUserIds, 0);
        var groupIds = groupsSeeder.AddToOrganization(orgId, 2, orgUserIds, 0);

        await PerformanceTestHelpers.AuthenticateClientAsync(factory, client, $"owner@{domain}");

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

    /// <summary>
    /// Tests PUT /organizations/{orgId}/users/enable-secrets-manager
    /// </summary>
    [Theory(Skip = "Performance test")]
    [InlineData(10)]
    //[InlineData(100)]
    //[InlineData(1000)]
    public async Task BulkEnableSecretsManager(int userCount)
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);

        var domain = OrganizationTestHelpers.GenerateRandomDomain();
        var orgId = orgSeeder.Seed(name: "Org", domain: domain, users: userCount);

        await PerformanceTestHelpers.AuthenticateClientAsync(factory, client, $"owner@{domain}");

        var usersToEnable = db.OrganizationUsers
            .Where(ou => ou.OrganizationId == orgId && ou.Type == OrganizationUserType.User)
            .Select(ou => ou.Id)
            .ToList();

        var enableRequest = new OrganizationUserBulkRequestModel { Ids = usersToEnable };

        var requestContent = new StringContent(JsonSerializer.Serialize(enableRequest), Encoding.UTF8, "application/json");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await client.PutAsync($"/organizations/{orgId}/users/enable-secrets-manager", requestContent);

        stopwatch.Stop();

        testOutputHelper.WriteLine($"PUT /users/enable-secrets-manager - Users: {usersToEnable.Count}; Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.True(response.IsSuccessStatusCode);
    }

    /// <summary>
    /// Tests DELETE /organizations/{orgId}/users/{id}/delete-account
    /// </summary>
    [Fact(Skip = "Performance test")]
    public async Task DeleteSingleUserAccount_FromVerifiedDomain()
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);
        var domainSeeder = new OrganizationDomainRecipe(db);

        var domain = OrganizationTestHelpers.GenerateRandomDomain();
        var orgId = orgSeeder.Seed(
            name: "Org",
            domain: domain,
            users: 2,
            usersStatus: OrganizationUserStatusType.Confirmed);

        domainSeeder.AddVerifiedDomainToOrganization(orgId, domain);

        await PerformanceTestHelpers.AuthenticateClientAsync(factory, client, $"owner@{domain}");

        var userToDelete = db.OrganizationUsers
            .FirstOrDefault(ou => ou.OrganizationId == orgId && ou.Type == OrganizationUserType.User);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await client.DeleteAsync($"/organizations/{orgId}/users/{userToDelete.Id}/delete-account");

        stopwatch.Stop();

        testOutputHelper.WriteLine($"DELETE /users/{{id}}/delete-account - Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Tests POST /organizations/{orgId}/users/invite
    /// </summary>
    [Theory(Skip = "Performance test")]
    [InlineData(1)]
    //[InlineData(5)]
    //[InlineData(20)]
    public async Task InviteUsers(int emailCount)
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);
        var collectionsSeeder = new CollectionsRecipe(db);

        var domain = OrganizationTestHelpers.GenerateRandomDomain();
        var orgId = orgSeeder.Seed(name: "Org", domain: domain, users: 1);

        var orgUserIds = db.OrganizationUsers.Where(ou => ou.OrganizationId == orgId).Select(ou => ou.Id).ToList();
        var collectionIds = collectionsSeeder.AddToOrganization(orgId, 2, orgUserIds, 0);

        await PerformanceTestHelpers.AuthenticateClientAsync(factory, client, $"owner@{domain}");

        var emails = Enumerable.Range(0, emailCount).Select(i => $"{i:D4}@{domain}").ToArray();
        var inviteRequest = new OrganizationUserInviteRequestModel
        {
            Emails = emails,
            Type = OrganizationUserType.User,
            AccessSecretsManager = false,
            Collections = Array.Empty<SelectionReadOnlyRequestModel>(),
            Groups = Array.Empty<Guid>(),
            Permissions = null
        };

        var requestContent = new StringContent(JsonSerializer.Serialize(inviteRequest), Encoding.UTF8, "application/json");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await client.PostAsync($"/organizations/{orgId}/users/invite", requestContent);

        stopwatch.Stop();

        testOutputHelper.WriteLine($"POST /users/invite - Emails: {emails.Length}; Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Tests POST /organizations/{orgId}/users/reinvite
    /// </summary>
    [Theory(Skip = "Performance test")]
    [InlineData(10)]
    //[InlineData(100)]
    //[InlineData(1000)]
    public async Task BulkReinviteUsers(int userCount)
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);

        var domain = OrganizationTestHelpers.GenerateRandomDomain();
        var orgId = orgSeeder.Seed(
            name: "Org",
            domain: domain,
            users: userCount,
            usersStatus: OrganizationUserStatusType.Invited);

        await PerformanceTestHelpers.AuthenticateClientAsync(factory, client, $"owner@{domain}");

        var usersToReinvite = db.OrganizationUsers
            .Where(ou => ou.OrganizationId == orgId && ou.Status == OrganizationUserStatusType.Invited)
            .Select(ou => ou.Id)
            .ToList();

        var reinviteRequest = new OrganizationUserBulkRequestModel { Ids = usersToReinvite };

        var requestContent = new StringContent(JsonSerializer.Serialize(reinviteRequest), Encoding.UTF8, "application/json");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await client.PostAsync($"/organizations/{orgId}/users/reinvite", requestContent);

        stopwatch.Stop();

        testOutputHelper.WriteLine($"POST /users/reinvite - Users: {usersToReinvite.Count}; Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.True(response.IsSuccessStatusCode);
    }
}
