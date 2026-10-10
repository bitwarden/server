using System.Security.Claims;
using Bit.Api.AdminConsole.Authorization;
using Bit.Api.KeyManagement.Controllers;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.KeyManagement.Controllers;

[ControllerCustomize(typeof(OrganizationUsersKeysController))]
[SutProviderCustomize]
public class OrganizationUsersKeysControllerTests
{
    private const string _userKeyId = "0123456789abcdef0123456789abcdef";
    private const string _accountRecoveryKey =
        "2.BPt52Ie9PQjDQYkzKLDjEB==|P7PIiu3V3iKHCTOHojnKnh==|jE44t9C79D9KiZZiTb5W2uBskwMs9fFbHrPW8CSp6Kl=";
    private const string _wrappedUserKey1 = "7.AOs41Hd8OQiCPXjyJKCiDA==";
    private const string _wrappedUserKey2 = "7.BPt52Ie9PQjDQYkzKLDjEB==";
    private const int _pageSize = OrganizationUserV2UpgradesRequestModel.MaxUpgrades;

    [Theory]
    [BitAutoData(OrganizationUserType.Owner, true)]
    [BitAutoData(OrganizationUserType.Admin, false)]
    public async Task GetPendingV2UpgradesAsync_ReadsOnePageWithOwnersOnlyForAnOwner(
        OrganizationUserType callerType,
        bool expectedIncludeOwners,
        Guid orgId,
        SutProvider<OrganizationUsersKeysController> sutProvider)
    {
        // Arrange
        MockCallerType(sutProvider, orgId, callerType);
        MockPendingUpgrades(sutProvider, orgId, expectedIncludeOwners);

        // Act
        await sutProvider.Sut.GetPendingV2UpgradesAsync(orgId);

        // Assert
        await sutProvider.GetDependency<IOrganizationUserKeyRepository>().Received(1)
            .GetManyPendingV2UpgradesByOrganizationIdAsync(orgId, expectedIncludeOwners, _pageSize);
    }

    [Theory, BitAutoData]
    public async Task GetPendingV2UpgradesAsync_PendingUpgrade_ReturnsTheTokenAndKeyId(
        Guid orgId,
        Guid organizationUserId,
        SutProvider<OrganizationUsersKeysController> sutProvider)
    {
        // Arrange
        MockCallerType(sutProvider, orgId, OrganizationUserType.Owner);
        MockPendingUpgrades(sutProvider, orgId, includeOwners: true, Details(organizationUserId));

        // Act
        var result = await sutProvider.Sut.GetPendingV2UpgradesAsync(orgId);

        // Assert
        var response = Assert.Single(result.Data);
        Assert.Equal(organizationUserId, response.OrganizationUserId);
        Assert.Equal(_userKeyId, response.UserKeyId);
        Assert.Equal(_accountRecoveryKey, response.AccountRecoveryKey);
        Assert.NotNull(response.V2UpgradeToken);
        Assert.Equal(_wrappedUserKey1, response.V2UpgradeToken.WrappedUserKey1);
        Assert.Equal(_wrappedUserKey2, response.V2UpgradeToken.WrappedUserKey2);
    }

    [Theory]
    [BitAutoData("not-json")]
    [BitAutoData("""{"WrappedUserKey1":"7.AOs41Hd8OQiCPXjyJKCiDA=="}""")]
    public async Task GetPendingV2UpgradesAsync_TokenCannotBeParsed_ReturnsTheRowWithANullToken(
        string unparseableToken,
        Guid orgId,
        Guid organizationUserId,
        SutProvider<OrganizationUsersKeysController> sutProvider)
    {
        // Arrange
        var unparseable = Details(organizationUserId);
        unparseable.V2UpgradeToken = unparseableToken;
        MockCallerType(sutProvider, orgId, OrganizationUserType.Owner);
        MockPendingUpgrades(sutProvider, orgId, includeOwners: true, unparseable);

        // Act
        var result = await sutProvider.Sut.GetPendingV2UpgradesAsync(orgId);

        // Assert
        var response = Assert.Single(result.Data);
        Assert.Equal(organizationUserId, response.OrganizationUserId);
        Assert.Equal(_userKeyId, response.UserKeyId);
        Assert.Equal(_accountRecoveryKey, response.AccountRecoveryKey);
        Assert.Null(response.V2UpgradeToken);
    }

    [Theory, BitAutoData]
    public async Task GetPendingV2UpgradesAsync_NoPendingUpgrades_ReturnsNothing(
        Guid orgId,
        SutProvider<OrganizationUsersKeysController> sutProvider)
    {
        // Arrange
        MockCallerType(sutProvider, orgId, OrganizationUserType.Owner);
        MockPendingUpgrades(sutProvider, orgId, includeOwners: true);

        // Act
        var result = await sutProvider.Sut.GetPendingV2UpgradesAsync(orgId);

        // Assert
        Assert.Empty(result.Data);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner, true)]
    [BitAutoData(OrganizationUserType.Admin, false)]
    public async Task PostV2UpgradesAsync_AppliesTheUpgradesWithOwnersOnlyForAnOwner(
        OrganizationUserType callerType,
        bool expectedIncludeOwners,
        Guid orgId,
        Guid organizationUserId,
        SutProvider<OrganizationUsersKeysController> sutProvider)
    {
        // Arrange
        MockCallerType(sutProvider, orgId, callerType);
        var model = ModelFor(organizationUserId);

        // Act
        await sutProvider.Sut.PostV2UpgradesAsync(orgId, model);

        // Assert
        await sutProvider.GetDependency<IApplyOrganizationUserV2UpgradesCommand>().Received(1).ApplyAsync(
            orgId,
            expectedIncludeOwners,
            Arg.Is<IEnumerable<OrganizationUserAccountRecoveryKeyUpdate>>(updates =>
                updates.Count() == 1
                && updates.Single().OrganizationUserId == organizationUserId
                && updates.Single().UserKeyId == _userKeyId
                && updates.Single().AccountRecoveryKey == _accountRecoveryKey));
    }

    private static OrganizationUserV2UpgradeDetails Details(Guid organizationUserId) =>
        new()
        {
            OrganizationUserId = organizationUserId,
            UserKeyId = _userKeyId,
            AccountRecoveryKey = _accountRecoveryKey,
            V2UpgradeToken =
                $$"""{"WrappedUserKey1":"{{_wrappedUserKey1}}","WrappedUserKey2":"{{_wrappedUserKey2}}"}"""
        };

    private static OrganizationUserV2UpgradesRequestModel ModelFor(params Guid[] organizationUserIds) =>
        new()
        {
            Upgrades = organizationUserIds.Select(id => new OrganizationUserV2UpgradeRequestModel
            {
                OrganizationUserId = id,
                UserKeyId = _userKeyId,
                AccountRecoveryKey = _accountRecoveryKey
            }).ToList()
        };

    private static void MockCallerType(SutProvider<OrganizationUsersKeysController> sutProvider, Guid orgId,
        OrganizationUserType callerType) =>
        sutProvider.GetDependency<IOrganizationContext>()
            .GetOrganizationClaims(Arg.Any<ClaimsPrincipal>(), orgId)
            .Returns(new CurrentContextOrganization { Id = orgId, Type = callerType });

    private static void MockPendingUpgrades(SutProvider<OrganizationUsersKeysController> sutProvider, Guid orgId,
        bool includeOwners, params OrganizationUserV2UpgradeDetails[] pending) =>
        sutProvider.GetDependency<IOrganizationUserKeyRepository>()
            .GetManyPendingV2UpgradesByOrganizationIdAsync(orgId, includeOwners, _pageSize)
            .Returns(pending.ToList());
}
