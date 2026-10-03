using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Commands;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
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
        string accountRecoveryKey)
    {
        // Arrange
        var sutProvider = GetSutProvider();
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
                && written.Single().AccountRecoveryKey == accountRecoveryKey),
            RevisionDate(sutProvider));
    }

    [Theory, BitAutoData]
    public async Task ApplyAsync_NoKeyGiven_WritesTheUnenrollment(
        Guid organizationId,
        Guid organizationUserId)
    {
        // Arrange - the upgrade cannot be completed, so the admin clears the member's enrollment instead
        var sutProvider = GetSutProvider();
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
                && written.Single().AccountRecoveryKey == null),
            RevisionDate(sutProvider));
    }

    [Theory, BitAutoData]
    public async Task ApplyAsync_NoKeyGivenAndKeyIdDoesNotMatchTheUserRow_SkipsTheUpgradeAndWritesNothing(
        Guid organizationId,
        Guid organizationUserId)
    {
        // Arrange - the member rotated again, so their pending upgrade is not the one the admin abandoned
        var sutProvider = GetSutProvider();
        var repository = sutProvider.GetDependency<IOrganizationUserKeyRepository>();
        repository.GetManyPendingV2UpgradesByOrganizationIdAsync(organizationId)
            .Returns(PendingUpgrades((organizationUserId, OtherUserKeyId)));

        var updates = new[] { Update(organizationUserId, UserKeyId, accountRecoveryKey: null) };

        // Act
        await sutProvider.Sut.ApplyAsync(organizationId, updates);

        // Assert
        await repository.DidNotReceiveWithAnyArgs().UpdateManyV2UpgradedAccountRecoveryKeysAsync(
            Arg.Any<Guid>(), Arg.Any<IEnumerable<OrganizationUserAccountRecoveryKeyUpdate>>(), Arg.Any<DateTime>());
    }

    [Theory, BitAutoData]
    public async Task ApplyAsync_KeyIdDoesNotMatchTheUserRow_SkipsTheUpgradeAndWritesNothing(
        Guid organizationId,
        Guid organizationUserId,
        string accountRecoveryKey)
    {
        // Arrange - the member rotated again, so the re-wrapped key uses a user key they no longer hold
        var sutProvider = GetSutProvider();
        var repository = sutProvider.GetDependency<IOrganizationUserKeyRepository>();
        repository.GetManyPendingV2UpgradesByOrganizationIdAsync(organizationId)
            .Returns(PendingUpgrades((organizationUserId, OtherUserKeyId)));

        var updates = new[] { Update(organizationUserId, UserKeyId, accountRecoveryKey) };

        // Act
        await sutProvider.Sut.ApplyAsync(organizationId, updates);

        // Assert
        await repository.DidNotReceiveWithAnyArgs().UpdateManyV2UpgradedAccountRecoveryKeysAsync(
            Arg.Any<Guid>(), Arg.Any<IEnumerable<OrganizationUserAccountRecoveryKeyUpdate>>(), Arg.Any<DateTime>());
    }

    [Theory, BitAutoData]
    public async Task ApplyAsync_MembershipHasNoPendingUpgrade_SkipsTheUpgradeAndWritesNothing(
        Guid organizationId,
        Guid organizationUserId,
        string accountRecoveryKey)
    {
        // Arrange - covers an unknown id, an id from another organization, a membership with no token, and an
        var sutProvider = GetSutProvider();
        // upgrade another admin completed first. None of these appear in the pending set.
        var repository = sutProvider.GetDependency<IOrganizationUserKeyRepository>();
        repository.GetManyPendingV2UpgradesByOrganizationIdAsync(organizationId)
            .Returns(PendingUpgrades());

        var updates = new[] { Update(organizationUserId, UserKeyId, accountRecoveryKey) };

        // Act
        await sutProvider.Sut.ApplyAsync(organizationId, updates);

        // Assert
        await repository.DidNotReceiveWithAnyArgs().UpdateManyV2UpgradedAccountRecoveryKeysAsync(
            Arg.Any<Guid>(), Arg.Any<IEnumerable<OrganizationUserAccountRecoveryKeyUpdate>>(), Arg.Any<DateTime>());
    }

    [Theory, BitAutoData]
    public async Task ApplyAsync_OneOfTwoMembershipsIsStale_WritesTheOtherOne(
        Guid organizationId,
        Guid freshOrganizationUserId,
        Guid staleOrganizationUserId,
        string accountRecoveryKey)
    {
        // Arrange - a stale membership must not hold back the memberships that can still be upgraded
        var sutProvider = GetSutProvider();
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
                && written.Single().OrganizationUserId == freshOrganizationUserId),
            RevisionDate(sutProvider));
    }

    [Theory, BitAutoData]
    public async Task ApplyAsync_NoKeyGivenAndWritten_LogsAWithdrawal(
        Guid organizationId,
        OrganizationUser organizationUser)
    {
        // Arrange
        var sutProvider = GetSutProvider();
        var repository = sutProvider.GetDependency<IOrganizationUserKeyRepository>();
        repository.GetManyPendingV2UpgradesByOrganizationIdAsync(organizationId)
            .Returns(PendingUpgrades((organizationUser.Id, UserKeyId)));
        repository.UpdateManyV2UpgradedAccountRecoveryKeysAsync(organizationId,
                Arg.Any<IEnumerable<OrganizationUserAccountRecoveryKeyUpdate>>(), Arg.Any<DateTime>())
            .Returns([organizationUser.Id]);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { organizationUser.Id })))
            .Returns([organizationUser]);

        var updates = new[] { Update(organizationUser.Id, UserKeyId, accountRecoveryKey: null) };

        // Act
        await sutProvider.Sut.ApplyAsync(organizationId, updates);

        // Assert
        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventsAsync(
            Arg.Is<IEnumerable<(OrganizationUser, EventType, DateTime?)>>(events =>
                events.Count() == 1
                && events.Single().Item1 == organizationUser
                && events.Single().Item2 == EventType.OrganizationUser_ResetPassword_Withdraw
                && events.Single().Item3 == RevisionDate(sutProvider)));
    }

    [Theory, BitAutoData]
    public async Task ApplyAsync_NoKeyGivenButSkippedByTheWrite_LogsNothing(
        Guid organizationId,
        Guid organizationUserId)
    {
        // Arrange - the member rotated or withdrew between the read and the write, so the row was not written
        var sutProvider = GetSutProvider();
        var repository = sutProvider.GetDependency<IOrganizationUserKeyRepository>();
        repository.GetManyPendingV2UpgradesByOrganizationIdAsync(organizationId)
            .Returns(PendingUpgrades((organizationUserId, UserKeyId)));
        repository.UpdateManyV2UpgradedAccountRecoveryKeysAsync(organizationId,
                Arg.Any<IEnumerable<OrganizationUserAccountRecoveryKeyUpdate>>(), Arg.Any<DateTime>())
            .Returns(new List<Guid>());

        var updates = new[] { Update(organizationUserId, UserKeyId, accountRecoveryKey: null) };

        // Act
        await sutProvider.Sut.ApplyAsync(organizationId, updates);

        // Assert
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>());
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventsAsync(Arg.Any<IEnumerable<(OrganizationUser, EventType, DateTime?)>>());
    }

    [Theory, BitAutoData]
    public async Task ApplyAsync_KeyGivenAndWritten_LogsNothing(
        Guid organizationId,
        Guid organizationUserId,
        string accountRecoveryKey)
    {
        // Arrange - the member stays enrolled, only the key that wraps their user key changes
        var sutProvider = GetSutProvider();
        var repository = sutProvider.GetDependency<IOrganizationUserKeyRepository>();
        repository.GetManyPendingV2UpgradesByOrganizationIdAsync(organizationId)
            .Returns(PendingUpgrades((organizationUserId, UserKeyId)));
        repository.UpdateManyV2UpgradedAccountRecoveryKeysAsync(organizationId,
                Arg.Any<IEnumerable<OrganizationUserAccountRecoveryKeyUpdate>>(), Arg.Any<DateTime>())
            .Returns([organizationUserId]);

        var updates = new[] { Update(organizationUserId, UserKeyId, accountRecoveryKey) };

        // Act
        await sutProvider.Sut.ApplyAsync(organizationId, updates);

        // Assert
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>());
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventsAsync(Arg.Any<IEnumerable<(OrganizationUser, EventType, DateTime?)>>());
    }

    [Theory, BitAutoData]
    public async Task ApplyAsync_NoUpgrades_ReadsAndWritesNothing(
        Guid organizationId)
    {
        // Arrange
        var sutProvider = GetSutProvider();

        // Act
        await sutProvider.Sut.ApplyAsync(organizationId, []);

        // Assert
        var repository = sutProvider.GetDependency<IOrganizationUserKeyRepository>();
        await repository.DidNotReceiveWithAnyArgs().GetManyPendingV2UpgradesByOrganizationIdAsync(Arg.Any<Guid>());
        await repository.DidNotReceiveWithAnyArgs().UpdateManyV2UpgradedAccountRecoveryKeysAsync(
            Arg.Any<Guid>(), Arg.Any<IEnumerable<OrganizationUserAccountRecoveryKeyUpdate>>(), Arg.Any<DateTime>());
    }

    private static SutProvider<ApplyOrganizationUserV2UpgradesCommand> GetSutProvider() =>
        new SutProvider<ApplyOrganizationUserV2UpgradesCommand>()
            .WithFakeTimeProvider()
            .Create();

    private static DateTime RevisionDate(SutProvider<ApplyOrganizationUserV2UpgradesCommand> sutProvider) =>
        sutProvider.GetDependency<FakeTimeProvider>().GetUtcNow().UtcDateTime;

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
