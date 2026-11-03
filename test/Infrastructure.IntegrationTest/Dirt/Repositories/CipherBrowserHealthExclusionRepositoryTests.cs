using System.Text.Json;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Dirt.Repositories;

public class CipherBrowserHealthExclusionRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task ExcludeAsync_PersonalLogin_ReturnsOneAndStoresRiskCategory(
        ICipherBrowserHealthExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var cipher = await CreateCipherAsync(cipherRepository, user.Id);

        var excluded = await sut.ExcludeAsync([cipher.Id], user.Id, "weak");

        Assert.Equal(1, excluded);
        var details = await cipherRepository.GetByIdAsync(cipher.Id, user.Id);
        Assert.Equal(["weak"], RiskCategories(details));
    }

    [DatabaseTheory, DatabaseData]
    public async Task ExcludeAsync_EmptyIds_ReturnsZero(
        ICipherBrowserHealthExclusionRepository sut, IUserRepository userRepository)
    {
        var user = await userRepository.CreateTestUserAsync();

        Assert.Equal(0, await sut.ExcludeAsync([], user.Id, "weak"));
    }

    [DatabaseTheory, DatabaseData]
    public async Task ExcludeAsync_OrganizationCipher_ReturnsZero(
        ICipherBrowserHealthExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository,
        IOrganizationRepository organizationRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var org = await organizationRepository.CreateTestOrganizationAsync();
        var cipher = await cipherRepository.CreateAsync(new Cipher
        {
            Type = CipherType.Login,
            UserId = user.Id,
            OrganizationId = org.Id,
            Data = "",
        });

        Assert.Equal(0, await sut.ExcludeAsync([cipher.Id], user.Id, "weak"));
    }

    [DatabaseTheory, DatabaseData]
    public async Task ExcludeAsync_NonLoginCipher_ReturnsZero(
        ICipherBrowserHealthExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var cipher = await CreateCipherAsync(cipherRepository, user.Id, CipherType.SecureNote);

        Assert.Equal(0, await sut.ExcludeAsync([cipher.Id], user.Id, "weak"));
    }

    [DatabaseTheory, DatabaseData]
    public async Task ExcludeAsync_DeletedCipher_ReturnsZero(
        ICipherBrowserHealthExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var cipher = await CreateCipherAsync(cipherRepository, user.Id);
        await cipherRepository.SoftDeleteAsync([cipher.Id], user.Id);

        Assert.Equal(0, await sut.ExcludeAsync([cipher.Id], user.Id, "weak"));
    }

    [DatabaseTheory, DatabaseData]
    public async Task ExcludeAsync_OtherUsersCipher_ReturnsZero(
        ICipherBrowserHealthExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository)
    {
        var owner = await userRepository.CreateTestUserAsync();
        var otherUser = await userRepository.CreateTestUserAsync();
        var cipher = await CreateCipherAsync(cipherRepository, owner.Id);

        Assert.Equal(0, await sut.ExcludeAsync([cipher.Id], otherUser.Id, "weak"));
        Assert.Null((await cipherRepository.GetByIdAsync(cipher.Id, owner.Id)).ExcludedRiskCategories);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ExcludeAsync_AnotherRiskCategory_ReturnsOneAndAddsIt(
        ICipherBrowserHealthExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var cipher = await CreateCipherAsync(cipherRepository, user.Id);
        await sut.ExcludeAsync([cipher.Id], user.Id, "weak");

        Assert.Equal(1, await sut.ExcludeAsync([cipher.Id], user.Id, "exposed"));
        Assert.Equal(["weak", "exposed"], RiskCategories(await cipherRepository.GetByIdAsync(cipher.Id, user.Id)));
    }

    [DatabaseTheory, DatabaseData]
    public async Task ExcludeAsync_SameRiskCategoryTwice_StoresItOnce(
        ICipherBrowserHealthExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var cipher = await CreateCipherAsync(cipherRepository, user.Id);
        await sut.ExcludeAsync([cipher.Id], user.Id, "weak");

        Assert.Equal(1, await sut.ExcludeAsync([cipher.Id], user.Id, "weak"));
        Assert.Equal(["weak"], RiskCategories(await cipherRepository.GetByIdAsync(cipher.Id, user.Id)));
    }

    [DatabaseTheory, DatabaseData]
    public async Task ExcludeAsync_MissingRiskCategory_ThrowsAndStoresNothing(
        ICipherBrowserHealthExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var cipher = await CreateCipherAsync(cipherRepository, user.Id);

        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.ExcludeAsync([cipher.Id], user.Id, null!));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.ExcludeAsync([cipher.Id], user.Id, " "));
        Assert.Null((await cipherRepository.GetByIdAsync(cipher.Id, user.Id)).ExcludedRiskCategories);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ExcludeAsync_DoesNotChangeCipherRevisionDate(
        ICipherBrowserHealthExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var cipher = await CreateCipherAsync(cipherRepository, user.Id);
        var before = (await cipherRepository.GetByIdAsync(cipher.Id, user.Id)).RevisionDate;

        await sut.ExcludeAsync([cipher.Id], user.Id, "weak");

        Assert.Equal(before, (await cipherRepository.GetByIdAsync(cipher.Id, user.Id)).RevisionDate);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ExcludeAsync_BumpsAccountRevisionDate(
        ICipherBrowserHealthExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var cipher = await CreateCipherAsync(cipherRepository, user.Id);
        var before = (await userRepository.GetByIdAsync(user.Id))!.AccountRevisionDate;

        await sut.ExcludeAsync([cipher.Id], user.Id, "weak");

        Assert.True((await userRepository.GetByIdAsync(user.Id))!.AccountRevisionDate > before);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByUserIdAsync_ReturnsExcludedRiskCategories(
        ICipherBrowserHealthExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var excludedCipher = await CreateCipherAsync(cipherRepository, user.Id);
        var otherCipher = await CreateCipherAsync(cipherRepository, user.Id);

        await sut.ExcludeAsync([excludedCipher.Id], user.Id, "weak");

        var ciphers = await cipherRepository.GetManyByUserIdAsync(user.Id);
        Assert.Equal(["weak"], RiskCategories(ciphers.Single(c => c.Id == excludedCipher.Id)));
        Assert.Null(ciphers.Single(c => c.Id == otherCipher.Id).ExcludedRiskCategories);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByUserIdAsync_WithoutOrganizations_ReturnsExcludedRiskCategories(
        ICipherBrowserHealthExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var cipher = await CreateCipherAsync(cipherRepository, user.Id);

        await sut.ExcludeAsync([cipher.Id], user.Id, "weak");

        var ciphers = await cipherRepository.GetManyByUserIdAsync(user.Id, withOrganizations: false);
        Assert.Equal(["weak"], RiskCategories(ciphers.Single(c => c.Id == cipher.Id)));
    }

    [DatabaseTheory, DatabaseData]
    public async Task ReplaceAsync_CipherDetails_PreservesExclusions(
        ICipherBrowserHealthExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var cipher = await CreateCipherAsync(cipherRepository, user.Id);
        await sut.ExcludeAsync([cipher.Id], user.Id, "weak");

        var details = await cipherRepository.GetByIdAsync(cipher.Id, user.Id);
        details.Data = "{\"updated\":true}";
        await cipherRepository.ReplaceAsync(details);

        Assert.Equal(["weak"], RiskCategories((await cipherRepository.GetByIdAsync(cipher.Id, user.Id))));
    }

    [DatabaseTheory, DatabaseData]
    public async Task ReplaceAsync_Cipher_PreservesExclusions(
        ICipherBrowserHealthExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var cipher = await CreateCipherAsync(cipherRepository, user.Id);
        await sut.ExcludeAsync([cipher.Id], user.Id, "weak");

        cipher.Data = "{\"updated\":true}";
        await cipherRepository.ReplaceAsync(cipher);

        Assert.Equal(["weak"], RiskCategories((await cipherRepository.GetByIdAsync(cipher.Id, user.Id))));
    }

    [DatabaseTheory, DatabaseData]
    public async Task SoftDeleteAsync_KeepsBrowserHealthExclusion(
        ICipherBrowserHealthExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var cipher = await CreateCipherAsync(cipherRepository, user.Id);
        await sut.ExcludeAsync([cipher.Id], user.Id, "weak");

        await cipherRepository.SoftDeleteAsync([cipher.Id], user.Id);

        Assert.Equal(["weak"], RiskCategories((await cipherRepository.GetByIdAsync(cipher.Id, user.Id))));
    }

    [DatabaseTheory, DatabaseData]
    public async Task ReplaceAsync_ShareToOrg_KeepsOwnersExclusionOnly(
        ICipherBrowserHealthExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository,
        IOrganizationRepository organizationRepository, IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        var owner = await userRepository.CreateTestUserAsync();
        var member = await userRepository.CreateTestUserAsync();
        var org = await organizationRepository.CreateTestOrganizationAsync();
        var ownerOrgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(org, owner);
        var memberOrgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(org, member);
        var collection = await collectionRepository.CreateAsync(new Collection { Name = "Shared", OrganizationId = org.Id });
        await collectionRepository.UpdateUsersAsync(collection.Id, new List<CollectionAccessSelection>
        {
            new() { Id = ownerOrgUser.Id, Manage = true },
            new() { Id = memberOrgUser.Id, Manage = true },
        });
        var cipher = await CreateCipherAsync(cipherRepository, owner.Id);
        await sut.ExcludeAsync([cipher.Id], owner.Id, "weak");

        cipher.OrganizationId = org.Id;
        await cipherRepository.ReplaceAsync(cipher, [collection.Id]);

        Assert.Equal(["weak"], RiskCategories((await cipherRepository.GetByIdAsync(cipher.Id, owner.Id))));
        Assert.Null((await cipherRepository.GetManyByUserIdAsync(member.Id)).Single(c => c.Id == cipher.Id).ExcludedRiskCategories);
    }
    private static List<string> RiskCategories(CipherDetails cipher) =>
        JsonSerializer.Deserialize<List<string>>(cipher.ExcludedRiskCategories);

    private static Task<Cipher> CreateCipherAsync(ICipherRepository cipherRepository, Guid userId,
        CipherType type = CipherType.Login) =>
        cipherRepository.CreateAsync(new Cipher { Type = type, UserId = userId, Data = "" });


}
