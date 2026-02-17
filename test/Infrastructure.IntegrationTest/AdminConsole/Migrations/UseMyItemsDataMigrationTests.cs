using Bit.Core.AdminConsole.Entities;
using Bit.Core.Repositories;
using Bit.Infrastructure.IntegrationTest.Services;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Migrations;

public class UseMyItemsDataMigrationTests
{
    private const string _migrationName = "UseMyItemsDataMigration";

    [Theory, DatabaseData(MigrationName = _migrationName)]
    public async Task Migration_WithUsePoliciesEnabled_SetsUseMyItemsToTrue(
        IOrganizationRepository organizationRepository,
        IMigrationTesterService migrationTester)
    {
        // Arrange
        var organization = await SetupOrganization(organizationRepository, usePolicies: true);

        // Verify initial state
        Assert.True(organization.UsePolicies);
        Assert.False(organization.UseMyItems);

        // Act
        migrationTester.ApplyMigration();

        // Assert
        var migratedOrganization = await organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(migratedOrganization);
        Assert.True(migratedOrganization.UsePolicies);
        Assert.True(migratedOrganization.UseMyItems);
    }

    [Theory, DatabaseData(MigrationName = _migrationName)]
    public async Task Migration_WithUsePoliciesDisabled_LeavesUseMyItemsFalse(
        IOrganizationRepository organizationRepository,
        IMigrationTesterService migrationTester)
    {
        // Arrange
        var organization = await SetupOrganization(organizationRepository, usePolicies: false);

        // Verify initial state
        Assert.False(organization.UsePolicies);
        Assert.False(organization.UseMyItems);

        // Act
        migrationTester.ApplyMigration();

        // Assert
        var migratedOrganization = await organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(migratedOrganization);
        Assert.False(migratedOrganization.UsePolicies);
        Assert.False(migratedOrganization.UseMyItems);
    }

    /// <summary>
    /// Helper method to create a test organization with specified UsePolicies value.
    /// UseMyItems is always initialized to false to simulate pre-migration state.
    /// </summary>
    private static async Task<Organization> SetupOrganization(
        IOrganizationRepository organizationRepository,
        bool usePolicies,
        string identifier = "test")
    {
        // CreateTestOrganizationAsync sets UsePolicies = true by default
        var organization = await organizationRepository.CreateTestOrganizationAsync(identifier: identifier);

        // Override to test both true and false scenarios
        organization.UsePolicies = usePolicies;
        organization.UseMyItems = false; // Simulate pre-migration state

        await organizationRepository.ReplaceAsync(organization);

        return organization;
    }
}
