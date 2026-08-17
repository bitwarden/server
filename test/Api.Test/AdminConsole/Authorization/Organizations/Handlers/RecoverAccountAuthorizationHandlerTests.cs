using System.Security.Claims;
using Bit.Api.AdminConsole.Authorization;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Context;
using Bit.Core.Entities;
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

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_CurrentUserIsMemberOfAllTargetUserProviders_DoesNotBlock(
        SutProvider<RecoverAccountAuthorizationHandler> sutProvider,
        [OrganizationUser] OrganizationUser targetOrganizationUser,
        ClaimsPrincipal claimsPrincipal,
        Guid providerId1,
        Guid providerId2)
    {
        // Arrange
        MockManageResult(sutProvider, null);

        var targetUserProviders = new List<ProviderUser>
        {
            new() { ProviderId = providerId1, UserId = targetOrganizationUser.UserId },
            new() { ProviderId = providerId2, UserId = targetOrganizationUser.UserId }
        };

        var context = new AuthorizationHandlerContext(
            [new RecoverAccountAuthorizationRequirement()],
            claimsPrincipal,
            targetOrganizationUser);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByUserAsync(targetOrganizationUser.UserId!.Value)
            .Returns(targetUserProviders);

        sutProvider.GetDependency<ICurrentContext>()
            .ProviderUser(providerId1)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>()
            .ProviderUser(providerId2)
            .Returns(true);

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
        MockManageResult(sutProvider, null);

        var targetUserProviders = new List<ProviderUser>
        {
            new() { ProviderId = providerId1, UserId = targetOrganizationUser.UserId },
            new() { ProviderId = providerId2, UserId = targetOrganizationUser.UserId }
        };

        var context = new AuthorizationHandlerContext(
            [new RecoverAccountAuthorizationRequirement()],
            claimsPrincipal,
            targetOrganizationUser);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByUserAsync(targetOrganizationUser.UserId!.Value)
            .Returns(targetUserProviders);

        sutProvider.GetDependency<ICurrentContext>()
            .ProviderUser(providerId1)
            .Returns(true);

        // Not a member of this provider
        sutProvider.GetDependency<ICurrentContext>()
            .ProviderUser(providerId2)
            .Returns(false);

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
