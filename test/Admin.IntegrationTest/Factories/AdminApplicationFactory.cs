using System.Text.RegularExpressions;
using System.Web;
using Bit.Admin.Jobs;
using Bit.Core.Services;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Test.Common.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Xunit;

namespace Bit.Admin.IntegrationTest.Factories;

/// <summary>
/// Runs the Admin host in process over an in-memory SQLite database that this factory owns.
/// Tests use the HTTP client accessor and the intent methods.
/// </summary>
public sealed class AdminApplicationFactory : IAsyncDisposable
{
    private const string AdminEmail = "admin@localhost";

    private readonly SqliteConnection _connection;
    private readonly WebApplicationFactory<Program> _factory;

    public AdminApplicationFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // The provider selects the EF Core setup. The connection string is never used,
                    // because ConfigureServices replaces the DbContext registration.
                    ["globalSettings:databaseProvider"] = "sqlite",
                    ["globalSettings:sqlite:connectionString"] = "Data Source=ignored.db",
                    ["globalSettings:distributedCache:redis:connectionString"] = "",

                    // The Admin portal reads its admins from configuration. Without an entry,
                    // the login page sends no passwordless email and sign-in cannot complete.
                    ["adminSettings:admins"] = AdminEmail,
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<DatabaseContext>>();
                services.AddDbContext<DatabaseContext>(options => options.UseSqlite(_connection));

                // The passwordless sign-in token reaches the admin only by email, so the
                // substitute records the send for SignInAdminAsync to read.
                services.AddSingleton(Substitute.For<IMailService>());

                // The scheduled jobs are not under test. Removing them keeps them from running during a test.
                var jobsHostedService = services.Single(sd => sd.ImplementationType == typeof(JobsHostedService));
                services.Remove(jobsHostedService);
            });
        });

        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<DatabaseContext>().Database.EnsureCreated();
    }

    public HttpClient CreateClient() => _factory.CreateClient();

    /// <summary>
    /// Signs in to the Admin portal with the passwordless flow and returns a client whose
    /// cookies carry an authenticated admin session.
    /// </summary>
    public async Task<HttpClient> SignInAdminAsync()
    {
        var client = _factory.CreateClient();

        var loginPage = await client.GetAsync("/login");
        await Assert.SuccessResponseAsync(loginPage);
        var antiforgeryToken = ExtractAntiforgeryToken(await loginPage.Content.ReadAsStringAsync());

        var loginResponse = await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Email", AdminEmail },
            { "__RequestVerificationToken", antiforgeryToken },
        }));
        await Assert.SuccessResponseAsync(loginResponse);

        var mailService = _factory.Services.GetRequiredService<IMailService>();
        var token = mailService.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IMailService.SendPasswordlessSignInAsync))
            .Select(c => (string?)c.GetArguments()[1])
            .LastOrDefault();
        if (string.IsNullOrEmpty(token))
        {
            Assert.Fail($"No passwordless sign-in email was sent to {AdminEmail}.");
        }

        var confirmResponse = await client.GetAsync(
            $"/login/confirm?email={HttpUtility.UrlEncode(AdminEmail)}&token={HttpUtility.UrlEncode(token)}&returnUrl=%2F");
        await Assert.SuccessResponseAsync(confirmResponse);

        return client;
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            """
            name="__RequestVerificationToken"[^>]*value="(?<token>[^"]+)"
            """);
        if (!match.Success)
        {
            Assert.Fail("The page has no antiforgery token.");
        }

        return match.Groups["token"].Value;
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        _connection.Dispose();
    }
}
