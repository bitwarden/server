#nullable enable
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Commands;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Core.Test.KeyManagement.Commands;

[SutProviderCustomize]
public class RegenerateUserAsymmetricKeysCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task RegenerateKeysAsync_NoCurrentContext_NotFoundException(
        SutProvider<RegenerateUserAsymmetricKeysCommand> sutProvider,
        UserAsymmetricKeys userAsymmetricKeys)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.ReturnsNullForAnyArgs();
        var usersOrganizationAccounts = new List<OrganizationUser>();
        var designatedEmergencyAccess = new List<EmergencyAccessDetails>();

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.RegenerateKeysAsync(userAsymmetricKeys,
            usersOrganizationAccounts, designatedEmergencyAccess));
    }

    [Theory]
    [BitAutoData]
    public async Task RegenerateKeysAsync_NoOrgMembershipOrEmergencyAccess_RegeneratesKeysWithNoStatusChanges(
        SutProvider<RegenerateUserAsymmetricKeysCommand> sutProvider,
        UserAsymmetricKeys userAsymmetricKeys)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.ReturnsForAnyArgs(userAsymmetricKeys.UserId);
        var usersOrganizationAccounts = new List<OrganizationUser>();
        var designatedEmergencyAccess = new List<EmergencyAccessDetails>();

        await sutProvider.Sut.RegenerateKeysAsync(userAsymmetricKeys,
            usersOrganizationAccounts, designatedEmergencyAccess);

        await sutProvider.GetDependency<IUserAsymmetricKeysRepository>()
            .Received(1)
            .RegenerateUserAsymmetricKeysAsync(
                Arg.Is(userAsymmetricKeys),
                Arg.Is<IEnumerable<DatabaseTransactionAction>>(actions => !actions.Any()));
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncSettingsAsync(Arg.Is(userAsymmetricKeys.UserId));
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .DidNotReceiveWithAnyArgs()
            .SetStatusToAcceptedForPublicKeyPairRegeneration(Arg.Any<IEnumerable<EmergencyAccessDetails>>());
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .SetStatusToAcceptedForPublicKeyPairRegeneration(Arg.Any<IEnumerable<OrganizationUser>>());
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .RemoveForPublicKeyPairRegeneration(Arg.Any<IEnumerable<OrganizationUser>>());
    }

    [Theory]
    [BitAutoData(false, false, true)]
    [BitAutoData(false, true, false)]
    [BitAutoData(false, true, true)]
    [BitAutoData(true, false, false)]
    [BitAutoData(true, false, true)]
    [BitAutoData(true, true, false)]
    [BitAutoData(true, true, true)]
    public async Task RegenerateKeysAsync_UserIdMisMatch_NotFoundException(
        bool userAsymmetricKeysMismatch,
        bool orgMismatch,
        bool emergencyAccessMismatch,
        SutProvider<RegenerateUserAsymmetricKeysCommand> sutProvider,
        UserAsymmetricKeys userAsymmetricKeys,
        ICollection<OrganizationUser> usersOrganizationAccounts,
        ICollection<EmergencyAccessDetails> designatedEmergencyAccess)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId
            .ReturnsForAnyArgs(userAsymmetricKeysMismatch ? new Guid() : userAsymmetricKeys.UserId);

        if (!orgMismatch)
        {
            usersOrganizationAccounts =
                SetupOrganizationUserAccounts(userAsymmetricKeys.UserId, usersOrganizationAccounts);
        }

        if (!emergencyAccessMismatch)
        {
            designatedEmergencyAccess = SetupEmergencyAccess(userAsymmetricKeys.UserId, designatedEmergencyAccess);
        }

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.RegenerateKeysAsync(userAsymmetricKeys,
            usersOrganizationAccounts, designatedEmergencyAccess));

        await sutProvider.GetDependency<IUserAsymmetricKeysRepository>()
            .ReceivedWithAnyArgs(0)
            .RegenerateUserAsymmetricKeysAsync(Arg.Any<UserAsymmetricKeys>(),
                Arg.Any<IEnumerable<DatabaseTransactionAction>>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .ReceivedWithAnyArgs(0)
            .PushSyncSettingsAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData(EmergencyAccessStatusType.Confirmed)]
    [BitAutoData(EmergencyAccessStatusType.RecoveryInitiated)]
    [BitAutoData(EmergencyAccessStatusType.RecoveryApproved)]
    public async Task RegenerateKeysAsync_EmergencyAccessNeedsReset_TransitionsToAccepted(
        EmergencyAccessStatusType statusType,
        SutProvider<RegenerateUserAsymmetricKeysCommand> sutProvider,
        UserAsymmetricKeys userAsymmetricKeys,
        ICollection<EmergencyAccessDetails> designatedEmergencyAccess)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.ReturnsForAnyArgs(userAsymmetricKeys.UserId);
        designatedEmergencyAccess =
            CreateDesignatedEmergencyAccess(userAsymmetricKeys.UserId, statusType, designatedEmergencyAccess);
        var usersOrganizationAccounts = new List<OrganizationUser>();

        await sutProvider.Sut.RegenerateKeysAsync(userAsymmetricKeys,
            usersOrganizationAccounts, designatedEmergencyAccess);

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .SetStatusToAcceptedForPublicKeyPairRegeneration(Arg.Is<IEnumerable<EmergencyAccessDetails>>(
                items => items.All(ea => ea.Status == statusType)));
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .SetStatusToAcceptedForPublicKeyPairRegeneration(Arg.Any<IEnumerable<OrganizationUser>>());
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .RemoveForPublicKeyPairRegeneration(Arg.Any<IEnumerable<OrganizationUser>>());
        await sutProvider.GetDependency<IUserAsymmetricKeysRepository>()
            .Received(1)
            .RegenerateUserAsymmetricKeysAsync(
                Arg.Is(userAsymmetricKeys),
                Arg.Is<IEnumerable<DatabaseTransactionAction>>(actions => actions.Count() == 1));
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncSettingsAsync(Arg.Is(userAsymmetricKeys.UserId));
        foreach (var ea in designatedEmergencyAccess)
        {
            await sutProvider.GetDependency<IMailService>()
                .Received(1)
                .SendEmergencyAccessAcceptedEmailAsync(ea.GranteeEmail!, ea.GrantorEmail!);
        }
    }

    [Theory]
    [BitAutoData(EmergencyAccessStatusType.Invited)]
    [BitAutoData(EmergencyAccessStatusType.Accepted)]
    public async Task RegenerateKeysAsync_EmergencyAccessNoResetNeeded_NoChange(
        EmergencyAccessStatusType statusType,
        SutProvider<RegenerateUserAsymmetricKeysCommand> sutProvider,
        UserAsymmetricKeys userAsymmetricKeys,
        ICollection<EmergencyAccessDetails> designatedEmergencyAccess)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.ReturnsForAnyArgs(userAsymmetricKeys.UserId);
        designatedEmergencyAccess =
            CreateDesignatedEmergencyAccess(userAsymmetricKeys.UserId, statusType, designatedEmergencyAccess);
        var usersOrganizationAccounts = new List<OrganizationUser>();

        await sutProvider.Sut.RegenerateKeysAsync(userAsymmetricKeys,
            usersOrganizationAccounts, designatedEmergencyAccess);

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .DidNotReceiveWithAnyArgs()
            .SetStatusToAcceptedForPublicKeyPairRegeneration(Arg.Any<IEnumerable<EmergencyAccessDetails>>());
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .SetStatusToAcceptedForPublicKeyPairRegeneration(Arg.Any<IEnumerable<OrganizationUser>>());
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .RemoveForPublicKeyPairRegeneration(Arg.Any<IEnumerable<OrganizationUser>>());
        await sutProvider.GetDependency<IMailService>()
            .DidNotReceiveWithAnyArgs()
            .SendEmergencyAccessAcceptedEmailAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    public async Task RegenerateKeysAsync_OrgUserConfirmed_TransitionsToAccepted(
        SutProvider<RegenerateUserAsymmetricKeysCommand> sutProvider,
        UserAsymmetricKeys userAsymmetricKeys,
        ICollection<OrganizationUser> usersOrganizationAccounts)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.ReturnsForAnyArgs(userAsymmetricKeys.UserId);
        usersOrganizationAccounts = CreateInOrganizationAccounts(userAsymmetricKeys.UserId,
            OrganizationUserStatusType.Confirmed, usersOrganizationAccounts);
        var designatedEmergencyAccess = new List<EmergencyAccessDetails>();

        await sutProvider.Sut.RegenerateKeysAsync(userAsymmetricKeys,
            usersOrganizationAccounts, designatedEmergencyAccess);

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .DidNotReceiveWithAnyArgs()
            .SetStatusToAcceptedForPublicKeyPairRegeneration(Arg.Any<IEnumerable<EmergencyAccessDetails>>());
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .SetStatusToAcceptedForPublicKeyPairRegeneration(Arg.Is<IEnumerable<OrganizationUser>>(
                items => items.All(ou => ou.Status == OrganizationUserStatusType.Confirmed)));
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .RemoveForPublicKeyPairRegeneration(Arg.Any<IEnumerable<OrganizationUser>>());
        await sutProvider.GetDependency<IUserAsymmetricKeysRepository>()
            .Received(1)
            .RegenerateUserAsymmetricKeysAsync(
                Arg.Is(userAsymmetricKeys),
                Arg.Is<IEnumerable<DatabaseTransactionAction>>(actions => actions.Count() == 1));
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncSettingsAsync(Arg.Is(userAsymmetricKeys.UserId));
    }

    [Theory]
    [BitAutoData]
    public async Task RegenerateKeysAsync_OrgUserRevoked_DeletedAndEventLogged(
        SutProvider<RegenerateUserAsymmetricKeysCommand> sutProvider,
        UserAsymmetricKeys userAsymmetricKeys,
        ICollection<OrganizationUser> usersOrganizationAccounts)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.ReturnsForAnyArgs(userAsymmetricKeys.UserId);
        usersOrganizationAccounts = CreateInOrganizationAccounts(userAsymmetricKeys.UserId,
            OrganizationUserStatusType.Revoked, usersOrganizationAccounts);
        var designatedEmergencyAccess = new List<EmergencyAccessDetails>();

        await sutProvider.Sut.RegenerateKeysAsync(userAsymmetricKeys,
            usersOrganizationAccounts, designatedEmergencyAccess);

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .DidNotReceiveWithAnyArgs()
            .SetStatusToAcceptedForPublicKeyPairRegeneration(Arg.Any<IEnumerable<EmergencyAccessDetails>>());
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .SetStatusToAcceptedForPublicKeyPairRegeneration(Arg.Any<IEnumerable<OrganizationUser>>());
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .RemoveForPublicKeyPairRegeneration(Arg.Is<IEnumerable<OrganizationUser>>(
                items => items.All(ou => ou.Status == OrganizationUserStatusType.Revoked)));
        await sutProvider.GetDependency<IUserAsymmetricKeysRepository>()
            .Received(1)
            .RegenerateUserAsymmetricKeysAsync(
                Arg.Is(userAsymmetricKeys),
                Arg.Is<IEnumerable<DatabaseTransactionAction>>(actions => actions.Count() == 1));
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncSettingsAsync(Arg.Is(userAsymmetricKeys.UserId));
        await sutProvider.GetDependency<IEventService>()
            .Received(usersOrganizationAccounts.Count)
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Is(EventType.OrganizationUser_Left));
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Invited)]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    public async Task RegenerateKeysAsync_OrgUserNoResetNeeded_NoChange(
        OrganizationUserStatusType organizationUserStatus,
        SutProvider<RegenerateUserAsymmetricKeysCommand> sutProvider,
        UserAsymmetricKeys userAsymmetricKeys,
        ICollection<OrganizationUser> usersOrganizationAccounts)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.ReturnsForAnyArgs(userAsymmetricKeys.UserId);
        usersOrganizationAccounts = CreateInOrganizationAccounts(userAsymmetricKeys.UserId,
            organizationUserStatus, usersOrganizationAccounts);
        var designatedEmergencyAccess = new List<EmergencyAccessDetails>();

        await sutProvider.Sut.RegenerateKeysAsync(userAsymmetricKeys,
            usersOrganizationAccounts, designatedEmergencyAccess);

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .DidNotReceiveWithAnyArgs()
            .SetStatusToAcceptedForPublicKeyPairRegeneration(Arg.Any<IEnumerable<EmergencyAccessDetails>>());
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .SetStatusToAcceptedForPublicKeyPairRegeneration(Arg.Any<IEnumerable<OrganizationUser>>());
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .RemoveForPublicKeyPairRegeneration(Arg.Any<IEnumerable<OrganizationUser>>());
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>());
    }

    private static ICollection<OrganizationUser> CreateInOrganizationAccounts(Guid userId,
        OrganizationUserStatusType organizationUserStatus, ICollection<OrganizationUser> organizationUserAccounts)
    {
        foreach (var organizationUserAccount in organizationUserAccounts)
        {
            organizationUserAccount.UserId = userId;
            organizationUserAccount.Status = organizationUserStatus;
        }

        return organizationUserAccounts;
    }

    private static ICollection<EmergencyAccessDetails> CreateDesignatedEmergencyAccess(Guid userId,
        EmergencyAccessStatusType status, ICollection<EmergencyAccessDetails> designatedEmergencyAccess)
    {
        foreach (var designated in designatedEmergencyAccess)
        {
            designated.GranteeId = userId;
            designated.Status = status;
        }

        return designatedEmergencyAccess;
    }

    private static ICollection<OrganizationUser> SetupOrganizationUserAccounts(Guid userId,
        ICollection<OrganizationUser> organizationUserAccounts)
    {
        foreach (var organizationUserAccount in organizationUserAccounts)
        {
            organizationUserAccount.UserId = userId;
        }

        return organizationUserAccounts;
    }

    private static ICollection<EmergencyAccessDetails> SetupEmergencyAccess(Guid userId,
        ICollection<EmergencyAccessDetails> emergencyAccessDetails)
    {
        foreach (var emergencyAccessDetail in emergencyAccessDetails)
        {
            emergencyAccessDetail.GranteeId = userId;
        }

        return emergencyAccessDetails;
    }
}
