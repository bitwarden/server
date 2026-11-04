using System.Net;
using Bit.IntegrationTestCommon;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bit.Billing.IntegrationTest;

public class AdminAntiforgeryConfigurationTests
{
    [Fact]
    public async Task AdminPipeline_RegistersGlobalAntiforgeryFilter()
    {
        ITestDatabase testDatabase = new SqliteTestDatabase();
        try
        {
            await using var factory = new AdminApplicationFactory(testDatabase);

            var mvcOptions = factory.Services.GetRequiredService<IOptions<MvcOptions>>().Value;

            Assert.Contains(mvcOptions.Filters, filter => filter is AutoValidateAntiforgeryTokenAttribute);
        }
        finally
        {
            testDatabase.Dispose();
        }
    }

    [Fact]
    public async Task AdminPipeline_RejectsUnsafePostWithoutAntiforgeryToken()
    {
        ITestDatabase testDatabase = new SqliteTestDatabase();
        try
        {
            await using var factory = new AdminApplicationFactory(testDatabase, disableAntiforgery: false);
            var client = await factory.SignInAdminAsync();

            // TriggerBillingSync has no [ValidateAntiForgeryToken] of its own, so only the global
            // filter protects it. A tokenless POST should be rejected with 400 before the action runs.
            var response = await client.PostAsync(
                $"/organizations/triggerbillingsync/{Guid.NewGuid()}",
                new FormUrlEncodedContent(new Dictionary<string, string>()));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            testDatabase.Dispose();
        }
    }

    [Fact]
    public async Task AdminPipeline_AcceptsUnsafePostWithValidAntiforgeryToken()
    {
        ITestDatabase testDatabase = new SqliteTestDatabase();
        try
        {
            await using var factory = new AdminApplicationFactory(testDatabase, disableAntiforgery: false);
            var client = await factory.SignInAdminAsync();

            // Same endpoint, now with a valid token from an authenticated page. An unknown
            // org id just redirects, so any non-400 response proves the token was accepted.
            var token = await factory.GetAntiforgeryTokenAsync(client);
            var response = await client.PostAsync(
                $"/organizations/triggerbillingsync/{Guid.NewGuid()}",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "__RequestVerificationToken", token },
                }));

            Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            testDatabase.Dispose();
        }
    }
}
