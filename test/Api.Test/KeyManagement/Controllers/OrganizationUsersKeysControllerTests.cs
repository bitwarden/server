using System.Security.Claims;
using Bit.Api.AdminConsole.Authorization;
using Bit.Api.KeyManagement.Controllers;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
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
        sutProvider.GetDependency<IOrganizationContext>()
            .GetOrganizationClaims(Arg.Any<ClaimsPrincipal>(), orgId)
            .Returns(new CurrentContextOrganization { Id = orgId, Type = callerType });
        sutProvider.GetDependency<IOrganizationUserKeyRepository>()
            .GetManyPendingV2UpgradesByOrganizationIdAsync(orgId, expectedIncludeOwners, null, _pageSize)
            .Returns([]);

        // Act
        await sutProvider.Sut.GetPendingV2UpgradesAsync(orgId, continuationToken: null);

        // Assert
        await sutProvider.GetDependency<IOrganizationUserKeyRepository>().Received(1)
            .GetManyPendingV2UpgradesByOrganizationIdAsync(orgId, expectedIncludeOwners, null, _pageSize);
    }

    [Theory, BitAutoData]
    public async Task GetPendingV2UpgradesAsync_WithContinuationToken_ReadsThePageAfterIt(
        Guid orgId,
        Guid continuationToken,
        SutProvider<OrganizationUsersKeysController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationContext>()
            .GetOrganizationClaims(Arg.Any<ClaimsPrincipal>(), orgId)
            .Returns(new CurrentContextOrganization { Id = orgId, Type = OrganizationUserType.Owner });
        sutProvider.GetDependency<IOrganizationUserKeyRepository>()
            .GetManyPendingV2UpgradesByOrganizationIdAsync(orgId, true, continuationToken, _pageSize)
            .Returns([]);

        // Act
        await sutProvider.Sut.GetPendingV2UpgradesAsync(orgId, continuationToken);

        // Assert
        await sutProvider.GetDependency<IOrganizationUserKeyRepository>().Received(1)
            .GetManyPendingV2UpgradesByOrganizationIdAsync(orgId, true, continuationToken, _pageSize);
    }

    [Theory, BitAutoData]
    public async Task GetPendingV2UpgradesAsync_PendingUpgrade_ReturnsTheTokenAndKeyId(
        Guid orgId,
        Guid organizationUserId,
        SutProvider<OrganizationUsersKeysController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationContext>()
            .GetOrganizationClaims(Arg.Any<ClaimsPrincipal>(), orgId)
            .Returns(new CurrentContextOrganization { Id = orgId, Type = OrganizationUserType.Owner });
        sutProvider.GetDependency<IOrganizationUserKeyRepository>()
            .GetManyPendingV2UpgradesByOrganizationIdAsync(orgId, true, null, _pageSize)
            .Returns([Details(organizationUserId)]);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([new OrganizationUser { Id = organizationUserId, OrganizationId = orgId }]);
        MockRecoverAccountAuthorization(sutProvider, [organizationUserId], succeeded: true);

        // Act
        var result = await sutProvider.Sut.GetPendingV2UpgradesAsync(orgId, continuationToken: null);

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
        sutProvider.GetDependency<IOrganizationContext>()
            .GetOrganizationClaims(Arg.Any<ClaimsPrincipal>(), orgId)
            .Returns(new CurrentContextOrganization { Id = orgId, Type = OrganizationUserType.Owner });
        sutProvider.GetDependency<IOrganizationUserKeyRepository>()
            .GetManyPendingV2UpgradesByOrganizationIdAsync(orgId, true, null, _pageSize)
            .Returns([unparseable]);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([new OrganizationUser { Id = organizationUserId, OrganizationId = orgId }]);
        MockRecoverAccountAuthorization(sutProvider, [organizationUserId], succeeded: true);

        // Act
        var result = await sutProvider.Sut.GetPendingV2UpgradesAsync(orgId, continuationToken: null);

        // Assert
        var response = Assert.Single(result.Data);
        Assert.Equal(organizationUserId, response.OrganizationUserId);
        Assert.Equal(_userKeyId, response.UserKeyId);
        Assert.Equal(_accountRecoveryKey, response.AccountRecoveryKey);
        Assert.Null(response.V2UpgradeToken);
    }

    [Theory, BitAutoData]
    public async Task GetPendingV2UpgradesAsync_MemberCannotBeRecovered_LeavesOutTheMember(
        Guid orgId,
        Guid recoverableId,
        Guid unrecoverableId,
        SutProvider<OrganizationUsersKeysController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationContext>()
            .GetOrganizationClaims(Arg.Any<ClaimsPrincipal>(), orgId)
            .Returns(new CurrentContextOrganization { Id = orgId, Type = OrganizationUserType.Owner });
        sutProvider.GetDependency<IOrganizationUserKeyRepository>()
            .GetManyPendingV2UpgradesByOrganizationIdAsync(orgId, true, null, _pageSize)
            .Returns([Details(recoverableId), Details(unrecoverableId)]);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([
                new OrganizationUser { Id = recoverableId, OrganizationId = orgId },
                new OrganizationUser { Id = unrecoverableId, OrganizationId = orgId }
            ]);
        MockRecoverAccountAuthorization(sutProvider, [recoverableId], succeeded: true);
        MockRecoverAccountAuthorization(sutProvider, [unrecoverableId], succeeded: false);

        // Act
        var result = await sutProvider.Sut.GetPendingV2UpgradesAsync(orgId, continuationToken: null);

        // Assert
        var response = Assert.Single(result.Data);
        Assert.Equal(recoverableId, response.OrganizationUserId);
    }

    [Theory, BitAutoData]
    public async Task GetPendingV2UpgradesAsync_PageIsNotFull_ReturnsNoContinuationToken(
        Guid orgId,
        Guid organizationUserId,
        SutProvider<OrganizationUsersKeysController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationContext>()
            .GetOrganizationClaims(Arg.Any<ClaimsPrincipal>(), orgId)
            .Returns(new CurrentContextOrganization { Id = orgId, Type = OrganizationUserType.Owner });
        sutProvider.GetDependency<IOrganizationUserKeyRepository>()
            .GetManyPendingV2UpgradesByOrganizationIdAsync(orgId, true, null, _pageSize)
            .Returns([Details(organizationUserId)]);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([new OrganizationUser { Id = organizationUserId, OrganizationId = orgId }]);
        MockRecoverAccountAuthorization(sutProvider, [organizationUserId], succeeded: true);

        // Act
        var result = await sutProvider.Sut.GetPendingV2UpgradesAsync(orgId, continuationToken: null);

        // Assert
        Assert.Null(result.ContinuationToken);
    }

    [Theory, BitAutoData]
    public async Task GetPendingV2UpgradesAsync_PageIsFull_ReturnsTheLastIdAsTheContinuationToken(
        Guid orgId,
        SutProvider<OrganizationUsersKeysController> sutProvider)
    {
        // Arrange
        var ids = NewIds(_pageSize);
        sutProvider.GetDependency<IOrganizationContext>()
            .GetOrganizationClaims(Arg.Any<ClaimsPrincipal>(), orgId)
            .Returns(new CurrentContextOrganization { Id = orgId, Type = OrganizationUserType.Owner });
        sutProvider.GetDependency<IOrganizationUserKeyRepository>()
            .GetManyPendingV2UpgradesByOrganizationIdAsync(orgId, true, null, _pageSize)
            .Returns(ids.Select(Details).ToList());
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(ids.Select(id => new OrganizationUser { Id = id, OrganizationId = orgId }).ToList());
        MockRecoverAccountAuthorization(sutProvider, ids, succeeded: true);

        // Act
        var result = await sutProvider.Sut.GetPendingV2UpgradesAsync(orgId, continuationToken: null);

        // Assert
        Assert.Equal(_pageSize, result.Data.Count());
        Assert.Equal(ids.Last().ToString(), result.ContinuationToken);
    }

    [Theory, BitAutoData]
    public async Task GetPendingV2UpgradesAsync_PageIsFullOfMembersThatCannotBeRecovered_ReturnsNothingWithAContinuationToken(
        Guid orgId,
        SutProvider<OrganizationUsersKeysController> sutProvider)
    {
        // Arrange - the token moves past the members the caller cannot recover, so they do not fill every page
        var ids = NewIds(_pageSize);
        sutProvider.GetDependency<IOrganizationContext>()
            .GetOrganizationClaims(Arg.Any<ClaimsPrincipal>(), orgId)
            .Returns(new CurrentContextOrganization { Id = orgId, Type = OrganizationUserType.Owner });
        sutProvider.GetDependency<IOrganizationUserKeyRepository>()
            .GetManyPendingV2UpgradesByOrganizationIdAsync(orgId, true, null, _pageSize)
            .Returns(ids.Select(Details).ToList());
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(ids.Select(id => new OrganizationUser { Id = id, OrganizationId = orgId }).ToList());
        MockRecoverAccountAuthorization(sutProvider, ids, succeeded: false);

        // Act
        var result = await sutProvider.Sut.GetPendingV2UpgradesAsync(orgId, continuationToken: null);

        // Assert
        Assert.Empty(result.Data);
        Assert.Equal(ids.Last().ToString(), result.ContinuationToken);
    }

    [Theory, BitAutoData]
    public async Task GetPendingV2UpgradesAsync_NoPendingUpgrades_ReturnsNothing(
        Guid orgId,
        SutProvider<OrganizationUsersKeysController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationContext>()
            .GetOrganizationClaims(Arg.Any<ClaimsPrincipal>(), orgId)
            .Returns(new CurrentContextOrganization { Id = orgId, Type = OrganizationUserType.Owner });
        sutProvider.GetDependency<IOrganizationUserKeyRepository>()
            .GetManyPendingV2UpgradesByOrganizationIdAsync(orgId, true, null, _pageSize)
            .Returns([]);

        // Act
        var result = await sutProvider.Sut.GetPendingV2UpgradesAsync(orgId, continuationToken: null);

        // Assert
        Assert.Empty(result.Data);
        Assert.Null(result.ContinuationToken);
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>());
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
        sutProvider.GetDependency<IOrganizationContext>()
            .GetOrganizationClaims(Arg.Any<ClaimsPrincipal>(), orgId)
            .Returns(new CurrentContextOrganization { Id = orgId, Type = callerType });
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([new OrganizationUser { Id = organizationUserId, OrganizationId = orgId }]);
        MockRecoverAccountAuthorization(sutProvider, [organizationUserId], succeeded: true);
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

    [Theory, BitAutoData]
    public async Task PostV2UpgradesAsync_OneMemberCannotBeRecovered_ThrowsNotFoundAndAppliesNothing(
        Guid orgId,
        Guid recoverableId,
        Guid unrecoverableId,
        SutProvider<OrganizationUsersKeysController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationContext>()
            .GetOrganizationClaims(Arg.Any<ClaimsPrincipal>(), orgId)
            .Returns(new CurrentContextOrganization { Id = orgId, Type = OrganizationUserType.Owner });
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([
                new OrganizationUser { Id = recoverableId, OrganizationId = orgId },
                new OrganizationUser { Id = unrecoverableId, OrganizationId = orgId }
            ]);
        MockRecoverAccountAuthorization(sutProvider, [recoverableId], succeeded: true);
        MockRecoverAccountAuthorization(sutProvider, [unrecoverableId], succeeded: false);
        var model = ModelFor(recoverableId, unrecoverableId);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PostV2UpgradesAsync(orgId, model));
        await sutProvider.GetDependency<IApplyOrganizationUserV2UpgradesCommand>().DidNotReceiveWithAnyArgs()
            .ApplyAsync(Arg.Any<Guid>(), Arg.Any<bool>(),
                Arg.Any<IEnumerable<OrganizationUserAccountRecoveryKeyUpdate>>());
    }

    [Theory, BitAutoData]
    public async Task PostV2UpgradesAsync_MembershipOfAnotherOrganization_ThrowsNotFoundAndAppliesNothing(
        Guid orgId,
        Guid otherOrgId,
        Guid organizationUserId,
        SutProvider<OrganizationUsersKeysController> sutProvider)
    {
        // Arrange - the membership could be recovered in its own organization, but not through this route
        sutProvider.GetDependency<IOrganizationContext>()
            .GetOrganizationClaims(Arg.Any<ClaimsPrincipal>(), orgId)
            .Returns(new CurrentContextOrganization { Id = orgId, Type = OrganizationUserType.Owner });
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([new OrganizationUser { Id = organizationUserId, OrganizationId = otherOrgId }]);
        MockRecoverAccountAuthorization(sutProvider, [organizationUserId], succeeded: true);
        var model = ModelFor(organizationUserId);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PostV2UpgradesAsync(orgId, model));
        await sutProvider.GetDependency<IAuthorizationService>().DidNotReceiveWithAnyArgs()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>());
        await sutProvider.GetDependency<IApplyOrganizationUserV2UpgradesCommand>().DidNotReceiveWithAnyArgs()
            .ApplyAsync(Arg.Any<Guid>(), Arg.Any<bool>(),
                Arg.Any<IEnumerable<OrganizationUserAccountRecoveryKeyUpdate>>());
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

    private static List<Guid> NewIds(int count) =>
        Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();

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

    private static void MockRecoverAccountAuthorization(SutProvider<OrganizationUsersKeysController> sutProvider,
        IEnumerable<Guid> organizationUserIds, bool succeeded)
    {
        foreach (var organizationUserId in organizationUserIds)
        {
            sutProvider.GetDependency<IAuthorizationService>()
                .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                    Arg.Is<object>(resource =>
                        resource is OrganizationUser && ((OrganizationUser)resource).Id == organizationUserId),
                    Arg.Is<IEnumerable<IAuthorizationRequirement>>(requirements =>
                        requirements.SingleOrDefault() is RecoverAccountAuthorizationRequirement))
                .Returns(succeeded ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        }
    }
}
