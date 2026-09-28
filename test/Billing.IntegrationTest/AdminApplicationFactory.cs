using System.Text.RegularExpressions;
using System.Web;
using Bit.Admin.Jobs;
using Bit.Core.Services;
using Bit.IntegrationTestCommon;
using Bit.Test.Common.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Bit.Billing.IntegrationTest;

/// <summary>
/// Application factory for the Admin host. Holds an inner
/// <see cref="WebApplicationFactory{TEntryPoint}"/> privately and exposes intent
/// methods for the side-effects (passwordless sign-in tokens, business-unit
/// conversion invite tokens) that would otherwise leave the process as emails.
/// Tokens are recovered after each request by inspecting the substituted
/// <see cref="IMailService"/>'s recorded calls — no captured state on the factory.
/// </summary>
public sealed class AdminApplicationFactory : IAsyncDisposable
{
    private readonly WebApplicationFactory<Admin.Program> _factory;

    public AdminApplicationFactory(ITestDatabase testDatabase, bool disableAntiforgery = true)
    {
        _factory = new WebApplicationFactory<Admin.Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var configValues = new Dictionary<string, string?>
                {
                    // Force the EF branch of AddDatabaseRepositories so it doesn't fall through to
                    // Dapper (which would talk to the user-secret's real SqlServer instead of the
                    // SQLite-backed DbContext the ITestDatabase registers).
                    ["globalSettings:databaseProvider"] = "sqlite",
                };
                testDatabase.ModifyGlobalSettings(configValues);
                config.AddInMemoryCollection(configValues);
            });

            builder.ConfigureServices(services =>
            {
                services.AddSingleton(Substitute.For<IMailService>());

                // Remove Quartz hosted jobs to avoid concurrent startup issues
                var jobHostedServiceDescriptor = services.Single(sd => sd.ImplementationType == typeof(JobsHostedService));
                services.Remove(jobHostedServiceDescriptor);

                // Disable antiforgery by default so tests don't thread antiforgery tokens
                // through every post. Tests that assert enforcement pass disableAntiforgery: false.
                if (disableAntiforgery)
                {
                    services.PostConfigure<MvcOptions>(options =>
                    {
                        options.Filters.Add(new IgnoreAntiforgeryTokenAttribute { Order = 1001 });
                    });
                }

                testDatabase.AddDatabase(services);
            });
        });
    }

    /// <summary>
    /// The service provider of the running Admin host, for tests that inspect
    /// how the pipeline was configured (e.g. registered MVC filters).
    /// </summary>
    public IServiceProvider Services => _factory.Services;

    /// <summary>
    /// Signs into the Admin Portal using the passwordless flow and returns a
    /// client whose cookies carry an authenticated admin session.
    /// </summary>
    public async Task<HttpClient> SignInAdminAsync()
    {
        const string Email = "admin@localhost";
        var client = _factory.CreateClient();
        var mailService = _factory.Services.GetRequiredService<IMailService>();

        // GET the login page first for the antiforgery cookie and token, then include the
        // token in the POST so sign-in works whether or not antiforgery is enabled.
        var loginPage = await client.GetAsync("/login");
        await Assert.SuccessResponseAsync(loginPage);
        var antiforgeryToken = ExtractAntiforgeryToken(await loginPage.Content.ReadAsStringAsync());

        var loginForm = new Dictionary<string, string> { { "Email", Email } };
        if (!string.IsNullOrEmpty(antiforgeryToken))
        {
            loginForm.Add("__RequestVerificationToken", antiforgeryToken);
        }

        var loginResponse = await client.PostAsync("/login", new FormUrlEncodedContent(loginForm));
        await Assert.SuccessResponseAsync(loginResponse);

        var token = mailService.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IMailService.SendPasswordlessSignInAsync))
            .Select(c => (string?)c.GetArguments()[1])
            .LastOrDefault();

        if (string.IsNullOrEmpty(token))
        {
            Assert.Fail($"Admin sign-in token was not captured for {Email}.");
        }

        var confirmResponse = await client.GetAsync(
            $"/login/confirm?email={HttpUtility.UrlEncode(Email)}&token={HttpUtility.UrlEncode(token)}&returnUrl=%2F");
        await Assert.SuccessResponseAsync(confirmResponse);

        return client;
    }

    /// <summary>
    /// Posts to the Admin business-unit conversion endpoint for the given
    /// organization and returns the invitation token that would otherwise have
    /// been emailed to the provider admin.
    /// </summary>
    public async Task<string> InitializeBusinessUnitConversionAsync(
        HttpClient adminSession, Guid organizationId, string providerAdminEmail)
    {
        var mailService = _factory.Services.GetRequiredService<IMailService>();

        var response = await adminSession.PostAsync(
            $"/organizations/billing/{organizationId}/business-unit",
            new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                { "ProviderAdminEmail", providerAdminEmail },
            }));
        await Assert.SuccessResponseAsync(response);

        var token = mailService.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IMailService.SendBusinessUnitConversionInviteAsync))
            .Where(c => (string?)c.GetArguments()[2] == providerAdminEmail)
            .Select(c => (string?)c.GetArguments()[1])
            .LastOrDefault();

        if (string.IsNullOrEmpty(token))
        {
            Assert.Fail($"No business-unit conversion token captured for {providerAdminEmail}.");
        }

        return token;
    }

    /// <summary>
    /// Gets the rendered Admin page with the authenticated client and returns a
    /// valid antiforgery token. The cookie is left on the
    /// client's cookie container, so a following unsafe request will validate.
    /// </summary>
    public async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path = "/")
    {
        var page = await client.GetAsync(path);
        await Assert.SuccessResponseAsync(page);
        var token = ExtractAntiforgeryToken(await page.Content.ReadAsStringAsync());
        if (string.IsNullOrEmpty(token))
        {
            Assert.Fail($"No antiforgery token found on '{path}'.");
        }

        return token;
    }

    /// <summary>
    /// Pulls the hidden <c>__RequestVerificationToken</c> field out of a rendered
    /// Admin page so a request can carry a valid antiforgery token.
    /// </summary>
    private static string? ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            """
            name="__RequestVerificationToken"[^>]*value="(?<token>[^"]+)"
            """);
        return match.Success ? match.Groups["token"].Value : null;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}
