using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Bit.Infrastructure.IntegrationTest.Comparers;
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
        Assert.Equal(now.AddMinutes(5), persisted.RevisionDate, LaxDateTimeComparer.Default);
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

    [DatabaseTheory, DatabaseData]
    public async Task DeleteWithAssignmentsAsync_CascadesAssignments_ButRefusesWhileAConfigNamesTheTarget(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamDaemonRepository pamDaemonRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var target = await pamTargetSystemRepository.CreateAsync(BuildTarget(organization.Id, "prod-entra", now));
        var daemon = await CreateEnrolledDaemonAsync(apiKeyRepository, pamDaemonRepository, organization.Id);
        await pamDaemonRepository.CreateAssignmentAsync(new PamDaemonTargetAssignment
        {
            Id = CombGuid.Generate(),
            DaemonId = daemon.Id,
            TargetSystemId = target.Id,
            OrganizationId = organization.Id,
            CreationDate = now,
        });
        var cipher = await cipherRepository.CreateAsync(new Cipher
        {
            OrganizationId = organization.Id,
            Type = CipherType.Login,
            Data = "{\"originalSecret\":true}",
        });
        var config = await pamRotationConfigRepository.CreateAsync(new PamRotationConfig
        {
            OrganizationId = organization.Id,
            CipherId = cipher.Id,
            TargetSystemId = target.Id,
            AccountIdentity = "svc-account",
            TerminateSessions = false,
            RotateOnAccessEnd = false,
            Enabled = true,
            CreationDate = now,
            RevisionDate = now,
        });

        // While a config names the target the delete is refused outright: the config -- and the credential it
        // manages -- would be left pointing at nothing.
        Assert.False(await pamTargetSystemRepository.DeleteWithAssignmentsAsync(target.Id));
        Assert.NotNull(await pamTargetSystemRepository.GetByIdAsync(target.Id));
        Assert.True(await pamDaemonRepository.AssignmentExistsAsync(daemon.Id, target.Id));

        Assert.True(await pamRotationConfigRepository.DeleteWithJobsAsync(config.Id));

        // With nothing configured against it the target goes, and its assignment -- only the connector-to-target
        // edge -- goes with it rather than blocking on the NO ACTION FK.
        Assert.True(await pamTargetSystemRepository.DeleteWithAssignmentsAsync(target.Id));

        Assert.Null(await pamTargetSystemRepository.GetByIdAsync(target.Id));
        Assert.False(await pamDaemonRepository.AssignmentExistsAsync(daemon.Id, target.Id));
        // The access connector itself outlives the target it was assigned to.
        Assert.NotNull(await pamDaemonRepository.GetByIdAsync(daemon.Id));
    }

    [DatabaseTheory, DatabaseData]
    public async Task DeleteWithAssignmentsAsync_NoAssignments_DeletesTheTarget(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var target = await pamTargetSystemRepository.CreateAsync(
            BuildTarget(organization.Id, "unassigned", DateTime.UtcNow));

        Assert.True(await pamTargetSystemRepository.DeleteWithAssignmentsAsync(target.Id));

        Assert.Null(await pamTargetSystemRepository.GetByIdAsync(target.Id));
    }

    private static async Task<PamDaemon> CreateEnrolledDaemonAsync(
        IApiKeyRepository apiKeyRepository, IPamDaemonRepository pamDaemonRepository, Guid organizationId)
    {
        var apiKey = await apiKeyRepository.CreateAsync(new ApiKey
        {
            ServiceAccountId = null,
            Name = $"daemon-{Guid.NewGuid()}",
            Scope = """["api.pam.rotation"]""",
            EncryptedPayload = "encrypted-payload",
            Key = "encrypted-key",
        });
        return await pamDaemonRepository.CreateAsync(new PamDaemon
        {
            OrganizationId = organizationId,
            Name = $"daemon-{Guid.NewGuid()}",
            ApiKeyId = apiKey.Id,
            Status = PamAccessConnectorStatus.Enabled,
        });
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
