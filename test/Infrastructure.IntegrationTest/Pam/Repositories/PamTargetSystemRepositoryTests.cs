using Bit.Core.Repositories;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Pam.Repositories;

public class PamTargetSystemRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task CreateAsync_ThenRead_RoundTripsFields(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;

        var target = await pamTargetSystemRepository.CreateAsync(new PamTargetSystem
        {
            OrganizationId = organization.Id,
            Name = "prod-mssql",
            Method = PamTargetSystemMethod.Automatic,
            Kind = PamTargetSystemKind.Mssql,
            PasswordPolicy = """{"minLength":16,"maxLength":32,"includeUppercase":true,"includeDigits":true}""",
            SupportsSessionTermination = true,
            Status = PamTargetSystemStatus.Active,
            CreationDate = now,
            RevisionDate = now,
        });

        var persisted = await pamTargetSystemRepository.GetByIdAsync(target.Id);

        Assert.NotNull(persisted);
        Assert.Equal(organization.Id, persisted!.OrganizationId);
        Assert.Equal("prod-mssql", persisted.Name);
        Assert.Equal(PamTargetSystemMethod.Automatic, persisted.Method);
        Assert.Equal(PamTargetSystemKind.Mssql, persisted.Kind);
        Assert.Contains("minLength", persisted.PasswordPolicy);
        Assert.True(persisted.SupportsSessionTermination);
        Assert.Equal(PamTargetSystemStatus.Active, persisted.Status);
    }

    // A manual target carries no connector: Kind/PasswordPolicy stay null through the round trip, and the narrow
    // fields a rename/status-change touches (Name, Status, RevisionDate) persist via the generic ReplaceAsync.
    [DatabaseTheory, DatabaseData]
    public async Task ReplaceAsync_UpdatesFields(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;

        var target = await pamTargetSystemRepository.CreateAsync(new PamTargetSystem
        {
            OrganizationId = organization.Id,
            Name = "manual-vault",
            Method = PamTargetSystemMethod.Manual,
            Status = PamTargetSystemStatus.Active,
            CreationDate = now,
            RevisionDate = now,
        });
        Assert.Null(target.Kind);
        Assert.Null(target.PasswordPolicy);

        target.Name = "manual-vault-renamed";
        target.Status = PamTargetSystemStatus.Disabled;
        target.RevisionDate = now.AddMinutes(5);
        await pamTargetSystemRepository.ReplaceAsync(target);

        var persisted = await pamTargetSystemRepository.GetByIdAsync(target.Id);
        Assert.NotNull(persisted);
        Assert.Equal("manual-vault-renamed", persisted!.Name);
        Assert.Equal(PamTargetSystemStatus.Disabled, persisted.Status);
        Assert.Equal(now.AddMinutes(5), persisted.RevisionDate);
        Assert.Null(persisted.Kind);
        Assert.Null(persisted.PasswordPolicy);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByOrganizationIdAsync_ScopesToOrganization(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;

        var mine = await pamTargetSystemRepository.CreateAsync(BuildTarget(organization.Id, "mine", now));
        await pamTargetSystemRepository.CreateAsync(BuildTarget(otherOrganization.Id, "not-mine", now));

        var results = await pamTargetSystemRepository.GetManyByOrganizationIdAsync(organization.Id);

        var row = Assert.Single(results);
        Assert.Equal(mine.Id, row.Id);
    }

    private static PamTargetSystem BuildTarget(Guid organizationId, string name, DateTime now) => new()
    {
        OrganizationId = organizationId,
        Name = name,
        Method = PamTargetSystemMethod.Automatic,
        Kind = PamTargetSystemKind.Entra,
        Status = PamTargetSystemStatus.Active,
        CreationDate = now,
        RevisionDate = now,
    };
}
