using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;
using Bit.RustSDK;
using Bit.Seeder.Scenes;
using Bit.Seeder.Services;
using NSubstitute;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

public class OrganizationCipherSceneGuardWiringTests
{
    private static readonly string _organizationKey = RustSdkService.GenerateOrganizationKeys().Key;

    private readonly Organization _organization = new() { Id = Guid.NewGuid() };
    private readonly Guid _memberUserId = Guid.NewGuid();
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly ICipherRepository _cipherRepository = Substitute.For<ICipherRepository>();
    private readonly ICollectionRepository _collectionRepository = Substitute.For<ICollectionRepository>();
    private readonly IOrganizationUserRepository _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
    private readonly IFolderRepository _folderRepository = Substitute.For<IFolderRepository>();
    private readonly IManglerService _manglerService = Substitute.For<IManglerService>();

    public OrganizationCipherSceneGuardWiringTests()
    {
        _organizationRepository.GetByIdAsync(_organization.Id).Returns(_organization);
        _organizationUserRepository.GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>()).Returns(
        [
            new OrganizationUser
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organization.Id,
                UserId = _memberUserId,
                Status = OrganizationUserStatusType.Confirmed
            }
        ]);
        _manglerService.GetMangleMap().Returns([]);
    }

    public static TheoryData<string> OrganizationCipherScenes() => new()
    {
        nameof(OrganizationLoginCipherScene),
        nameof(OrganizationBankAccountCipherScene),
        nameof(OrganizationCardCipherScene),
        nameof(OrganizationDriversLicenseCipherScene),
        nameof(OrganizationIdentityCipherScene),
        nameof(OrganizationPassportCipherScene),
        nameof(OrganizationSecureNoteCipherScene)
    };

    [Theory]
    [MemberData(nameof(OrganizationCipherScenes))]
    public async Task SeedAsync_ForeignCollection_ThrowsWithoutCreatingCipher(string scene)
    {
        var foreignCollection = new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), Name = "c" };
        _collectionRepository.GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([foreignCollection]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SeedAsync(scene, foreignCollection.Id));

        Assert.Contains("not in organization", exception.Message);
        await _cipherRepository.DidNotReceive().CreateAsync(Arg.Any<Cipher>(), Arg.Any<IEnumerable<Guid>>());
    }

    [Theory]
    [MemberData(nameof(OrganizationCipherScenes))]
    public async Task SeedAsync_DuplicateCollectionIds_CreatesCipherWithDistinctIds(string scene)
    {
        var collection = new Collection { Id = Guid.NewGuid(), OrganizationId = _organization.Id, Name = "c" };
        _collectionRepository.GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([collection]);

        await SeedAsync(scene, collection.Id, collection.Id);

        await _cipherRepository.Received(1).CreateAsync(
            Arg.Is<Cipher>(c => c.OrganizationId == _organization.Id),
            Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { collection.Id })));
    }

    [Fact]
    public async Task OrganizationLoginCipherScene_UserNotConfirmedMember_ThrowsWithoutCreatingCipher()
    {
        var scene = NewLoginScene();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => scene.SeedAsync(LoginRequest(userId: Guid.NewGuid(), folderId: null)));

        Assert.Contains("not confirmed members", exception.Message);
        await _cipherRepository.DidNotReceive().CreateAsync(Arg.Any<Cipher>(), Arg.Any<IEnumerable<Guid>>());
    }

    [Fact]
    public async Task OrganizationLoginCipherScene_FolderOwnedByAnotherUser_ThrowsWithoutCreatingCipher()
    {
        _folderRepository.GetManyByUserIdAsync(_memberUserId).Returns([]);
        var scene = NewLoginScene();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => scene.SeedAsync(LoginRequest(_memberUserId, folderId: Guid.NewGuid())));

        Assert.Contains("not owned by user", exception.Message);
        await _cipherRepository.DidNotReceive().CreateAsync(Arg.Any<Cipher>(), Arg.Any<IEnumerable<Guid>>());
    }

    private OrganizationLoginCipherScene NewLoginScene() => new(
        _organizationRepository, _cipherRepository, _collectionRepository,
        _organizationUserRepository, _folderRepository, _manglerService);

    private OrganizationLoginCipherScene.Request LoginRequest(Guid userId, Guid? folderId, params Guid[] collectionIds) => new()
    {
        OrganizationId = _organization.Id,
        OrganizationKeyB64 = _organizationKey,
        CollectionIds = collectionIds,
        UserId = userId,
        Name = "Login",
        FolderId = folderId
    };

    private Task SeedAsync(string scene, params Guid[] collectionIds) => scene switch
    {
        nameof(OrganizationLoginCipherScene) =>
            NewLoginScene().SeedAsync(LoginRequest(_memberUserId, folderId: null, collectionIds)),
        nameof(OrganizationBankAccountCipherScene) =>
            new OrganizationBankAccountCipherScene(_organizationRepository, _cipherRepository, _collectionRepository, _manglerService)
                .SeedAsync(new OrganizationBankAccountCipherScene.Request
                {
                    OrganizationId = _organization.Id,
                    OrganizationKeyB64 = _organizationKey,
                    CollectionIds = collectionIds,
                    Name = "Bank"
                }),
        nameof(OrganizationCardCipherScene) =>
            new OrganizationCardCipherScene(_organizationRepository, _cipherRepository, _collectionRepository, _manglerService)
                .SeedAsync(new OrganizationCardCipherScene.Request
                {
                    OrganizationId = _organization.Id,
                    OrganizationKeyB64 = _organizationKey,
                    CollectionIds = collectionIds,
                    Name = "Card",
                    CardholderName = "Jane Doe",
                    Number = "4111111111111111",
                    ExpMonth = "12",
                    ExpYear = "2030",
                    Code = "123"
                }),
        nameof(OrganizationDriversLicenseCipherScene) =>
            new OrganizationDriversLicenseCipherScene(_organizationRepository, _cipherRepository, _collectionRepository, _manglerService)
                .SeedAsync(new OrganizationDriversLicenseCipherScene.Request
                {
                    OrganizationId = _organization.Id,
                    OrganizationKeyB64 = _organizationKey,
                    CollectionIds = collectionIds,
                    Name = "License"
                }),
        nameof(OrganizationIdentityCipherScene) =>
            new OrganizationIdentityCipherScene(_organizationRepository, _cipherRepository, _collectionRepository, _manglerService)
                .SeedAsync(new OrganizationIdentityCipherScene.Request
                {
                    OrganizationId = _organization.Id,
                    OrganizationKeyB64 = _organizationKey,
                    CollectionIds = collectionIds,
                    Name = "Identity"
                }),
        nameof(OrganizationPassportCipherScene) =>
            new OrganizationPassportCipherScene(_organizationRepository, _cipherRepository, _collectionRepository, _manglerService)
                .SeedAsync(new OrganizationPassportCipherScene.Request
                {
                    OrganizationId = _organization.Id,
                    OrganizationKeyB64 = _organizationKey,
                    CollectionIds = collectionIds,
                    Name = "Passport"
                }),
        nameof(OrganizationSecureNoteCipherScene) =>
            new OrganizationSecureNoteCipherScene(_organizationRepository, _cipherRepository, _collectionRepository, _manglerService)
                .SeedAsync(new OrganizationSecureNoteCipherScene.Request
                {
                    OrganizationId = _organization.Id,
                    OrganizationKeyB64 = _organizationKey,
                    CollectionIds = collectionIds,
                    Name = "Note"
                }),
        _ => throw new ArgumentOutOfRangeException(nameof(scene), scene, null)
    };
}
