using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture;
using Xunit;
using EfRepo = Bit.Infrastructure.EntityFramework.Repositories;
using Organization = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Infrastructure.EFIntegration.Test.AdminConsole.Migrations;

public class FinalFlexibleCollectionsDataMigrationsTests
{
    private const string _migrationName = "FinalFlexibleCollectionsDataMigrations";

    [CiSkippedTheory, EfOrganizationUserAutoData]
    public async Task RunMigration_WithEditAssignedCollectionsUser_Works(OrganizationUser orgUser, User user, Organization org,
        List<EfRepo.UserRepository> efUserRepos,
        List<EfRepo.OrganizationRepository> efOrgRepos,
        List<EfRepo.OrganizationUserRepository> efOrgUserRepos)
    {
        var editAssignedCollectionsPermissionJson =
            "{\"accessEventLogs\":false,\"accessImportExport\":false,\"accessReports\":false,\"createNewCollections\""
            + ":false,\"editAnyCollection\":false,\"deleteAnyCollection\":false,\"editAssignedCollections\":true,\""
            + "deleteAssignedCollections\":false,\"manageGroups\":false,\"managePolicies\":false,\"manageSso\":false,\""
            + "manageUsers\":false,\"manageResetPassword\":false,\"manageScim\":false}";

        var initialOrgUsers = new List<OrganizationUser>();
        var migratedOrgUsers = new List<OrganizationUser>();
        foreach (var sut in efOrgUserRepos)
        {
            var i = efOrgUserRepos.IndexOf(sut);
            var postEfUser = await efUserRepos[i].CreateAsync(user);
            var postEfOrg = await efOrgRepos[i].CreateAsync(org);
            sut.ClearChangeTracking();

            orgUser.UserId = postEfUser.Id;
            orgUser.OrganizationId = postEfOrg.Id;
            orgUser.Type = OrganizationUserType.Custom;
            orgUser.Permissions = editAssignedCollectionsPermissionJson;

            // Create the initial organization user and add it to the list
            var initialOrgUser = await sut.CreateAsync(orgUser);
            initialOrgUsers.Add(initialOrgUser);
            sut.ClearChangeTracking();

            // Run the migration
            sut.RunMigration(_migrationName);

            // Get the migrated organization user and add it to the list
            var migratedOrgUser = await sut.GetByIdAsync(initialOrgUser.Id);
            migratedOrgUsers.Add(migratedOrgUser);
            sut.ClearChangeTracking();
        }

        foreach (var iou in initialOrgUsers)
        {
            // Find the migrated organization user that corresponds to the initial organization user
            var mou = migratedOrgUsers.FirstOrDefault(mou => mou.Id == iou.Id);

            // Assert that the migrated organization user has the same id as the initial organization user
            Assert.Equal(iou.Id, mou.Id);

            // Assert that the initial organization user was created with the correct Type and Permissions
            Assert.Equal(OrganizationUserType.Custom, iou.Type);
            Assert.Equal(editAssignedCollectionsPermissionJson, iou.Permissions);

            // Assert that the migrated organization user has the User Type and Null Permissions
            Assert.Equal(OrganizationUserType.User, mou.Type);
            Assert.Null(mou.Permissions);
        }
    }
}
