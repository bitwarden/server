using System.Security.Claims;
using Bit.Api.AdminConsole.Authorization;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
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
        MockTargetUserProviders(sutProvider, targetOrganizationUser, []);

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
        MockTargetUserProviders(sutProvider, targetOrganizationUser, []);

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
