using System.Security.Claims;
using Bit.Api.AdminConsole.Authorization;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization;

/// <summary>
/// These tests stub <see cref="IOrganizationUserValidationService"/> directly rather than exercising a real
/// instance: the Owner/Admin/Custom/User role hierarchy (plus provider override) it implements is already
/// covered by <c>OrganizationUserValidationServiceTests</c>. These tests instead focus on behavior specific to
/// this handler: how it maps its inputs into that service call, which permission it gates on, how it reacts to
/// the service's decision, and the additional provider-membership check it layers on top.
/// </summary>
[SutProviderCustomize]
public class RecoverAccountAuthorizationHandlerTests
{
    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_ManageCheckAuthorized_TargetHasNoProviders_Authorized(
        SutProvider<RecoverAccountAuthorizationHandler> sutProvider,
        [OrganizationUser] OrganizationUser targetOrganizationUser,
        ClaimsPrincipal claimsPrincipal)
    {
        // Arrange
        MockOrganizationClaims(sutProvider, claimsPrincipal, targetOrganizationUser, null);
        MockManageResult(sutProvider, null);

        var context = new AuthorizationHandlerContext(
            [new RecoverAccountAuthorizationRequirement()],
            claimsPrincipal,
            targetOrganizationUser);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_ManageCheckDenied_NotAuthorized(
        SutProvider<RecoverAccountAuthorizationHandler> sutProvider,
        [OrganizationUser] OrganizationUser targetOrganizationUser,
        ClaimsPrincipal claimsPrincipal)
    {
        // Arrange
        MockOrganizationClaims(sutProvider, claimsPrincipal, targetOrganizationUser, null);
        MockManageResult(sutProvider, new CustomUsersCannotManageAdminsOrOwners());

        var context = new AuthorizationHandlerContext(
            [new RecoverAccountAuthorizationRequirement()],
            claimsPrincipal,
            targetOrganizationUser);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        AssertFailed(context, RecoverAccountAuthorizationHandler.FailureReason);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_UsesManageResetPasswordPermissionGate_NotManageUsers(
        SutProvider<RecoverAccountAuthorizationHandler> sutProvider,
        [OrganizationUser] OrganizationUser targetOrganizationUser,
        ClaimsPrincipal claimsPrincipal,
        CurrentContextOrganization currentContextOrganization)
    {
        // Arrange: account recovery is gated on ManageResetPassword rather than the validation service's default
        // ManageUsers permission, so capture the gate the handler passes in and assert on it directly.
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(Guid.NewGuid());
        currentContextOrganization.Id = targetOrganizationUser.OrganizationId;
        MockOrganizationClaims(sutProvider, claimsPrincipal, targetOrganizationUser, currentContextOrganization);

        CustomUserManagePermission? capturedGate = null;
        sutProvider.GetDependency<IOrganizationUserValidationService>()
            .CanManageAsync(Arg.Any<Guid>(), Arg.Any<IOrganizationUserRole?>(), Arg.Any<IOrganizationUserRole>(),
                Arg.Do<CustomUserManagePermission>(gate => capturedGate = gate))
            .Returns((Error?)null);

        var context = new AuthorizationHandlerContext(
            [new RecoverAccountAuthorizationRequirement()],
            claimsPrincipal,
            targetOrganizationUser);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        Assert.Equal(CustomUserManagePermission.ManageResetPassword, capturedGate);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_TargetUserIdIsNull_DoesNotBlock(
        SutProvider<RecoverAccountAuthorizationHandler> sutProvider,
        OrganizationUser targetOrganizationUser,
        ClaimsPrincipal claimsPrincipal)
    {
        // Arrange
        targetOrganizationUser.UserId = null;
        MockManageResult(sutProvider, null);

        var context = new AuthorizationHandlerContext(
            [new RecoverAccountAuthorizationRequirement()],
            claimsPrincipal,
            targetOrganizationUser);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
        // This should shortcut the provider escalation check
        await sutProvider.GetDependency<IProviderUserRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByUserAsync(Arg.Any<Guid>());
    }

    // Pairing of the current user's provider role and the target user's provider role, in the same provider.
    // Read this as: a ___ can recover the account for a ___
    public static IEnumerable<object[]> AuthorizedProviderRoleCombinations => new object[][]
    {
        [ProviderUserType.ProviderAdmin, ProviderUserType.ProviderAdmin],
        [ProviderUserType.ProviderAdmin, ProviderUserType.ServiceUser],
        [ProviderUserType.ServiceUser, ProviderUserType.ServiceUser],
    };

    [Theory, BitMemberAutoData(nameof(AuthorizedProviderRoleCombinations))]
    public async Task CanRecoverProviderAsync_RecoverEqualOrLesserProviderRoles_Authorized(
        ProviderUserType currentUserProviderType,
        ProviderUserType targetProviderUserType,
        SutProvider<RecoverAccountAuthorizationHandler> sutProvider,
        [OrganizationUser] OrganizationUser targetOrganizationUser,
        ClaimsPrincipal claimsPrincipal,
        Guid providerId)
    {
        // Arrange
        var context = new AuthorizationHandlerContext(
            [new RecoverAccountAuthorizationRequirement()],
            claimsPrincipal,
            targetOrganizationUser);

        MockCurrentUserIsProvider(sutProvider, claimsPrincipal, targetOrganizationUser);
        MockCurrentUserProviderRole(sutProvider, providerId, currentUserProviderType);
        MockTargetUserProviders(sutProvider, targetOrganizationUser,
            [new ProviderUser { ProviderId = providerId, UserId = targetOrganizationUser.UserId, Type = targetProviderUserType }]);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    // Pairing of the current user's provider role and the target user's provider role, in the same provider.
    // Read this as: a ___ cannot recover the account for a ___
    // A null current user role means they are not a member of the target's provider at all.
    public static IEnumerable<object[]> UnauthorizedProviderRoleCombinations => new object[][]
    {
        // A Service User cannot escalate into the higher provider role
        [ProviderUserType.ServiceUser, ProviderUserType.ProviderAdmin],

        // A non-member of the provider cannot recover any of its members
        [null!, ProviderUserType.ProviderAdmin],
        [null!, ProviderUserType.ServiceUser],
    };

    [Theory, BitMemberAutoData(nameof(UnauthorizedProviderRoleCombinations))]
    public async Task CanRecoverProviderAsync_InvalidProviderRoles_Unauthorized(
        ProviderUserType? currentUserProviderType,
        ProviderUserType targetProviderUserType,
        SutProvider<RecoverAccountAuthorizationHandler> sutProvider,
        [OrganizationUser] OrganizationUser targetOrganizationUser,
        ClaimsPrincipal claimsPrincipal,
        Guid providerId)
    {
        // Arrange
        var context = new AuthorizationHandlerContext(
            [new RecoverAccountAuthorizationRequirement()],
            claimsPrincipal,
            targetOrganizationUser);

        // The current user clears step 1 as an organization Owner, so only the provider check can fail
        MockCurrentUserIsOwner(sutProvider, claimsPrincipal, targetOrganizationUser);
        MockCurrentUserProviderRole(sutProvider, providerId, currentUserProviderType);
        MockTargetUserProviders(sutProvider, targetOrganizationUser,
            [new ProviderUser { ProviderId = providerId, UserId = targetOrganizationUser.UserId, Type = targetProviderUserType }]);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        AssertFailed(context, RecoverAccountAuthorizationHandler.ProviderFailureReason);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_CurrentUserIsMemberOfAllTargetUserProviders_DoesNotBlock(
        SutProvider<RecoverAccountAuthorizationHandler> sutProvider,
        [OrganizationUser] OrganizationUser targetOrganizationUser,
        ClaimsPrincipal claimsPrincipal,
        Guid providerId1,
        Guid providerId2)
    {
        // Arrange
        var context = new AuthorizationHandlerContext(
            [new RecoverAccountAuthorizationRequirement()],
            claimsPrincipal,
            targetOrganizationUser);

        MockCurrentUserIsProvider(sutProvider, claimsPrincipal, targetOrganizationUser);
        MockCurrentUserProviderRole(sutProvider, providerId1, ProviderUserType.ProviderAdmin);
        MockCurrentUserProviderRole(sutProvider, providerId2, ProviderUserType.ProviderAdmin);
        MockTargetUserProviders(sutProvider, targetOrganizationUser,
        [
            new ProviderUser { ProviderId = providerId1, UserId = targetOrganizationUser.UserId, Type = ProviderUserType.ServiceUser },
            new ProviderUser { ProviderId = providerId2, UserId = targetOrganizationUser.UserId, Type = ProviderUserType.ProviderAdmin }
        ]);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_CurrentUserMissingProviderMembership_Blocks(
        SutProvider<RecoverAccountAuthorizationHandler> sutProvider,
        [OrganizationUser] OrganizationUser targetOrganizationUser,
        ClaimsPrincipal claimsPrincipal,
        Guid providerId1,
        Guid providerId2)
    {
        // Arrange
        var context = new AuthorizationHandlerContext(
            [new RecoverAccountAuthorizationRequirement()],
            claimsPrincipal,
            targetOrganizationUser);

        MockCurrentUserIsOwner(sutProvider, claimsPrincipal, targetOrganizationUser);
        MockCurrentUserProviderRole(sutProvider, providerId1, ProviderUserType.ProviderAdmin);
        // Not a member of this provider
        MockCurrentUserProviderRole(sutProvider, providerId2, null);
        MockTargetUserProviders(sutProvider, targetOrganizationUser,
        [
            new ProviderUser { ProviderId = providerId1, UserId = targetOrganizationUser.UserId, Type = ProviderUserType.ServiceUser },
            new ProviderUser { ProviderId = providerId2, UserId = targetOrganizationUser.UserId, Type = ProviderUserType.ServiceUser }
        ]);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        AssertFailed(context, RecoverAccountAuthorizationHandler.ProviderFailureReason);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_NoActingUserId_NotAuthorized(
        SutProvider<RecoverAccountAuthorizationHandler> sutProvider,
        [OrganizationUser] OrganizationUser targetOrganizationUser,
        ClaimsPrincipal claimsPrincipal)
    {
        // Arrange: simulate a request with no resolvable acting user id. The handler must deny outright rather
        // than substituting a fabricated id (e.g. Guid.Empty) into the "can manage" security check.
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        var context = new AuthorizationHandlerContext(
            [new RecoverAccountAuthorizationRequirement()],
            claimsPrincipal,
            targetOrganizationUser);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        AssertFailed(context, RecoverAccountAuthorizationHandler.FailureReason);
        await sutProvider.GetDependency<IOrganizationUserValidationService>().DidNotReceiveWithAnyArgs()
            .CanManageAsync(default, default, default!);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_TargetUserHasNoProviders_DoesNotBlock(
        SutProvider<RecoverAccountAuthorizationHandler> sutProvider,
        [OrganizationUser] OrganizationUser targetOrganizationUser,
        ClaimsPrincipal claimsPrincipal)
    {
        // Arrange
        var context = new AuthorizationHandlerContext(
            [new RecoverAccountAuthorizationRequirement()],
            claimsPrincipal,
            targetOrganizationUser);

        MockCurrentUserIsOwner(sutProvider, claimsPrincipal, targetOrganizationUser);
        MockTargetUserProviders(sutProvider, targetOrganizationUser, []);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    private static void MockTargetUserProviders(SutProvider<RecoverAccountAuthorizationHandler> sutProvider,
        OrganizationUser targetOrganizationUser, List<ProviderUser> targetUserProviders)
    {
        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByUserAsync(targetOrganizationUser.UserId!.Value)
            .Returns(targetUserProviders);
    }

    /// <summary>
    /// Mocks the current user's role in the specified provider. A null <paramref name="providerUserType"/>
    /// means they are not a member of that provider.
    /// </summary>
    private static void MockCurrentUserProviderRole(SutProvider<RecoverAccountAuthorizationHandler> sutProvider,
        Guid providerId, ProviderUserType? providerUserType)
    {
        sutProvider.GetDependency<ICurrentContext>()
            .ProviderUser(providerId)
            .Returns(providerUserType is not null);

        sutProvider.GetDependency<ICurrentContext>()
            .ProviderProviderAdmin(providerId)
            .Returns(providerUserType is ProviderUserType.ProviderAdmin);
    }

    private static void MockCurrentUserIsOwner(SutProvider<RecoverAccountAuthorizationHandler> sutProvider,
        ClaimsPrincipal currentUser, OrganizationUser targetOrganizationUser)
    {
        MockManageResult(sutProvider, null);
        MockOrganizationClaims(sutProvider, currentUser, targetOrganizationUser,
            new CurrentContextOrganization { Id = targetOrganizationUser.OrganizationId, Type = OrganizationUserType.Owner });
    }

    private static void MockCurrentUserIsProvider(SutProvider<RecoverAccountAuthorizationHandler> sutProvider,
        ClaimsPrincipal currentUser, OrganizationUser targetOrganizationUser)
    {
        MockManageResult(sutProvider, null);
        MockOrganizationClaims(sutProvider, currentUser, targetOrganizationUser, null);
    }

    /// <summary>
    /// Stubs <see cref="IOrganizationUserValidationService.CanManageAsync(Guid, IOrganizationUserRole?, IOrganizationUserRole, CustomUserManagePermission)"/>
    /// to return the given result for any input, so tests can control the "can manage this user's role" outcome
    /// directly instead of relying on the real role-hierarchy implementation. Also ensures a resolvable acting
    /// user id is present, since that's a precondition for the handler to reach this check at all.
    /// </summary>
    private static void MockManageResult(SutProvider<RecoverAccountAuthorizationHandler> sutProvider, Error? result)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(Guid.NewGuid());

        sutProvider.GetDependency<IOrganizationUserValidationService>()
            .CanManageAsync(Arg.Any<Guid>(), Arg.Any<IOrganizationUserRole?>(), Arg.Any<IOrganizationUserRole>(),
                Arg.Any<CustomUserManagePermission>())
            .Returns(result);
    }

    private static void MockOrganizationClaims(SutProvider<RecoverAccountAuthorizationHandler> sutProvider,
        ClaimsPrincipal currentUser, OrganizationUser targetOrganizationUser,
        CurrentContextOrganization? currentContextOrganization)
    {
        sutProvider.GetDependency<IOrganizationContext>()
            .GetOrganizationClaims(currentUser, targetOrganizationUser.OrganizationId)
            .Returns(currentContextOrganization);
    }

    private static void AssertFailed(AuthorizationHandlerContext context, string expectedMessage)
    {
        Assert.True(context.HasFailed);
        var failureReason = Assert.Single(context.FailureReasons);
        Assert.Equal(expectedMessage, failureReason.Message);
    }
}
