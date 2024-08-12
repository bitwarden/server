using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Infrastructure.IntegrationTest.Services;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Migrations;

public class FinalFlexibleCollectionsDataMigrationsTests
{
    [DatabaseTheory, DatabaseData(MigrationName = "FinalFlexibleCollectionsDataMigrations")]
    public async Task RunMigration_WithEditAssignedCollections_WithCustomUserType_MigratesToUserNullPermissions(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IMigrationTesterService migrationTester)
    {
        // Setup data
        var orgUser = await SetupData(
            userRepository, organizationRepository, organizationUserRepository,
            editAssignedCollections: true, deleteAssignedCollections: false);

        // Run data migration
        migrationTester.ApplyMigration();

        // Assert that the user was migrated to a User type with null permissions
        var migratedOrgUser = await organizationUserRepository.GetByIdAsync(orgUser.Id);
        Assert.NotNull(migratedOrgUser);
        Assert.Equal(orgUser.Id, migratedOrgUser.Id);
        Assert.Equal(OrganizationUserType.User, migratedOrgUser.Type);
        Assert.Null(migratedOrgUser.Permissions);
    }

    [DatabaseTheory, DatabaseData(MigrationName = "FinalFlexibleCollectionsDataMigrations")]
    public async Task RunMigration_WithDeleteAssignedCollections_WithCustomUserType_MigratesToUserNullPermissions(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IMigrationTesterService migrationTester)
    {
        // Setup data
        var orgUser = await SetupData(
            userRepository, organizationRepository, organizationUserRepository,
            editAssignedCollections: false, deleteAssignedCollections: true);

        // Run data migration
        migrationTester.ApplyMigration();

        // Assert that the user was migrated to a User type with null permissions
        var migratedOrgUser = await organizationUserRepository.GetByIdAsync(orgUser.Id);
        Assert.NotNull(migratedOrgUser);
        Assert.Equal(orgUser.Id, migratedOrgUser.Id);
        Assert.Equal(OrganizationUserType.User, migratedOrgUser.Type);
        Assert.Null(migratedOrgUser.Permissions);
    }

    [DatabaseTheory, DatabaseData(MigrationName = "FinalFlexibleCollectionsDataMigrations")]
    public async Task RunMigration_WithEditAndDeleteAssignedCollections_WithCustomUserType_MigratesToUserNullPermissions(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IMigrationTesterService migrationTester)
    {
        // Setup data
        var orgUser = await SetupData(
            userRepository, organizationRepository, organizationUserRepository,
            editAssignedCollections: true, deleteAssignedCollections: true);

        // Run data migration
        migrationTester.ApplyMigration();

        // Assert that the user was migrated to a User type with null permissions
        var migratedOrgUser = await organizationUserRepository.GetByIdAsync(orgUser.Id);
        Assert.NotNull(migratedOrgUser);
        Assert.Equal(orgUser.Id, migratedOrgUser.Id);
        Assert.Equal(OrganizationUserType.User, migratedOrgUser.Type);
        Assert.Null(migratedOrgUser.Permissions);
    }

    [DatabaseTheory, DatabaseData(MigrationName = "FinalFlexibleCollectionsDataMigrations")]
    public async Task RunMigration_WithoutAssignedCollectionsPermissions_WithCustomUserType_RemovesAssignedCollectionsPermissions(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IMigrationTesterService migrationTester)
    {
        // Setup data
        var orgUser = await SetupData(
            userRepository, organizationRepository, organizationUserRepository,
            editAssignedCollections: false, deleteAssignedCollections: false);

        // Run data migration
        migrationTester.ApplyMigration();

        // Assert that the user was migrated to a User type with null permissions
        var migratedOrgUser = await organizationUserRepository.GetByIdAsync(orgUser.Id);
        Assert.NotNull(migratedOrgUser);
        Assert.Equal(orgUser.Id, migratedOrgUser.Id);
        Assert.Equal(OrganizationUserType.Custom, migratedOrgUser.Type);
        Assert.NotEqual(orgUser.Permissions, migratedOrgUser.Permissions);
        Assert.NotNull(migratedOrgUser.Permissions);
        Assert.Contains("editAssignedCollections", orgUser.Permissions);
        Assert.Contains("deleteAssignedCollections", orgUser.Permissions);
        Assert.DoesNotContain("editAssignedCollections", migratedOrgUser.Permissions);
        Assert.DoesNotContain("deleteAssignedCollections", migratedOrgUser.Permissions);
    }

    private async Task<OrganizationUser> SetupData(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        bool editAssignedCollections,
        bool deleteAssignedCollections)
    {
        var permissionJson =
            "{\"accessEventLogs\":false,\"accessImportExport\":false,\"accessReports\":false,\"createNewCollections\""
            + ":false,\"editAnyCollection\":false,\"deleteAnyCollection\":false,\""
            + "editAssignedCollections\":" + editAssignedCollections.ToString().ToLower() + ",\""
            + "deleteAssignedCollections\":" + deleteAssignedCollections.ToString().ToLower()
            + ",\"manageGroups\":false,\"managePolicies\":false,\"manageSso\":false,\""
            + "manageUsers\":false,\"manageResetPassword\":false,\"manageScim\":false}";

        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User 1",
            Email = $"test+{Guid.NewGuid()}@example.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 1,
            KdfMemory = 2,
            KdfParallelism = 3
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = user.Email, // TODO: EF does not enforce this being NOT NULl
            Plan = "Test", // TODO: EF does not enforce this being NOT NULl
            PrivateKey = "privatekey",
        });

        var orgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            ResetPasswordKey = "resetpasswordkey1",
            Type = OrganizationUserType.Custom,
            Permissions = permissionJson
        });

        return orgUser;
    }
}
