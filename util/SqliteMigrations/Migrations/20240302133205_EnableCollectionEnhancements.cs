using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

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
            var groupIdsWithAccessAll = _dbContext.Groups
                .Where(g => g.OrganizationId == organizationId && g.AccessAll == true)
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
            foreach (var group in groupIdsWithAccessAll)
            {
                var newCollectionGroups = _dbContext.Collections
                    .Where(c =>
                        c.OrganizationId == organizationId &&
                        !_dbContext.CollectionGroups.Any(cg => cg.CollectionId == c.Id && cg.GroupId == group))
                    .Select(c => new CollectionGroup
                    {
                        CollectionId = c.Id,
                        GroupId = group,
                        ReadOnly = false,
                        HidePasswords = false,
                        Manage = false
                    })
                    .ToList();
                _dbContext.CollectionGroups.AddRange(newCollectionGroups);
            }

            // Update Group to clear AccessAll flag
            _dbContext.Groups
                .Where(g => groupIdsWithAccessAll.Contains(g.Id))
                .ToList()
                .ForEach(g => g.AccessAll = false);

            // Save changes for Step 1
            _dbContext.SaveChanges();

            // Step 2: AccessAll migration for OrganizationUsers
            var orgUserIdsWithAccessAll = _dbContext.OrganizationUsers
                .Where(ou => ou.OrganizationId == organizationId && ou.AccessAll == true)
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
            foreach (var organizationUser in orgUserIdsWithAccessAll)
            {
                var newCollectionUsers = _dbContext.Collections
                    .Where(c =>
                        c.OrganizationId == organizationId &&
                        !_dbContext.CollectionUsers.Any(cu => cu.CollectionId == c.Id && cu.OrganizationUserId == organizationUser))
                    .Select(c => new CollectionUser
                    {
                        CollectionId = c.Id,
                        OrganizationUserId = organizationUser,
                        ReadOnly = false,
                        HidePasswords = false,
                        Manage = false
                    })
                    .ToList();
                _dbContext.CollectionUsers.AddRange(newCollectionUsers);
            }

            // Update OrganizationUser to clear AccessAll flag
            _dbContext.OrganizationUsers
                .Where(ou => orgUserIdsWithAccessAll.Contains(ou.Id))
                .ToList()
                .ForEach(ou => ou.AccessAll = false);

            // Save changes for Step 2
            _dbContext.SaveChanges();

            // Step 3: For all OrganizationUsers with Manager role or 'EditAssignedCollections' permission
            // update their existing CollectionUser rows and insert new rows with [Manage] = 1
            // and finally update all OrganizationUsers with Manager role to User role
            var managerOrgUsersIds = _dbContext.OrganizationUsers
                .Where(ou =>
                    ou.OrganizationId == organizationId &&
                    (ou.Type == OrganizationUserType.Manager ||
                     (ou.Permissions != null && EF.Functions.Like(ou.Permissions, "%\"editAssignedCollections\":true%"))))
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
            foreach (var manager in managerOrgUsersIds)
            {
                var newCollectionUsersWithManage = (from cg in _dbContext.CollectionGroups
                                                    join gu in _dbContext.GroupUsers on cg.GroupId equals gu.GroupId
                                                    where gu.OrganizationUserId == manager &&
                                                          !_dbContext.CollectionUsers.Any(cu =>
                                                              cu.CollectionId == cg.CollectionId &&
                                                              cu.OrganizationUserId == manager)
                                                    select new CollectionUser
                                                    {
                                                        CollectionId = cg.CollectionId,
                                                        OrganizationUserId = manager,
                                                        ReadOnly = false,
                                                        HidePasswords = false,
                                                        Manage = true
                                                    }).Distinct().ToList();
                _dbContext.CollectionUsers.AddRange(newCollectionUsersWithManage);
            }

            // Update OrganizationUser to migrate Managers to User role
            _dbContext.OrganizationUsers
                .Where(ou =>
                    managerOrgUsersIds.Contains(ou.Id) &&
                    ou.Type == OrganizationUserType.Manager)
                .ToList()
                .ForEach(ou => ou.Type = OrganizationUserType.User);

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
                    .FirstOrDefault(u => _dbContext.OrganizationUsers.Any(ou =>
                        ou.UserId == u.Id &&
                        ou.Id == organizationUserId &&
                        ou.Status == OrganizationUserStatusType.Confirmed));

                if (userToUpdate != null)
                {
                    userToUpdate.AccountRevisionDate = DateTime.UtcNow;
                }
            }

            // Save changes for Step 4
            _dbContext.SaveChanges();

            // Step 5: Enable FlexibleCollections for the Organization
            var organization = _dbContext.Organizations.SingleOrDefault(o => o.Id == organizationId);
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
