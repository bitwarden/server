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

public class OrganizationsControllerPerformanceTest(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task DeleteOrganizationAsync()
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);

        var domain = $"deleteorg.test.{Guid.NewGuid():N}";
        var orgId = orgSeeder.Seed(name: "Org", domain: domain, users: 1);

        var tokens = await factory.LoginAsync($"owner@{domain}", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

        var deleteRequest = new SecretVerificationRequestModel
        {
            MasterPasswordHash = "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs="
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/organizations/{orgId}")
        {
            Content = new StringContent(JsonSerializer.Serialize(deleteRequest), Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(request);

        stopwatch.Stop();
        testOutputHelper.WriteLine($"DELETE /organizations/{{id}} - Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteOrganizationWithTokenAsync()
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);

        var domain = $"deletetoken.test.{Guid.NewGuid():N}";
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

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var requestContent = new StringContent(JsonSerializer.Serialize(deleteRequest), Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"/organizations/{orgId}/delete-recover-token", requestContent);

        stopwatch.Stop();
        testOutputHelper.WriteLine($"POST /organizations/{{id}}/delete-recover-token - Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostStorageAsync()
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var db = factory.GetDatabaseContext();
        var orgSeeder = new OrganizationWithUsersRecipe(db);

        var domain = $"storage.test.{Guid.NewGuid():N}";
        var orgId = orgSeeder.Seed(name: "Org", domain: domain, users: 1);

        var tokens = await factory.LoginAsync($"owner@{domain}", "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

        var storageRequest = new StorageRequestModel
        {
            StorageGbAdjustment = 1
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var requestContent = new StringContent(JsonSerializer.Serialize(storageRequest), Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"/organizations/{orgId}/storage", requestContent);

        stopwatch.Stop();
        testOutputHelper.WriteLine($"POST /organizations/{{id}}/storage - Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateWithoutPaymentAsync()
    {
        await using var factory = new SqlServerApiApplicationFactory();
        var client = factory.CreateClient();

        var email = $"createorg.test.{Guid.NewGuid():N}@example.com";
        var masterPasswordHash = "c55hlJ/cfdvTd4awTXUqow6X3cOQCfGwn11o3HblnPs=";

        await factory.LoginWithNewAccount(email, masterPasswordHash);

        var tokens = await factory.LoginAsync(email, masterPasswordHash);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

        var createRequest = new OrganizationNoPaymentCreateRequest
        {
            Name = "Test Organization",
            BillingEmail = email,
            PlanType = PlanType.EnterpriseAnnually,
            Key = "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=",
            AdditionalSeats = 1,
            AdditionalStorageGb = 1,
            UseSecretsManager = true,
            AdditionalSmSeats = 1
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var requestContent = new StringContent(JsonSerializer.Serialize(createRequest), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/organizations/create-without-payment", requestContent);

        stopwatch.Stop();
        testOutputHelper.WriteLine($"POST /organizations/create-without-payment - Request duration: {stopwatch.ElapsedMilliseconds} ms; Status: {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(result);
    }
}

