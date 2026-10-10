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

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task ApplyAsync_KeyIdsMatch_WritesTheRewrappedKeys(
        bool includeOwners,
        Guid organizationId,
        Guid organizationUserId,
        string accountRecoveryKey)
    {
        // Arrange
        var sutProvider = GetSutProvider();
        var repository = sutProvider.GetDependency<IOrganizationUserKeyRepository>();

        var updates = new[] { Update(organizationUserId, UserKeyId, accountRecoveryKey) };

        // Act
        await sutProvider.Sut.ApplyAsync(organizationId, includeOwners, updates);

        // Assert
        await repository.Received(1).UpdateManyV2UpgradedAccountRecoveryKeysAsync(
            organizationId,
            includeOwners,
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

        var updates = new[] { Update(organizationUserId, UserKeyId, accountRecoveryKey: null) };

        // Act
        await sutProvider.Sut.ApplyAsync(organizationId, includeOwners: true, updates);

        // Assert
        await repository.Received(1).UpdateManyV2UpgradedAccountRecoveryKeysAsync(
            organizationId,
            true,
            Arg.Is<IEnumerable<OrganizationUserAccountRecoveryKeyUpdate>>(written =>
                written.Count() == 1
                && written.Single().OrganizationUserId == organizationUserId
                && written.Single().UserKeyId == UserKeyId
                && written.Single().AccountRecoveryKey == null),
            RevisionDate(sutProvider));
    }

    [Theory, BitAutoData]
    public async Task ApplyAsync_TwoUpgrades_WritesBothWithoutReadingThePendingUpgrades(
        Guid organizationId,
        Guid firstOrganizationUserId,
        Guid secondOrganizationUserId,
        string accountRecoveryKey)
    {
        // Arrange - the write checks each row itself, so a stale update is skipped there and not before
        var sutProvider = GetSutProvider();
        var repository = sutProvider.GetDependency<IOrganizationUserKeyRepository>();

        var updates = new[]
        {
            Update(firstOrganizationUserId, UserKeyId, accountRecoveryKey),
            Update(secondOrganizationUserId, OtherUserKeyId, accountRecoveryKey: null)
        };

        // Act
        await sutProvider.Sut.ApplyAsync(organizationId, includeOwners: true, updates);

        // Assert
        await repository.Received(1).UpdateManyV2UpgradedAccountRecoveryKeysAsync(
            organizationId,
            true,
            Arg.Is<IEnumerable<OrganizationUserAccountRecoveryKeyUpdate>>(written =>
                written.Count() == 2
                && written.Any(update => update.OrganizationUserId == firstOrganizationUserId
                    && update.UserKeyId == UserKeyId
                    && update.AccountRecoveryKey == accountRecoveryKey)
                && written.Any(update => update.OrganizationUserId == secondOrganizationUserId
                    && update.UserKeyId == OtherUserKeyId
                    && update.AccountRecoveryKey == null)),
            RevisionDate(sutProvider));
        await repository.DidNotReceiveWithAnyArgs().GetManyPendingV2UpgradesByOrganizationIdAsync(
            Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<int>());
    }

    [Theory, BitAutoData]
    public async Task ApplyAsync_NoKeyGivenAndWritten_LogsAWithdrawal(
        Guid organizationId,
        OrganizationUser organizationUser)
    {
        // Arrange
        var sutProvider = GetSutProvider();
        var repository = sutProvider.GetDependency<IOrganizationUserKeyRepository>();
        repository.UpdateManyV2UpgradedAccountRecoveryKeysAsync(organizationId, true,
                Arg.Any<IEnumerable<OrganizationUserAccountRecoveryKeyUpdate>>(), Arg.Any<DateTime>())
            .Returns([organizationUser.Id]);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { organizationUser.Id })))
            .Returns([organizationUser]);

        var updates = new[] { Update(organizationUser.Id, UserKeyId, accountRecoveryKey: null) };

        // Act
        await sutProvider.Sut.ApplyAsync(organizationId, includeOwners: true, updates);

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
        // Arrange - the member rotated or withdrew after the admin's read, so the row was not written
        var sutProvider = GetSutProvider();
        var repository = sutProvider.GetDependency<IOrganizationUserKeyRepository>();
        repository.UpdateManyV2UpgradedAccountRecoveryKeysAsync(organizationId, true,
                Arg.Any<IEnumerable<OrganizationUserAccountRecoveryKeyUpdate>>(), Arg.Any<DateTime>())
            .Returns(new List<Guid>());

        var updates = new[] { Update(organizationUserId, UserKeyId, accountRecoveryKey: null) };

        // Act
        await sutProvider.Sut.ApplyAsync(organizationId, includeOwners: true, updates);

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
        repository.UpdateManyV2UpgradedAccountRecoveryKeysAsync(organizationId, true,
                Arg.Any<IEnumerable<OrganizationUserAccountRecoveryKeyUpdate>>(), Arg.Any<DateTime>())
            .Returns([organizationUserId]);

        var updates = new[] { Update(organizationUserId, UserKeyId, accountRecoveryKey) };

        // Act
        await sutProvider.Sut.ApplyAsync(organizationId, includeOwners: true, updates);

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
        await sutProvider.Sut.ApplyAsync(organizationId, includeOwners: true, []);

        // Assert
        var repository = sutProvider.GetDependency<IOrganizationUserKeyRepository>();
        await repository.DidNotReceiveWithAnyArgs().GetManyPendingV2UpgradesByOrganizationIdAsync(
            Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<int>());
        await repository.DidNotReceiveWithAnyArgs().UpdateManyV2UpgradedAccountRecoveryKeysAsync(
            Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<IEnumerable<OrganizationUserAccountRecoveryKeyUpdate>>(),
            Arg.Any<DateTime>());
    }

    private static SutProvider<ApplyOrganizationUserV2UpgradesCommand> GetSutProvider() =>
        new SutProvider<ApplyOrganizationUserV2UpgradesCommand>()
            .WithFakeTimeProvider()
            .Create();

    private static DateTime RevisionDate(SutProvider<ApplyOrganizationUserV2UpgradesCommand> sutProvider) =>
        sutProvider.GetDependency<FakeTimeProvider>().GetUtcNow().UtcDateTime;

    private static OrganizationUserAccountRecoveryKeyUpdate Update(Guid organizationUserId, string userKeyId,
        string? accountRecoveryKey) =>
        new()
        {
            OrganizationUserId = organizationUserId,
            UserKeyId = userKeyId,
            AccountRecoveryKey = accountRecoveryKey
        };
}
