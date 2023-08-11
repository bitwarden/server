using System.Text.Json;
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
        ICipherRepository cipherRepository,
        ITestDatabaseHelper helper)
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

        helper.ClearTracker();

        await cipherRepository.DeleteAsync(cipher);

        var deletedCipher = await cipherRepository.GetByIdAsync(cipher.Id);

        Assert.Null(deletedCipher);
        var updatedUser = await userRepository.GetByIdAsync(user.Id);
        Assert.NotEqual(updatedUser.AccountRevisionDate, user.AccountRevisionDate);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateAsync_UpdateWithCollections_Works(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository,
        ICipherRepository cipherRepository,
        ICollectionCipherRepository collectionCipherRepository,
        ITestDatabaseHelper helper)
    {
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        helper.ClearTracker();

        user = await userRepository.GetByIdAsync(user.Id);

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

        helper.ClearTracker();

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
        IFolderRepository folderRepository,
        ITestDatabaseHelper helper)
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

        helper.ClearTracker();

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
}
