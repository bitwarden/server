using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Repositories;
using Bit.Infrastructure.IntegrationTest.Services;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Migrations;

public class UseInviteLinksDataMigrationTests
{
    private const string MigrationName = "UseInviteLinksDataMigration";

    [Theory]
    [DatabaseData(MigrationName = MigrationName)]
    public async Task Migration_SetsUseInviteLinks_ForAllEnterprisePlanTypes(
        IMigrationTesterService migrationTester,
        IOrganizationRepository organizationRepository)
    {
        var enterprisePlanTypes = new[]
        {
            PlanType.EnterpriseMonthly2019,
            PlanType.EnterpriseAnnually2019,
            PlanType.EnterpriseMonthly2020,
            PlanType.EnterpriseAnnually2020,
            PlanType.EnterpriseMonthly2023,
            PlanType.EnterpriseAnnually2023,
            PlanType.EnterpriseMonthly,
            PlanType.EnterpriseAnnually,
        };

        var orgs = new List<Organization>();
        foreach (var planType in enterprisePlanTypes)
        {
            orgs.Add(await CreateOrganizationAsync(organizationRepository, planType, useInviteLinks: false));
        }

        migrationTester.ApplyMigration();

        foreach (var org in orgs)
        {
            var updated = await organizationRepository.GetByIdAsync(org.Id);
            Assert.NotNull(updated);
            Assert.True(updated.UseInviteLinks,
                $"Expected UseInviteLinks=true for {org.PlanType} (value {(int)org.PlanType})");
        }

        foreach (var org in orgs)
        {
            await organizationRepository.DeleteAsync(org);
        }
    }

    [Theory]
    [DatabaseData(MigrationName = MigrationName)]
    public async Task Migration_DoesNotSetUseInviteLinks_ForNonEnterprisePlanTypes(
        IMigrationTesterService migrationTester,
        IOrganizationRepository organizationRepository)
    {
        var nonEnterprisePlanTypes = new[]
        {
            PlanType.Free,
            PlanType.FamiliesAnnually2019,
            PlanType.TeamsMonthly2019,
            PlanType.TeamsAnnually2019,
            PlanType.Custom,
            PlanType.FamiliesAnnually2025,
            PlanType.TeamsMonthly2020,
            PlanType.TeamsAnnually2020,
            PlanType.TeamsMonthly2023,
            PlanType.TeamsAnnually2023,
            PlanType.TeamsStarter2023,
            PlanType.TeamsMonthly,
            PlanType.TeamsAnnually,
            PlanType.TeamsStarter,
            PlanType.FamiliesAnnually,
        };

        var orgs = new List<Organization>();
        foreach (var planType in nonEnterprisePlanTypes)
        {
            orgs.Add(await CreateOrganizationAsync(organizationRepository, planType, useInviteLinks: false));
        }

        migrationTester.ApplyMigration();

        foreach (var org in orgs)
        {
            var updated = await organizationRepository.GetByIdAsync(org.Id);
            Assert.NotNull(updated);
            Assert.False(updated.UseInviteLinks,
                $"Expected UseInviteLinks=false for {org.PlanType} (value {(int)org.PlanType})");
        }

        foreach (var org in orgs)
        {
            await organizationRepository.DeleteAsync(org);
        }
    }

    [Theory]
    [DatabaseData(MigrationName = MigrationName)]
    public async Task Migration_IsIdempotent_WhenEnterpriseOrgAlreadyHasUseInviteLinksTrue(
        IMigrationTesterService migrationTester,
        IOrganizationRepository organizationRepository)
    {
        var org = await CreateOrganizationAsync(
            organizationRepository,
            PlanType.EnterpriseAnnually,
            useInviteLinks: true);

        migrationTester.ApplyMigration();

        var updated = await organizationRepository.GetByIdAsync(org.Id);
        Assert.NotNull(updated);
        Assert.True(updated.UseInviteLinks);

        await organizationRepository.DeleteAsync(org);
    }

    private static Task<Organization> CreateOrganizationAsync(
        IOrganizationRepository organizationRepository,
        PlanType planType,
        bool useInviteLinks)
    {
        var id = Guid.NewGuid();
        return organizationRepository.CreateAsync(new Organization
        {
            Name = $"test-{id}",
            BillingEmail = $"billing-{id}@example.com",
            Plan = planType.ToString(),
            PlanType = planType,
            Enabled = true,
            UseInviteLinks = useInviteLinks,
        });
    }
}
