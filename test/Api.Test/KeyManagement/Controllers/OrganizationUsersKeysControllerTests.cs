using System.Security.Claims;
using Bit.Api.AdminConsole.Authorization;
using Bit.Api.KeyManagement.Controllers;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
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

    [Theory, BitAutoData]
    public async Task GetPendingV2UpgradesAsync_MembershipAuthorizedForRecovery_ReturnsTheTokenAndKeyId(
        Guid orgId,
        Guid organizationUserId,
        SutProvider<OrganizationUsersKeysController> sutProvider)
    {
        // Arrange
        MockPendingUpgrades(sutProvider, orgId, Details(organizationUserId));
        MockMemberships(sutProvider, orgId, [organizationUserId], authorizedIds: [organizationUserId]);

        // Act
        var result = await sutProvider.Sut.GetPendingV2UpgradesAsync(orgId);

        // Assert
        var response = Assert.Single(result.Data);
        Assert.Equal(organizationUserId, response.OrganizationUserId);
        Assert.Equal(_userKeyId, response.UserKeyId);
        Assert.Equal(_accountRecoveryKey, response.AccountRecoveryKey);
        Assert.Equal(_wrappedUserKey1, response.V2UpgradeToken.WrappedUserKey1);
        Assert.Equal(_wrappedUserKey2, response.V2UpgradeToken.WrappedUserKey2);
    }

    [Theory, BitAutoData]
    public async Task GetPendingV2UpgradesAsync_MembershipTheCallerCannotRecover_IsLeftOut(
        Guid orgId,
        Guid organizationUserId,
        SutProvider<OrganizationUsersKeysController> sutProvider)
    {
        // Arrange - ManageResetPassword is not sufficient to read an Owner's key material
        MockPendingUpgrades(sutProvider, orgId, Details(organizationUserId));
        MockMemberships(sutProvider, orgId, [organizationUserId], authorizedIds: []);

        // Act
        var result = await sutProvider.Sut.GetPendingV2UpgradesAsync(orgId);

        // Assert
        Assert.Empty(result.Data);
    }

    [Theory, BitAutoData]
    public async Task GetPendingV2UpgradesAsync_TokenIsNotReadableJson_IsLeftOut(
        Guid orgId,
        Guid organizationUserId,
        SutProvider<OrganizationUsersKeysController> sutProvider)
    {
        // Arrange
        var details = Details(organizationUserId);
        details.V2UpgradeToken = "not-json";
        MockPendingUpgrades(sutProvider, orgId, details);
        MockMemberships(sutProvider, orgId, [organizationUserId], authorizedIds: [organizationUserId]);

        // Act
        var result = await sutProvider.Sut.GetPendingV2UpgradesAsync(orgId);

        // Assert
        Assert.Empty(result.Data);
    }

    [Theory, BitAutoData]
    public async Task GetPendingV2UpgradesAsync_NoPendingUpgrades_ReadsNoMemberships(
        Guid orgId,
        SutProvider<OrganizationUsersKeysController> sutProvider)
    {
        // Arrange
        MockPendingUpgrades(sutProvider, orgId);

        // Act
        var result = await sutProvider.Sut.GetPendingV2UpgradesAsync(orgId);

        // Assert
        Assert.Empty(result.Data);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs().GetManyAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task PostV2UpgradesAsync_MembershipAuthorizedForRecovery_AppliesTheUpgrade(
        Guid orgId,
        Guid organizationUserId,
        SutProvider<OrganizationUsersKeysController> sutProvider)
    {
        // Arrange
        MockMemberships(sutProvider, orgId, [organizationUserId], authorizedIds: [organizationUserId]);
        var model = ModelFor(organizationUserId);

        // Act
        await sutProvider.Sut.PostV2UpgradesAsync(orgId, model);

        // Assert
        await sutProvider.GetDependency<IApplyOrganizationUserV2UpgradesCommand>().Received(1).ApplyAsync(
            orgId,
            Arg.Is<IEnumerable<OrganizationUserAccountRecoveryKeyUpdate>>(updates =>
                updates.Count() == 1
                && updates.Single().OrganizationUserId == organizationUserId
                && updates.Single().UserKeyId == _userKeyId
                && updates.Single().AccountRecoveryKey == _accountRecoveryKey));
    }

    [Theory, BitAutoData]
    public async Task PostV2UpgradesAsync_MembershipTheCallerCannotRecover_ThrowsAndAppliesNothing(
        Guid orgId,
        Guid organizationUserId,
        SutProvider<OrganizationUsersKeysController> sutProvider)
    {
        // Arrange
        MockMemberships(sutProvider, orgId, [organizationUserId], authorizedIds: []);
        var model = ModelFor(organizationUserId);

        // Act
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PostV2UpgradesAsync(orgId, model));

        // Assert
        await sutProvider.GetDependency<IApplyOrganizationUserV2UpgradesCommand>()
            .DidNotReceiveWithAnyArgs().ApplyAsync(default, default!);
    }

    [Theory, BitAutoData]
    public async Task PostV2UpgradesAsync_OneOfTwoMembershipsIsNotAuthorized_ThrowsAndAppliesNothing(
        Guid orgId,
        Guid authorizedId,
        Guid unauthorizedId,
        SutProvider<OrganizationUsersKeysController> sutProvider)
    {
        // Arrange
        MockMemberships(sutProvider, orgId, [authorizedId, unauthorizedId],
            authorizedIds: [authorizedId]);

        var model = ModelFor(authorizedId, unauthorizedId);

        // Act
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PostV2UpgradesAsync(orgId, model));

        // Assert
        await sutProvider.GetDependency<IApplyOrganizationUserV2UpgradesCommand>()
            .DidNotReceiveWithAnyArgs().ApplyAsync(default, default!);
    }

    [Theory, BitAutoData]
    public async Task PostV2UpgradesAsync_MembershipOfAnotherOrganization_ThrowsAndAppliesNothing(
        Guid orgId,
        Guid otherOrgId,
        Guid organizationUserId,
        SutProvider<OrganizationUsersKeysController> sutProvider)
    {
        // Arrange - an admin of both organizations passes the per-membership check, so the route's organization
        // must be enforced separately
        MockMemberships(sutProvider, otherOrgId, [organizationUserId], authorizedIds: [organizationUserId]);
        var model = ModelFor(organizationUserId);

        // Act
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PostV2UpgradesAsync(orgId, model));

        // Assert
        await sutProvider.GetDependency<IApplyOrganizationUserV2UpgradesCommand>()
            .DidNotReceiveWithAnyArgs().ApplyAsync(default, default!);
    }

    [Theory, BitAutoData]
    public async Task GetPendingV2UpgradesAsync_MembershipOfAnotherOrganization_IsLeftOut(
        Guid orgId,
        Guid otherOrgId,
        Guid organizationUserId,
        SutProvider<OrganizationUsersKeysController> sutProvider)
    {
        // Arrange
        MockPendingUpgrades(sutProvider, orgId, Details(organizationUserId));
        MockMemberships(sutProvider, otherOrgId, [organizationUserId], authorizedIds: [organizationUserId]);

        // Act
        var result = await sutProvider.Sut.GetPendingV2UpgradesAsync(orgId);

        // Assert
        Assert.Empty(result.Data);
    }

    [Theory, BitAutoData]
    public async Task PostV2UpgradesAsync_MembershipIsUnknown_ThrowsAndAppliesNothing(
        Guid orgId,
        Guid organizationUserId,
        SutProvider<OrganizationUsersKeysController> sutProvider)
    {
        // Arrange - the repository returns nothing, so there is no membership to authorize
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([]);

        var model = ModelFor(organizationUserId);

        // Act
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PostV2UpgradesAsync(orgId, model));

        // Assert
        await sutProvider.GetDependency<IApplyOrganizationUserV2UpgradesCommand>()
            .DidNotReceiveWithAnyArgs().ApplyAsync(default, default!);
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

    private static void MockPendingUpgrades(SutProvider<OrganizationUsersKeysController> sutProvider, Guid orgId,
        params OrganizationUserV2UpgradeDetails[] pending) =>
        sutProvider.GetDependency<IOrganizationUserKeyRepository>()
            .GetManyPendingV2UpgradesByOrganizationIdAsync(orgId)
            .Returns(pending.ToList());

    /// <summary>
    /// Stubs the membership lookup for <paramref name="orgId"/> and sets, per membership, whether the caller is
    /// authorized to recover that account. Ids that are not in <paramref name="authorizedIds"/> return unauthorized.
    /// </summary>
    private static void MockMemberships(SutProvider<OrganizationUsersKeysController> sutProvider, Guid orgId,
        Guid[] allIds, Guid[] authorizedIds)
    {
        var organizationUsers = allIds
            .Select(id => new OrganizationUser { Id = id, OrganizationId = orgId, UserId = Guid.NewGuid() })
            .ToList();

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(organizationUsers);

        foreach (var organizationUser in organizationUsers)
        {
            sutProvider.GetDependency<IAuthorizationService>()
                .AuthorizeAsync(
                    Arg.Any<ClaimsPrincipal>(),
                    organizationUser,
                    Arg.Is<IEnumerable<IAuthorizationRequirement>>(requirements =>
                        requirements.SingleOrDefault() is RecoverAccountAuthorizationRequirement))
                .Returns(authorizedIds.Contains(organizationUser.Id)
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());
        }
    }
}
