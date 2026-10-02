using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Vault.Repositories;

public class FolderRepositoryTests
{
    [Theory, DatabaseData]
    public async Task DeleteManyAsync_DeletesRequestedFolders_AndUnfilesTheirCiphers(
        IUserRepository userRepository,
        IFolderRepository folderRepository,
        ICipherRepository cipherRepository)
    {
        var user = await CreateUserAsync(userRepository);

        var deletedFolder = await CreateFolderAsync(folderRepository, user.Id, "deleted");
        var keptFolder = await CreateFolderAsync(folderRepository, user.Id, "kept");

        var cipherInDeletedFolder = await CreateCipherAsync(cipherRepository, user.Id, deletedFolder.Id);
        var cipherInKeptFolder = await CreateCipherAsync(cipherRepository, user.Id, keptFolder.Id);
        var unfiledCipher = await CreateCipherAsync(cipherRepository, user.Id, null);

        await folderRepository.DeleteManyAsync([deletedFolder.Id], user.Id);

        Assert.Null(await folderRepository.GetByIdAsync(deletedFolder.Id, user.Id));
        Assert.NotNull(await folderRepository.GetByIdAsync(keptFolder.Id, user.Id));

        Assert.Null((await cipherRepository.GetByIdAsync(cipherInDeletedFolder.Id, user.Id)).FolderId);
        Assert.Equal(keptFolder.Id, (await cipherRepository.GetByIdAsync(cipherInKeptFolder.Id, user.Id)).FolderId);
        Assert.Null((await cipherRepository.GetByIdAsync(unfiledCipher.Id, user.Id)).FolderId);
    }

    [Theory, DatabaseData]
    public async Task DeleteManyAsync_DeletesEveryRequestedFolder(
        IUserRepository userRepository,
        IFolderRepository folderRepository,
        ICipherRepository cipherRepository)
    {
        var user = await CreateUserAsync(userRepository);

        var firstFolder = await CreateFolderAsync(folderRepository, user.Id, "first");
        var secondFolder = await CreateFolderAsync(folderRepository, user.Id, "second");

        var firstCipher = await CreateCipherAsync(cipherRepository, user.Id, firstFolder.Id);
        var secondCipher = await CreateCipherAsync(cipherRepository, user.Id, secondFolder.Id);

        await folderRepository.DeleteManyAsync([firstFolder.Id, secondFolder.Id], user.Id);

        Assert.Empty(await folderRepository.GetManyByUserIdAsync(user.Id));

        var firstDetails = await cipherRepository.GetByIdAsync(firstCipher.Id, user.Id);
        var secondDetails = await cipherRepository.GetByIdAsync(secondCipher.Id, user.Id);
        Assert.NotNull(firstDetails);
        Assert.NotNull(secondDetails);
        Assert.Null(firstDetails.FolderId);
        Assert.Null(secondDetails.FolderId);
    }

    [Theory, DatabaseData]
    public async Task DeleteManyAsync_IgnoresFoldersBelongingToAnotherUser(
        IUserRepository userRepository,
        IFolderRepository folderRepository,
        ICipherRepository cipherRepository)
    {
        var user = await CreateUserAsync(userRepository);
        var otherUser = await CreateUserAsync(userRepository);

        var ownFolder = await CreateFolderAsync(folderRepository, user.Id, "own");
        var otherUsersFolder = await CreateFolderAsync(folderRepository, otherUser.Id, "other");
        var otherUsersCipher = await CreateCipherAsync(cipherRepository, otherUser.Id, otherUsersFolder.Id);

        await folderRepository.DeleteManyAsync([ownFolder.Id, otherUsersFolder.Id], user.Id);

        Assert.Null(await folderRepository.GetByIdAsync(ownFolder.Id, user.Id));
        Assert.NotNull(await folderRepository.GetByIdAsync(otherUsersFolder.Id, otherUser.Id));
        Assert.Equal(
            otherUsersFolder.Id,
            (await cipherRepository.GetByIdAsync(otherUsersCipher.Id, otherUser.Id)).FolderId);
    }

    [Theory, DatabaseData]
    public async Task DeleteManyAsync_WithUnknownIds_LeavesTheVaultUnchanged(
        IUserRepository userRepository,
        IFolderRepository folderRepository,
        ICipherRepository cipherRepository)
    {
        var user = await CreateUserAsync(userRepository);
        var folder = await CreateFolderAsync(folderRepository, user.Id, "kept");
        var cipher = await CreateCipherAsync(cipherRepository, user.Id, folder.Id);

        await folderRepository.DeleteManyAsync([Guid.NewGuid()], user.Id);

        Assert.NotNull(await folderRepository.GetByIdAsync(folder.Id, user.Id));

        var details = await cipherRepository.GetByIdAsync(cipher.Id, user.Id);
        Assert.NotNull(details);
        Assert.Equal(folder.Id, details.FolderId);
    }

    [Theory, DatabaseData]
    public async Task DeleteAsync_UnfilesTheCiphersInTheDeletedFolder(
        IUserRepository userRepository,
        IFolderRepository folderRepository,
        ICipherRepository cipherRepository)
    {
        var user = await CreateUserAsync(userRepository);
        var folder = await CreateFolderAsync(folderRepository, user.Id, "deleted");
        var cipher = await CreateCipherAsync(cipherRepository, user.Id, folder.Id);

        await folderRepository.DeleteAsync(folder);

        Assert.Null(await folderRepository.GetByIdAsync(folder.Id, user.Id));
        Assert.Null((await cipherRepository.GetByIdAsync(cipher.Id, user.Id)).FolderId);
    }

    private static Task<User> CreateUserAsync(IUserRepository userRepository)
        => userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

    private static Task<Folder> CreateFolderAsync(IFolderRepository folderRepository, Guid userId, string name)
        => folderRepository.CreateAsync(new Folder
        {
            UserId = userId,
            Name = name,
        });

    private static async Task<CipherDetails> CreateCipherAsync(
        ICipherRepository cipherRepository, Guid userId, Guid? folderId)
    {
        var cipher = new CipherDetails
        {
            Type = CipherType.Login,
            UserId = userId,
            FolderId = folderId,
            Data = "", // EF does not enforce this as NOT NULL
        };

        await cipherRepository.CreateAsync(cipher);
        return cipher;
    }
}
