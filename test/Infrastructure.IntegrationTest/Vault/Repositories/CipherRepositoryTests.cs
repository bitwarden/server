using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Xunit;

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
        Assert.False(manageCipherPermission.Unassigned);

        var editCipherPermission = permissions.FirstOrDefault(c => c.Id == editCipher.Id);
        Assert.NotNull(editCipherPermission);
        Assert.False(editCipherPermission.Manage);
        Assert.True(editCipherPermission.Edit);
        Assert.True(editCipherPermission.Read);
        Assert.True(editCipherPermission.ViewPassword);
        Assert.False(editCipherPermission.Unassigned);

        var editExceptPasswordCipherPermission = permissions.FirstOrDefault(c => c.Id == editExceptPasswordCipher.Id);
        Assert.NotNull(editExceptPasswordCipherPermission);
        Assert.False(editExceptPasswordCipherPermission.Manage);
        Assert.True(editExceptPasswordCipherPermission.Edit);
        Assert.True(editExceptPasswordCipherPermission.Read);
        Assert.False(editExceptPasswordCipherPermission.ViewPassword);
        Assert.False(editExceptPasswordCipherPermission.Unassigned);

        var viewOnlyCipherPermission = permissions.FirstOrDefault(c => c.Id == viewOnlyCipher.Id);
        Assert.NotNull(viewOnlyCipherPermission);
        Assert.False(viewOnlyCipherPermission.Manage);
        Assert.False(viewOnlyCipherPermission.Edit);
        Assert.True(viewOnlyCipherPermission.Read);
        Assert.True(viewOnlyCipherPermission.ViewPassword);
        Assert.False(viewOnlyCipherPermission.Unassigned);

        var viewExceptPasswordCipherPermission = permissions.FirstOrDefault(c => c.Id == viewExceptPasswordCipher.Id);
        Assert.NotNull(viewExceptPasswordCipherPermission);
        Assert.False(viewExceptPasswordCipherPermission.Manage);
        Assert.False(viewExceptPasswordCipherPermission.Edit);
        Assert.True(viewExceptPasswordCipherPermission.Read);
        Assert.False(viewExceptPasswordCipherPermission.ViewPassword);
        Assert.False(viewExceptPasswordCipherPermission.Unassigned);

        var unassignedCipherPermission = permissions.FirstOrDefault(c => c.Id == unassignedCipher.Id);
        Assert.NotNull(unassignedCipherPermission);
        Assert.True(unassignedCipherPermission.Unassigned);
        Assert.False(unassignedCipherPermission.Manage);
        Assert.False(unassignedCipherPermission.Edit);
        Assert.False(unassignedCipherPermission.Read);
        Assert.False(unassignedCipherPermission.ViewPassword);
    }
}
