using Bit.Core.KeyManagement.Commands;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.KeyManagement.Commands;

[SutProviderCustomize]
public class ApplyOrganizationUserV2UpgradesCommandTests
{
    private const string UserKeyId = "0123456789abcdef0123456789abcdef";
    private const string OtherUserKeyId = "fedcba9876543210fedcba9876543210";

    [Theory, BitAutoData]
    public async Task ApplyAsync_KeyIdsMatch_WritesTheRewrappedKeys(
        Guid organizationId,
        Guid organizationUserId,
        string accountRecoveryKey,
        SutProvider<ApplyOrganizationUserV2UpgradesCommand> sutProvider)
    {
        // Arrange
        var repository = sutProvider.GetDependency<IOrganizationUserKeyRepository>();
        repository.GetManyPendingV2UpgradesByOrganizationIdAsync(organizationId)
            .Returns(PendingUpgrades((organizationUserId, UserKeyId)));

        var updates = new[] { Update(organizationUserId, UserKeyId, accountRecoveryKey) };

        // Act
        await sutProvider.Sut.ApplyAsync(organizationId, updates);

        // Assert
        await repository.Received(1).UpdateManyV2UpgradedAccountRecoveryKeysAsync(
            organizationId,
            Arg.Is<IEnumerable<OrganizationUserAccountRecoveryKeyUpdate>>(written =>
                written.Count() == 1
                && written.Single().OrganizationUserId == organizationUserId
                && written.Single().UserKeyId == UserKeyId
                && written.Single().AccountRecoveryKey == accountRecoveryKey));
    }

    [Theory, BitAutoData]
    public async Task ApplyAsync_NoKeyGiven_WritesTheUnenrollment(
        Guid organizationId,
        Guid organizationUserId,
        SutProvider<ApplyOrganizationUserV2UpgradesCommand> sutProvider)
    {
        // Arrange - the upgrade cannot be completed, so the admin clears the member's enrollment instead
        var repository = sutProvider.GetDependency<IOrganizationUserKeyRepository>();
        repository.GetManyPendingV2UpgradesByOrganizationIdAsync(organizationId)
            .Returns(PendingUpgrades((organizationUserId, UserKeyId)));

        var updates = new[] { Update(organizationUserId, UserKeyId, accountRecoveryKey: null) };

        // Act
        await sutProvider.Sut.ApplyAsync(organizationId, updates);

        // Assert
        await repository.Received(1).UpdateManyV2UpgradedAccountRecoveryKeysAsync(
            organizationId,
            Arg.Is<IEnumerable<OrganizationUserAccountRecoveryKeyUpdate>>(written =>
                written.Count() == 1
                && written.Single().OrganizationUserId == organizationUserId
                && written.Single().UserKeyId == UserKeyId
                && written.Single().AccountRecoveryKey == null));
    }

    [Theory, BitAutoData]
    public async Task ApplyAsync_NoKeyGivenAndKeyIdDoesNotMatchTheUserRow_SkipsTheUpgradeAndWritesNothing(
        Guid organizationId,
        Guid organizationUserId,
        SutProvider<ApplyOrganizationUserV2UpgradesCommand> sutProvider)
    {
        // Arrange - the member rotated again, so their pending upgrade is not the one the admin abandoned
        var repository = sutProvider.GetDependency<IOrganizationUserKeyRepository>();
        repository.GetManyPendingV2UpgradesByOrganizationIdAsync(organizationId)
            .Returns(PendingUpgrades((organizationUserId, OtherUserKeyId)));

        var updates = new[] { Update(organizationUserId, UserKeyId, accountRecoveryKey: null) };

        // Act
        await sutProvider.Sut.ApplyAsync(organizationId, updates);

        // Assert
        await repository.DidNotReceiveWithAnyArgs().UpdateManyV2UpgradedAccountRecoveryKeysAsync(
            default, default!);
    }

    [Theory, BitAutoData]
    public async Task ApplyAsync_KeyIdDoesNotMatchTheUserRow_SkipsTheUpgradeAndWritesNothing(
        Guid organizationId,
        Guid organizationUserId,
        string accountRecoveryKey,
        SutProvider<ApplyOrganizationUserV2UpgradesCommand> sutProvider)
    {
        // Arrange - the member rotated again, so the re-wrapped key uses a user key they no longer hold
        var repository = sutProvider.GetDependency<IOrganizationUserKeyRepository>();
        repository.GetManyPendingV2UpgradesByOrganizationIdAsync(organizationId)
            .Returns(PendingUpgrades((organizationUserId, OtherUserKeyId)));

        var updates = new[] { Update(organizationUserId, UserKeyId, accountRecoveryKey) };

        // Act
        await sutProvider.Sut.ApplyAsync(organizationId, updates);

        // Assert
        await repository.DidNotReceiveWithAnyArgs().UpdateManyV2UpgradedAccountRecoveryKeysAsync(
            default, default!);
    }

    [Theory, BitAutoData]
    public async Task ApplyAsync_MembershipHasNoPendingUpgrade_SkipsTheUpgradeAndWritesNothing(
        Guid organizationId,
        Guid organizationUserId,
        string accountRecoveryKey,
        SutProvider<ApplyOrganizationUserV2UpgradesCommand> sutProvider)
    {
        // Arrange - covers an unknown id, an id from another organization, a membership with no token, and an
        // upgrade another admin completed first. None of these appear in the pending set.
        var repository = sutProvider.GetDependency<IOrganizationUserKeyRepository>();
        repository.GetManyPendingV2UpgradesByOrganizationIdAsync(organizationId)
            .Returns(PendingUpgrades());

        var updates = new[] { Update(organizationUserId, UserKeyId, accountRecoveryKey) };

        // Act
        await sutProvider.Sut.ApplyAsync(organizationId, updates);

        // Assert
        await repository.DidNotReceiveWithAnyArgs().UpdateManyV2UpgradedAccountRecoveryKeysAsync(
            default, default!);
    }

    [Theory, BitAutoData]
    public async Task ApplyAsync_OneOfTwoMembershipsIsStale_WritesTheOtherOne(
        Guid organizationId,
        Guid freshOrganizationUserId,
        Guid staleOrganizationUserId,
        string accountRecoveryKey,
        SutProvider<ApplyOrganizationUserV2UpgradesCommand> sutProvider)
    {
        // Arrange - a stale membership must not hold back the memberships that can still be upgraded
        var repository = sutProvider.GetDependency<IOrganizationUserKeyRepository>();
        repository.GetManyPendingV2UpgradesByOrganizationIdAsync(organizationId)
            .Returns(PendingUpgrades(
                (freshOrganizationUserId, UserKeyId),
                (staleOrganizationUserId, OtherUserKeyId)));

        var updates = new[]
        {
            Update(freshOrganizationUserId, UserKeyId, accountRecoveryKey),
            Update(staleOrganizationUserId, UserKeyId, accountRecoveryKey)
        };

        // Act
        await sutProvider.Sut.ApplyAsync(organizationId, updates);

        // Assert
        await repository.Received(1).UpdateManyV2UpgradedAccountRecoveryKeysAsync(
            organizationId,
            Arg.Is<IEnumerable<OrganizationUserAccountRecoveryKeyUpdate>>(written =>
                written.Count() == 1
                && written.Single().OrganizationUserId == freshOrganizationUserId));
    }

    [Theory, BitAutoData]
    public async Task ApplyAsync_NoUpgrades_ReadsAndWritesNothing(
        Guid organizationId,
        SutProvider<ApplyOrganizationUserV2UpgradesCommand> sutProvider)
    {
        // Act
        await sutProvider.Sut.ApplyAsync(organizationId, []);

        // Assert
        var repository = sutProvider.GetDependency<IOrganizationUserKeyRepository>();
        await repository.DidNotReceiveWithAnyArgs().GetManyPendingV2UpgradesByOrganizationIdAsync(default);
        await repository.DidNotReceiveWithAnyArgs().UpdateManyV2UpgradedAccountRecoveryKeysAsync(
            default, default!);
    }

    private static ICollection<OrganizationUserV2UpgradeDetails> PendingUpgrades(
        params (Guid OrganizationUserId, string UserKeyId)[] rows) =>
        rows.Select(row => new OrganizationUserV2UpgradeDetails
        {
            OrganizationUserId = row.OrganizationUserId,
            UserKeyId = row.UserKeyId,
            AccountRecoveryKey = "2.stale|key|mac",
            V2UpgradeToken = """{"WrappedUserKey1":"2.a|b|c","WrappedUserKey2":"2.d|e|f"}"""
        }).ToList();

    private static OrganizationUserAccountRecoveryKeyUpdate Update(Guid organizationUserId, string userKeyId,
        string? accountRecoveryKey) =>
        new()
        {
            OrganizationUserId = organizationUserId,
            UserKeyId = userKeyId,
            AccountRecoveryKey = accountRecoveryKey
        };
}
