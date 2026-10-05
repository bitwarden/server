using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Bit.Infrastructure.IntegrationTest.Comparers;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Pam.Repositories;

public class PamAccessConnectorRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task CreateAsync_ThenRead_RoundTripsFields(
        IApiKeyRepository apiKeyRepository,
        IOrganizationRepository organizationRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var apiKey = await apiKeyRepository.CreateAsync(BuildAccessConnectorApiKey());

        var accessConnector = await pamAccessConnectorRepository.CreateAsync(new PamAccessConnector
        {
            OrganizationId = organization.Id,
            Name = "prod-access-connector",
            ApiKeyId = apiKey.Id,
            Status = PamAccessConnectorStatus.Enabled,
        });

        var persisted = await pamAccessConnectorRepository.GetByIdAsync(accessConnector.Id);

        Assert.NotNull(persisted);
        Assert.Equal(organization.Id, persisted!.OrganizationId);
        Assert.Equal("prod-access-connector", persisted.Name);
        Assert.Equal(apiKey.Id, persisted.ApiKeyId);
        Assert.Equal(PamAccessConnectorStatus.Enabled, persisted.Status);
        Assert.Null(persisted.LastHeartbeatAt);
    }

    // PamAccessConnectorClientProvider's token-issuance lookup, keyed by the ApiKey credential rather than the access
    // connector's id.
    [DatabaseTheory, DatabaseData]
    public async Task GetDetailsByApiKeyIdAsync_ReturnsAccessConnectorWithOrganizationLicensingFlags(
        IApiKeyRepository apiKeyRepository,
        IOrganizationRepository organizationRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        Assert.True(organization.Enabled);
        Assert.True(organization.UsePam);
        var apiKey = await apiKeyRepository.CreateAsync(BuildAccessConnectorApiKey());
        var accessConnector = await pamAccessConnectorRepository.CreateAsync(new PamAccessConnector
        {
            OrganizationId = organization.Id,
            Name = "prod-access-connector",
            ApiKeyId = apiKey.Id,
            Status = PamAccessConnectorStatus.Enabled,
        });

        var details = await pamAccessConnectorRepository.GetDetailsByApiKeyIdAsync(apiKey.Id);

        Assert.NotNull(details);
        Assert.Equal(accessConnector.Id, details!.Id);
        Assert.Equal(PamAccessConnectorStatus.Enabled, details.Status);
        Assert.True(details.OrganizationEnabled);
        Assert.True(details.OrganizationUsePam);

        // Flip UsePam only: OrganizationEnabled must stay true, proving the two columns map independently.
        organization.UsePam = false;
        await organizationRepository.ReplaceAsync(organization);

        var afterLicenseLapse = await pamAccessConnectorRepository.GetDetailsByApiKeyIdAsync(apiKey.Id);
        Assert.NotNull(afterLicenseLapse);
        Assert.True(afterLicenseLapse!.OrganizationEnabled);
        Assert.False(afterLicenseLapse.OrganizationUsePam);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetDetailsByApiKeyIdAsync_UnknownApiKeyId_ReturnsNull(
        IPamAccessConnectorRepository pamAccessConnectorRepository)
    {
        Assert.Null(await pamAccessConnectorRepository.GetDetailsByApiKeyIdAsync(Guid.NewGuid()));
    }

    // The sproc's WHERE guard turns a poll before MinInterval into a no-op; only one after it bumps the column.
    [DatabaseTheory, DatabaseData]
    public async Task UpdateHeartbeatAsync_ConditionalBump(
        IApiKeyRepository apiKeyRepository,
        IOrganizationRepository organizationRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var apiKey = await apiKeyRepository.CreateAsync(BuildAccessConnectorApiKey());
        var accessConnector = await pamAccessConnectorRepository.CreateAsync(new PamAccessConnector
        {
            OrganizationId = organization.Id,
            Name = "prod-access-connector",
            ApiKeyId = apiKey.Id,
            Status = PamAccessConnectorStatus.Enabled,
        });
        var minInterval = TimeSpan.FromSeconds(15);
        var firstHeartbeat = DateTime.UtcNow;

        // First heartbeat: LastHeartbeatAt was null, so it always bumps.
        await pamAccessConnectorRepository.UpdateHeartbeatAsync(accessConnector.Id, firstHeartbeat, minInterval);
        var afterFirst = await pamAccessConnectorRepository.GetByIdAsync(accessConnector.Id);
        Assert.NotNull(afterFirst!.LastHeartbeatAt);
        var recordedFirst = afterFirst.LastHeartbeatAt!.Value;

        // Second poll arrives well within MinInterval: the guard's WHERE clause keeps this a no-op.
        await pamAccessConnectorRepository.UpdateHeartbeatAsync(
            accessConnector.Id, firstHeartbeat.AddSeconds(5), minInterval);
        var afterSecond = await pamAccessConnectorRepository.GetByIdAsync(accessConnector.Id);
        Assert.Equal(recordedFirst, afterSecond!.LastHeartbeatAt);

        // Third poll arrives after MinInterval has elapsed since the last recorded bump: it updates.
        var thirdHeartbeat = firstHeartbeat.AddSeconds(20);
        await pamAccessConnectorRepository.UpdateHeartbeatAsync(accessConnector.Id, thirdHeartbeat, minInterval);
        var afterThird = await pamAccessConnectorRepository.GetByIdAsync(accessConnector.Id);
        Assert.Equal(thirdHeartbeat, afterThird!.LastHeartbeatAt.Value, LaxDateTimeComparer.Default);
        Assert.NotEqual(recordedFirst, afterThird.LastHeartbeatAt);
    }

    [DatabaseTheory, DatabaseData]
    public async Task Assignment_CreateExistsDeleteReadByOrganization_RoundTrips(
        IApiKeyRepository apiKeyRepository,
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var target = await pamTargetSystemRepository.CreateAsync(new PamTargetSystem
        {
            OrganizationId = organization.Id,
            Name = "target",
            Method = PamTargetSystemMethod.Automatic,
            Kind = PamTargetSystemKind.Mssql,
            Status = PamTargetSystemStatus.Active,
            CreationDate = now,
            RevisionDate = now,
        });
        var apiKey = await apiKeyRepository.CreateAsync(BuildAccessConnectorApiKey());
        var accessConnector = await pamAccessConnectorRepository.CreateAsync(new PamAccessConnector
        {
            OrganizationId = organization.Id,
            Name = "access-connector",
            ApiKeyId = apiKey.Id,
            Status = PamAccessConnectorStatus.Enabled,
        });

        Assert.False(await pamAccessConnectorRepository.AssignmentExistsAsync(accessConnector.Id, target.Id));
        Assert.Empty(await pamAccessConnectorRepository.GetAssignmentsByOrganizationIdAsync(organization.Id));

        var assignment = new PamAccessConnectorTargetAssignment
        {
            Id = CombGuid.Generate(),
            AccessConnectorId = accessConnector.Id,
            TargetSystemId = target.Id,
            OrganizationId = organization.Id,
            CreationDate = now,
        };
        await pamAccessConnectorRepository.CreateAssignmentAsync(assignment);

        Assert.True(await pamAccessConnectorRepository.AssignmentExistsAsync(accessConnector.Id, target.Id));
        var assignments = await pamAccessConnectorRepository.GetAssignmentsByOrganizationIdAsync(organization.Id);
        var persisted = Assert.Single(assignments);
        Assert.Equal(assignment.Id, persisted.Id);
        Assert.Equal(accessConnector.Id, persisted.AccessConnectorId);
        Assert.Equal(target.Id, persisted.TargetSystemId);

        await pamAccessConnectorRepository.DeleteAssignmentAsync(accessConnector.Id, target.Id);

        Assert.False(await pamAccessConnectorRepository.AssignmentExistsAsync(accessConnector.Id, target.Id));
        Assert.Empty(await pamAccessConnectorRepository.GetAssignmentsByOrganizationIdAsync(organization.Id));
    }

    // PamAccessConnector_Update only declares Name/Status/RevisionDate; ApiKeyId and OrganizationId must be ignored
    // even if set on the in-memory entity before ReplaceAsync.
    [DatabaseTheory, DatabaseData]
    public async Task ReplaceAsync_OnlyPersistsNameStatusRevisionDate(
        IApiKeyRepository apiKeyRepository,
        IOrganizationRepository organizationRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var apiKey = await apiKeyRepository.CreateAsync(BuildAccessConnectorApiKey());
        var accessConnector = await pamAccessConnectorRepository.CreateAsync(new PamAccessConnector
        {
            OrganizationId = organization.Id,
            Name = "access-connector",
            ApiKeyId = apiKey.Id,
            Status = PamAccessConnectorStatus.Enabled,
        });
        var originalOrganizationId = accessConnector.OrganizationId;
        var originalApiKeyId = accessConnector.ApiKeyId;
        var newRevisionDate = DateTime.UtcNow.AddMinutes(10);

        accessConnector.Name = "renamed-access-connector";
        accessConnector.Status = PamAccessConnectorStatus.Disabled;
        accessConnector.RevisionDate = newRevisionDate;
        accessConnector.OrganizationId = Guid.NewGuid();
        accessConnector.ApiKeyId = Guid.NewGuid();
        await pamAccessConnectorRepository.ReplaceAsync(accessConnector);

        var persisted = await pamAccessConnectorRepository.GetByIdAsync(accessConnector.Id);
        Assert.NotNull(persisted);
        Assert.Equal("renamed-access-connector", persisted!.Name);
        Assert.Equal(PamAccessConnectorStatus.Disabled, persisted.Status);
        Assert.Equal(newRevisionDate, persisted.RevisionDate, LaxDateTimeComparer.Default);
        Assert.Equal(originalOrganizationId, persisted.OrganizationId);
        Assert.Equal(originalApiKeyId, persisted.ApiKeyId);
    }

    [DatabaseTheory, DatabaseData]
    public async Task DeleteAsync_RemovesTheAccessConnectorWithItsCredentialAndAssignments(
        IApiKeyRepository apiKeyRepository,
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var target = await pamTargetSystemRepository.CreateAsync(new PamTargetSystem
        {
            OrganizationId = organization.Id,
            Name = $"target-{Guid.NewGuid()}",
            Method = PamTargetSystemMethod.Automatic,
            Kind = PamTargetSystemKind.Mssql,
            Status = PamTargetSystemStatus.Active,
            CreationDate = now,
            RevisionDate = now,
        });
        var apiKey = await apiKeyRepository.CreateAsync(BuildAccessConnectorApiKey());
        var accessConnector = await pamAccessConnectorRepository.CreateAsync(new PamAccessConnector
        {
            OrganizationId = organization.Id,
            Name = "doomed-access-connector",
            ApiKeyId = apiKey.Id,
            Status = PamAccessConnectorStatus.Enabled,
        });
        // An access connector with assignments cannot be deleted row-by-row: that FK is ON DELETE NO ACTION.
        await pamAccessConnectorRepository.CreateAssignmentAsync(new PamAccessConnectorTargetAssignment
        {
            Id = CombGuid.Generate(),
            AccessConnectorId = accessConnector.Id,
            TargetSystemId = target.Id,
            OrganizationId = organization.Id,
            CreationDate = now,
        });

        var survivingKey = await apiKeyRepository.CreateAsync(BuildAccessConnectorApiKey("survivor"));
        var survivor = await pamAccessConnectorRepository.CreateAsync(new PamAccessConnector
        {
            OrganizationId = organization.Id,
            Name = "surviving-access-connector",
            ApiKeyId = survivingKey.Id,
            Status = PamAccessConnectorStatus.Enabled,
        });

        // The delete reads ApiKeyId from the stored row, so this value must be ignored.
        accessConnector.ApiKeyId = survivingKey.Id;
        await pamAccessConnectorRepository.DeleteAsync(accessConnector);

        Assert.Null(await pamAccessConnectorRepository.GetByIdAsync(accessConnector.Id));
        Assert.Null(await apiKeyRepository.GetByIdAsync(apiKey.Id));
        Assert.Empty(await pamAccessConnectorRepository.GetAssignmentsByOrganizationIdAsync(organization.Id));

        Assert.NotNull(await pamAccessConnectorRepository.GetByIdAsync(survivor.Id));
        Assert.NotNull(await apiKeyRepository.GetByIdAsync(survivingKey.Id));
    }

    private static ApiKey BuildAccessConnectorApiKey(string identifier = "accessConnector") => new()
    {
        ServiceAccountId = null,
        Name = $"{identifier}-{Guid.NewGuid()}",
        Scope = """["api.pam.rotation"]""",
        EncryptedPayload = "encrypted-payload",
        Key = "encrypted-key",
    };
}
