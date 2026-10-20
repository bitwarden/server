using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.Repositories;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.KeyManagement.Repositories;

public class OrganizationUserKeyRepositoryTests
{
    private const string _userKeyId = "0123456789abcdef0123456789abcdef";
    private const string _rotatedUserKeyId = "fedcba9876543210fedcba9876543210";
    private const string _v1AccountRecoveryKey = "4.v1-account-recovery-key";
    private const string _v2AccountRecoveryKey = "4.v2-account-recovery-key";
    private const string _v2UpgradeToken = """{"WrappedUserKey1":"7.key-one","WrappedUserKey2":"2.key-two"}""";
    private const int _maxCount = 100;

    [Theory, DatabaseData]
    public async Task GetManyPendingV2UpgradesByOrganizationIdAsync_MembershipWithAToken_IsReturned(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationUserKeyRepository sut)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var (_, organizationUser) = await CreateMemberAsync(userRepository, organizationUserRepository, organization,
            _userKeyId, _v1AccountRecoveryKey, _v2UpgradeToken);

        // Act
        var pending = await sut.GetManyPendingV2UpgradesByOrganizationIdAsync(organization.Id, includeOwners: true, afterId: null, _maxCount);

        // Assert
        var result = Assert.Single(pending);
        Assert.Equal(organizationUser.Id, result.OrganizationUserId);
        Assert.Equal(_userKeyId, result.UserKeyId);
        Assert.Equal(_v1AccountRecoveryKey, result.AccountRecoveryKey);
        Assert.Equal(_v2UpgradeToken, result.V2UpgradeToken);
    }

    [Theory, DatabaseData]
    public async Task GetManyPendingV2UpgradesByOrganizationIdAsync_MembershipWithoutAToken_IsNotReturned(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationUserKeyRepository sut)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        await CreateMemberAsync(userRepository, organizationUserRepository, organization,
            _userKeyId, _v1AccountRecoveryKey, v2UpgradeToken: null);

        // Act
        var pending = await sut.GetManyPendingV2UpgradesByOrganizationIdAsync(organization.Id, includeOwners: true, afterId: null, _maxCount);

        // Assert
        Assert.Empty(pending);
    }

    [Theory, DatabaseData]
    public async Task GetManyPendingV2UpgradesByOrganizationIdAsync_MembershipWithoutAUserKeyId_IsNotReturned(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationUserKeyRepository sut)
    {
        // Arrange - without a key id the server cannot validate what the admin re-wrapped
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        await CreateMemberAsync(userRepository, organizationUserRepository, organization,
            userKeyId: null, _v1AccountRecoveryKey, _v2UpgradeToken);

        // Act
        var pending = await sut.GetManyPendingV2UpgradesByOrganizationIdAsync(organization.Id, includeOwners: true, afterId: null, _maxCount);

        // Assert
        Assert.Empty(pending);
    }

    [Theory, DatabaseData]
    public async Task GetManyPendingV2UpgradesByOrganizationIdAsync_MembershipOfAnotherOrganization_IsNotReturned(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationUserKeyRepository sut)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync();
        await CreateMemberAsync(userRepository, organizationUserRepository, otherOrganization,
            _userKeyId, _v1AccountRecoveryKey, _v2UpgradeToken);

        // Act
        var pending = await sut.GetManyPendingV2UpgradesByOrganizationIdAsync(organization.Id, includeOwners: true, afterId: null, _maxCount);

        // Assert
        Assert.Empty(pending);
    }

    [Theory, DatabaseData]
    public async Task UpdateManyV2UpgradedAccountRecoveryKeysAsync_KeyIdMatches_WritesTheKeyAndClearsTheToken(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationUserKeyRepository sut)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var (user, organizationUser) = await CreateMemberAsync(userRepository, organizationUserRepository, organization,
            _userKeyId, _v1AccountRecoveryKey, _v2UpgradeToken);
        var before = (await userRepository.GetByIdAsync(user.Id))!.AccountRevisionDate;
        // Ahead of the member's current account revision date, so the bump is visible
        var revisionDate = DateTime.UtcNow.AddMinutes(1);

        // Act
        var updatedIds = await sut.UpdateManyV2UpgradedAccountRecoveryKeysAsync(organization.Id, includeOwners: true,
            [Update(organizationUser.Id, _userKeyId)], revisionDate);

        // Assert
        Assert.Equal([organizationUser.Id], updatedIds);
        var written = await organizationUserRepository.GetByIdAsync(organizationUser.Id);
        Assert.Equal(_v2AccountRecoveryKey, written!.ResetPasswordKey);
        Assert.Null(written.V2UpgradeToken);
        // Databases store fractions of a second with different precision
        Assert.Equal(revisionDate, written.RevisionDate, TimeSpan.FromSeconds(1));
        // The member's clients resync from this date, so they learn that their enrollment changed
        var after = (await userRepository.GetByIdAsync(user.Id))!.AccountRevisionDate;
        Assert.True(after > before);
    }

    [Theory, DatabaseData]
    public async Task UpdateManyV2UpgradedAccountRecoveryKeysAsync_NoKeyGiven_ClearsTheKeyAndTheToken(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationUserKeyRepository sut)
    {
        // Arrange - the upgrade cannot be completed, so the admin unenrolls the member instead
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var (_, organizationUser) = await CreateMemberAsync(userRepository, organizationUserRepository, organization,
            _userKeyId, _v1AccountRecoveryKey, _v2UpgradeToken);

        // Act
        await sut.UpdateManyV2UpgradedAccountRecoveryKeysAsync(organization.Id, includeOwners: true,
            [Update(organizationUser.Id, _userKeyId, accountRecoveryKey: null)], DateTime.UtcNow);

        // Assert
        var written = await organizationUserRepository.GetByIdAsync(organizationUser.Id);
        Assert.Null(written!.ResetPasswordKey);
        Assert.Null(written.V2UpgradeToken);
    }

    [Theory, DatabaseData]
    public async Task UpdateManyV2UpgradedAccountRecoveryKeysAsync_NoKeyGivenAndKeyIdIsStale_WritesNothing(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationUserKeyRepository sut)
    {
        // Arrange - the member rotated again, so their pending upgrade is not the one the admin abandoned
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var (user, organizationUser) = await CreateMemberAsync(userRepository, organizationUserRepository, organization,
            _rotatedUserKeyId, _v1AccountRecoveryKey, _v2UpgradeToken);
        var before = (await userRepository.GetByIdAsync(user.Id))!.AccountRevisionDate;

        // Act
        await sut.UpdateManyV2UpgradedAccountRecoveryKeysAsync(organization.Id, includeOwners: true,
            [Update(organizationUser.Id, _userKeyId, accountRecoveryKey: null)], DateTime.UtcNow);

        // Assert
        var written = await organizationUserRepository.GetByIdAsync(organizationUser.Id);
        Assert.Equal(_v1AccountRecoveryKey, written!.ResetPasswordKey);
        Assert.Equal(_v2UpgradeToken, written.V2UpgradeToken);
        var after = (await userRepository.GetByIdAsync(user.Id))!.AccountRevisionDate;
        Assert.Equal(before, after);
    }

    [Theory, DatabaseData]
    public async Task UpdateManyV2UpgradedAccountRecoveryKeysAsync_KeyIdIsStale_WritesNothing(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationUserKeyRepository sut)
    {
        // Arrange - the member rotated again, so the re-wrapped key uses a user key they no longer hold
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var (_, organizationUser) = await CreateMemberAsync(userRepository, organizationUserRepository, organization,
            _rotatedUserKeyId, _v1AccountRecoveryKey, _v2UpgradeToken);
        var before = (await organizationUserRepository.GetByIdAsync(organizationUser.Id))!.RevisionDate;

        // Act
        var updatedIds = await sut.UpdateManyV2UpgradedAccountRecoveryKeysAsync(organization.Id, includeOwners: true,
            [Update(organizationUser.Id, _userKeyId)], DateTime.UtcNow);

        // Assert
        Assert.Empty(updatedIds);
        var written = await organizationUserRepository.GetByIdAsync(organizationUser.Id);
        Assert.Equal(_v1AccountRecoveryKey, written!.ResetPasswordKey);
        Assert.Equal(_v2UpgradeToken, written.V2UpgradeToken);
        Assert.Equal(before, written.RevisionDate);
    }

    [Theory, DatabaseData]
    public async Task UpdateManyV2UpgradedAccountRecoveryKeysAsync_MembershipIsNotEnrolled_WritesNothing(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationUserKeyRepository sut)
    {
        // Arrange - the member withdrew after the admin read the upgrade, which leaves the token in place
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var (_, organizationUser) = await CreateMemberAsync(userRepository, organizationUserRepository, organization,
            _userKeyId, accountRecoveryKey: null, _v2UpgradeToken);

        // Act
        var updatedIds = await sut.UpdateManyV2UpgradedAccountRecoveryKeysAsync(organization.Id, includeOwners: true,
            [Update(organizationUser.Id, _userKeyId)], DateTime.UtcNow);

        // Assert
        Assert.Empty(updatedIds);
        var written = await organizationUserRepository.GetByIdAsync(organizationUser.Id);
        Assert.Null(written!.ResetPasswordKey);
        Assert.Equal(_v2UpgradeToken, written.V2UpgradeToken);
    }

    [Theory, DatabaseData]
    public async Task UpdateManyV2UpgradedAccountRecoveryKeysAsync_OneOfTwoKeyIdsIsStale_WritesTheOtherRow(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationUserKeyRepository sut)
    {
        // Arrange - one stale row must not hold back the row that can still be upgraded
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var (_, fresh) = await CreateMemberAsync(userRepository, organizationUserRepository, organization,
            _userKeyId, _v1AccountRecoveryKey, _v2UpgradeToken);
        var (_, stale) = await CreateMemberAsync(userRepository, organizationUserRepository, organization,
            _rotatedUserKeyId, _v1AccountRecoveryKey, _v2UpgradeToken);

        // Act
        var updatedIds = await sut.UpdateManyV2UpgradedAccountRecoveryKeysAsync(organization.Id, includeOwners: true,
            [Update(fresh.Id, _userKeyId), Update(stale.Id, _userKeyId)], DateTime.UtcNow);

        // Assert
        Assert.Equal([fresh.Id], updatedIds);
        var writtenFresh = await organizationUserRepository.GetByIdAsync(fresh.Id);
        Assert.Equal(_v2AccountRecoveryKey, writtenFresh!.ResetPasswordKey);
        Assert.Null(writtenFresh.V2UpgradeToken);

        var writtenStale = await organizationUserRepository.GetByIdAsync(stale.Id);
        Assert.Equal(_v1AccountRecoveryKey, writtenStale!.ResetPasswordKey);
        Assert.Equal(_v2UpgradeToken, writtenStale.V2UpgradeToken);
    }

    [Theory, DatabaseData]
    public async Task UpdateManyV2UpgradedAccountRecoveryKeysAsync_MembershipOfAnotherOrganization_WritesNothing(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationUserKeyRepository sut)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync();
        var (_, organizationUser) = await CreateMemberAsync(userRepository, organizationUserRepository,
            otherOrganization, _userKeyId, _v1AccountRecoveryKey, _v2UpgradeToken);

        // Act
        await sut.UpdateManyV2UpgradedAccountRecoveryKeysAsync(organization.Id, includeOwners: true,
            [Update(organizationUser.Id, _userKeyId)], DateTime.UtcNow);

        // Assert
        var written = await organizationUserRepository.GetByIdAsync(organizationUser.Id);
        Assert.Equal(_v1AccountRecoveryKey, written!.ResetPasswordKey);
        Assert.Equal(_v2UpgradeToken, written.V2UpgradeToken);
    }

    [Theory, DatabaseData]
    public async Task UpdateManyV2UpgradedAccountRecoveryKeysAsync_MembershipHasNoToken_WritesNothing(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationUserKeyRepository sut)
    {
        // Arrange - an upgrade that already ran must not replay over a newer account recovery key
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var (_, organizationUser) = await CreateMemberAsync(userRepository, organizationUserRepository, organization,
            _userKeyId, _v1AccountRecoveryKey, v2UpgradeToken: null);

        // Act
        await sut.UpdateManyV2UpgradedAccountRecoveryKeysAsync(organization.Id, includeOwners: true,
            [Update(organizationUser.Id, _userKeyId)], DateTime.UtcNow);

        // Assert
        var written = await organizationUserRepository.GetByIdAsync(organizationUser.Id);
        Assert.Equal(_v1AccountRecoveryKey, written!.ResetPasswordKey);
    }

    [Theory, DatabaseData]
    public async Task GetManyPendingV2UpgradesByOrganizationIdAsync_OwnersNotIncluded_LeavesOutTheOwner(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationUserKeyRepository sut)
    {
        // Arrange - an Admin cannot access the key material of an Owner
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        await CreateMemberAsync(userRepository, organizationUserRepository, organization,
            _userKeyId, _v1AccountRecoveryKey, _v2UpgradeToken, OrganizationUserType.Owner);
        var (_, admin) = await CreateMemberAsync(userRepository, organizationUserRepository, organization,
            _userKeyId, _v1AccountRecoveryKey, _v2UpgradeToken, OrganizationUserType.Admin);

        // Act
        var pending = await sut.GetManyPendingV2UpgradesByOrganizationIdAsync(organization.Id,
            includeOwners: false, afterId: null, _maxCount);

        // Assert
        var result = Assert.Single(pending);
        Assert.Equal(admin.Id, result.OrganizationUserId);
    }

    [Theory, DatabaseData]
    public async Task GetManyPendingV2UpgradesByOrganizationIdAsync_OwnersIncluded_ReturnsTheOwner(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationUserKeyRepository sut)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var (_, owner) = await CreateMemberAsync(userRepository, organizationUserRepository, organization,
            _userKeyId, _v1AccountRecoveryKey, _v2UpgradeToken, OrganizationUserType.Owner);

        // Act
        var pending = await sut.GetManyPendingV2UpgradesByOrganizationIdAsync(organization.Id,
            includeOwners: true, afterId: null, _maxCount);

        // Assert
        var result = Assert.Single(pending);
        Assert.Equal(owner.Id, result.OrganizationUserId);
    }

    [Theory, DatabaseData]
    public async Task GetManyPendingV2UpgradesByOrganizationIdAsync_MemberOfAProvider_IsReturned(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IProviderRepository providerRepository,
        IProviderUserRepository providerUserRepository,
        IOrganizationUserKeyRepository sut)
    {
        // Arrange - the members of a provider's own organization are upgraded by that organization's admins
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var (user, organizationUser) = await CreateMemberAsync(userRepository, organizationUserRepository,
            organization, _userKeyId, _v1AccountRecoveryKey, _v2UpgradeToken);
        await AddToProviderAsync(providerRepository, providerUserRepository, user);

        // Act
        var pending = await sut.GetManyPendingV2UpgradesByOrganizationIdAsync(organization.Id,
            includeOwners: true, afterId: null, _maxCount);

        // Assert
        var result = Assert.Single(pending);
        Assert.Equal(organizationUser.Id, result.OrganizationUserId);
    }

    [Theory, DatabaseData]
    public async Task GetManyPendingV2UpgradesByOrganizationIdAsync_MoreThanTheMaxCount_ReturnsTheMaxCount(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationUserKeyRepository sut)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        for (var i = 0; i < 3; i++)
        {
            await CreateMemberAsync(userRepository, organizationUserRepository, organization,
                _userKeyId, _v1AccountRecoveryKey, _v2UpgradeToken);
        }

        // Act
        var pending = await sut.GetManyPendingV2UpgradesByOrganizationIdAsync(organization.Id,
            includeOwners: true, afterId: null, maxCount: 2);

        // Assert
        Assert.Equal(2, pending.Count);
    }

    [Theory, DatabaseData]
    public async Task GetManyPendingV2UpgradesByOrganizationIdAsync_ReadAfterTheLastIdOfAPage_ReturnsTheRemainingMemberships(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationUserKeyRepository sut)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var organizationUserIds = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var (_, organizationUser) = await CreateMemberAsync(userRepository, organizationUserRepository,
                organization, _userKeyId, _v1AccountRecoveryKey, _v2UpgradeToken);
            organizationUserIds.Add(organizationUser.Id);
        }

        var firstPage = await sut.GetManyPendingV2UpgradesByOrganizationIdAsync(organization.Id,
            includeOwners: true, afterId: null, maxCount: 2);

        // Act
        var secondPage = await sut.GetManyPendingV2UpgradesByOrganizationIdAsync(organization.Id,
            includeOwners: true, afterId: firstPage.Last().OrganizationUserId, maxCount: 2);

        // Assert
        var read = firstPage.Concat(secondPage).Select(details => details.OrganizationUserId).ToList();
        Assert.Equal(organizationUserIds.Order(), read.Order());
    }

    [Theory, DatabaseData]
    public async Task UpdateManyV2UpgradedAccountRecoveryKeysAsync_OwnerAndOwnersNotIncluded_WritesNothing(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationUserKeyRepository sut)
    {
        // Arrange - an Admin cannot change the key material of an Owner
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var (_, owner) = await CreateMemberAsync(userRepository, organizationUserRepository, organization,
            _userKeyId, _v1AccountRecoveryKey, _v2UpgradeToken, OrganizationUserType.Owner);

        // Act
        var updatedIds = await sut.UpdateManyV2UpgradedAccountRecoveryKeysAsync(organization.Id,
            includeOwners: false, [Update(owner.Id, _userKeyId)], DateTime.UtcNow);

        // Assert
        Assert.Empty(updatedIds);
        var written = await organizationUserRepository.GetByIdAsync(owner.Id);
        Assert.Equal(_v1AccountRecoveryKey, written!.ResetPasswordKey);
        Assert.Equal(_v2UpgradeToken, written.V2UpgradeToken);
    }

    [Theory, DatabaseData]
    public async Task UpdateManyV2UpgradedAccountRecoveryKeysAsync_OwnerAndOwnersIncluded_WritesTheKey(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationUserKeyRepository sut)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var (_, owner) = await CreateMemberAsync(userRepository, organizationUserRepository, organization,
            _userKeyId, _v1AccountRecoveryKey, _v2UpgradeToken, OrganizationUserType.Owner);

        // Act
        var updatedIds = await sut.UpdateManyV2UpgradedAccountRecoveryKeysAsync(organization.Id,
            includeOwners: true, [Update(owner.Id, _userKeyId)], DateTime.UtcNow);

        // Assert
        Assert.Equal([owner.Id], updatedIds);
        var written = await organizationUserRepository.GetByIdAsync(owner.Id);
        Assert.Equal(_v2AccountRecoveryKey, written!.ResetPasswordKey);
        Assert.Null(written.V2UpgradeToken);
    }

    [Theory, DatabaseData]
    public async Task UpdateManyV2UpgradedAccountRecoveryKeysAsync_MemberOfAProvider_WritesTheKey(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IProviderRepository providerRepository,
        IProviderUserRepository providerUserRepository,
        IOrganizationUserKeyRepository sut)
    {
        // Arrange - the members of a provider's own organization are upgraded by that organization's admins
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var (user, organizationUser) = await CreateMemberAsync(userRepository, organizationUserRepository,
            organization, _userKeyId, _v1AccountRecoveryKey, _v2UpgradeToken);
        await AddToProviderAsync(providerRepository, providerUserRepository, user);

        // Act
        var updatedIds = await sut.UpdateManyV2UpgradedAccountRecoveryKeysAsync(organization.Id,
            includeOwners: true, [Update(organizationUser.Id, _userKeyId)], DateTime.UtcNow);

        // Assert
        Assert.Equal([organizationUser.Id], updatedIds);
        var written = await organizationUserRepository.GetByIdAsync(organizationUser.Id);
        Assert.Equal(_v2AccountRecoveryKey, written!.ResetPasswordKey);
        Assert.Null(written.V2UpgradeToken);
    }

    private static async Task AddToProviderAsync(IProviderRepository providerRepository,
        IProviderUserRepository providerUserRepository, User user)
    {
        var provider = await providerRepository.CreateAsync(new Provider
        {
            Name = "Test Provider",
            Type = ProviderType.Msp,
            Status = ProviderStatusType.Created,
            Enabled = true
        });

        await providerUserRepository.CreateAsync(new ProviderUser
        {
            ProviderId = provider.Id,
            UserId = user.Id,
            Type = ProviderUserType.ServiceUser,
            Status = ProviderUserStatusType.Confirmed
        });
    }

    private static OrganizationUserAccountRecoveryKeyUpdate Update(Guid organizationUserId, string userKeyId,
        string? accountRecoveryKey = _v2AccountRecoveryKey) =>
        new()
        {
            OrganizationUserId = organizationUserId,
            UserKeyId = userKeyId,
            AccountRecoveryKey = accountRecoveryKey
        };

    private static async Task<(User User, OrganizationUser OrganizationUser)> CreateMemberAsync(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        Bit.Core.AdminConsole.Entities.Organization organization,
        string? userKeyId,
        string? accountRecoveryKey,
        string? v2UpgradeToken,
        OrganizationUserType type = OrganizationUserType.User)
    {
        var user = await userRepository.CreateTestUserAsync();
        user.UserKeyId = userKeyId;
        // Dated back, so a test can tell an account revision date the write bumped from the one it was created with
        user.AccountRevisionDate = DateTime.UtcNow.AddDays(-1);
        await userRepository.ReplaceAsync(user);

        var organizationUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);
        organizationUser.Type = type;
        organizationUser.ResetPasswordKey = accountRecoveryKey;
        organizationUser.V2UpgradeToken = v2UpgradeToken;
        // Dated back, so a test can tell a revision date the write bumped from the one it was created with
        organizationUser.RevisionDate = DateTime.UtcNow.AddDays(-1);
        await organizationUserRepository.ReplaceAsync(organizationUser);

        return (user, organizationUser);
    }
}
