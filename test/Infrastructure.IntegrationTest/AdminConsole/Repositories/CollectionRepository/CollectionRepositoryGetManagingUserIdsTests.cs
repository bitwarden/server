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
