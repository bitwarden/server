using Bit.Core.AdminConsole.Entities;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Dirt.Repositories;

/// <summary>
/// Covers the circuit breaker's write paths. The behaviour that matters lives entirely in T-SQL and LINQ — the
/// enabled-state predicate that makes a re-trip idempotent, and the join that keeps a write inside one
/// organization — so a mocked repository cannot observe any of it.
/// </summary>
public class OrganizationIntegrationConfigurationDisableTests
{
    [Theory, DatabaseData]
    public async Task DisableAsync_EnabledConfiguration_DisablesItAndReportsTheTransition(
        IOrganizationIntegrationConfigurationRepository sut,
        IOrganizationIntegrationRepository integrationRepository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await CreateOrganizationAsync(organizationRepository);
        var configuration = await CreateConfigurationAsync(sut, integrationRepository, organization.Id);

        var disabled = await sut.DisableAsync(
            organizationId: organization.Id,
            id: configuration.Id,
            disabledDate: _disabledDate,
            disabledReason: IntegrationFailureCategory.AuthenticationFailed);

        Assert.True(disabled);
        var stored = await sut.GetByIdAsync(configuration.Id);
        Assert.NotNull(stored);
        Assert.Equal(_disabledDate, stored.DisabledDate);
        Assert.Equal(IntegrationFailureCategory.AuthenticationFailed, stored.DisabledReason);
    }

    [Theory, DatabaseData]
    public async Task DisableAsync_AlreadyDisabled_ReportsNoTransitionAndKeepsTheOriginalReason(
        IOrganizationIntegrationConfigurationRepository sut,
        IOrganizationIntegrationRepository integrationRepository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await CreateOrganizationAsync(organizationRepository);
        var configuration = await CreateConfigurationAsync(sut, integrationRepository, organization.Id);

        await sut.DisableAsync(
            organization.Id, configuration.Id, _disabledDate, IntegrationFailureCategory.AuthenticationFailed);

        // The breaker relies on this to tell "I disabled it" from "another instance already had", which is what
        // keeps concurrent trips to a single cache invalidation and a single log line
        var second = await sut.DisableAsync(
            organization.Id, configuration.Id, _disabledDate.AddHours(1), IntegrationFailureCategory.ConfigurationError);

        Assert.False(second);
        var stored = await sut.GetByIdAsync(configuration.Id);
        Assert.NotNull(stored);
        Assert.Equal(IntegrationFailureCategory.AuthenticationFailed, stored.DisabledReason);
    }

    [Theory, DatabaseData]
    public async Task DisableAsync_OrganizationDoesNotOwnTheConfiguration_WritesNothing(
        IOrganizationIntegrationConfigurationRepository sut,
        IOrganizationIntegrationRepository integrationRepository,
        IOrganizationRepository organizationRepository)
    {
        var owner = await CreateOrganizationAsync(organizationRepository);
        var other = await CreateOrganizationAsync(organizationRepository);
        var configuration = await CreateConfigurationAsync(sut, integrationRepository, owner.Id);

        var disabled = await sut.DisableAsync(
            organizationId: other.Id,
            id: configuration.Id,
            disabledDate: _disabledDate,
            disabledReason: IntegrationFailureCategory.AuthenticationFailed);

        Assert.False(disabled);
        var stored = await sut.GetByIdAsync(configuration.Id);
        Assert.NotNull(stored);
        Assert.Null(stored.DisabledDate);
    }

    [Theory, DatabaseData]
    public async Task ClearDisabledByIntegrationAsync_ReEnablesOnlyTheIntegrationsDisabledConfigurations(
        IOrganizationIntegrationConfigurationRepository sut,
        IOrganizationIntegrationRepository integrationRepository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await CreateOrganizationAsync(organizationRepository);
        var integration = await CreateIntegrationAsync(integrationRepository, organization.Id);
        var disabled = await CreateConfigurationAsync(sut, integration, EventType.Cipher_Created);
        var enabled = await CreateConfigurationAsync(sut, integration, EventType.Cipher_Deleted);
        var otherIntegration = await CreateIntegrationAsync(organizationRepository, integrationRepository, organization.Id);
        var bystander = await CreateConfigurationAsync(sut, otherIntegration, EventType.Cipher_Created);

        await sut.DisableAsync(
            organization.Id, disabled.Id, _disabledDate, IntegrationFailureCategory.AuthenticationFailed);
        await sut.DisableAsync(
            organization.Id, bystander.Id, _disabledDate, IntegrationFailureCategory.AuthenticationFailed);

        var reEnabled = await sut.ClearDisabledByIntegrationAsync(
            organizationId: organization.Id,
            organizationIntegrationId: integration.Id,
            revisionDate: _revisionDate);

        Assert.Equal(1, reEnabled);
        var clearedRow = await sut.GetByIdAsync(disabled.Id);
        Assert.NotNull(clearedRow);
        Assert.Null(clearedRow.DisabledDate);
        Assert.Null(clearedRow.DisabledReason);
        Assert.Equal(_revisionDate, clearedRow.RevisionDate);

        // An already-enabled sibling is untouched, and another integration's disable survives
        var untouched = await sut.GetByIdAsync(enabled.Id);
        Assert.NotNull(untouched);
        Assert.Null(untouched.DisabledDate);
        var bystanderRow = await sut.GetByIdAsync(bystander.Id);
        Assert.NotNull(bystanderRow);
        Assert.NotNull(bystanderRow.DisabledDate);
    }

    [Theory, DatabaseData]
    public async Task ClearDisabledByIntegrationAsync_OrganizationDoesNotOwnTheIntegration_WritesNothing(
        IOrganizationIntegrationConfigurationRepository sut,
        IOrganizationIntegrationRepository integrationRepository,
        IOrganizationRepository organizationRepository)
    {
        var owner = await CreateOrganizationAsync(organizationRepository);
        var other = await CreateOrganizationAsync(organizationRepository);
        var integration = await CreateIntegrationAsync(integrationRepository, owner.Id);
        var configuration = await CreateConfigurationAsync(sut, integration, EventType.Cipher_Created);
        await sut.DisableAsync(
            owner.Id, configuration.Id, _disabledDate, IntegrationFailureCategory.AuthenticationFailed);

        var reEnabled = await sut.ClearDisabledByIntegrationAsync(
            organizationId: other.Id,
            organizationIntegrationId: integration.Id,
            revisionDate: _revisionDate);

        Assert.Equal(0, reEnabled);
        var stored = await sut.GetByIdAsync(configuration.Id);
        Assert.NotNull(stored);
        Assert.NotNull(stored.DisabledDate);
    }

    private static readonly DateTime _disabledDate = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _revisionDate = new(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc);

    private static async Task<Organization> CreateOrganizationAsync(IOrganizationRepository organizationRepository) =>
        await organizationRepository.CreateAsync(new Organization
        {
            Name = "Integration Circuit Breaker Test Org",
            BillingEmail = "test@example.com",
            Plan = "Test",
            PrivateKey = "privatekey",
        });

    private static async Task<OrganizationIntegration> CreateIntegrationAsync(
        IOrganizationIntegrationRepository integrationRepository,
        Guid organizationId,
        IntegrationType type = IntegrationType.Webhook) =>
        await integrationRepository.CreateAsync(new OrganizationIntegration
        {
            OrganizationId = organizationId,
            Type = type,
            Configuration = "{}",
        });

    // A second integration for the same organization needs a different type: the table is unique on
    // (OrganizationId, Type)
    private static async Task<OrganizationIntegration> CreateIntegrationAsync(
        IOrganizationRepository organizationRepository,
        IOrganizationIntegrationRepository integrationRepository,
        Guid organizationId) =>
        await CreateIntegrationAsync(integrationRepository, organizationId, IntegrationType.Hec);

    private static async Task<OrganizationIntegrationConfiguration> CreateConfigurationAsync(
        IOrganizationIntegrationConfigurationRepository sut,
        IOrganizationIntegrationRepository integrationRepository,
        Guid organizationId) =>
        await CreateConfigurationAsync(
            sut,
            await CreateIntegrationAsync(integrationRepository, organizationId),
            EventType.Cipher_Created);

    private static async Task<OrganizationIntegrationConfiguration> CreateConfigurationAsync(
        IOrganizationIntegrationConfigurationRepository sut,
        OrganizationIntegration integration,
        EventType eventType) =>
        await sut.CreateAsync(new OrganizationIntegrationConfiguration
        {
            OrganizationIntegrationId = integration.Id,
            EventType = eventType,
            Template = "{}",
        });
}
