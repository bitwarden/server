using System.Net;
using System.Text;
using System.Text.Json;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Models.Business.Tokenables;
using Bit.Core.Billing.Enums;
using Bit.Core.Tokens;
using Bit.Seeder.Recipes;
using Xunit;
using Xunit.Abstractions;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationsControllerPerformanceTests(ITestOutputHelper testOutputHelper)
{
    /// <summary>
    /// Tests DELETE /organizations/{id} with password verification
    /// </summary>
    [Theory(Skip = "Performance test")]
    [InlineData(10, 5, 3)]
    //[InlineData(100, 20, 10)]
    //[InlineData(1000, 50, 25)]
    public async Task DeleteOrganization_WithPasswordVerification(int userCount, int collectionCount, int groupCount)
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);
        var collectionsSeeder = new CollectionsRecipe(db);
        var groupsSeeder = new GroupsRecipe(db);

        var domain = OrganizationTestHelpers.GenerateRandomDomain();
        var orgId = orgSeeder.Seed(name: "Org", domain: domain, users: userCount);

        var orgUserIds = db.OrganizationUsers.Where(ou => ou.OrganizationId == orgId).Select(ou => ou.Id).ToList();
        collectionsSeeder.AddToOrganization(orgId, collectionCount, orgUserIds, 0);
        groupsSeeder.AddToOrganization(orgId, groupCount, orgUserIds, 0);

        await PerformanceTestHelpers.AuthenticateClientAsync(factory, client, $"owner@{domain}");

        var deleteRequest = new SecretVerificationRequestModel
        {
            MasterPasswordHash = "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs="
        };

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/organizations/{orgId}")
        {
            Content = new StringContent(JsonSerializer.Serialize(deleteRequest), Encoding.UTF8, "application/json")
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();


        var response = await client.SendAsync(request);

        stopwatch.Stop();

        testOutputHelper.WriteLine($"DELETE /organizations/{{id}} - Users: {userCount}; Collections: {collectionCount}; Groups: {groupCount}; Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Tests POST /organizations/{id}/delete-recover-token with token verification
    /// </summary>
    [Theory(Skip = "Performance test")]
    [InlineData(10, 5, 3)]
    //[InlineData(100, 20, 10)]
    //[InlineData(1000, 50, 25)]
    public async Task DeleteOrganization_WithTokenVerification(int userCount, int collectionCount, int groupCount)
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);
        var collectionsSeeder = new CollectionsRecipe(db);
        var groupsSeeder = new GroupsRecipe(db);

        var domain = OrganizationTestHelpers.GenerateRandomDomain();
        var orgId = orgSeeder.Seed(name: "Org", domain: domain, users: userCount);

        var orgUserIds = db.OrganizationUsers.Where(ou => ou.OrganizationId == orgId).Select(ou => ou.Id).ToList();
        collectionsSeeder.AddToOrganization(orgId, collectionCount, orgUserIds, 0);
        groupsSeeder.AddToOrganization(orgId, groupCount, orgUserIds, 0);

        await PerformanceTestHelpers.AuthenticateClientAsync(factory, client, $"owner@{domain}");

        var organization = db.Organizations.FirstOrDefault(o => o.Id == orgId);
        Assert.NotNull(organization);

        var tokenFactory = factory.GetService<IDataProtectorTokenFactory<OrgDeleteTokenable>>();
        var tokenable = new OrgDeleteTokenable(organization, 24);
        var token = tokenFactory.Protect(tokenable);

        var deleteRequest = new OrganizationVerifyDeleteRecoverRequestModel
        {
            Token = token
        };

        var requestContent = new StringContent(JsonSerializer.Serialize(deleteRequest), Encoding.UTF8, "application/json");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await client.PostAsync($"/organizations/{orgId}/delete-recover-token", requestContent);

        stopwatch.Stop();

        testOutputHelper.WriteLine($"POST /organizations/{{id}}/delete-recover-token - Users: {userCount}; Collections: {collectionCount}; Groups: {groupCount}; Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Tests POST /organizations/create-without-payment
    /// </summary>
    [Fact(Skip = "Performance test")]
    public async Task CreateOrganization_WithoutPayment()
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var email = $"user@{OrganizationTestHelpers.GenerateRandomDomain()}";
        var masterPasswordHash = "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=";

        await factory.LoginWithNewAccount(email, masterPasswordHash);

        await PerformanceTestHelpers.AuthenticateClientAsync(factory, client, email, masterPasswordHash);

        var createRequest = new OrganizationNoPaymentCreateRequest
        {
            Name = "Test Organization",
            BusinessName = "Test Business Name",
            BillingEmail = email,
            PlanType = PlanType.EnterpriseAnnually,
            Key = "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=",
            AdditionalSeats = 1,
            AdditionalStorageGb = 1,
            UseSecretsManager = true,
            AdditionalSmSeats = 1,
            AdditionalServiceAccounts = 2,
            MaxAutoscaleSeats = 100,
            PremiumAccessAddon = false,
            CollectionName = "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk="
        };

        var requestContent = new StringContent(JsonSerializer.Serialize(createRequest), Encoding.UTF8, "application/json");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await client.PostAsync("/organizations/create-without-payment", requestContent);

        stopwatch.Stop();

        testOutputHelper.WriteLine($"POST /organizations/create-without-payment - AdditionalSeats: {createRequest.AdditionalSeats}; AdditionalStorageGb: {createRequest.AdditionalStorageGb}; AdditionalSmSeats: {createRequest.AdditionalSmSeats}; AdditionalServiceAccounts: {createRequest.AdditionalServiceAccounts}; MaxAutoscaleSeats: {createRequest.MaxAutoscaleSeats}; Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
