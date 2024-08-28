using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.IntegrationTest.Services;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Migrations;

public class FinalFlexibleCollectionsDataMigrationsTests
{
    private const string _migrationName = "FinalFlexibleCollectionsDataMigrations";

    [DatabaseTheory, DatabaseData(MigrationName = _migrationName)]
    public async Task RunMigration_WithEditAssignedCollections_WithCustomUserType_MigratesToUserNullPermissions(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IMigrationTesterService migrationTester)
    {
        // Setup data
        var orgUser = await SetupData(
            userRepository, organizationRepository, organizationUserRepository,
            OrganizationUserType.Custom, editAssignedCollections: true, deleteAssignedCollections: false);

        // Run data migration
        migrationTester.ApplyMigration();

        // Assert that the user was migrated to a User type with null permissions
        var migratedOrgUser = await organizationUserRepository.GetByIdAsync(orgUser.Id);
        Assert.NotNull(migratedOrgUser);
        Assert.Equal(orgUser.Id, migratedOrgUser.Id);
        Assert.Equal(OrganizationUserType.User, migratedOrgUser.Type);
        Assert.Null(migratedOrgUser.Permissions);
    }

    [DatabaseTheory, DatabaseData(MigrationName = _migrationName)]
    public async Task RunMigration_WithDeleteAssignedCollections_WithCustomUserType_MigratesToUserNullPermissions(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IMigrationTesterService migrationTester)
    {
        // Setup data
        var orgUser = await SetupData(
            userRepository, organizationRepository, organizationUserRepository,
            OrganizationUserType.Custom, editAssignedCollections: false, deleteAssignedCollections: true);

        // Run data migration
        migrationTester.ApplyMigration();

        // Assert that the user was migrated to a User type with null permissions
        var migratedOrgUser = await organizationUserRepository.GetByIdAsync(orgUser.Id);
        Assert.NotNull(migratedOrgUser);
        Assert.Equal(orgUser.Id, migratedOrgUser.Id);
        Assert.Equal(OrganizationUserType.User, migratedOrgUser.Type);
        Assert.Null(migratedOrgUser.Permissions);
    }

    [DatabaseTheory, DatabaseData(MigrationName = _migrationName)]
    public async Task RunMigration_WithEditAndDeleteAssignedCollections_WithCustomUserType_MigratesToUserNullPermissions(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IMigrationTesterService migrationTester)
    {
        // Setup data
        var orgUser = await SetupData(
            userRepository, organizationRepository, organizationUserRepository,
            OrganizationUserType.Custom, editAssignedCollections: true, deleteAssignedCollections: true);

        // Run data migration
        migrationTester.ApplyMigration();

        // Assert that the user was migrated to a User type with null permissions
        var migratedOrgUser = await organizationUserRepository.GetByIdAsync(orgUser.Id);
        Assert.NotNull(migratedOrgUser);
        Assert.Equal(orgUser.Id, migratedOrgUser.Id);
        Assert.Equal(OrganizationUserType.User, migratedOrgUser.Type);
        Assert.Null(migratedOrgUser.Permissions);
    }

    [DatabaseTheory, DatabaseData(MigrationName = _migrationName)]
    public async Task RunMigration_WithoutAssignedCollectionsPermissions_WithCustomUserType_RemovesAssignedCollectionsPermissions(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IMigrationTesterService migrationTester)
    {
        // Setup data
        var orgUser = await SetupData(
            userRepository, organizationRepository, organizationUserRepository, OrganizationUserType.Custom,
            editAssignedCollections: false, deleteAssignedCollections: false, accessEventLogs: true);

        // Run data migration
        migrationTester.ApplyMigration();

        // Assert that the user kept the accessEventLogs permission and lost the editAssignedCollections and deleteAssignedCollections permissions
        var migratedOrgUser = await organizationUserRepository.GetByIdAsync(orgUser.Id);
        Assert.NotNull(migratedOrgUser);
        Assert.Equal(orgUser.Id, migratedOrgUser.Id);
        Assert.Equal(OrganizationUserType.Custom, migratedOrgUser.Type);
        Assert.NotEqual(orgUser.Permissions, migratedOrgUser.Permissions);
        Assert.NotNull(migratedOrgUser.Permissions);
        Assert.Contains("accessEventLogs", orgUser.Permissions);
        Assert.Contains("editAssignedCollections", orgUser.Permissions);
        Assert.Contains("deleteAssignedCollections", orgUser.Permissions);

        Assert.Contains("accessEventLogs", migratedOrgUser.Permissions);
        var migratedOrgUserPermissions = migratedOrgUser.GetPermissions();
        Assert.NotNull(migratedOrgUserPermissions);
        Assert.True(migratedOrgUserPermissions.AccessEventLogs);
        Assert.DoesNotContain("editAssignedCollections", migratedOrgUser.Permissions);
        Assert.DoesNotContain("deleteAssignedCollections", migratedOrgUser.Permissions);
    }

    [DatabaseTheory, DatabaseData(MigrationName = _migrationName)]
    public async Task RunMigration_WithAdminUserType_RemovesAssignedCollectionsPermissions(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IMigrationTesterService migrationTester)
    {
        // Setup data
        var orgUser = await SetupData(
            userRepository, organizationRepository, organizationUserRepository, OrganizationUserType.Admin,
            editAssignedCollections: false, deleteAssignedCollections: false, accessEventLogs: true);

        // Run data migration
        migrationTester.ApplyMigration();

        // Assert that the user kept the Admin type and lost the editAssignedCollections and deleteAssignedCollections
        // permissions but kept the accessEventLogs permission
        var migratedOrgUser = await organizationUserRepository.GetByIdAsync(orgUser.Id);
        Assert.NotNull(migratedOrgUser);
        Assert.Equal(orgUser.Id, migratedOrgUser.Id);
        Assert.Equal(OrganizationUserType.Admin, migratedOrgUser.Type);
        Assert.NotEqual(orgUser.Permissions, migratedOrgUser.Permissions);
        Assert.NotNull(migratedOrgUser.Permissions);
        Assert.Contains("accessEventLogs", orgUser.Permissions);
        Assert.Contains("editAssignedCollections", orgUser.Permissions);
        Assert.Contains("deleteAssignedCollections", orgUser.Permissions);

        Assert.Contains("accessEventLogs", migratedOrgUser.Permissions);
        Assert.True(migratedOrgUser.GetPermissions().AccessEventLogs);
        Assert.DoesNotContain("editAssignedCollections", migratedOrgUser.Permissions);
        Assert.DoesNotContain("deleteAssignedCollections", migratedOrgUser.Permissions);
    }

    [DatabaseTheory, DatabaseData(MigrationName = _migrationName)]
    public async Task RunMigration_WithoutAssignedCollectionsPermissions_DoesNothing(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IMigrationTesterService migrationTester)
    {
        // Setup data
        var orgUser = await SetupData(
            userRepository, organizationRepository, organizationUserRepository, OrganizationUserType.Custom,
            editAssignedCollections: false, deleteAssignedCollections: false, accessEventLogs: false);
        // Remove the editAssignedCollections and deleteAssignedCollections permissions
        orgUser.Permissions = JsonSerializer.Serialize(new
        {
            AccessEventLogs = false,
            AccessImportExport = false,
            AccessReports = false,
            CreateNewCollections = false,
            EditAnyCollection = false,
            DeleteAnyCollection = false,
            ManageGroups = false,
            ManagePolicies = false,
            ManageSso = false,
            ManageUsers = false,
            ManageResetPassword = false,
            ManageScim = false
        }, JsonHelpers.CamelCase);
        await organizationUserRepository.ReplaceAsync(orgUser);

        // Run data migration
        migrationTester.ApplyMigration();

        // Assert that the user kept the Admin type and no changes were made to the permissions
        var migratedOrgUser = await organizationUserRepository.GetByIdAsync(orgUser.Id);
        Assert.NotNull(migratedOrgUser);
        Assert.Equal(orgUser.Id, migratedOrgUser.Id);
        Assert.Equal(orgUser.Type, migratedOrgUser.Type);
        Assert.NotNull(migratedOrgUser.Permissions);
        // Assert that the permissions remain unchanged by comparing JSON data, ignoring the order of properties
        Assert.True(JToken.DeepEquals(JObject.Parse(orgUser.Permissions), JObject.Parse(migratedOrgUser.Permissions)));
    }

    [DatabaseTheory, DatabaseData(MigrationName = _migrationName)]
    public async Task RunMigration_HandlesNull(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IMigrationTesterService migrationTester)
    {
        // Setup data
        var orgUser = await SetupData(
            userRepository, organizationRepository, organizationUserRepository, OrganizationUserType.Custom,
            editAssignedCollections: false, deleteAssignedCollections: false, accessEventLogs: false);

        orgUser.Permissions = null;
        await organizationUserRepository.ReplaceAsync(orgUser);

        // Run data migration
        migrationTester.ApplyMigration();

        // Assert no changes
        var migratedOrgUser = await organizationUserRepository.GetByIdAsync(orgUser.Id);
        Assert.NotNull(migratedOrgUser);
        Assert.Equal(orgUser.Id, migratedOrgUser.Id);
        Assert.Equal(orgUser.Type, migratedOrgUser.Type);
        Assert.Null(migratedOrgUser.Permissions);
    }

    [DatabaseTheory, DatabaseData(MigrationName = _migrationName)]
    public async Task RunMigration_HandlesNullString(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IMigrationTesterService migrationTester)
    {
        // Setup data
        var orgUser = await SetupData(
            userRepository, organizationRepository, organizationUserRepository, OrganizationUserType.Custom,
            editAssignedCollections: false, deleteAssignedCollections: false, accessEventLogs: false);

        // We haven't tracked down the source of this yet but it does occur in our cloud database
        orgUser.Permissions = "NULL";
        await organizationUserRepository.ReplaceAsync(orgUser);

        // Run data migration
        migrationTester.ApplyMigration();

        // Assert no changes
        var migratedOrgUser = await organizationUserRepository.GetByIdAsync(orgUser.Id);
        Assert.NotNull(migratedOrgUser);
        Assert.Equal(orgUser.Id, migratedOrgUser.Id);
        Assert.Equal(orgUser.Type, migratedOrgUser.Type);
        Assert.Equal("NULL", migratedOrgUser.Permissions);
    }

    [DatabaseTheory, DatabaseData(MigrationName = _migrationName)]
    public async Task RunMigration_HandlesNonJsonValues(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IMigrationTesterService migrationTester)
    {
        // Setup data
        var orgUser = await SetupData(
            userRepository, organizationRepository, organizationUserRepository, OrganizationUserType.Custom,
            editAssignedCollections: false, deleteAssignedCollections: false, accessEventLogs: false);

        orgUser.Permissions = "asdfasdfasfd";
        await organizationUserRepository.ReplaceAsync(orgUser);

        // Run data migration
        migrationTester.ApplyMigration();

        // Assert no changes
        var migratedOrgUser = await organizationUserRepository.GetByIdAsync(orgUser.Id);
        Assert.NotNull(migratedOrgUser);
        Assert.Equal(orgUser.Id, migratedOrgUser.Id);
        Assert.Equal(orgUser.Type, migratedOrgUser.Type);
        Assert.Equal("asdfasdfasfd", migratedOrgUser.Permissions);
    }

    private async Task<OrganizationUser> SetupData(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        OrganizationUserType organizationUserType,
        bool editAssignedCollections,
        bool deleteAssignedCollections,
        bool accessEventLogs = false)
    {
        var permissions = new Permissions
        {
            AccessEventLogs = accessEventLogs,
            AccessImportExport = false,
            AccessReports = false,
            CreateNewCollections = false,
            EditAnyCollection = false,
            DeleteAnyCollection = false,
            EditAssignedCollections = editAssignedCollections,
            DeleteAssignedCollections = deleteAssignedCollections,
            ManageGroups = false,
            ManagePolicies = false,
            ManageSso = false,
            ManageUsers = false,
            ManageResetPassword = false,
            ManageScim = false
        };

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
            Type = organizationUserType,
            Permissions = JsonSerializer.Serialize(permissions, JsonHelpers.CamelCase)
        });

        return orgUser;
    }
}
