using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.MySqlMigrations.Migrations;
using Microsoft.EntityFrameworkCore;

namespace Bit.MySqlMigrations.IntegrationTests;

public class AccessAllCollectionGroupsMigrationScriptTests : IDisposable
{
    private readonly DatabaseContext _dbContext = new DatabaseContextFactory().CreateDbContext([]);

    [Fact]
    public void Migrate_Group_WithAccessAll_GivesCanEditAccessToAllCollections()
    {
        // Arrange: Create test data
        var organization = CreateOrganization();
        var group = CreateGroup(organization, accessAll: true);
        var collection1 = CreateCollection(organization);
        var collection2 = CreateCollection(organization);
        var collection3 = CreateCollection(organization);

        // Act: Apply data migration
        ApplyDataMigration();

        // Assert: Verify that data has been migrated correctly
        var updatedGroup = _dbContext.Groups
            .AsNoTracking()
            .FirstOrDefault(g => g.Id == group.Id);

        var groupCollectionAccess = _dbContext.CollectionGroups
            .AsNoTracking()
            .Where(cg => cg.GroupId == group.Id)
            .ToList();

        Assert.NotNull(updatedGroup);
        Assert.False(updatedGroup.AccessAll);

        Assert.Equal(3, groupCollectionAccess.Count);
        Assert.Contains(groupCollectionAccess, cg =>
            cg.CollectionId == collection1.Id &&
            CanEdit(cg));
        Assert.Contains(groupCollectionAccess, cg =>
            cg.CollectionId == collection2.Id &&
            CanEdit(cg));
        Assert.Contains(groupCollectionAccess, cg =>
            cg.CollectionId == collection3.Id &&
            CanEdit(cg));
    }

    private static string GetMigrationScript()
    {
        var assembly = typeof(FCAccessAllCollectionGroups).Assembly;
        return CoreHelpers.GetEmbeddedResourceContentsAsync(
            FCAccessAllCollectionGroups.AccessAllCollectionGroupsScript,
            assembly);
    }

    private void ApplyDataMigration()
    {
        var migrationScript = GetMigrationScript();
        _dbContext.Database.ExecuteSqlRaw(migrationScript);
    }

    private Organization CreateOrganization()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = $"Test Org {Guid.NewGuid()}"
        };

        _dbContext.Organizations.Add(organization);
        _dbContext.SaveChanges();

        return organization;
    }

    private Group CreateGroup(Organization organization, bool accessAll)
    {
        var group = new Group
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = $"Test Group {Guid.NewGuid()}",
            AccessAll = accessAll
        };

        _dbContext.Groups.Add(group);
        _dbContext.SaveChanges();

        return group;
    }

    private User CreateUser()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@example.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        };

        _dbContext.Users.Add(user);
        _dbContext.SaveChanges();

        return user;
    }

    private OrganizationUser CreateOrganizationUser(User user, Organization organization,
        OrganizationUserType type, bool accessAll,
        Permissions? permissions = null)
    {
        var organizationUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = type,
            AccessAll = accessAll,
            Permissions = permissions == null ? null : CoreHelpers.ClassToJsonData(permissions)
        };

        _dbContext.OrganizationUsers.Add(organizationUser);
        _dbContext.SaveChanges();

        return organizationUser;
    }

    private GroupUser CreateGroupUser(Group group, OrganizationUser organizationUser)
    {
        var groupUser = new GroupUser
        {
            GroupId = group.Id,
            OrganizationUserId = organizationUser.Id
        };

        _dbContext.GroupUsers.Add(groupUser);
        _dbContext.SaveChanges();

        return groupUser;
    }

    private Collection CreateCollection(Organization organization,
        IEnumerable<CollectionAccessSelection>? groups = null, IEnumerable<CollectionAccessSelection>? users = null)
    {
        var collection = new Collection
        {
            Id = Guid.NewGuid(),
            Name = $"Test collection {Guid.NewGuid()}",
            OrganizationId = organization.Id
        };

        _dbContext.Collections.Add(collection);
        _dbContext.SaveChanges();

        return collection;
    }

    private static bool CanEdit(CollectionGroup collectionGroup)
    {
        return collectionGroup is { HidePasswords: false, ReadOnly: false, Manage: false };
    }

    private void DeleteAllTestData(
        IEnumerable<Organization> organizations,
        IEnumerable<Group> groups,
        IEnumerable<User> users,
        IEnumerable<OrganizationUser> organizationUsers,
        IEnumerable<GroupUser> groupUsers)
    {
        _dbContext.GroupUsers.RemoveRange(groupUsers);
        _dbContext.OrganizationUsers.RemoveRange(organizationUsers);
        _dbContext.Users.RemoveRange(users);
        _dbContext.Groups.RemoveRange(groups);
        _dbContext.Organizations.RemoveRange(organizations);
        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
