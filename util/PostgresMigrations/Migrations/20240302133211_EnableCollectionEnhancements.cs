using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

public partial class EnableCollectionEnhancements : Migration
{
    private DatabaseContext _dbContext = new DatabaseContextFactory().CreateDbContext([]);

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var organizations = _dbContext.Organizations.Where(o => o.FlexibleCollections == false).ToList();
        foreach (var organization in organizations)
        {
            EnableOrganizationCollectionEnhancements(organization.Id);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new Exception("Irreversible migration");
    }

    /// <summary>
    /// This method is a copy of IOrganizationRepository.EnableCollectionEnhancements(Guid organizationId)
    /// The only difference is the added Step 5 to update <c>Organization.FlexibleCollections = true</c>
    /// </summary>
    private void EnableOrganizationCollectionEnhancements(Guid organizationId)
    {
        using var transaction = _dbContext.Database.BeginTransaction();

        try
        {
            // Step 1: AccessAll migration for Groups
            var groupsWithAccessAll = _dbContext.Groups
                .Where(g => g.OrganizationId == organizationId && g.AccessAll == true)
                .ToList();
            var groupIdsWithAccessAll = groupsWithAccessAll
                .Select(g => g.Id)
                .ToList();

            // Update existing CollectionGroup rows
            _dbContext.CollectionGroups
                .Where(cg =>
                    groupIdsWithAccessAll.Contains(cg.GroupId) &&
                    cg.Collection.OrganizationId == organizationId)
                .ToList()
                .ForEach(cg =>
                {
                    cg.ReadOnly = false;
                    cg.HidePasswords = false;
                    cg.Manage = false;
                });

            // Insert new rows into CollectionGroup
            foreach (var group in groupsWithAccessAll)
            {
                var newCollectionGroups = _dbContext.Collections
                    .Where(c =>
                        c.OrganizationId == organizationId &&
                        c.CollectionGroups.All(cg => cg.GroupId != group.Id))
                    .Select(c => new CollectionGroup
                    {
                        CollectionId = c.Id,
                        GroupId = group.Id,
                        ReadOnly = false,
                        HidePasswords = false,
                        Manage = false
                    })
                    .ToList();
                _dbContext.CollectionGroups.AddRange(newCollectionGroups);

                // Update Group to clear AccessAll flag
                group.AccessAll = false;
            }

            // Save changes for Step 1
            _dbContext.SaveChanges();

            // Step 2: AccessAll migration for OrganizationUsers
            var orgUsersWithAccessAll = _dbContext.OrganizationUsers
                .Where(ou => ou.OrganizationId == organizationId && ou.AccessAll == true)
                .ToList();
            var orgUserIdsWithAccessAll = orgUsersWithAccessAll
                .Select(ou => ou.Id)
                .ToList();

            // Update existing CollectionUser rows
            _dbContext.CollectionUsers
                .Where(cu =>
                    orgUserIdsWithAccessAll.Contains(cu.OrganizationUserId) &&
                    cu.Collection.OrganizationId == organizationId)
                .ToList()
                .ForEach(cu =>
                {
                    cu.ReadOnly = false;
                    cu.HidePasswords = false;
                    cu.Manage = false;
                });

            // Insert new rows into CollectionUser
            foreach (var organizationUser in orgUsersWithAccessAll)
            {
                var newCollectionUsers = _dbContext.Collections
                    .Where(c =>
                        c.OrganizationId == organizationId &&
                        c.CollectionUsers.All(cu => cu.OrganizationUserId != organizationUser.Id))
                    .Select(c => new CollectionUser
                    {
                        CollectionId = c.Id,
                        OrganizationUserId = organizationUser.Id,
                        ReadOnly = false,
                        HidePasswords = false,
                        Manage = false
                    })
                    .ToList();
                _dbContext.CollectionUsers.AddRange(newCollectionUsers);

                // Update OrganizationUser to clear AccessAll flag
                organizationUser.AccessAll = false;
            }

            // Save changes for Step 2
            _dbContext.SaveChanges();

            // Step 3: For all OrganizationUsers with Manager role or 'EditAssignedCollections' permission
            // update their existing CollectionUser rows and insert new rows with [Manage] = 1
            // and finally update all OrganizationUsers with Manager role to User role
            var managerOrgUsers = _dbContext.OrganizationUsers
                .Where(ou =>
                    ou.OrganizationId == organizationId &&
                    (ou.Type == OrganizationUserType.Manager ||
                     (ou.Type == OrganizationUserType.Custom &&
                      ou.Permissions != null &&
                      EF.Functions.Like(ou.Permissions, "%\"editAssignedCollections\":true%"))))
                .ToList();
            var managerOrgUsersIds = managerOrgUsers
                .Select(ou => ou.Id)
                .ToList();

            // Update CollectionUser rows with Manage = true
            _dbContext.CollectionUsers
                .Where(cu => managerOrgUsersIds.Contains(cu.OrganizationUserId))
                .ToList()
                .ForEach(cu =>
                {
                    cu.ReadOnly = false;
                    cu.HidePasswords = false;
                    cu.Manage = true;
                });

            // Insert rows into CollectionUser with Manage = true
            foreach (var manager in managerOrgUsers)
            {
                var newCollectionUsersWithManage = (from cg in _dbContext.CollectionGroups
                                                    join gu in _dbContext.GroupUsers on cg.GroupId equals gu.GroupId
                                                    where gu.OrganizationUserId == manager.Id &&
                                                          !_dbContext.CollectionUsers.Any(cu =>
                                                              cu.CollectionId == cg.CollectionId &&
                                                              cu.OrganizationUserId == manager.Id)
                                                    select new CollectionUser
                                                    {
                                                        CollectionId = cg.CollectionId,
                                                        OrganizationUserId = manager.Id,
                                                        ReadOnly = false,
                                                        HidePasswords = false,
                                                        Manage = true
                                                    }).Distinct().ToList();
                _dbContext.CollectionUsers.AddRange(newCollectionUsersWithManage);

                // Update OrganizationUser to migrate Managers to User role
                if (manager.Type == OrganizationUserType.Manager)
                {
                    manager.Type = OrganizationUserType.User;
                }
            }

            // Save changes for Step 3
            _dbContext.SaveChanges();

            // Step 4: Bump AccountRevisionDate for all OrganizationUsers updated in the previous steps
            // Get all OrganizationUserIds that have AccessAll from Group
            var accessAllGroupOrgUserIds = _dbContext.GroupUsers
                .Where(gu => groupIdsWithAccessAll.Contains(gu.GroupId))
                .Select(gu => gu.OrganizationUserId)
                .ToList();

            // Combine and union the distinct OrganizationUserIds from all steps into a single variable
            var orgUsersToBump = accessAllGroupOrgUserIds
                .Union(orgUserIdsWithAccessAll)
                .Union(managerOrgUsersIds)
                .Distinct()
                .ToList();

            foreach (var organizationUserId in orgUsersToBump)
            {
                var userToUpdate = _dbContext.Users
                    .Join(_dbContext.OrganizationUsers,
                        u => u.Id,
                        ou => ou.UserId,
                        (u, ou) => new { User = u, OrganizationUser = ou })
                    .Where(pair => pair.OrganizationUser.Id == organizationUserId &&
                                   pair.OrganizationUser.Status == OrganizationUserStatusType.Confirmed)
                    .Select(pair => pair.User)
                    .FirstOrDefault();

                if (userToUpdate != null)
                {
                    userToUpdate.AccountRevisionDate = DateTime.UtcNow;
                }
            }

            // Save changes for Step 4
            _dbContext.SaveChanges();

            // Step 5: Enable FlexibleCollections for the Organization
            var organization = _dbContext.Organizations.FirstOrDefault(o => o.Id == organizationId);
            organization.FlexibleCollections = true;
            organization.RevisionDate = DateTime.UtcNow;

            // Save changes for Step 5
            _dbContext.SaveChanges();

            transaction.Commit();
        }
        catch
        {
            // Rollback transaction
            transaction.Rollback();
            throw new Exception("Error occurred. Rolling back transaction.");
        }
    }
}
