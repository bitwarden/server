using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.CollectionRepository;

public class CollectionRepositoryGetManagingUserIdsTests
{
    [DatabaseTheory, DatabaseData]
    public async Task GetManagingUserIdsAsync_DirectManageUser_Included_NonManageExcluded(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        var manager = await userRepository.CreateTestUserAsync("manager");
        var managerOrgUser = await CreateConfirmedUserAsync(organizationUserRepository, organization, manager);

        var viewer = await userRepository.CreateTestUserAsync("viewer");
        var viewerOrgUser = await CreateConfirmedUserAsync(organizationUserRepository, organization, viewer);

        var collection = new Collection { Name = "Leased", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(collection, groups: [], users:
        [
            new CollectionAccessSelection { Id = managerOrgUser.Id, Manage = true },
            new CollectionAccessSelection { Id = viewerOrgUser.Id, Manage = false, ReadOnly = true },
        ]);

        var userIds = await collectionRepository.GetManagingUserIdsAsync(collection.Id);

        Assert.Contains(manager.Id, userIds);
        Assert.DoesNotContain(viewer.Id, userIds);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManagingUserIdsAsync_GroupManageMember_Included(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository,
        IGroupRepository groupRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var member = await userRepository.CreateTestUserAsync("groupmember");
        var memberOrgUser = await CreateConfirmedUserAsync(organizationUserRepository, organization, member);

        var group = await groupRepository.CreateTestGroupAsync(organization);
        await groupRepository.UpdateUsersAsync(group.Id, [memberOrgUser.Id], DateTime.UtcNow);

        var collection = new Collection { Name = "Leased", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(collection, groups:
        [
            new CollectionAccessSelection { Id = group.Id, Manage = true },
        ], users: []);

        var userIds = await collectionRepository.GetManagingUserIdsAsync(collection.Id);

        Assert.Contains(member.Id, userIds);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManagingUserIdsAsync_OwnerWithAdminAccess_Included(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        organization.AllowAdminAccessToAllCollectionItems = true;
        await organizationRepository.ReplaceAsync(organization);

        var owner = await userRepository.CreateTestUserAsync("owner");
        await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = owner.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Owner,
        });

        // A collection the owner is not directly assigned to.
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);

        var userIds = await collectionRepository.GetManagingUserIdsAsync(collection.Id);

        Assert.Contains(owner.Id, userIds);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManagingUserIdsAsync_OwnerWithoutAdminAccess_Excluded(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        // The test-org helper enables admin access by default, so turn it off for this case.
        organization.AllowAdminAccessToAllCollectionItems = false;
        await organizationRepository.ReplaceAsync(organization);

        var owner = await userRepository.CreateTestUserAsync("owner");
        await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = owner.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Owner,
        });

        var collection = await collectionRepository.CreateTestCollectionAsync(organization);

        var userIds = await collectionRepository.GetManagingUserIdsAsync(collection.Id);

        Assert.DoesNotContain(owner.Id, userIds);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManagingUserIdsAsync_CustomEditAnyCollection_Included_WithoutPermissionExcluded(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        // Admin access is off so that EditAnyCollection is the only thing that can grant Manage here.
        organization.AllowAdminAccessToAllCollectionItems = false;
        await organizationRepository.ReplaceAsync(organization);

        var editor = await userRepository.CreateTestUserAsync("editany");
        await organizationUserRepository.CreateAsync(CreateCustomUser(organization, editor,
            new Permissions { EditAnyCollection = true }));

        var manager = await userRepository.CreateTestUserAsync("managegroups");
        await organizationUserRepository.CreateAsync(CreateCustomUser(organization, manager,
            new Permissions { ManageGroups = true }));

        // A collection neither user is directly assigned to.
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);

        var userIds = await collectionRepository.GetManagingUserIdsAsync(collection.Id);

        Assert.Contains(editor.Id, userIds);
        Assert.DoesNotContain(manager.Id, userIds);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManagingUserIdsAsync_UnconfirmedMembers_Excluded(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository,
        IGroupRepository groupRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        var member = await userRepository.CreateTestUserAsync("member");
        var memberOrgUser = await CreateConfirmedUserAsync(organizationUserRepository, organization, member);

        // An accepted Owner would manage by role, by assignment and by group if it were confirmed.
        var accepted = await userRepository.CreateTestUserAsync("accepted");
        var acceptedOrgUser =
            await organizationUserRepository.CreateAcceptedTestOrganizationUserAsync(organization, accepted);

        var invitedOrgUser = await organizationUserRepository.CreateTestOrganizationUserInviteAsync(organization);

        var group = await groupRepository.CreateTestGroupAsync(organization);
        await groupRepository.UpdateUsersAsync(group.Id, [acceptedOrgUser.Id], DateTime.UtcNow);

        var collection = new Collection { Name = "Leased", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(collection, groups:
        [
            new CollectionAccessSelection { Id = group.Id, Manage = true },
        ], users:
        [
            new CollectionAccessSelection { Id = memberOrgUser.Id, Manage = true },
            new CollectionAccessSelection { Id = acceptedOrgUser.Id, Manage = true },
            new CollectionAccessSelection { Id = invitedOrgUser.Id, Manage = true },
        ]);

        var userIds = await collectionRepository.GetManagingUserIdsAsync(collection.Id);

        Assert.Equal(member.Id, Assert.Single(userIds));
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManagingUserIdsAsync_ManageByEveryRoute_ReturnsTheUserOnce(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository,
        IGroupRepository groupRepository)
    {
        // The test-org helper allows admin access, so a confirmed Owner manages by role as well.
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        var owner = await userRepository.CreateTestUserAsync("owner");
        var ownerOrgUser =
            await organizationUserRepository.CreateTestOrganizationUserAsync(organization, owner);

        var group = await groupRepository.CreateTestGroupAsync(organization);
        await groupRepository.UpdateUsersAsync(group.Id, [ownerOrgUser.Id], DateTime.UtcNow);

        var collection = new Collection { Name = "Leased", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(collection, groups:
        [
            new CollectionAccessSelection { Id = group.Id, Manage = true },
        ], users:
        [
            new CollectionAccessSelection { Id = ownerOrgUser.Id, Manage = true },
        ]);

        var userIds = await collectionRepository.GetManagingUserIdsAsync(collection.Id);

        Assert.Equal(owner.Id, Assert.Single(userIds));
    }

    private static OrganizationUser CreateCustomUser(Organization organization, User user, Permissions permissions)
    {
        var organizationUser = new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Custom,
        };

        organizationUser.SetPermissions(permissions);

        return organizationUser;
    }

    private static Task<OrganizationUser> CreateConfirmedUserAsync(
        IOrganizationUserRepository organizationUserRepository, Organization organization, User user)
        => organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
        });
}
