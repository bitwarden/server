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
    public async Task RegenerateKeysAsync_UserHasNoSharedAccess_Success(
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
            .RegenerateUserAsymmetricKeysAsync(Arg.Is(userAsymmetricKeys));
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
            .RegenerateUserAsymmetricKeysAsync(Arg.Any<UserAsymmetricKeys>());
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    [BitAutoData(OrganizationUserStatusType.Revoked)]
    public async Task RegenerateKeysAsync_UserInOrganizations_BadRequestException(
        OrganizationUserStatusType organizationUserStatus,
        SutProvider<RegenerateUserAsymmetricKeysCommand> sutProvider,
        UserAsymmetricKeys userAsymmetricKeys,
        ICollection<OrganizationUser> usersOrganizationAccounts)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.ReturnsForAnyArgs(userAsymmetricKeys.UserId);
        usersOrganizationAccounts = CreateInOrganizationAccounts(userAsymmetricKeys.UserId, organizationUserStatus,
            usersOrganizationAccounts);
        var designatedEmergencyAccess = new List<EmergencyAccessDetails>();

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.RegenerateKeysAsync(userAsymmetricKeys,
            usersOrganizationAccounts, designatedEmergencyAccess));

        await sutProvider.GetDependency<IUserAsymmetricKeysRepository>()
            .ReceivedWithAnyArgs(0)
            .RegenerateUserAsymmetricKeysAsync(Arg.Any<UserAsymmetricKeys>());
    }

    [Theory]
    [BitAutoData(EmergencyAccessStatusType.Confirmed)]
    [BitAutoData(EmergencyAccessStatusType.RecoveryApproved)]
    [BitAutoData(EmergencyAccessStatusType.RecoveryInitiated)]
    public async Task RegenerateKeysAsync_UserHasDesignatedEmergencyAccess_BadRequestException(
        EmergencyAccessStatusType statusType,
        SutProvider<RegenerateUserAsymmetricKeysCommand> sutProvider,
        UserAsymmetricKeys userAsymmetricKeys,
        ICollection<EmergencyAccessDetails> designatedEmergencyAccess)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.ReturnsForAnyArgs(userAsymmetricKeys.UserId);
        designatedEmergencyAccess =
            CreateDesignatedEmergencyAccess(userAsymmetricKeys.UserId, statusType, designatedEmergencyAccess);
        var usersOrganizationAccounts = new List<OrganizationUser>();


        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.RegenerateKeysAsync(userAsymmetricKeys,
            usersOrganizationAccounts, designatedEmergencyAccess));

        await sutProvider.GetDependency<IUserAsymmetricKeysRepository>()
            .ReceivedWithAnyArgs(0)
            .RegenerateUserAsymmetricKeysAsync(Arg.Any<UserAsymmetricKeys>());
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
