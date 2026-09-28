using Bit.Core.Entities;
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
        var pending = await sut.GetManyPendingV2UpgradesByOrganizationIdAsync(organization.Id);

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
        var pending = await sut.GetManyPendingV2UpgradesByOrganizationIdAsync(organization.Id);

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
        var pending = await sut.GetManyPendingV2UpgradesByOrganizationIdAsync(organization.Id);

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
        var pending = await sut.GetManyPendingV2UpgradesByOrganizationIdAsync(organization.Id);

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
        var (_, organizationUser) = await CreateMemberAsync(userRepository, organizationUserRepository, organization,
            _userKeyId, _v1AccountRecoveryKey, _v2UpgradeToken);

        // Act
        await sut.UpdateManyV2UpgradedAccountRecoveryKeysAsync(organization.Id,
            [Update(organizationUser.Id, _userKeyId)]);

        // Assert
        var written = await organizationUserRepository.GetByIdAsync(organizationUser.Id);
        Assert.Equal(_v2AccountRecoveryKey, written!.ResetPasswordKey);
        Assert.Null(written.V2UpgradeToken);
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
        await sut.UpdateManyV2UpgradedAccountRecoveryKeysAsync(organization.Id,
            [Update(organizationUser.Id, _userKeyId, accountRecoveryKey: null)]);

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
        var (_, organizationUser) = await CreateMemberAsync(userRepository, organizationUserRepository, organization,
            _rotatedUserKeyId, _v1AccountRecoveryKey, _v2UpgradeToken);

        // Act
        await sut.UpdateManyV2UpgradedAccountRecoveryKeysAsync(organization.Id,
            [Update(organizationUser.Id, _userKeyId, accountRecoveryKey: null)]);

        // Assert
        var written = await organizationUserRepository.GetByIdAsync(organizationUser.Id);
        Assert.Equal(_v1AccountRecoveryKey, written!.ResetPasswordKey);
        Assert.Equal(_v2UpgradeToken, written.V2UpgradeToken);
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

        // Act
        await sut.UpdateManyV2UpgradedAccountRecoveryKeysAsync(organization.Id,
            [Update(organizationUser.Id, _userKeyId)]);

        // Assert
        var written = await organizationUserRepository.GetByIdAsync(organizationUser.Id);
        Assert.Equal(_v1AccountRecoveryKey, written!.ResetPasswordKey);
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
        await sut.UpdateManyV2UpgradedAccountRecoveryKeysAsync(organization.Id,
            [Update(fresh.Id, _userKeyId), Update(stale.Id, _userKeyId)]);

        // Assert
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
        await sut.UpdateManyV2UpgradedAccountRecoveryKeysAsync(organization.Id,
            [Update(organizationUser.Id, _userKeyId)]);

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
        await sut.UpdateManyV2UpgradedAccountRecoveryKeysAsync(organization.Id,
            [Update(organizationUser.Id, _userKeyId)]);

        // Assert
        var written = await organizationUserRepository.GetByIdAsync(organizationUser.Id);
        Assert.Equal(_v1AccountRecoveryKey, written!.ResetPasswordKey);
    }

    [Theory, DatabaseData]
    public async Task UpdateManyV2UpgradedAccountRecoveryKeysAsync_SameMembershipTwice_WritesTheKeyOnce(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationUserKeyRepository sut)
    {
        // Arrange - the request model rejects a repeated membership, so the repository only has to stay graceful
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var (_, organizationUser) = await CreateMemberAsync(userRepository, organizationUserRepository, organization,
            _userKeyId, _v1AccountRecoveryKey, _v2UpgradeToken);

        // Act
        await sut.UpdateManyV2UpgradedAccountRecoveryKeysAsync(organization.Id,
            [Update(organizationUser.Id, _userKeyId), Update(organizationUser.Id, _userKeyId)]);

        // Assert
        var written = await organizationUserRepository.GetByIdAsync(organizationUser.Id);
        Assert.Equal(_v2AccountRecoveryKey, written!.ResetPasswordKey);
        Assert.Null(written.V2UpgradeToken);
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
        string? v2UpgradeToken)
    {
        var user = await userRepository.CreateTestUserAsync();
        user.UserKeyId = userKeyId;
        await userRepository.ReplaceAsync(user);

        var organizationUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);
        organizationUser.ResetPasswordKey = accountRecoveryKey;
        organizationUser.V2UpgradeToken = v2UpgradeToken;
        await organizationUserRepository.ReplaceAsync(organizationUser);

        return (user, organizationUser);
    }
}
