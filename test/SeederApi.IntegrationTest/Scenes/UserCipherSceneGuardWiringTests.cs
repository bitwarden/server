using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;
using Bit.RustSDK;
using Bit.Seeder.Scenes;
using Bit.Seeder.Services;
using NSubstitute;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

public class UserCipherSceneGuardWiringTests
{
    private static readonly string _userKey = RustSdkService.GenerateUserKeys("wiring@example.com", "asdfasdfasdf").Key;

    private readonly User _user = new() { Id = Guid.NewGuid(), Email = "wiring@example.com" };
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ICipherRepository _cipherRepository = Substitute.For<ICipherRepository>();
    private readonly IFolderRepository _folderRepository = Substitute.For<IFolderRepository>();
    private readonly IManglerService _manglerService = Substitute.For<IManglerService>();

    public UserCipherSceneGuardWiringTests()
    {
        _userRepository.GetByIdAsync(_user.Id).Returns(_user);
        _manglerService.GetMangleMap().Returns([]);
    }

    public static TheoryData<string> UserCipherScenes() => new()
    {
        nameof(UserLoginCipherScene),
        nameof(UserBankAccountCipherScene),
        nameof(UserCardCipherScene),
        nameof(UserDriversLicenseCipherScene),
        nameof(UserIdentityCipherScene),
        nameof(UserPassportCipherScene),
        nameof(UserSecureNoteCipherScene),
        nameof(UserSshKeyCipherScene)
    };

    [Theory]
    [MemberData(nameof(UserCipherScenes))]
    public async Task SeedAsync_FolderOwnedByAnotherUser_ThrowsWithoutCreatingCipher(string scene)
    {
        _folderRepository.GetManyByUserIdAsync(_user.Id).Returns([]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => SeedAsync(scene, Guid.NewGuid()));

        Assert.Contains("not owned by user", exception.Message);
        await _cipherRepository.DidNotReceive().CreateAsync(Arg.Any<Cipher>());
    }

    [Theory]
    [MemberData(nameof(UserCipherScenes))]
    public async Task SeedAsync_OwnFolder_CreatesCipherInFolder(string scene)
    {
        var folder = new Folder { Id = Guid.NewGuid(), UserId = _user.Id, Name = "folder" };
        _folderRepository.GetManyByUserIdAsync(_user.Id).Returns([folder]);

        await SeedAsync(scene, folder.Id);

        await _cipherRepository.Received(1).CreateAsync(Arg.Is<Cipher>(c =>
            c.UserId == _user.Id &&
            c.Folders!.Contains(folder.Id.ToString(), StringComparison.OrdinalIgnoreCase)));
    }

    private Task SeedAsync(string scene, Guid folderId) => scene switch
    {
        nameof(UserLoginCipherScene) =>
            new UserLoginCipherScene(_userRepository, _cipherRepository, _folderRepository, _manglerService)
                .SeedAsync(new UserLoginCipherScene.Request
                {
                    UserId = _user.Id,
                    UserKeyB64 = _userKey,
                    Name = "Login",
                    FolderId = folderId
                }),
        nameof(UserBankAccountCipherScene) =>
            new UserBankAccountCipherScene(_userRepository, _cipherRepository, _folderRepository, _manglerService)
                .SeedAsync(new UserBankAccountCipherScene.Request
                {
                    UserId = _user.Id,
                    UserKeyB64 = _userKey,
                    Name = "Bank",
                    FolderId = folderId
                }),
        nameof(UserCardCipherScene) =>
            new UserCardCipherScene(_userRepository, _cipherRepository, _folderRepository, _manglerService)
                .SeedAsync(new UserCardCipherScene.Request
                {
                    UserId = _user.Id,
                    UserKeyB64 = _userKey,
                    Name = "Card",
                    CardholderName = "Jane Doe",
                    Number = "4111111111111111",
                    ExpMonth = "12",
                    ExpYear = "2030",
                    Code = "123",
                    FolderId = folderId
                }),
        nameof(UserDriversLicenseCipherScene) =>
            new UserDriversLicenseCipherScene(_userRepository, _cipherRepository, _folderRepository, _manglerService)
                .SeedAsync(new UserDriversLicenseCipherScene.Request
                {
                    UserId = _user.Id,
                    UserKeyB64 = _userKey,
                    Name = "License",
                    FolderId = folderId
                }),
        nameof(UserIdentityCipherScene) =>
            new UserIdentityCipherScene(_userRepository, _cipherRepository, _folderRepository, _manglerService)
                .SeedAsync(new UserIdentityCipherScene.Request
                {
                    UserId = _user.Id,
                    UserKeyB64 = _userKey,
                    Name = "Identity",
                    FolderId = folderId
                }),
        nameof(UserPassportCipherScene) =>
            new UserPassportCipherScene(_userRepository, _cipherRepository, _folderRepository, _manglerService)
                .SeedAsync(new UserPassportCipherScene.Request
                {
                    UserId = _user.Id,
                    UserKeyB64 = _userKey,
                    Name = "Passport",
                    FolderId = folderId
                }),
        nameof(UserSecureNoteCipherScene) =>
            new UserSecureNoteCipherScene(_userRepository, _cipherRepository, _folderRepository, _manglerService)
                .SeedAsync(new UserSecureNoteCipherScene.Request
                {
                    UserId = _user.Id,
                    UserKeyB64 = _userKey,
                    Name = "Note",
                    FolderId = folderId
                }),
        nameof(UserSshKeyCipherScene) =>
            new UserSshKeyCipherScene(_userRepository, _cipherRepository, _folderRepository, _manglerService)
                .SeedAsync(new UserSshKeyCipherScene.Request
                {
                    UserId = _user.Id,
                    UserKeyB64 = _userKey,
                    Name = "SSH",
                    FolderId = folderId
                }),
        _ => throw new ArgumentOutOfRangeException(nameof(scene), scene, null)
    };
}
