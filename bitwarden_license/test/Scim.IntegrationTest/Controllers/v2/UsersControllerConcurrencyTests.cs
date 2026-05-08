using Bit.Core;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Services;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.IntegrationTestCommon;
using Bit.Scim.IntegrationTest.Factories;
using Bit.Scim.Models;
using Bit.Scim.Utilities;
using NSubstitute;
using Xunit;

namespace Bit.Scim.IntegrationTest.Controllers.v2;

/// <summary>
/// Verifies seat-count integrity when SCIM invite requests run concurrently.
/// Runs against every supported real RDBMS (SqlServer, Postgres, MySql) when its
/// connection string is configured via BW_TEST_DATABASES__N__*. SQLite is excluded
/// because it serializes writes globally and cannot reproduce the read-modify-write
/// race on Organization.Seats.
/// </summary>
public class UsersControllerConcurrencyTests
{
    private static readonly Lazy<IReadOnlyDictionary<SupportedDatabaseProviders, string>> _configuredConnections =
        new(LoadConfiguredConnections);

    [SkippableTheory]
    [MemberData(nameof(DatabaseProviders))]
    public async Task Post_ConcurrentInvites_DoNotOvershootMaxAutoscaleSeats(
        SupportedDatabaseProviders providerType)
    {
        var baseConnectionString = _configuredConnections.Value.GetValueOrDefault(providerType);
        Skip.If(baseConnectionString is null,
            $"{providerType}: not configured (set BW_TEST_DATABASES__N__TYPE/CONNECTIONSTRING).");

        const short startingSeats = 3;
        const int availableSeats = 2;
        const int concurrentInvites = 6;

        const string testDatabaseName = "vault_test_scim";
        ITestDatabase testDatabase = providerType switch
        {
            SupportedDatabaseProviders.SqlServer => new SqlServerTestDatabase(baseConnectionString!, testDatabaseName),
            SupportedDatabaseProviders.Postgres => new PostgresTestDatabase(baseConnectionString!, testDatabaseName),
            SupportedDatabaseProviders.MySql => new MySqlTestDatabase(baseConnectionString!, testDatabaseName),
            _ => throw new InvalidOperationException($"Unsupported provider: {providerType}"),
        };

        var factory = new ScimApplicationFactory
        {
            TestDatabase = testDatabase
        };

        factory.SubstituteService((IFeatureService f) => f.IsEnabled(FeatureFlagKeys.ScimInviteUserOptimization)
            .Returns(true));

        try
        {
            factory.ReinitializeDbForTests(factory.GetDatabaseContext());

            using (var setupScope = factory.Services.CreateScope())
            {
                var setupContext = setupScope.ServiceProvider.GetRequiredService<DatabaseContext>();
                var org = setupContext.Organizations.Single(o => o.Id == ScimApplicationFactory.TestOrganizationId1);
                org.PlanType = PlanType.EnterpriseAnnually;
                org.Plan = "Enterprise (Annually)";
                org.Seats = startingSeats;
                org.MaxAutoscaleSeats = startingSeats + availableSeats;
                await setupContext.SaveChangesAsync();
            }

            var inputs = Enumerable.Range(0, concurrentInvites).Select(BuildInvite).ToArray();

            var responses = await Task.WhenAll(
                inputs.Select(input =>
                    factory.UsersPostAsync(ScimApplicationFactory.TestOrganizationId1, input)));

            var successfulInvites = responses.Count(r => r.Response.StatusCode == StatusCodes.Status201Created);

            using var verifyScope = factory.Services.CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var finalOrg = verifyContext.Organizations
                .Single(o => o.Id == ScimApplicationFactory.TestOrganizationId1);
            var finalActiveUserCount = verifyContext.OrganizationUsers
                .Count(ou => ou.OrganizationId == ScimApplicationFactory.TestOrganizationId1 && ou.Status >= 0);

            Assert.All(responses, r => Assert.True(r.Response.StatusCode < 500,
                $"Expected non-5xx status, got {r.Response.StatusCode}"));

            Assert.Equal(startingSeats + successfulInvites, finalOrg.Seats);

            Assert.Equal(startingSeats + successfulInvites, finalActiveUserCount);

            Assert.True(finalOrg.Seats <= finalOrg.MaxAutoscaleSeats,
                $"Seats {finalOrg.Seats} exceeded MaxAutoscaleSeats {finalOrg.MaxAutoscaleSeats}");
        }
        finally
        {
            await factory.DisposeAsync();
        }
    }

    public static IEnumerable<object?[]> DatabaseProviders()
    {
        yield return new object?[] { SupportedDatabaseProviders.SqlServer };
        yield return new object?[] { SupportedDatabaseProviders.Postgres };
        yield return new object?[] { SupportedDatabaseProviders.MySql };
    }

    private static IReadOnlyDictionary<SupportedDatabaseProviders, string> LoadConfiguredConnections()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(Bit.Identity.Startup).Assembly, optional: true)
            .AddEnvironmentVariables("BW_TEST_")
            .Build();

        var configured = new Dictionary<SupportedDatabaseProviders, string>();

        // Preferred source: BW_TEST_DATABASES__N__* env vars (set by test-database.yml in CI)
        for (var i = 0; ; i++)
        {
            var rawType = config[$"DATABASES:{i}:TYPE"];
            var connectionString = config[$"DATABASES:{i}:CONNECTIONSTRING"];
            if (rawType is null && connectionString is null)
            {
                break;
            }
            if (rawType is null || connectionString is null)
            {
                continue;
            }
            if (Enum.TryParse<SupportedDatabaseProviders>(rawType, ignoreCase: true, out var type)
                && !configured.ContainsKey(type))
            {
                configured[type] = connectionString;
            }
        }

        // Fallback for local dev: Identity user secrets (globalSettings:<provider>:connectionString)
        TryAddFromUserSecrets(SupportedDatabaseProviders.SqlServer, "globalSettings:sqlServer:connectionString");
        TryAddFromUserSecrets(SupportedDatabaseProviders.Postgres, "globalSettings:postgreSql:connectionString");
        TryAddFromUserSecrets(SupportedDatabaseProviders.MySql, "globalSettings:mySql:connectionString");

        return configured;

        void TryAddFromUserSecrets(SupportedDatabaseProviders type, string key)
        {
            if (configured.ContainsKey(type))
            {
                return;
            }
            var value = config[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                configured[type] = value;
            }
        }
    }

    private static ScimUserRequestModel BuildInvite(int i) => new()
    {
        DisplayName = $"Concurrent User {i}",
        Emails = new List<BaseScimUserModel.EmailModel>
        {
            new() { Primary = true, Type = "work", Value = $"concurrent-{i}@example.com" }
        },
        ExternalId = $"CONC-{i}",
        Active = true,
        Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
    };
}
