using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories;

public class OrganizationEnableCollectionEnhancementTests
{
    [DatabaseTheory, DatabaseData]
    public async Task MigrateAccessAll_NonManagers_CreatesCanEditAssociations(IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        var user = await CreateUser(userRepository);
        var organization = await CreateOrganization(organizationRepository);
        var orgUser = await CreateOrganizationUser(user, organization, OrganizationUserType.User, accessAll:true, organizationUserRepository);
        var collection1 = await CreateCollection(organization, collectionRepository);
        var collection2 = await CreateCollection(organization, collectionRepository);

        await organizationRepository.EnableCollectionEnhancements(organization.Id);

        var (updatedOrgUser, collectionAccessSelections) = await organizationUserRepository.GetByIdWithCollectionsAsync(orgUser.Id);

        Assert.False(updatedOrgUser.AccessAll);

        Assert.Equal(2, collectionAccessSelections.Count);
        Assert.Contains(collectionAccessSelections, cas =>
            cas.Id == collection1.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: false });
        Assert.Contains(collectionAccessSelections, cas =>
            cas.Id == collection2.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: false });
    }

    [DatabaseTheory, DatabaseData]
    public async Task MigrateAccessAll_Managers_CreatesCanManageAssociationsAndDemotesToUser(IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        var user = await CreateUser(userRepository);
        var organization = await CreateOrganization(organizationRepository);
        var orgUser = await CreateOrganizationUser(user, organization, OrganizationUserType.Manager, accessAll:true, organizationUserRepository);
        var collection1 = await CreateCollection(organization, collectionRepository);
        var collection2 = await CreateCollection(organization, collectionRepository);

        await organizationRepository.EnableCollectionEnhancements(organization.Id);

        var (updatedOrgUser, collectionAccessSelections) = await organizationUserRepository.GetByIdWithCollectionsAsync(orgUser.Id);

        Assert.False(updatedOrgUser.AccessAll);

        Assert.Equal(2, collectionAccessSelections.Count);
        Assert.Contains(collectionAccessSelections, cas =>
            cas.Id == collection1.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: true });
        Assert.Contains(collectionAccessSelections, cas =>
            cas.Id == collection2.Id &&
            cas is { HidePasswords: false, ReadOnly: false, Manage: true });
    }

    private async Task<User> CreateUser(IUserRepository userRepository)
    {
        return await userRepository.CreateAsync(new User
        {
            Name = "Test User", Email = $"test+{Guid.NewGuid()}@email.com", ApiKey = "TEST", SecurityStamp = "stamp",
        });
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
        OrganizationUserType type, bool accessAll, IOrganizationUserRepository organizationUserRepository)
    {
        return await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = type,
            AccessAll = accessAll
        });
    }

    private async Task<Collection> CreateCollection(Organization organization, ICollectionRepository collectionRepository)
    {
        return await collectionRepository.CreateAsync(new Collection
        {
            Name = $"Test collection {Guid.NewGuid()}", OrganizationId = organization.Id
        });
    }
}
