using System.Security.Claims;
using Bit.Api.AdminConsole.Authorization;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization;

[SutProviderCustomize]
public class RecoverAccountAuthorizationHandlerTests
{
    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_CurrentUserIsProvider_TargetUserNotProvider_Authorized(
        SutProvider<RecoverAccountAuthorizationHandler> sutProvider,
        [OrganizationUser] OrganizationUser targetOrganizationUser,
        ClaimsPrincipal claimsPrincipal)
    {
        // Arrange
        var context = new AuthorizationHandlerContext(
            [new RecoverAccountAuthorizationRequirement()],
            claimsPrincipal,
            targetOrganizationUser);

        MockOrganizationClaims(sutProvider, claimsPrincipal, targetOrganizationUser, null);
        MockCurrentUserIsProvider(sutProvider, claimsPrincipal, targetOrganizationUser);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_CurrentUserIsNotMemberOrProvider_NotAuthorized(
        SutProvider<RecoverAccountAuthorizationHandler> sutProvider,
        [OrganizationUser] OrganizationUser targetOrganizationUser,
        ClaimsPrincipal claimsPrincipal)
    {
        // Arrange
        var context = new AuthorizationHandlerContext(
            [new RecoverAccountAuthorizationRequirement()],
            claimsPrincipal,
            targetOrganizationUser);

        MockOrganizationClaims(sutProvider, claimsPrincipal, targetOrganizationUser, null);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        AssertFailed(context, RecoverAccountAuthorizationHandler.FailureReason);
    }

    // Pairing of CurrentContextOrganization (current user permissions) and target user role
    // Read this as: a ___ can recover the account for a ___
    public static IEnumerable<object[]> AuthorizedRoleCombinations => new object[][]
    {
        [new CurrentContextOrganization { Type = OrganizationUserType.Owner }, OrganizationUserType.Owner],
        [new CurrentContextOrganization { Type = OrganizationUserType.Owner }, OrganizationUserType.Admin],
        [new CurrentContextOrganization { Type = OrganizationUserType.Owner }, OrganizationUserType.Custom],
        [new CurrentContextOrganization { Type = OrganizationUserType.Owner }, OrganizationUserType.User],
        [new CurrentContextOrganization { Type = OrganizationUserType.Admin }, OrganizationUserType.Admin],
        [new CurrentContextOrganization { Type = OrganizationUserType.Admin }, OrganizationUserType.Custom],
        [new CurrentContextOrganization { Type = OrganizationUserType.Admin }, OrganizationUserType.User],
        [new CurrentContextOrganization { Type = OrganizationUserType.Custom, Permissions = new Permissions { ManageResetPassword = true}}, OrganizationUserType.Custom],
        [new CurrentContextOrganization { Type = OrganizationUserType.Custom, Permissions = new Permissions { ManageResetPassword = true}}, OrganizationUserType.User],
    };

    [Theory, BitMemberAutoData(nameof(AuthorizedRoleCombinations))]
    public async Task AuthorizeMemberAsync_RecoverEqualOrLesserRoles_TargetUserNotProvider_Authorized(
        CurrentContextOrganization currentContextOrganization,
        OrganizationUserType targetOrganizationUserType,
        SutProvider<RecoverAccountAuthorizationHandler> sutProvider,
        [OrganizationUser] OrganizationUser targetOrganizationUser,
        ClaimsPrincipal claimsPrincipal)
    {
        // Arrange
        targetOrganizationUser.Type = targetOrganizationUserType;
        currentContextOrganization.Id = targetOrganizationUser.OrganizationId;

        var context = new AuthorizationHandlerContext(
            [new RecoverAccountAuthorizationRequirement()],
            claimsPrincipal,
            targetOrganizationUser);

        MockOrganizationClaims(sutProvider, claimsPrincipal, targetOrganizationUser, currentContextOrganization);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    // Pairing of CurrentContextOrganization (current user permissions) and target user role
    // Read this as: a ___ cannot recover the account for a ___
    public static IEnumerable<object[]> UnauthorizedRoleCombinations => new object[][]
    {
        // These roles should fail because you cannot recover a greater role
        [new CurrentContextOrganization { Type = OrganizationUserType.Admin }, OrganizationUserType.Owner],
        [new CurrentContextOrganization { Type = OrganizationUserType.Custom, Permissions = new Permissions { ManageResetPassword = true}}, OrganizationUserType.Owner],
        [new CurrentContextOrganization { Type = OrganizationUserType.Custom, Permissions = new Permissions { ManageResetPassword = true} }, OrganizationUserType.Admin],

        // These roles are never authorized to recover any account
        [new CurrentContextOrganization { Type = OrganizationUserType.User }, OrganizationUserType.Owner],
        [new CurrentContextOrganization { Type = OrganizationUserType.User }, OrganizationUserType.Admin],
        [new CurrentContextOrganization { Type = OrganizationUserType.User }, OrganizationUserType.Custom],
        [new CurrentContextOrganization { Type = OrganizationUserType.User }, OrganizationUserType.User],
        [new CurrentContextOrganization { Type = OrganizationUserType.Custom }, OrganizationUserType.Owner],
        [new CurrentContextOrganization { Type = OrganizationUserType.Custom }, OrganizationUserType.Admin],
        [new CurrentContextOrganization { Type = OrganizationUserType.Custom }, OrganizationUserType.Custom],
        [new CurrentContextOrganization { Type = OrganizationUserType.Custom }, OrganizationUserType.User],
    };

    [Theory, BitMemberAutoData(nameof(UnauthorizedRoleCombinations))]
    public async Task AuthorizeMemberAsync_InvalidRoles_TargetUserNotProvider_Unauthorized(
        CurrentContextOrganization currentContextOrganization,
        OrganizationUserType targetOrganizationUserType,
        SutProvider<RecoverAccountAuthorizationHandler> sutProvider,
        [OrganizationUser] OrganizationUser targetOrganizationUser,
        ClaimsPrincipal claimsPrincipal)
    {
        // Arrange
        targetOrganizationUser.Type = targetOrganizationUserType;
        currentContextOrganization.Id = targetOrganizationUser.OrganizationId;

        var context = new AuthorizationHandlerContext(
            [new RecoverAccountAuthorizationRequirement()],
            claimsPrincipal,
            targetOrganizationUser);

        MockOrganizationClaims(sutProvider, claimsPrincipal, targetOrganizationUser, currentContextOrganization);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        AssertFailed(context, RecoverAccountAuthorizationHandler.FailureReason);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_TargetUserIdIsNull_DoesNotBlock(
        SutProvider<RecoverAccountAuthorizationHandler> sutProvider,
        OrganizationUser targetOrganizationUser,
        ClaimsPrincipal claimsPrincipal)
    {
        // Arrange
        targetOrganizationUser.UserId = null;
        MockCurrentUserIsOwner(sutProvider, claimsPrincipal, targetOrganizationUser);

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
        var targetUserProviders = new List<ProviderUser>
        {
            new() { ProviderId = providerId1, UserId = targetOrganizationUser.UserId },
            new() { ProviderId = providerId2, UserId = targetOrganizationUser.UserId }
        };

        var context = new AuthorizationHandlerContext(
            [new RecoverAccountAuthorizationRequirement()],
            claimsPrincipal,
            targetOrganizationUser);

        MockCurrentUserIsProvider(sutProvider, claimsPrincipal, targetOrganizationUser);

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
        var targetUserProviders = new List<ProviderUser>
        {
            new() { ProviderId = providerId1, UserId = targetOrganizationUser.UserId },
            new() { ProviderId = providerId2, UserId = targetOrganizationUser.UserId }
        };

        var context = new AuthorizationHandlerContext(
            [new RecoverAccountAuthorizationRequirement()],
            claimsPrincipal,
            targetOrganizationUser);

        MockCurrentUserIsOwner(sutProvider, claimsPrincipal, targetOrganizationUser);

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

    private static void MockOrganizationClaims(SutProvider<RecoverAccountAuthorizationHandler> sutProvider,
        ClaimsPrincipal currentUser, OrganizationUser targetOrganizationUser,
        CurrentContextOrganization? currentContextOrganization)
    {
        sutProvider.GetDependency<IOrganizationContext>()
            .GetOrganizationClaims(currentUser, targetOrganizationUser.OrganizationId)
            .Returns(currentContextOrganization);
    }

    private static void MockCurrentUserIsProvider(SutProvider<RecoverAccountAuthorizationHandler> sutProvider,
        ClaimsPrincipal currentUser, OrganizationUser targetOrganizationUser)
    {
        sutProvider.GetDependency<IOrganizationContext>()
            .IsProviderUserForOrganization(currentUser, targetOrganizationUser.OrganizationId)
            .Returns(true);
    }

    private static void MockCurrentUserIsOwner(SutProvider<RecoverAccountAuthorizationHandler> sutProvider,
        ClaimsPrincipal currentUser, OrganizationUser targetOrganizationUser)
    {
        var currentContextOrganization = new CurrentContextOrganization
        {
            Id = targetOrganizationUser.OrganizationId,
            Type = OrganizationUserType.Owner
        };

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
