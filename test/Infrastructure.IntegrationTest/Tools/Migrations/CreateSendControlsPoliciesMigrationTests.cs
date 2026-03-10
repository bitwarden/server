// PM-31885 — delete this test file after the CreateSendControlsPolicies transition migration
// has been run in all environments.

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Bit.Infrastructure.IntegrationTest.Services;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Tools.Migrations;

public class CreateSendControlsPoliciesMigrationTests
{
    /// <summary>
    /// Org with only DisableSend enabled — migration creates a SendControls row with disableSend=true.
    /// </summary>
    [Theory, DatabaseData(MigrationName = "CreateSendControlsPolicies")]
    public async Task Migration_CreatesEnabledSendControls_WhenOnlyDisableSendEnabled(
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository,
        IMigrationTesterService migrationTester)
    {
        // Arrange
        var org = await organizationRepository.CreateTestOrganizationAsync();
        await CreatePolicyAsync(policyRepository, org.Id, PolicyType.DisableSend, enabled: true);

        // Act
        migrationTester.ApplyMigration();

        // Assert
        var result = await policyRepository.GetByOrganizationIdTypeAsync(org.Id, PolicyType.SendControls);
        AssertSendControlsPolicy(result, org.Id, expectedEnabled: true, expectedDisableSend: true, expectedDisableHideEmail: false);

        // Annul
        await organizationRepository.DeleteAsync(org);
    }

    /// <summary>
    /// Org with only DisableSend disabled — migration still creates a SendControls row, but disabled.
    /// </summary>
    [Theory, DatabaseData(MigrationName = "CreateSendControlsPolicies")]
    public async Task Migration_CreatesDisabledSendControls_WhenOnlyDisableSendDisabled(
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository,
        IMigrationTesterService migrationTester)
    {
        // Arrange
        var org = await organizationRepository.CreateTestOrganizationAsync();
        await CreatePolicyAsync(policyRepository, org.Id, PolicyType.DisableSend, enabled: false);

        // Act
        migrationTester.ApplyMigration();

        // Assert
        var result = await policyRepository.GetByOrganizationIdTypeAsync(org.Id, PolicyType.SendControls);
        AssertSendControlsPolicy(result, org.Id, expectedEnabled: false, expectedDisableSend: false, expectedDisableHideEmail: false);

        // Annul
        await organizationRepository.DeleteAsync(org);
    }

    /// <summary>
    /// Org with only SendOptions enabled, disableHideEmail=true in data — migration creates an enabled
    /// SendControls row with disableHideEmail=true.
    /// </summary>
    [Theory, DatabaseData(MigrationName = "CreateSendControlsPolicies")]
    public async Task Migration_CreatesEnabledSendControls_WhenOnlySendOptionsEnabledWithDisableHideEmail(
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository,
        IMigrationTesterService migrationTester)
    {
        // Arrange
        var org = await organizationRepository.CreateTestOrganizationAsync();
        await CreatePolicyAsync(policyRepository, org.Id, PolicyType.SendOptions, enabled: true,
            data: CoreHelpers.ClassToJsonData(new SendOptionsPolicyData { DisableHideEmail = true }));

        // Act
        migrationTester.ApplyMigration();

        // Assert
        var result = await policyRepository.GetByOrganizationIdTypeAsync(org.Id, PolicyType.SendControls);
        AssertSendControlsPolicy(result, org.Id, expectedEnabled: true, expectedDisableSend: false, expectedDisableHideEmail: true);

        // Annul
        await organizationRepository.DeleteAsync(org);
    }

    /// <summary>
    /// Org with SendOptions enabled but disableHideEmail=false in data — SendControls is still enabled
    /// (because SendOptions was enabled), but neither data field is enforced.
    /// </summary>
    [Theory, DatabaseData(MigrationName = "CreateSendControlsPolicies")]
    public async Task Migration_CreatesEnabledSendControls_WhenSendOptionsEnabledWithoutDisableHideEmail(
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository,
        IMigrationTesterService migrationTester)
    {
        // Arrange
        var org = await organizationRepository.CreateTestOrganizationAsync();
        await CreatePolicyAsync(policyRepository, org.Id, PolicyType.SendOptions, enabled: true,
            data: CoreHelpers.ClassToJsonData(new SendOptionsPolicyData { DisableHideEmail = false }));

        // Act
        migrationTester.ApplyMigration();

        // Assert
        var result = await policyRepository.GetByOrganizationIdTypeAsync(org.Id, PolicyType.SendControls);
        AssertSendControlsPolicy(result, org.Id, expectedEnabled: true, expectedDisableSend: false, expectedDisableHideEmail: false);

        // Annul
        await organizationRepository.DeleteAsync(org);
    }

    /// <summary>
    /// Org with both source policies enabled and disableHideEmail=true — full enforcement migrated.
    /// </summary>
    [Theory, DatabaseData(MigrationName = "CreateSendControlsPolicies")]
    public async Task Migration_CreatesSendControls_WithBothFieldsEnabled_WhenBothSourcePoliciesEnabled(
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository,
        IMigrationTesterService migrationTester)
    {
        // Arrange
        var org = await organizationRepository.CreateTestOrganizationAsync();
        await CreatePolicyAsync(policyRepository, org.Id, PolicyType.DisableSend, enabled: true);
        await CreatePolicyAsync(policyRepository, org.Id, PolicyType.SendOptions, enabled: true,
            data: CoreHelpers.ClassToJsonData(new SendOptionsPolicyData { DisableHideEmail = true }));

        // Act
        migrationTester.ApplyMigration();

        // Assert
        var result = await policyRepository.GetByOrganizationIdTypeAsync(org.Id, PolicyType.SendControls);
        AssertSendControlsPolicy(result, org.Id, expectedEnabled: true, expectedDisableSend: true, expectedDisableHideEmail: true);

        // Annul
        await organizationRepository.DeleteAsync(org);
    }

    /// <summary>
    /// Org with both source policies disabled but disableHideEmail=true in SendOptions data —
    /// SendControls is disabled, but the data field value is preserved from the JSON (no Enabled guard
    /// on the disableHideEmail field in the migration SQL).
    /// </summary>
    [Theory, DatabaseData(MigrationName = "CreateSendControlsPolicies")]
    public async Task Migration_CreatesDisabledSendControls_WhenBothSourcePoliciesDisabled(
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository,
        IMigrationTesterService migrationTester)
    {
        // Arrange
        var org = await organizationRepository.CreateTestOrganizationAsync();
        await CreatePolicyAsync(policyRepository, org.Id, PolicyType.DisableSend, enabled: false);
        await CreatePolicyAsync(policyRepository, org.Id, PolicyType.SendOptions, enabled: false,
            data: CoreHelpers.ClassToJsonData(new SendOptionsPolicyData { DisableHideEmail = true }));

        // Act
        migrationTester.ApplyMigration();

        // Assert
        var result = await policyRepository.GetByOrganizationIdTypeAsync(org.Id, PolicyType.SendControls);
        // disableHideEmail is copied from the data JSON regardless of SendOptions.Enabled
        AssertSendControlsPolicy(result, org.Id, expectedEnabled: false, expectedDisableSend: false, expectedDisableHideEmail: true);

        // Annul
        await organizationRepository.DeleteAsync(org);
    }

    /// <summary>
    /// Org with DisableSend enabled but SendOptions disabled — SendControls is enabled via DisableSend.
    /// disableHideEmail is still copied from the SendOptions data JSON regardless of its Enabled state.
    /// </summary>
    [Theory, DatabaseData(MigrationName = "CreateSendControlsPolicies")]
    public async Task Migration_CreatesSendControls_WithDisableHideEmailFromData_WhenSendOptionsDisabled(
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository,
        IMigrationTesterService migrationTester)
    {
        // Arrange
        var org = await organizationRepository.CreateTestOrganizationAsync();
        await CreatePolicyAsync(policyRepository, org.Id, PolicyType.DisableSend, enabled: true);
        await CreatePolicyAsync(policyRepository, org.Id, PolicyType.SendOptions, enabled: false,
            data: CoreHelpers.ClassToJsonData(new SendOptionsPolicyData { DisableHideEmail = true }));

        // Act
        migrationTester.ApplyMigration();

        // Assert
        var result = await policyRepository.GetByOrganizationIdTypeAsync(org.Id, PolicyType.SendControls);
        // disableHideEmail is copied from the data JSON regardless of SendOptions.Enabled
        AssertSendControlsPolicy(result, org.Id, expectedEnabled: true, expectedDisableSend: true, expectedDisableHideEmail: true);

        // Annul
        await organizationRepository.DeleteAsync(org);
    }

    /// <summary>
    /// Org with neither DisableSend nor SendOptions — no SendControls row is created.
    /// </summary>
    [Theory, DatabaseData(MigrationName = "CreateSendControlsPolicies")]
    public async Task Migration_DoesNotCreateSendControls_WhenNoSourcePoliciesExist(
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository,
        IMigrationTesterService migrationTester)
    {
        // Arrange
        var org = await organizationRepository.CreateTestOrganizationAsync();

        // Act
        migrationTester.ApplyMigration();

        // Assert
        var result = await policyRepository.GetByOrganizationIdTypeAsync(org.Id, PolicyType.SendControls);
        Assert.Null(result);

        // Annul
        await organizationRepository.DeleteAsync(org);
    }

    /// <summary>
    /// Org that already has a SendControls row — the WHERE NOT EXISTS guard means the existing row is
    /// left completely unchanged and no duplicate is inserted.
    /// </summary>
    [Theory, DatabaseData(MigrationName = "CreateSendControlsPolicies")]
    public async Task Migration_SkipsOrg_WhenSendControlsAlreadyExists(
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository,
        IMigrationTesterService migrationTester)
    {
        // Arrange
        var org = await organizationRepository.CreateTestOrganizationAsync();
        await CreatePolicyAsync(policyRepository, org.Id, PolicyType.DisableSend, enabled: true);
        var preExistingSendControls = await CreatePolicyAsync(policyRepository, org.Id, PolicyType.SendControls,
            enabled: false,
            data: CoreHelpers.ClassToJsonData(new SendControlsPolicyData { DisableSend = false, DisableHideEmail = false }));

        // Act
        migrationTester.ApplyMigration();

        // Assert — the pre-existing row is returned unchanged; no duplicate was created
        var result = await policyRepository.GetByOrganizationIdTypeAsync(org.Id, PolicyType.SendControls);
        Assert.NotNull(result);
        Assert.Equal(preExistingSendControls.Id, result.Id);
        Assert.False(result.Enabled);
        var data = CoreHelpers.LoadClassFromJsonData<SendControlsPolicyData>(result.Data);
        Assert.NotNull(data);
        Assert.False(data.DisableSend);
        Assert.False(data.DisableHideEmail);

        // Annul
        await organizationRepository.DeleteAsync(org);
    }

    /// <summary>
    /// Two orgs processed together — each org's result is independent (no cross-org contamination
    /// in the JOIN / COALESCE logic).
    /// </summary>
    [Theory, DatabaseData(MigrationName = "CreateSendControlsPolicies")]
    public async Task Migration_IsIsolatedPerOrganization(
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository,
        IMigrationTesterService migrationTester)
    {
        // Arrange
        var orgWithPolicy = await organizationRepository.CreateTestOrganizationAsync();
        await CreatePolicyAsync(policyRepository, orgWithPolicy.Id, PolicyType.DisableSend, enabled: true);

        var orgWithoutPolicy = await organizationRepository.CreateTestOrganizationAsync();

        // Act
        migrationTester.ApplyMigration();

        // Assert
        var resultWithPolicy = await policyRepository.GetByOrganizationIdTypeAsync(orgWithPolicy.Id, PolicyType.SendControls);
        AssertSendControlsPolicy(resultWithPolicy, orgWithPolicy.Id, expectedEnabled: true, expectedDisableSend: true, expectedDisableHideEmail: false);

        var resultWithoutPolicy = await policyRepository.GetByOrganizationIdTypeAsync(orgWithoutPolicy.Id, PolicyType.SendControls);
        Assert.Null(resultWithoutPolicy);

        // Annul
        await organizationRepository.DeleteAsync(orgWithPolicy);
        await organizationRepository.DeleteAsync(orgWithoutPolicy);
    }

    private static Task<Policy> CreatePolicyAsync(
        IPolicyRepository policyRepository,
        Guid organizationId,
        PolicyType type,
        bool enabled,
        string? data = null)
        => policyRepository.CreateAsync(new Policy
        {
            OrganizationId = organizationId,
            Type = type,
            Enabled = enabled,
            Data = data,
        });

    private static void AssertSendControlsPolicy(
        Policy? policy,
        Guid organizationId,
        bool expectedEnabled,
        bool expectedDisableSend,
        bool expectedDisableHideEmail)
    {
        Assert.NotNull(policy);
        Assert.Equal(organizationId, policy.OrganizationId);
        Assert.Equal(PolicyType.SendControls, policy.Type);
        Assert.Equal(expectedEnabled, policy.Enabled);
        Assert.NotEqual(default, policy.CreationDate);
        Assert.NotEqual(default, policy.RevisionDate);
        var data = CoreHelpers.LoadClassFromJsonData<SendControlsPolicyData>(policy.Data);
        Assert.NotNull(data);
        Assert.Equal(expectedDisableSend, data.DisableSend);
        Assert.Equal(expectedDisableHideEmail, data.DisableHideEmail);
    }
}
