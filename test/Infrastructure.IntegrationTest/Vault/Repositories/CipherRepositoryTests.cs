using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Xunit;
using CipherType = Bit.Core.Vault.Enums.CipherType;

namespace Bit.Infrastructure.IntegrationTest.Repositories;

public class CipherRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task DeleteAsync_UpdatesUserRevisionDate(
        IUserRepository userRepository,
        ICipherRepository cipherRepository)
    {
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var cipher = await cipherRepository.CreateAsync(new Cipher
        {
            Type = CipherType.Login,
            UserId = user.Id,
            Data = "", // TODO: EF does not enforce this as NOT NULL
        });

        await cipherRepository.DeleteAsync(cipher);

        var deletedCipher = await cipherRepository.GetByIdAsync(cipher.Id);

        Assert.Null(deletedCipher);
        var updatedUser = await userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.NotEqual(updatedUser.AccountRevisionDate, user.AccountRevisionDate);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateAsync_UpdateWithCollections_Works(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository,
        ICipherRepository cipherRepository,
        ICollectionCipherRepository collectionCipherRepository)
    {
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        user = await userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(user);

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Organization",
            BillingEmail = user.Email,
            Plan = "Test" // TODO: EF does not enforce this as NOT NULL
        });

        var orgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Owner,
        });

        var collection = await collectionRepository.CreateAsync(new Collection
        {
            Name = "Test Collection",
            OrganizationId = organization.Id
        });

        await Task.Delay(100);

        await collectionRepository.UpdateUsersAsync(collection.Id, new[]
        {
            new CollectionAccessSelection
            {
                Id = orgUser.Id,
                HidePasswords = true,
                ReadOnly = true,
                Manage = true
            },
        });

        await Task.Delay(100);

        await cipherRepository.CreateAsync(new CipherDetails
        {
            Type = CipherType.Login,
            OrganizationId = organization.Id,
            Data = "", // TODO: EF does not enforce this as NOT NULL
        }, new List<Guid>
        {
            collection.Id,
        });

        var updatedUser = await userRepository.GetByIdAsync(user.Id);

        Assert.NotNull(updatedUser);
        Assert.True(updatedUser.AccountRevisionDate - user.AccountRevisionDate > TimeSpan.Zero,
            "The AccountRevisionDate is expected to be changed");

        var collectionCiphers = await collectionCipherRepository.GetManyByOrganizationIdAsync(organization.Id);
        Assert.NotEmpty(collectionCiphers);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ReplaceAsync_SuccessfullyMovesCipherToOrganization(IUserRepository userRepository,
        ICipherRepository cipherRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IFolderRepository folderRepository)
    {
        // This tests what happens when a cipher is moved into an organizations
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });


        user = await userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(user);

        // Create cipher in personal vault
        var createdCipher = await cipherRepository.CreateAsync(new Cipher
        {
            UserId = user.Id,
            Data = "", // TODO: EF does not enforce this as NOT NULL
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Organization",
            BillingEmail = user.Email,
            Plan = "Test" // TODO: EF does not enforce this as NOT NULL
        });

        _ = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Owner,
        });

        var folder = await folderRepository.CreateAsync(new Folder
        {
            Name = "FolderName",
            UserId = user.Id,
        });

        // Move cipher to organization vault
        await cipherRepository.ReplaceAsync(new CipherDetails
        {
            Id = createdCipher.Id,
            UserId = user.Id,
            OrganizationId = organization.Id,
            FolderId = folder.Id,
            Data = "", // TODO: EF does not enforce this as NOT NULL
        });

        var updatedCipher = await cipherRepository.GetByIdAsync(createdCipher.Id);

        Assert.NotNull(updatedCipher);
        Assert.Null(updatedCipher.UserId);
        Assert.Equal(organization.Id, updatedCipher.OrganizationId);
        Assert.NotNull(updatedCipher.Folders);

        using var foldersJsonDocument = JsonDocument.Parse(updatedCipher.Folders);
        var foldersJsonElement = foldersJsonDocument.RootElement;
        Assert.Equal(JsonValueKind.Object, foldersJsonElement.ValueKind);

        // TODO: Should we force similar casing for guids across DB's
        // I'd rather we only interact with them as the actual Guid type
        var userProperty = foldersJsonElement
            .EnumerateObject()
            .FirstOrDefault(jp => string.Equals(jp.Name, user.Id.ToString(), StringComparison.OrdinalIgnoreCase));

        Assert.NotEqual(default, userProperty);
        Assert.Equal(folder.Id, userProperty.Value.GetGuid());
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetCipherPermissionsForOrganizationAsync_Works(
        ICipherRepository cipherRepository,
        IUserRepository userRepository,
        ICollectionCipherRepository collectionCipherRepository,
        ICollectionRepository collectionRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository
        )
    {

        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Organization",
            BillingEmail = user.Email,
            Plan = "Test"
        });

        var orgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Owner,
        });

        // A group that will be assigned Edit permissions to any collections
        var editGroup = await groupRepository.CreateAsync(new Group
        {
            OrganizationId = organization.Id,
            Name = "Edit Group",
        });
        await groupRepository.UpdateUsersAsync(editGroup.Id, new[] { orgUser.Id });

        // MANAGE

        var manageCollection = await collectionRepository.CreateAsync(new Collection
        {
            Name = "Manage Collection",
            OrganizationId = organization.Id
        });

        var manageCipher = await cipherRepository.CreateAsync(new Cipher
        {
            Type = CipherType.Login,
            OrganizationId = organization.Id,
            Data = ""
        });

        await collectionCipherRepository.UpdateCollectionsForAdminAsync(manageCipher.Id, organization.Id,
            new List<Guid> { manageCollection.Id });

        await collectionRepository.UpdateUsersAsync(manageCollection.Id, new List<CollectionAccessSelection>
        {
            new()
            {
                Id = orgUser.Id,
                HidePasswords = false,
                ReadOnly = false,
                Manage = true
            }
        });

        // EDIT

        var editCollection = await collectionRepository.CreateAsync(new Collection
        {
            Name = "Edit Collection",
            OrganizationId = organization.Id
        });

        var editCipher = await cipherRepository.CreateAsync(new Cipher
        {
            Type = CipherType.Login,
            OrganizationId = organization.Id,
            Data = ""
        });

        await collectionCipherRepository.UpdateCollectionsForAdminAsync(editCipher.Id, organization.Id,
            new List<Guid> { editCollection.Id });

        await collectionRepository.UpdateUsersAsync(editCollection.Id,
            new List<CollectionAccessSelection>
            {
                new() { Id = orgUser.Id, HidePasswords = false, ReadOnly = false, Manage = false }
            });

        // EDIT EXCEPT PASSWORD

        var editExceptPasswordCollection = await collectionRepository.CreateAsync(new Collection
        {
            Name = "Edit Except Password Collection",
            OrganizationId = organization.Id
        });

        var editExceptPasswordCipher = await cipherRepository.CreateAsync(new Cipher
        {
            Type = CipherType.Login,
            OrganizationId = organization.Id,
            Data = ""
        });

        await collectionCipherRepository.UpdateCollectionsForAdminAsync(editExceptPasswordCipher.Id, organization.Id,
            new List<Guid> { editExceptPasswordCollection.Id });

        await collectionRepository.UpdateUsersAsync(editExceptPasswordCollection.Id, new List<CollectionAccessSelection>
        {
            new() { Id = orgUser.Id, HidePasswords = true, ReadOnly = false, Manage = false }
        });

        // VIEW ONLY

        var viewOnlyCollection = await collectionRepository.CreateAsync(new Collection
        {
            Name = "View Only Collection",
            OrganizationId = organization.Id
        });

        var viewOnlyCipher = await cipherRepository.CreateAsync(new Cipher
        {
            Type = CipherType.Login,
            OrganizationId = organization.Id,
            Data = ""
        });

        await collectionCipherRepository.UpdateCollectionsForAdminAsync(viewOnlyCipher.Id, organization.Id,
            new List<Guid> { viewOnlyCollection.Id });

        await collectionRepository.UpdateUsersAsync(viewOnlyCollection.Id,
            new List<CollectionAccessSelection>
            {
                new() { Id = orgUser.Id, HidePasswords = false, ReadOnly = true, Manage = false }
            });

        // Assign the EditGroup to this View Only collection. The user belongs to this group.
        // The user permissions specified above (ViewOnly) should take precedence.
        await groupRepository.ReplaceAsync(editGroup,
            new[]
            {
                new CollectionAccessSelection
                {
                    Id = viewOnlyCollection.Id, HidePasswords = false, ReadOnly = false, Manage = false
                },
            });

        // VIEW EXCEPT PASSWORD

        var viewExceptPasswordCollection = await collectionRepository.CreateAsync(new Collection
        {
            Name = "View Except Password Collection",
            OrganizationId = organization.Id
        });

        var viewExceptPasswordCipher = await cipherRepository.CreateAsync(new Cipher
        {
            Type = CipherType.Login,
            OrganizationId = organization.Id,
            Data = ""
        });

        await collectionCipherRepository.UpdateCollectionsForAdminAsync(viewExceptPasswordCipher.Id, organization.Id,
            new List<Guid> { viewExceptPasswordCollection.Id });

        await collectionRepository.UpdateUsersAsync(viewExceptPasswordCollection.Id,
            new List<CollectionAccessSelection>
            {
                new() { Id = orgUser.Id, HidePasswords = true, ReadOnly = true, Manage = false }
            });

        // UNASSIGNED

        var unassignedCipher = await cipherRepository.CreateAsync(new Cipher
        {
            Type = CipherType.Login,
            OrganizationId = organization.Id,
            Data = ""
        });

        var permissions = await cipherRepository.GetCipherPermissionsForOrganizationAsync(organization.Id, user.Id);

        Assert.NotEmpty(permissions);

        var manageCipherPermission = permissions.FirstOrDefault(c => c.Id == manageCipher.Id);
        Assert.NotNull(manageCipherPermission);
        Assert.True(manageCipherPermission.Manage);
        Assert.True(manageCipherPermission.Edit);
        Assert.True(manageCipherPermission.Read);
        Assert.True(manageCipherPermission.ViewPassword);

        var editCipherPermission = permissions.FirstOrDefault(c => c.Id == editCipher.Id);
        Assert.NotNull(editCipherPermission);
        Assert.False(editCipherPermission.Manage);
        Assert.True(editCipherPermission.Edit);
        Assert.True(editCipherPermission.Read);
        Assert.True(editCipherPermission.ViewPassword);

        var editExceptPasswordCipherPermission = permissions.FirstOrDefault(c => c.Id == editExceptPasswordCipher.Id);
        Assert.NotNull(editExceptPasswordCipherPermission);
        Assert.False(editExceptPasswordCipherPermission.Manage);
        Assert.True(editExceptPasswordCipherPermission.Edit);
        Assert.True(editExceptPasswordCipherPermission.Read);
        Assert.False(editExceptPasswordCipherPermission.ViewPassword);

        var viewOnlyCipherPermission = permissions.FirstOrDefault(c => c.Id == viewOnlyCipher.Id);
        Assert.NotNull(viewOnlyCipherPermission);
        Assert.False(viewOnlyCipherPermission.Manage);
        Assert.False(viewOnlyCipherPermission.Edit);
        Assert.True(viewOnlyCipherPermission.Read);
        Assert.True(viewOnlyCipherPermission.ViewPassword);

        var viewExceptPasswordCipherPermission = permissions.FirstOrDefault(c => c.Id == viewExceptPasswordCipher.Id);
        Assert.NotNull(viewExceptPasswordCipherPermission);
        Assert.False(viewExceptPasswordCipherPermission.Manage);
        Assert.False(viewExceptPasswordCipherPermission.Edit);
        Assert.True(viewExceptPasswordCipherPermission.Read);
        Assert.False(viewExceptPasswordCipherPermission.ViewPassword);

        var unassignedCipherPermission = permissions.FirstOrDefault(c => c.Id == unassignedCipher.Id);
        Assert.NotNull(unassignedCipherPermission);
        Assert.False(unassignedCipherPermission.Manage);
        Assert.False(unassignedCipherPermission.Edit);
        Assert.False(unassignedCipherPermission.Read);
        Assert.False(unassignedCipherPermission.ViewPassword);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetCipherPermissionsForOrganizationAsync_ManageProperty_RespectsCollectionUserRules(
        ICipherRepository cipherRepository,
        IUserRepository userRepository,
        ICollectionCipherRepository collectionCipherRepository,
        ICollectionRepository collectionRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        var (user, organization, orgUser) = await CreateTestUserAndOrganization(userRepository, organizationRepository, organizationUserRepository);

        var manageCipher = await CreateCipherInOrganizationCollection(
            organization, orgUser, cipherRepository, collectionRepository, collectionCipherRepository,
            hasManagePermission: true, "Manage Collection");

        var nonManageCipher = await CreateCipherInOrganizationCollection(
            organization, orgUser, cipherRepository, collectionRepository, collectionCipherRepository,
            hasManagePermission: false, "Non-Manage Collection");

        var permissions = await cipherRepository.GetCipherPermissionsForOrganizationAsync(organization.Id, user.Id);
        Assert.Equal(2, permissions.Count);

        var managePermission = permissions.FirstOrDefault(c => c.Id == manageCipher.Id);
        Assert.NotNull(managePermission);
        Assert.True(managePermission.Manage, "Collection with Manage=true should grant Manage permission");

        var nonManagePermission = permissions.FirstOrDefault(c => c.Id == nonManageCipher.Id);
        Assert.NotNull(nonManagePermission);
        Assert.False(nonManagePermission.Manage, "Collection with Manage=false should not grant Manage permission");
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetCipherPermissionsForOrganizationAsync_ManageProperty_RespectsCollectionGroupRules(
        ICipherRepository cipherRepository,
        IUserRepository userRepository,
        ICollectionCipherRepository collectionCipherRepository,
        ICollectionRepository collectionRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository)
    {
        var (user, organization, orgUser) = await CreateTestUserAndOrganization(userRepository, organizationRepository, organizationUserRepository);

        var group = await groupRepository.CreateAsync(new Group
        {
            OrganizationId = organization.Id,
            Name = "Test Group",
        });
        await groupRepository.UpdateUsersAsync(group.Id, new[] { orgUser.Id });

        var (manageCipher, nonManageCipher) = await CreateCipherInOrganizationCollectionWithGroup(
            organization, group, cipherRepository, collectionRepository, collectionCipherRepository, groupRepository);

        var permissions = await cipherRepository.GetCipherPermissionsForOrganizationAsync(organization.Id, user.Id);
        Assert.Equal(2, permissions.Count);

        var managePermission = permissions.FirstOrDefault(c => c.Id == manageCipher.Id);
        Assert.NotNull(managePermission);
        Assert.True(managePermission.Manage, "Collection with Group Manage=true should grant Manage permission");

        var nonManagePermission = permissions.FirstOrDefault(c => c.Id == nonManageCipher.Id);
        Assert.NotNull(nonManagePermission);
        Assert.False(nonManagePermission.Manage, "Collection with Group Manage=false should not grant Manage permission");
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByUserIdAsync_ManageProperty_RespectsCollectionAndOwnershipRules(
        ICipherRepository cipherRepository,
        IUserRepository userRepository,
        ICollectionCipherRepository collectionCipherRepository,
        ICollectionRepository collectionRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        var (user, organization, orgUser) = await CreateTestUserAndOrganization(userRepository, organizationRepository, organizationUserRepository);

        var manageCipher = await CreateCipherInOrganizationCollection(
            organization, orgUser, cipherRepository, collectionRepository, collectionCipherRepository,
            hasManagePermission: true, "Manage Collection");

        var nonManageCipher = await CreateCipherInOrganizationCollection(
            organization, orgUser, cipherRepository, collectionRepository, collectionCipherRepository,
            hasManagePermission: false, "Non-Manage Collection");

        var personalCipher = await CreatePersonalCipher(user, cipherRepository);

        var userCiphers = await cipherRepository.GetManyByUserIdAsync(user.Id);
        Assert.Equal(3, userCiphers.Count);

        var managePermission = userCiphers.FirstOrDefault(c => c.Id == manageCipher.Id);
        Assert.NotNull(managePermission);
        Assert.True(managePermission.Manage, "Collection with Manage=true should grant Manage permission");

        var nonManagePermission = userCiphers.FirstOrDefault(c => c.Id == nonManageCipher.Id);
        Assert.NotNull(nonManagePermission);
        Assert.False(nonManagePermission.Manage, "Collection with Manage=false should not grant Manage permission");

        var personalPermission = userCiphers.FirstOrDefault(c => c.Id == personalCipher.Id);
        Assert.NotNull(personalPermission);
        Assert.True(personalPermission.Manage, "Personal ciphers should always have Manage permission");
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetByIdAsync_ManageProperty_RespectsCollectionAndOwnershipRules(
        ICipherRepository cipherRepository,
        IUserRepository userRepository,
        ICollectionCipherRepository collectionCipherRepository,
        ICollectionRepository collectionRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        var (user, organization, orgUser) = await CreateTestUserAndOrganization(userRepository, organizationRepository, organizationUserRepository);

        var manageCipher = await CreateCipherInOrganizationCollection(
            organization, orgUser, cipherRepository, collectionRepository, collectionCipherRepository,
            hasManagePermission: true, "Manage Collection");

        var nonManageCipher = await CreateCipherInOrganizationCollection(
            organization, orgUser, cipherRepository, collectionRepository, collectionCipherRepository,
            hasManagePermission: false, "Non-Manage Collection");

        var personalCipher = await CreatePersonalCipher(user, cipherRepository);

        var manageDetails = await cipherRepository.GetByIdAsync(manageCipher.Id, user.Id);
        Assert.NotNull(manageDetails);
        Assert.True(manageDetails.Manage, "Collection with Manage=true should grant Manage permission");

        var nonManageDetails = await cipherRepository.GetByIdAsync(nonManageCipher.Id, user.Id);
        Assert.NotNull(nonManageDetails);
        Assert.False(nonManageDetails.Manage, "Collection with Manage=false should not grant Manage permission");

        var personalDetails = await cipherRepository.GetByIdAsync(personalCipher.Id, user.Id);
        Assert.NotNull(personalDetails);
        Assert.True(personalDetails.Manage, "Personal ciphers should always have Manage permission");
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByUserIdAsync_WhenOneCipherIsAssignedToTwoCollectionsWithDifferentPermissions_MostPrivilegedAccessIsReturnedOnTheCipher(
        ICipherRepository cipherRepository,
        IUserRepository userRepository,
        ICollectionCipherRepository collectionCipherRepository,
        ICollectionRepository collectionRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        //Arrange
        var (user, organization, orgUser) = await CreateTestUserAndOrganization(userRepository, organizationRepository, organizationUserRepository);

        var cipher = await cipherRepository.CreateAsync(new Cipher
        {
            Type = CipherType.Login,
            OrganizationId = organization.Id,
            Data = ""
        });

        var managedPermissionsCollection = await collectionRepository.CreateAsync(new Collection
        {
            Name = "Managed",
            OrganizationId = organization.Id
        });

        var unmanagedPermissionsCollection = await collectionRepository.CreateAsync(new Collection
        {
            Name = "Unmanaged",
            OrganizationId = organization.Id
        });
        await collectionCipherRepository.UpdateCollectionsForAdminAsync(cipher.Id, organization.Id,
            [managedPermissionsCollection.Id, unmanagedPermissionsCollection.Id]);

        await collectionRepository.UpdateUsersAsync(managedPermissionsCollection.Id, new List<CollectionAccessSelection>
        {
            new() { Id = orgUser.Id, HidePasswords = false, ReadOnly = false, Manage = true }
        });

        await collectionRepository.UpdateUsersAsync(unmanagedPermissionsCollection.Id, new List<CollectionAccessSelection>
        {
            new() { Id = orgUser.Id, HidePasswords = false, ReadOnly = false, Manage = false }
        });

        // Act
        var ciphers = await cipherRepository.GetManyByUserIdAsync(user.Id);

        // Assert
        Assert.Single(ciphers);
        var deletableCipher = ciphers.SingleOrDefault(x => x.Id == cipher.Id);
        Assert.NotNull(deletableCipher);
        Assert.True(deletableCipher.Manage);

        // Annul
        await cipherRepository.DeleteAsync(cipher);
        await organizationUserRepository.DeleteAsync(orgUser);
        await organizationRepository.DeleteAsync(organization);
        await userRepository.DeleteAsync(user);
    }

    private async Task<(User user, Organization org, OrganizationUser orgUser)> CreateTestUserAndOrganization(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Organization",
            BillingEmail = user.Email,
            Plan = "Test"
        });

        var orgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Owner,
        });

        return (user, organization, orgUser);
    }

    private async Task<Cipher> CreateCipherInOrganizationCollection(
        Organization organization,
        OrganizationUser orgUser,
        ICipherRepository cipherRepository,
        ICollectionRepository collectionRepository,
        ICollectionCipherRepository collectionCipherRepository,
        bool hasManagePermission,
        string collectionName)
    {
        var collection = await collectionRepository.CreateAsync(new Collection
        {
            Name = collectionName,
            OrganizationId = organization.Id,
        });

        var cipher = await cipherRepository.CreateAsync(new Cipher
        {
            Type = CipherType.Login,
            OrganizationId = organization.Id,
            Data = ""
        });

        await collectionCipherRepository.UpdateCollectionsForAdminAsync(cipher.Id, organization.Id,
            new List<Guid> { collection.Id });

        await collectionRepository.UpdateUsersAsync(collection.Id, new List<CollectionAccessSelection>
        {
            new() { Id = orgUser.Id, HidePasswords = false, ReadOnly = false, Manage = hasManagePermission }
        });

        return cipher;
    }

    private async Task<(Cipher manageCipher, Cipher nonManageCipher)> CreateCipherInOrganizationCollectionWithGroup(
        Organization organization,
        Group group,
        ICipherRepository cipherRepository,
        ICollectionRepository collectionRepository,
        ICollectionCipherRepository collectionCipherRepository,
        IGroupRepository groupRepository)
    {
        var manageCollection = await collectionRepository.CreateAsync(new Collection
        {
            Name = "Group Manage Collection",
            OrganizationId = organization.Id,
        });

        var nonManageCollection = await collectionRepository.CreateAsync(new Collection
        {
            Name = "Group Non-Manage Collection",
            OrganizationId = organization.Id,
        });

        var manageCipher = await cipherRepository.CreateAsync(new Cipher
        {
            Type = CipherType.Login,
            OrganizationId = organization.Id,
            Data = ""
        });

        var nonManageCipher = await cipherRepository.CreateAsync(new Cipher
        {
            Type = CipherType.Login,
            OrganizationId = organization.Id,
            Data = ""
        });

        await collectionCipherRepository.UpdateCollectionsForAdminAsync(manageCipher.Id, organization.Id,
            new List<Guid> { manageCollection.Id });
        await collectionCipherRepository.UpdateCollectionsForAdminAsync(nonManageCipher.Id, organization.Id,
            new List<Guid> { nonManageCollection.Id });

        await groupRepository.ReplaceAsync(group,
            new[]
            {
                new CollectionAccessSelection
                {
                    Id = manageCollection.Id,
                    HidePasswords = false,
                    ReadOnly = false,
                    Manage = true
                },
                new CollectionAccessSelection
                {
                    Id = nonManageCollection.Id,
                    HidePasswords = false,
                    ReadOnly = false,
                    Manage = false
                }
            });

        return (manageCipher, nonManageCipher);
    }

    private async Task<Cipher> CreatePersonalCipher(User user, ICipherRepository cipherRepository)
    {
        return await cipherRepository.CreateAsync(new Cipher
        {
            Type = CipherType.Login,
            UserId = user.Id,
            Data = ""
        });
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetUserSecurityTasksByCipherIdsAsync_Works(
        ICipherRepository cipherRepository,
        IUserRepository userRepository,
        ICollectionCipherRepository collectionCipherRepository,
        ICollectionRepository collectionRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository
        )
    {
        // Users
        var user1 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 1",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var user2 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 2",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        // Organization
        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Organization",
            BillingEmail = user1.Email,
            Plan = "Test"
        });

        // Org Users
        var orgUser1 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            UserId = user1.Id,
            OrganizationId = organization.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Owner,
        });

        var orgUser2 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            UserId = user2.Id,
            OrganizationId = organization.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
        });

        // A group that will be assigned Edit permissions to any collections
        var editGroup = await groupRepository.CreateAsync(new Group
        {
            OrganizationId = organization.Id,
            Name = "Edit Group",
        });
        await groupRepository.UpdateUsersAsync(editGroup.Id, new[] { orgUser1.Id });

        // Add collections to Org
        var manageCollection = await collectionRepository.CreateAsync(new Collection
        {
            Name = "Manage Collection",
            OrganizationId = organization.Id
        });

        // Use a 2nd collection to differentiate between the two users
        var manageCollection2 = await collectionRepository.CreateAsync(new Collection
        {
            Name = "Manage Collection 2",
            OrganizationId = organization.Id
        });
        var viewOnlyCollection = await collectionRepository.CreateAsync(new Collection
        {
            Name = "View Only Collection",
            OrganizationId = organization.Id
        });

        // Ciphers
        var manageCipher1 = await cipherRepository.CreateAsync(new Cipher
        {
            Type = CipherType.Login,
            OrganizationId = organization.Id,
            Data = ""
        });

        var manageCipher2 = await cipherRepository.CreateAsync(new Cipher
        {
            Type = CipherType.Login,
            OrganizationId = organization.Id,
            Data = ""
        });

        var viewOnlyCipher = await cipherRepository.CreateAsync(new Cipher
        {
            Type = CipherType.Login,
            OrganizationId = organization.Id,
            Data = ""
        });

        await collectionCipherRepository.UpdateCollectionsForAdminAsync(manageCipher1.Id, organization.Id,
            new List<Guid> { manageCollection.Id });

        await collectionCipherRepository.UpdateCollectionsForAdminAsync(manageCipher2.Id, organization.Id,
            new List<Guid> { manageCollection2.Id });

        await collectionCipherRepository.UpdateCollectionsForAdminAsync(viewOnlyCipher.Id, organization.Id,
            new List<Guid> { viewOnlyCollection.Id });

        await collectionRepository.UpdateUsersAsync(manageCollection.Id, new List<CollectionAccessSelection>
        {
            new()
            {
                Id = orgUser1.Id,
                HidePasswords = false,
                ReadOnly = false,
                Manage = true
            },
             new()
            {
                Id = orgUser2.Id,
                HidePasswords = false,
                ReadOnly = false,
                Manage = true
            }
        });

        // Only add second user to the second manage collection
        await collectionRepository.UpdateUsersAsync(manageCollection2.Id, new List<CollectionAccessSelection>
        {
            new()
            {
                Id = orgUser2.Id,
                HidePasswords = false,
                ReadOnly = false,
                Manage = true
            },
        });

        await collectionRepository.UpdateUsersAsync(viewOnlyCollection.Id, new List<CollectionAccessSelection>
        {
            new()
            {
                Id = orgUser1.Id,
                HidePasswords = false,
                ReadOnly = false,
                Manage = false
            }
        });

        var securityTasks = new List<SecurityTask>
        {
            new SecurityTask { CipherId = manageCipher1.Id, Id = Guid.NewGuid() },
            new SecurityTask { CipherId = manageCipher2.Id, Id = Guid.NewGuid() },
            new SecurityTask { CipherId = viewOnlyCipher.Id, Id = Guid.NewGuid() }
        };

        var userSecurityTaskCiphers = await cipherRepository.GetUserSecurityTasksByCipherIdsAsync(organization.Id, securityTasks);

        Assert.NotEmpty(userSecurityTaskCiphers);
        Assert.Equal(3, userSecurityTaskCiphers.Count);

        var user1TaskCiphers = userSecurityTaskCiphers.Where(t => t.UserId == user1.Id);
        Assert.Single(user1TaskCiphers);
        Assert.Equal(user1.Email, user1TaskCiphers.First().Email);
        Assert.Equal(user1.Id, user1TaskCiphers.First().UserId);
        Assert.Equal(manageCipher1.Id, user1TaskCiphers.First().CipherId);

        var user2TaskCiphers = userSecurityTaskCiphers.Where(t => t.UserId == user2.Id);
        Assert.NotNull(user2TaskCiphers);
        Assert.Equal(2, user2TaskCiphers.Count());
        Assert.Equal(user2.Email, user2TaskCiphers.Last().Email);
        Assert.Equal(user2.Id, user2TaskCiphers.Last().UserId);
        Assert.Contains(user2TaskCiphers, t => t.CipherId == manageCipher1.Id && t.TaskId == securityTasks[0].Id);
        Assert.Contains(user2TaskCiphers, t => t.CipherId == manageCipher2.Id && t.TaskId == securityTasks[1].Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task UpdateCiphersAsync_Works(ICipherRepository cipherRepository, IUserRepository userRepository)
    {
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var cipher1 = await CreatePersonalCipher(user, cipherRepository);
        var cipher2 = await CreatePersonalCipher(user, cipherRepository);

        cipher1.Type = CipherType.SecureNote;
        cipher2.Attachments = "new_attachments";

        await cipherRepository.UpdateCiphersAsync(user.Id, [cipher1, cipher2]);

        var updatedCipher1 = await cipherRepository.GetByIdAsync(cipher1.Id);
        var updatedCipher2 = await cipherRepository.GetByIdAsync(cipher2.Id);

        Assert.NotNull(updatedCipher1);
        Assert.NotNull(updatedCipher2);

        Assert.Equal(CipherType.SecureNote, updatedCipher1.Type);
        Assert.Equal("new_attachments", updatedCipher2.Attachments);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateAsync_vNext_WithFolders_Works(
        IUserRepository userRepository, ICipherRepository cipherRepository, IFolderRepository folderRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"{Guid.NewGuid()}@example.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var folder1 = new Folder { Id = CoreHelpers.GenerateComb(), UserId = user.Id, Name = "Test Folder 1" };
        var folder2 = new Folder { Id = CoreHelpers.GenerateComb(), UserId = user.Id, Name = "Test Folder 2" };
        var cipher1 = new Cipher { Id = CoreHelpers.GenerateComb(), Type = CipherType.Login, UserId = user.Id, Data = "" };
        var cipher2 = new Cipher { Id = CoreHelpers.GenerateComb(), Type = CipherType.SecureNote, UserId = user.Id, Data = "" };

        // Act
        await cipherRepository.CreateAsync_vNext(
            userId: user.Id,
            ciphers: [cipher1, cipher2],
            folders: [folder1, folder2]);

        // Assert
        var readCipher1 = await cipherRepository.GetByIdAsync(cipher1.Id);
        var readCipher2 = await cipherRepository.GetByIdAsync(cipher2.Id);
        Assert.NotNull(readCipher1);
        Assert.NotNull(readCipher2);

        var readFolder1 = await folderRepository.GetByIdAsync(folder1.Id);
        var readFolder2 = await folderRepository.GetByIdAsync(folder2.Id);
        Assert.NotNull(readFolder1);
        Assert.NotNull(readFolder2);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateAsync_vNext_WithCollectionsAndUsers_Works(
        IOrganizationRepository orgRepository,
        IOrganizationUserRepository orgUserRepository,
        ICollectionRepository collectionRepository,
        ICollectionCipherRepository collectionCipherRepository,
        ICipherRepository cipherRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"{Guid.NewGuid()}@example.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var org = await orgRepository.CreateAsync(new Organization
        {
            Name = "Test Organization",
            BillingEmail = user.Email,
            Plan = "Test"
        });

        var orgUser = await orgUserRepository.CreateAsync(new OrganizationUser
        {
            UserId = user.Id,
            OrganizationId = org.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Owner,
        });

        var collection = new Collection { Id = CoreHelpers.GenerateComb(), Name = "Test Collection", OrganizationId = org.Id };
        var cipher = new Cipher { Id = CoreHelpers.GenerateComb(), Type = CipherType.Login, OrganizationId = org.Id, Data = "" };
        var collectionCipher = new CollectionCipher { CollectionId = collection.Id, CipherId = cipher.Id };
        var collectionUser = new CollectionUser
        {
            CollectionId = collection.Id,
            OrganizationUserId = orgUser.Id,
            HidePasswords = false,
            ReadOnly = false,
            Manage = true
        };

        // Act
        await cipherRepository.CreateAsync_vNext(
            ciphers: [cipher],
            collections: [collection],
            collectionCiphers: [collectionCipher],
            collectionUsers: [collectionUser]);

        // Assert
        var orgCiphers = await cipherRepository.GetManyByOrganizationIdAsync(org.Id);
        Assert.Contains(orgCiphers, c => c.Id == cipher.Id);

        var collCiphers = await collectionCipherRepository.GetManyByOrganizationIdAsync(org.Id);
        Assert.Contains(collCiphers, cc => cc.CipherId == cipher.Id && cc.CollectionId == collection.Id);

        var collectionsInOrg = await collectionRepository.GetManyByOrganizationIdAsync(org.Id);
        Assert.Contains(collectionsInOrg, c => c.Id == collection.Id);

        var collectionUsers = await collectionRepository.GetManyUsersByIdAsync(collection.Id);
        var foundCollectionUser = collectionUsers.FirstOrDefault(cu => cu.Id == orgUser.Id);
        Assert.NotNull(foundCollectionUser);
        Assert.True(foundCollectionUser.Manage);
        Assert.False(foundCollectionUser.ReadOnly);
        Assert.False(foundCollectionUser.HidePasswords);
    }

    [DatabaseTheory, DatabaseData]
    public async Task UpdateCiphersAsync_vNext_Works(
        IUserRepository userRepository, ICipherRepository cipherRepository)
    {
        // Arrange
        var expectedNewType = CipherType.SecureNote;
        var expectedNewAttachments = "bulk_new_attachments";

        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"{Guid.NewGuid()}@example.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var c1 = new Cipher { Id = CoreHelpers.GenerateComb(), Type = CipherType.Login, UserId = user.Id, Data = "" };
        var c2 = new Cipher { Id = CoreHelpers.GenerateComb(), Type = CipherType.Login, UserId = user.Id, Data = "" };
        await cipherRepository.CreateAsync(
            userId: user.Id,
            ciphers: [c1, c2],
            folders: []);

        c1.Type = expectedNewType;
        c2.Attachments = expectedNewAttachments;

        // Act
        await cipherRepository.UpdateCiphersAsync_vNext(user.Id, [c1, c2]);

        // Assert
        var updated1 = await cipherRepository.GetByIdAsync(c1.Id);
        Assert.NotNull(updated1);
        Assert.Equal(c1.Id, updated1.Id);
        Assert.Equal(expectedNewType, updated1.Type);
        Assert.Equal(c1.UserId, updated1.UserId);
        Assert.Equal(c1.Data, updated1.Data);
        Assert.Equal(c1.OrganizationId, updated1.OrganizationId);
        Assert.Equal(c1.Attachments, updated1.Attachments);

        var updated2 = await cipherRepository.GetByIdAsync(c2.Id);
        Assert.NotNull(updated2);
        Assert.Equal(c2.Id, updated2.Id);
        Assert.Equal(c2.Type, updated2.Type);
        Assert.Equal(c2.UserId, updated2.UserId);
        Assert.Equal(c2.Data, updated2.Data);
        Assert.Equal(c2.OrganizationId, updated2.OrganizationId);
        Assert.Equal(expectedNewAttachments, updated2.Attachments);
    }

    [DatabaseTheory, DatabaseData]
    public async Task DeleteCipherWithSecurityTaskAsync_Works(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        ICipherRepository cipherRepository,
        ISecurityTaskRepository securityTaskRepository,
        INotificationRepository notificationRepository,
        INotificationStatusRepository notificationStatusRepository)
    {
        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            PlanType = PlanType.EnterpriseAnnually,
            Plan = "Test Plan",
            BillingEmail = ""
        });

        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var cipher1 = new Cipher { Type = CipherType.Login, OrganizationId = organization.Id, Data = "", };
        await cipherRepository.CreateAsync(cipher1);

        var cipher2 = new Cipher { Type = CipherType.Login, OrganizationId = organization.Id, Data = "", };
        await cipherRepository.CreateAsync(cipher2);

        var tasks = new List<SecurityTask>
        {
            new()
            {
                OrganizationId = organization.Id,
                CipherId = cipher1.Id,
                Status = SecurityTaskStatus.Pending,
                Type = SecurityTaskType.UpdateAtRiskCredential,
            },
            new()
            {
                OrganizationId = organization.Id,
                CipherId = cipher2.Id,
                Status = SecurityTaskStatus.Completed,
                Type = SecurityTaskType.UpdateAtRiskCredential,
            }
        };

        await securityTaskRepository.CreateManyAsync(tasks);
        var notification = await notificationRepository.CreateAsync(new Notification
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            TaskId = tasks[1].Id,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
        });
        await notificationStatusRepository.CreateAsync(new NotificationStatus
        {
            NotificationId = notification.Id,
            UserId = user.Id,
            ReadDate = DateTime.UtcNow,
        });

        // Delete cipher with pending security task
        await cipherRepository.DeleteAsync(cipher1);

        var deletedCipher1 = await cipherRepository.GetByIdAsync(cipher1.Id);

        Assert.Null(deletedCipher1);

        // Delete cipher with completed security task
        await cipherRepository.DeleteAsync(cipher2);

        var deletedCipher2 = await cipherRepository.GetByIdAsync(cipher2.Id);

        Assert.Null(deletedCipher2);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ArchiveAsync_Works(
        ICipherRepository sutRepository,
        IUserRepository userRepository)
    {
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        // Ciphers
        var cipher = await sutRepository.CreateAsync(new Cipher
        {
            Type = CipherType.Login,
            Data = "",
            UserId = user.Id
        });

        // Act
        await sutRepository.ArchiveAsync(new List<Guid> { cipher.Id }, user.Id);

        // Assert
        var archivedCipher = await sutRepository.GetByIdAsync(cipher.Id, user.Id);
        Assert.NotNull(archivedCipher);
        Assert.NotNull(archivedCipher.ArchivedDate);
    }
}
