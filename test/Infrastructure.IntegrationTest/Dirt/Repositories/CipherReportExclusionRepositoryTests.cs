using Bit.Core.Dirt.Repositories;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Dirt.Repositories;

public class CipherReportExclusionRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task ExcludeAsync_PersonalLogin_ReturnsOneAndSetsUsersReportExcludedDate(
        ICipherReportExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var cipher = await CreateCipherAsync(cipherRepository, user.Id);

        var excluded = await sut.ExcludeAsync([cipher.Id], user.Id);

        Assert.Equal(1, excluded);
        var details = await cipherRepository.GetByIdAsync(cipher.Id, user.Id);
        Assert.NotNull(details.ReportExcludedDate);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ExcludeAsync_OrganizationCipher_ReturnsZero(
        ICipherReportExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository,
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

        Assert.Equal(0, await sut.ExcludeAsync([cipher.Id], user.Id));
    }

    [DatabaseTheory, DatabaseData]
    public async Task ExcludeAsync_NonLoginCipher_ReturnsZero(
        ICipherReportExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var cipher = await CreateCipherAsync(cipherRepository, user.Id, CipherType.SecureNote);

        Assert.Equal(0, await sut.ExcludeAsync([cipher.Id], user.Id));
    }

    [DatabaseTheory, DatabaseData]
    public async Task ExcludeAsync_DeletedCipher_ReturnsZero(
        ICipherReportExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var cipher = await CreateCipherAsync(cipherRepository, user.Id);
        await cipherRepository.SoftDeleteAsync([cipher.Id], user.Id);

        Assert.Equal(0, await sut.ExcludeAsync([cipher.Id], user.Id));
    }

    [DatabaseTheory, DatabaseData]
    public async Task ExcludeAsync_OtherUsersCipher_ReturnsZero(
        ICipherReportExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository)
    {
        var owner = await userRepository.CreateTestUserAsync();
        var otherUser = await userRepository.CreateTestUserAsync();
        var cipher = await CreateCipherAsync(cipherRepository, owner.Id);

        Assert.Equal(0, await sut.ExcludeAsync([cipher.Id], otherUser.Id));
        Assert.Null((await cipherRepository.GetByIdAsync(cipher.Id, owner.Id)).ReportExcludedDate);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ExcludeAsync_AlreadyExcluded_ReturnsOne(
        ICipherReportExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var cipher = await CreateCipherAsync(cipherRepository, user.Id);
        await sut.ExcludeAsync([cipher.Id], user.Id);

        Assert.Equal(1, await sut.ExcludeAsync([cipher.Id], user.Id));
    }

    [DatabaseTheory, DatabaseData]
    public async Task ExcludeAsync_DoesNotChangeCipherRevisionDate(
        ICipherReportExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var cipher = await CreateCipherAsync(cipherRepository, user.Id);
        var before = (await cipherRepository.GetByIdAsync(cipher.Id, user.Id)).RevisionDate;

        await sut.ExcludeAsync([cipher.Id], user.Id);

        Assert.Equal(before, (await cipherRepository.GetByIdAsync(cipher.Id, user.Id)).RevisionDate);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ExcludeAsync_BumpsAccountRevisionDate(
        ICipherReportExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var cipher = await CreateCipherAsync(cipherRepository, user.Id);
        var before = (await userRepository.GetByIdAsync(user.Id))!.AccountRevisionDate;

        await sut.ExcludeAsync([cipher.Id], user.Id);

        Assert.True((await userRepository.GetByIdAsync(user.Id))!.AccountRevisionDate > before);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByUserIdAsync_ReturnsReportExcludedDate(
        ICipherReportExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var excludedCipher = await CreateCipherAsync(cipherRepository, user.Id);
        var otherCipher = await CreateCipherAsync(cipherRepository, user.Id);

        await sut.ExcludeAsync([excludedCipher.Id], user.Id);

        var ciphers = await cipherRepository.GetManyByUserIdAsync(user.Id);
        Assert.NotNull(ciphers.Single(c => c.Id == excludedCipher.Id).ReportExcludedDate);
        Assert.Null(ciphers.Single(c => c.Id == otherCipher.Id).ReportExcludedDate);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ReplaceAsync_CipherDetails_PreservesReportExclusions(
        ICipherReportExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var cipher = await CreateCipherAsync(cipherRepository, user.Id);
        await sut.ExcludeAsync([cipher.Id], user.Id);

        var details = await cipherRepository.GetByIdAsync(cipher.Id, user.Id);
        details.Data = "{\"updated\":true}";
        await cipherRepository.ReplaceAsync(details);

        Assert.NotNull((await cipherRepository.GetByIdAsync(cipher.Id, user.Id)).ReportExcludedDate);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ReplaceAsync_Cipher_PreservesReportExclusions(
        ICipherReportExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var cipher = await CreateCipherAsync(cipherRepository, user.Id);
        await sut.ExcludeAsync([cipher.Id], user.Id);

        cipher.Data = "{\"updated\":true}";
        await cipherRepository.ReplaceAsync(cipher);

        Assert.NotNull((await cipherRepository.GetByIdAsync(cipher.Id, user.Id)).ReportExcludedDate);
    }

    [DatabaseTheory, DatabaseData]
    public async Task SoftDeleteAsync_KeepsReportExclusion(
        ICipherReportExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var cipher = await CreateCipherAsync(cipherRepository, user.Id);
        await sut.ExcludeAsync([cipher.Id], user.Id);

        await cipherRepository.SoftDeleteAsync([cipher.Id], user.Id);

        Assert.NotNull((await cipherRepository.GetByIdAsync(cipher.Id, user.Id)).ReportExcludedDate);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ReplaceAsync_ShareToOrg_KeepsOwnersExclusionOnly(
        ICipherReportExclusionRepository sut, ICipherRepository cipherRepository, IUserRepository userRepository,
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
        await sut.ExcludeAsync([cipher.Id], owner.Id);

        cipher.OrganizationId = org.Id;
        await cipherRepository.ReplaceAsync(cipher, [collection.Id]);

        Assert.NotNull((await cipherRepository.GetByIdAsync(cipher.Id, owner.Id)).ReportExcludedDate);
        Assert.Null((await cipherRepository.GetManyByUserIdAsync(member.Id)).Single(c => c.Id == cipher.Id).ReportExcludedDate);
    }
    private static Task<Cipher> CreateCipherAsync(ICipherRepository cipherRepository, Guid userId,
        CipherType type = CipherType.Login) =>
        cipherRepository.CreateAsync(new Cipher { Type = type, UserId = userId, Data = "" });


}
