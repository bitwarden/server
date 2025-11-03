using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.Models.Request.Accounts;
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
    [Fact]
    public async Task DeleteOrganization_WithPasswordVerification()
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);

        var domain = $"{Guid.NewGuid().ToString("N").Substring(0, 8)}.com";
        var orgId = orgSeeder.Seed(name: "Org", domain: domain, users: 1);

        var tokens = await factory.LoginAsync($"owner@{domain}", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

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

        testOutputHelper.WriteLine($"DELETE /organizations/{{id}} - Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Tests POST /organizations/{id}/delete-recover-token with token verification
    /// </summary>
    [Fact]
    public async Task DeleteOrganization_WithTokenVerification()
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);

        var domain = $"{Guid.NewGuid().ToString("N").Substring(0, 8)}.com";
        var orgId = orgSeeder.Seed(name: "Org", domain: domain, users: 1);

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

        testOutputHelper.WriteLine($"POST /organizations/{{id}}/delete-recover-token - Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Tests POST /organizations/{id}/storage
    /// </summary>
    [Fact]
    public async Task AdjustStorage_IncrementByOneGb()
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);

        var domain = $"{Guid.NewGuid().ToString("N").Substring(0, 8)}.com";
        var orgId = orgSeeder.Seed(name: "Org", domain: domain, users: 1);

        var tokens = await factory.LoginAsync($"owner@{domain}", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

        var storageRequest = new StorageRequestModel
        {
            StorageGbAdjustment = 1
        };

        var requestContent = new StringContent(JsonSerializer.Serialize(storageRequest), Encoding.UTF8, "application/json");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await client.PostAsync($"/organizations/{orgId}/storage", requestContent);

        stopwatch.Stop();

        testOutputHelper.WriteLine($"POST /organizations/{{id}}/storage - Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Tests POST /organizations/create-without-payment
    /// </summary>
    [Fact]
    public async Task CreateOrganization_WithoutPayment()
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var email = $"user@{Guid.NewGuid().ToString("N").Substring(0, 8)}.com";
        var masterPasswordHash = "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=";

        await factory.LoginWithNewAccount(email, masterPasswordHash);

        var tokens = await factory.LoginAsync(email, masterPasswordHash);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

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
            PremiumAccessAddon = true,
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
