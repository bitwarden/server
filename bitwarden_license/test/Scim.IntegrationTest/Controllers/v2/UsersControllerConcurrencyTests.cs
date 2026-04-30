using Bit.Core;
using Bit.Core.Billing.Enums;
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
/// Requires a real SQL Server (vault_test) — SQLite serializes writes globally and
/// cannot reproduce the read-modify-write race on Organization.Seats.
/// </summary>
public class UsersControllerConcurrencyTests
{
    [Fact]
    public async Task Post_ConcurrentInvites_DoNotOvershootMaxAutoscaleSeats()
    {
        const short startingSeats = 3;
        const int availableSeats = 2;
        const int concurrentInvites = 6;

        var factory = new ScimApplicationFactory
        {
            TestDatabase = new SqlServerTestDatabase()
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
