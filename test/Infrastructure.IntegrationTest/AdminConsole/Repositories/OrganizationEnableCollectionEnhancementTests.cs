using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Xunit;
using EfOrganizationRepository = Bit.Infrastructure.EntityFramework.Repositories.OrganizationRepository;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories;

public class OrganizationEnableCollectionEnhancementTests
{
    [DatabaseTheory, DatabaseData]
    public async Task Migrate_User_WithAccessAll_GivesCanEditAccessToAllCollections(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        if (IsEfDatabase(organizationRepository))
        {
            return;
        }

        var user = await CreateUser(userRepository);
        var organization = await CreateOrganization(organizationRepository);
        var orgUser = await CreateOrganizationUser(user, organization, OrganizationUserType.User, accessAll: true, organizationUserRepository);
        var collection1 = await CreateCollection(organization, collectionRepository);
        var collection2 = await CreateCollection(organization, collectionRepository);
        var collection3 = await CreateCollection(organization, collectionRepository);

        await organizationRepository.EnableCollectionEnhancements(organization.Id);

        var (updatedOrgUser, collectionAccessSelections) = await organizationUserRepository.GetDetailsByIdWithCollectionsAsync(orgUser.Id);

        Assert.False(updatedOrgUser.AccessAll);

        Assert.Equal(3, collectionAccessSelections.Count);
        Assert.Contains(collectionAccessSelections, cas =>
            cas.Id == collection1.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: false });
        Assert.Contains(collectionAccessSelections, cas =>
            cas.Id == collection2.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: false });
        Assert.Contains(collectionAccessSelections, cas =>
            cas.Id == collection3.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: false });
    }

    [DatabaseTheory, DatabaseData]
    public async Task Migrate_Group_WithAccessAll_GivesCanEditAccessToAllCollections(
        IGroupRepository groupRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository)
    {
        if (IsEfDatabase(organizationRepository))
        {
            return;
        }

        var organization = await CreateOrganization(organizationRepository);
        var group = await CreateGroup(organization, accessAll: true, groupRepository);
        var collection1 = await CreateCollection(organization, collectionRepository);
        var collection2 = await CreateCollection(organization, collectionRepository);
        var collection3 = await CreateCollection(organization, collectionRepository);

        await organizationRepository.EnableCollectionEnhancements(organization.Id);

        var (updatedGroup, collectionAccessSelections) = await groupRepository.GetByIdWithCollectionsAsync(group.Id);

        Assert.False(updatedGroup.AccessAll);

        Assert.Equal(3, collectionAccessSelections.Count);
        Assert.Contains(collectionAccessSelections, cas =>
            cas.Id == collection1.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: false });
        Assert.Contains(collectionAccessSelections, cas =>
            cas.Id == collection2.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: false });
        Assert.Contains(collectionAccessSelections, cas =>
            cas.Id == collection3.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: false });
    }

    [DatabaseTheory, DatabaseData]
    public async Task Migrate_Manager_WithAccessAll_GivesCanManageAccessToAllCollections(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        if (IsEfDatabase(organizationRepository))
        {
            return;
        }

        var user = await CreateUser(userRepository);
        var organization = await CreateOrganization(organizationRepository);
        var orgUser = await CreateOrganizationUser(user, organization, OrganizationUserType.Manager, accessAll: true, organizationUserRepository);
        var collection1 = await CreateCollection(organization, collectionRepository);
        var collection2 = await CreateCollection(organization, collectionRepository);
        var collection3 = await CreateCollection(organization, collectionRepository);

        await organizationRepository.EnableCollectionEnhancements(organization.Id);

        var (updatedOrgUser, collectionAccessSelections) = await organizationUserRepository.GetDetailsByIdWithCollectionsAsync(orgUser.Id);

        Assert.False(updatedOrgUser.AccessAll);
        Assert.Equal(OrganizationUserType.User, updatedOrgUser.Type);

        Assert.Equal(3, collectionAccessSelections.Count);
        Assert.Contains(collectionAccessSelections, cas =>
            cas.Id == collection1.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: true });
        Assert.Contains(collectionAccessSelections, cas =>
            cas.Id == collection2.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: true });
        Assert.Contains(collectionAccessSelections, cas =>
            cas.Id == collection3.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: true });
    }

    [DatabaseTheory, DatabaseData]
    public async Task Migrate_Manager_WithoutAccessAll_GivesCanManageAccessToAssignedCollections(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        if (IsEfDatabase(organizationRepository))
        {
            return;
        }

        var user = await CreateUser(userRepository);
        var organization = await CreateOrganization(organizationRepository);
        var orgUser = await CreateOrganizationUser(user, organization, OrganizationUserType.Manager, accessAll: false, organizationUserRepository);
        var collection1 = await CreateCollection(organization, collectionRepository, null, [new CollectionAccessSelection { Id = orgUser.Id, HidePasswords = true, ReadOnly = false, Manage = false }]);
        var collection2 = await CreateCollection(organization, collectionRepository, null, [new CollectionAccessSelection { Id = orgUser.Id, HidePasswords = false, ReadOnly = false, Manage = false }]);
        var collection3 = await CreateCollection(organization, collectionRepository); // no access

        await organizationRepository.EnableCollectionEnhancements(organization.Id);

        var (updatedOrgUser, collectionAccessSelections) = await organizationUserRepository.GetDetailsByIdWithCollectionsAsync(orgUser.Id);

        Assert.Equal(OrganizationUserType.User, updatedOrgUser.Type);

        Assert.Equal(2, collectionAccessSelections.Count);
        Assert.Contains(collectionAccessSelections, cas =>
            cas.Id == collection1.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: true });
        Assert.Contains(collectionAccessSelections, cas =>
            cas.Id == collection2.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: true });
        Assert.DoesNotContain(collectionAccessSelections, cas =>
            cas.Id == collection3.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task Migrate_Manager_WithoutAccessAll_GivesCanManageAccess_ToGroupAssignedCollections(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository,
        IGroupRepository groupRepository)
    {
        if (IsEfDatabase(organizationRepository))
        {
            return;
        }

        var user = await CreateUser(userRepository);
        var organization = await CreateOrganization(organizationRepository);
        var orgUser = await CreateOrganizationUser(user, organization, OrganizationUserType.Manager, accessAll: false, organizationUserRepository);
        var group = await CreateGroup(organization, accessAll: false, groupRepository, orgUser);

        var collection1 = await CreateCollection(organization, collectionRepository, new []{new CollectionAccessSelection { Id = group.Id, HidePasswords = false, Manage = false, ReadOnly = false}});
        var collection2 = await CreateCollection(organization, collectionRepository, new []{new CollectionAccessSelection { Id = group.Id, HidePasswords = false, Manage = false, ReadOnly = false}});
        var collection3 = await CreateCollection(organization, collectionRepository); // no access

        await organizationRepository.EnableCollectionEnhancements(organization.Id);

        var (updatedOrgUser, updatedUserAccess) = await organizationUserRepository.GetDetailsByIdWithCollectionsAsync(orgUser.Id);

        // Assert: orgUser should be downgraded from Manager to User
        // and given Can Manage permissions over all group assigned collections
        Assert.Equal(OrganizationUserType.User, updatedOrgUser.Type);
        Assert.Equal(2, updatedUserAccess.Count);
        Assert.Contains(updatedUserAccess, cas =>
            cas.Id == collection1.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: true });
        Assert.Contains(updatedUserAccess, cas =>
            cas.Id == collection2.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: true });
        Assert.DoesNotContain(updatedUserAccess, cas =>
            cas.Id == collection3.Id);

        // Assert: group should only have Can Edit permissions (making sure no side-effects from the Manager migration)
        var (updatedGroup, updatedGroupAccess) = await groupRepository.GetByIdWithCollectionsAsync(group.Id);
        Assert.Equal(2, updatedGroupAccess.Count);
        Assert.Contains(updatedGroupAccess, cas =>
            cas.Id == collection1.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: false });
        Assert.Contains(updatedGroupAccess, cas =>
            cas.Id == collection2.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: false });
        Assert.DoesNotContain(updatedGroupAccess, cas =>
            cas.Id == collection3.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task Migrate_Manager_WithoutAccessAll_InGroupWithAccessAll_GivesCanManageAccessToAllCollections(
        IUserRepository userRepository,
        IGroupRepository groupRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        if (IsEfDatabase(organizationRepository))
        {
            return;
        }

        var user = await CreateUser(userRepository);
        var organization = await CreateOrganization(organizationRepository);
        var orgUser = await CreateOrganizationUser(user, organization, OrganizationUserType.Manager, accessAll: false, organizationUserRepository);

        // Use 2 groups to test for overlapping access
        var group1 = await CreateGroup(organization, accessAll: true, groupRepository, orgUser);
        var group2 = await CreateGroup(organization, accessAll: true, groupRepository, orgUser);

        var collection1 = await CreateCollection(organization, collectionRepository);
        var collection2 = await CreateCollection(organization, collectionRepository);
        var collection3 = await CreateCollection(organization, collectionRepository);

        await organizationRepository.EnableCollectionEnhancements(organization.Id);

        var (updatedOrgUser, collectionAccessSelections) = await organizationUserRepository.GetDetailsByIdWithCollectionsAsync(orgUser.Id);

        Assert.Equal(OrganizationUserType.User, updatedOrgUser.Type);

        // OrgUser has direct Can Manage access to all collections
        Assert.Equal(3, collectionAccessSelections.Count);
        Assert.Contains(collectionAccessSelections, cas =>
            cas.Id == collection1.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: true });
        Assert.Contains(collectionAccessSelections, cas =>
            cas.Id == collection2.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: true });
        Assert.Contains(collectionAccessSelections, cas =>
            cas.Id == collection3.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: true });

        // Assert: group should only have Can Edit permissions (making sure no side-effects from the Manager migration)
        var (updatedGroup1, updatedGroupAccess1) = await groupRepository.GetByIdWithCollectionsAsync(group1.Id);
        Assert.Equal(3, updatedGroupAccess1.Count);
        Assert.Contains(updatedGroupAccess1, cas =>
            cas.Id == collection1.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: false });
        Assert.Contains(updatedGroupAccess1, cas =>
            cas.Id == collection2.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: false });
        Assert.Contains(updatedGroupAccess1, cas =>
            cas.Id == collection3.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: false });

        var (updatedGroup2, updatedGroupAccess2) = await groupRepository.GetByIdWithCollectionsAsync(group2.Id);
        Assert.Equal(3, updatedGroupAccess2.Count);
        Assert.Contains(updatedGroupAccess2, cas =>
            cas.Id == collection1.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: false });
        Assert.Contains(updatedGroupAccess2, cas =>
            cas.Id == collection2.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: false });
        Assert.Contains(updatedGroupAccess2, cas =>
            cas.Id == collection3.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: false });
    }

    [DatabaseTheory, DatabaseData]
    public async Task Migrate_CustomUser_WithEditAssignedCollections_WithAccessAll_GivesCanManageAccessToAllCollections(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        if (IsEfDatabase(organizationRepository))
        {
            return;
        }

        var user = await CreateUser(userRepository);
        var organization = await CreateOrganization(organizationRepository);
        var orgUser = await CreateOrganizationUser(user, organization, OrganizationUserType.Custom, accessAll: true,
            organizationUserRepository, new Permissions { EditAssignedCollections = true});
        var collection1 = await CreateCollection(organization, collectionRepository);
        var collection2 = await CreateCollection(organization, collectionRepository);
        var collection3 = await CreateCollection(organization, collectionRepository);

        await organizationRepository.EnableCollectionEnhancements(organization.Id);

        var (updatedOrgUser, collectionAccessSelections) = await organizationUserRepository.GetDetailsByIdWithCollectionsAsync(orgUser.Id);

        Assert.False(updatedOrgUser.AccessAll);
        // Note: custom users do not have their types changed yet, this was done in code with a migration to follow
        Assert.Equal(OrganizationUserType.Custom, updatedOrgUser.Type);

        Assert.Equal(3, collectionAccessSelections.Count);
        Assert.Contains(collectionAccessSelections, cas =>
            cas.Id == collection1.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: true });
        Assert.Contains(collectionAccessSelections, cas =>
            cas.Id == collection2.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: true });
        Assert.Contains(collectionAccessSelections, cas =>
            cas.Id == collection3.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: true });
    }

    [DatabaseTheory, DatabaseData]
    public async Task Migrate_CustomUser_WithEditAssignedCollections_WithoutAccessAll_GivesCanManageAccessToAssignedCollections(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        if (IsEfDatabase(organizationRepository))
        {
            return;
        }

        var user = await CreateUser(userRepository);
        var organization = await CreateOrganization(organizationRepository);
        var orgUser = await CreateOrganizationUser(user, organization, OrganizationUserType.Custom, accessAll: false,
            organizationUserRepository, new Permissions { EditAssignedCollections = true});
        var collection1 = await CreateCollection(organization, collectionRepository, null, [new CollectionAccessSelection { Id = orgUser.Id, HidePasswords = true, ReadOnly = false, Manage = false }]);
        var collection2 = await CreateCollection(organization, collectionRepository, null, [new CollectionAccessSelection { Id = orgUser.Id, HidePasswords = false, ReadOnly = false, Manage = false }]);
        var collection3 = await CreateCollection(organization, collectionRepository); // no access

        await organizationRepository.EnableCollectionEnhancements(organization.Id);

        var (updatedOrgUser, collectionAccessSelections) = await organizationUserRepository.GetDetailsByIdWithCollectionsAsync(orgUser.Id);

        Assert.Equal(OrganizationUserType.Custom, updatedOrgUser.Type);

        Assert.Equal(2, collectionAccessSelections.Count);
        Assert.Contains(collectionAccessSelections, cas =>
            cas.Id == collection1.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: true });
        Assert.Contains(collectionAccessSelections, cas =>
            cas.Id == collection2.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: true });
        Assert.DoesNotContain(collectionAccessSelections, cas =>
            cas.Id == collection3.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task Migrate_CustomUser_WithEditAssignedCollections_WithoutAccessAll_GivesCanManageAccess_ToGroupAssignedCollections(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository,
        IGroupRepository groupRepository)
    {
        if (IsEfDatabase(organizationRepository))
        {
            return;
        }

        var user = await CreateUser(userRepository);
        var organization = await CreateOrganization(organizationRepository);
        var orgUser = await CreateOrganizationUser(user, organization, OrganizationUserType.Custom, accessAll: false,
            organizationUserRepository, new Permissions { EditAssignedCollections = true});
        var group = await CreateGroup(organization, accessAll: false, groupRepository, orgUser);

        var collection1 = await CreateCollection(organization, collectionRepository, new []{new CollectionAccessSelection { Id = group.Id, HidePasswords = false, Manage = false, ReadOnly = false}});
        var collection2 = await CreateCollection(organization, collectionRepository, new []{new CollectionAccessSelection { Id = group.Id, HidePasswords = false, Manage = false, ReadOnly = false}});
        var collection3 = await CreateCollection(organization, collectionRepository); // no access

        await organizationRepository.EnableCollectionEnhancements(organization.Id);

        var (updatedOrgUser, updatedUserAccess) = await organizationUserRepository.GetDetailsByIdWithCollectionsAsync(orgUser.Id);

        // Assert: user should be given Can Manage permissions over all group assigned collections
        Assert.Equal(OrganizationUserType.Custom, updatedOrgUser.Type);
        Assert.Equal(2, updatedUserAccess.Count);
        Assert.Contains(updatedUserAccess, cas =>
            cas.Id == collection1.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: true });
        Assert.Contains(updatedUserAccess, cas =>
            cas.Id == collection2.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: true });
        Assert.DoesNotContain(updatedUserAccess, cas =>
            cas.Id == collection3.Id);

        // Assert: group should only have Can Edit permissions (making sure no side-effects from the Manager migration)
        var (updatedGroup, updatedGroupAccess) = await groupRepository.GetByIdWithCollectionsAsync(group.Id);
        Assert.Equal(2, updatedGroupAccess.Count);
        Assert.Contains(updatedGroupAccess, cas =>
            cas.Id == collection1.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: false });
        Assert.Contains(updatedGroupAccess, cas =>
            cas.Id == collection2.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: false });
        Assert.DoesNotContain(updatedGroupAccess, cas =>
            cas.Id == collection3.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task Migrate_CustomUser_WithEditAssignedCollections_WithoutAccessAll_InGroupWithAccessAll_GivesCanManageAccessToAllCollections(
        IUserRepository userRepository,
        IGroupRepository groupRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        if (IsEfDatabase(organizationRepository))
        {
            return;
        }

        var user = await CreateUser(userRepository);
        var organization = await CreateOrganization(organizationRepository);
        var orgUser = await CreateOrganizationUser(user, organization, OrganizationUserType.Custom, accessAll: false,
            organizationUserRepository, new Permissions { EditAssignedCollections = true});

        // Use 2 groups to test for overlapping access
        var group1 = await CreateGroup(organization, accessAll: true, groupRepository, orgUser);
        var group2 = await CreateGroup(organization, accessAll: true, groupRepository, orgUser);

        var collection1 = await CreateCollection(organization, collectionRepository);
        var collection2 = await CreateCollection(organization, collectionRepository);
        var collection3 = await CreateCollection(organization, collectionRepository);

        await organizationRepository.EnableCollectionEnhancements(organization.Id);

        var (updatedOrgUser, collectionAccessSelections) = await organizationUserRepository.GetDetailsByIdWithCollectionsAsync(orgUser.Id);

        Assert.Equal(OrganizationUserType.Custom, updatedOrgUser.Type);

        // OrgUser has direct Can Manage access to all collections
        Assert.Equal(3, collectionAccessSelections.Count);
        Assert.Contains(collectionAccessSelections, cas =>
            cas.Id == collection1.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: true });
        Assert.Contains(collectionAccessSelections, cas =>
            cas.Id == collection2.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: true });
        Assert.Contains(collectionAccessSelections, cas =>
            cas.Id == collection3.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: true });

        // Assert: group should only have Can Edit permissions (making sure no side-effects from the Manager migration)
        var (updatedGroup1, updatedGroupAccess1) = await groupRepository.GetByIdWithCollectionsAsync(group1.Id);
        Assert.Equal(3, updatedGroupAccess1.Count);
        Assert.Contains(updatedGroupAccess1, cas =>
            cas.Id == collection1.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: false });
        Assert.Contains(updatedGroupAccess1, cas =>
            cas.Id == collection2.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: false });
        Assert.Contains(updatedGroupAccess1, cas =>
            cas.Id == collection3.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: false });

        var (updatedGroup2, updatedGroupAccess2) = await groupRepository.GetByIdWithCollectionsAsync(group2.Id);
        Assert.Equal(3, updatedGroupAccess2.Count);
        Assert.Contains(updatedGroupAccess2, cas =>
            cas.Id == collection1.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: false });
        Assert.Contains(updatedGroupAccess2, cas =>
            cas.Id == collection2.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: false });
        Assert.Contains(updatedGroupAccess2, cas =>
            cas.Id == collection3.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: false });
    }

    [DatabaseTheory, DatabaseData]
    public async Task Migrate_NonManagers_WithoutAccessAll_NoChangeToRoleOrCollectionAccess(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        if (IsEfDatabase(organizationRepository))
        {
            return;
        }

        var userUser= await CreateUser(userRepository);
        var adminUser= await CreateUser(userRepository);
        var ownerUser= await CreateUser(userRepository);
        var customUser= await CreateUser(userRepository);

        var organization = await CreateOrganization(organizationRepository);

        // All roles that are unaffected by this change without AccessAll
        var orgUser = await CreateOrganizationUser(userUser, organization, OrganizationUserType.User, accessAll: false, organizationUserRepository);
        var admin = await CreateOrganizationUser(adminUser, organization, OrganizationUserType.Admin, accessAll: false, organizationUserRepository);
        var owner = await CreateOrganizationUser(ownerUser, organization, OrganizationUserType.Owner, accessAll: false, organizationUserRepository);
        var custom = await CreateOrganizationUser(customUser, organization, OrganizationUserType.Custom, accessAll: false, organizationUserRepository, new Permissions { DeleteAssignedCollections = true, AccessReports = true});

        var collection1 = await CreateCollection(organization, collectionRepository, null, new []
        {
            new CollectionAccessSelection {Id = orgUser.Id},
            new CollectionAccessSelection {Id = custom.Id, HidePasswords = true}
        });
        var collection2 = await CreateCollection(organization, collectionRepository,null, new []
        {
            new CollectionAccessSelection { Id = owner.Id, HidePasswords = true}  ,
            new CollectionAccessSelection { Id = admin.Id, ReadOnly = true}
        });
        var collection3 = await CreateCollection(organization, collectionRepository, null, new []
        {
            new CollectionAccessSelection { Id = owner.Id }
        });

        await organizationRepository.EnableCollectionEnhancements(organization.Id);

        var (updatedOrgUser, orgUserAccess) = await organizationUserRepository
            .GetDetailsByIdWithCollectionsAsync(orgUser.Id);
        Assert.Equal(OrganizationUserType.User, updatedOrgUser.Type);
        Assert.Equal(1, orgUserAccess.Count);
        Assert.Contains(orgUserAccess, cas =>
            cas.Id == collection1.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: false });

        var (updatedAdmin, adminAccess) = await organizationUserRepository
            .GetDetailsByIdWithCollectionsAsync(admin.Id);
        Assert.Equal(OrganizationUserType.Admin, updatedAdmin.Type);
        Assert.Equal(1, adminAccess.Count);
        Assert.Contains(adminAccess, cas =>
            cas.Id == collection2.Id &&
            cas is { HidePasswords: false, ReadOnly: true, Manage: false });

        var (updatedOwner, ownerAccess) = await organizationUserRepository
            .GetDetailsByIdWithCollectionsAsync(owner.Id);
        Assert.Equal(OrganizationUserType.Owner, updatedOwner.Type);
        Assert.Equal(2, ownerAccess.Count);
        Assert.Contains(ownerAccess, cas =>
            cas.Id == collection2.Id &&
            cas is { HidePasswords: true, ReadOnly: false, Manage: false });
        Assert.Contains(ownerAccess, cas =>
            cas.Id == collection3.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: false });

        var (updatedCustom, customAccess) = await organizationUserRepository
            .GetDetailsByIdWithCollectionsAsync(custom.Id);
        Assert.Equal(OrganizationUserType.Custom, updatedCustom.Type);
        Assert.Equal(1, customAccess.Count);
        Assert.Contains(customAccess, cas =>
            cas.Id == collection1.Id &&
            cas is { HidePasswords: true, ReadOnly: false, Manage: false });
    }

    [DatabaseTheory, DatabaseData]
    public async Task Migrate_DoesNotAffect_OtherOrganizations(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        if (IsEfDatabase(organizationRepository))
        {
            return;
        }

        // Target organization to be migrated
        var targetUser = await CreateUser(userRepository);
        var targetOrganization = await CreateOrganization(organizationRepository);
        await CreateOrganizationUser(targetUser, targetOrganization, OrganizationUserType.Manager, accessAll: true, organizationUserRepository);
        await CreateCollection(targetOrganization, collectionRepository);
        await CreateCollection(targetOrganization, collectionRepository);
        await CreateCollection(targetOrganization, collectionRepository);

        // Unrelated organization
        var user = await CreateUser(userRepository);
        var organization = await CreateOrganization(organizationRepository);
        var orgUser = await CreateOrganizationUser(user, organization, OrganizationUserType.Manager, accessAll: true, organizationUserRepository);
        await CreateCollection(organization, collectionRepository);
        await CreateCollection(organization, collectionRepository);
        await CreateCollection(organization, collectionRepository);

        await organizationRepository.EnableCollectionEnhancements(targetOrganization.Id);

        var (updatedOrgUser, collectionAccessSelections) = await organizationUserRepository
            .GetDetailsByIdWithCollectionsAsync(orgUser.Id);

        // OrgUser should not have changed
        Assert.Equal(OrganizationUserType.Manager, updatedOrgUser.Type);
        Assert.True(updatedOrgUser.AccessAll);
        Assert.Equal(0, collectionAccessSelections.Count);

        var updatedOrganization = await organizationRepository.GetByIdAsync(organization.Id);
        Assert.False(updatedOrganization.FlexibleCollections);
    }

    private async Task<User> CreateUser(IUserRepository userRepository)
    {
        return await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });
    }

    private async Task<Group> CreateGroup(Organization organization, bool accessAll, IGroupRepository groupRepository,
        OrganizationUser? orgUser = null)
    {
        var group = await groupRepository.CreateAsync(new Group
        {
            Name = $"Test Group {Guid.NewGuid()}",
            OrganizationId = organization.Id,
            AccessAll = accessAll
        });

        if (orgUser != null)
        {
            await groupRepository.UpdateUsersAsync(group.Id, [orgUser.Id]);
        }

        return group;
    }

    private async Task<Organization> CreateOrganization(IOrganizationRepository organizationRepository)
    {
        return await organizationRepository.CreateAsync(new Organization
        {
            Name = $"Test Org {Guid.NewGuid()}",
            BillingEmail = "Billing Email", // TODO: EF does not enforce this being NOT NULL
            Plan = "Test Plan", // TODO: EF does not enforce this being NOT NULl
        });
    }

    private async Task<OrganizationUser> CreateOrganizationUser(User user, Organization organization,
        OrganizationUserType type, bool accessAll, IOrganizationUserRepository organizationUserRepository,
        Permissions? permissions = null)
    {
        return await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = type,
            AccessAll = accessAll,
            Permissions = permissions == null ? null : CoreHelpers.ClassToJsonData(permissions)
        });
    }

    private async Task<Collection> CreateCollection(Organization organization, ICollectionRepository collectionRepository,
        IEnumerable<CollectionAccessSelection>? groups = null, IEnumerable<CollectionAccessSelection>? users = null)
    {
        var collection = new Collection { Name = $"Test collection {Guid.NewGuid()}", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(collection, groups: groups, users: users);
        return collection;
    }

    // This sproc is intentionally not implemented in EF repositories because it is for opt-in on cloud only.
    // We handle this by returning early if we detect an EF repository, using IOrganizationRepository as a canary.
    // This is intentionally NOT scalable because we really shouldn't be repeating this pattern in the future.
    private bool IsEfDatabase(IOrganizationRepository organizationRepository)
    {
        return organizationRepository is EfOrganizationRepository;
    }
}
