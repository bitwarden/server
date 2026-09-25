using System.Net;
using Bit.IntegrationTestCommon;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bit.Billing.IntegrationTest;

/// <summary>
/// Guards the fix for VULN-826: the Admin MVC pipeline must register a global
/// <see cref="AutoValidateAntiforgeryTokenAttribute"/> so antiforgery protection
/// is default-on, and an unsafe request without a token must actually be rejected.
/// </summary>
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
            var client = factory.CreateClient();

            var response = await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "Email", "admin@localhost" },
            }));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            testDatabase.Dispose();
        }
    }
}
