using System.Security.Claims;
using Bit.Api.AdminConsole.Authorization;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Test.AdminConsole.AutoFixture;
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
    public async Task HandleRequirementAsync_SuccessViaMember_Authorized(
        SutProvider<RecoverAccountAuthorizationHandler> sutProvider,
        [OrganizationUser] OrganizationUser targetOrganizationUser,
        ClaimsPrincipal claimsPrincipal)
    {
        // Arrange
        var context = new AuthorizationHandlerContext(
            [new RecoverAccountAuthorizationRequirement()],
            claimsPrincipal,
            targetOrganizationUser);

        var currentContextOrganization = new CurrentContextOrganization
        {
            Type = OrganizationUserType.Owner,
            Id = targetOrganizationUser.OrganizationId
        };

        MockOrganizationClaims(sutProvider, claimsPrincipal, targetOrganizationUser, currentContextOrganization);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_SuccessViaProvider_Authorized(
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
    public async Task HandleRequirementAsync_NotMemberOrProvider_NotAuthorized(
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
        Assert.False(context.HasSucceeded);
    }

    // Pairing of CurrentContextOrganization (current user permissions) and target user role
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
    public async Task AuthorizeMemberAsync_RecoverEqualOrLesserRoles_Authorized(
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
    public static IEnumerable<object[]> UnauthorizedRoleCombinations => new object[][]
    {
        [new CurrentContextOrganization { Type = OrganizationUserType.Admin }, OrganizationUserType.Owner],
        [new CurrentContextOrganization { Type = OrganizationUserType.Custom, Permissions = new Permissions { ManageResetPassword = true}}, OrganizationUserType.Owner],
        [new CurrentContextOrganization { Type = OrganizationUserType.Custom, Permissions = new Permissions { ManageResetPassword = true} }, OrganizationUserType.Admin],
    };

    [Theory, BitMemberAutoData(nameof(UnauthorizedRoleCombinations))]
    public async Task AuthorizeMemberAsync_RecoverGreaterRoles_Unauthorized(
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
        Assert.False(context.HasSucceeded);
    }

    // Pairing of CurrentContextOrganization (current user permissions) and target user role
    public static IEnumerable<object[]> UnauthorizedRoles => new object[][]
    {
        [new CurrentContextOrganization { Type = OrganizationUserType.User }, OrganizationUserType.Owner],
        [new CurrentContextOrganization { Type = OrganizationUserType.User }, OrganizationUserType.Admin],
        [new CurrentContextOrganization { Type = OrganizationUserType.User }, OrganizationUserType.Custom],
        [new CurrentContextOrganization { Type = OrganizationUserType.User }, OrganizationUserType.User],
        [new CurrentContextOrganization { Type = OrganizationUserType.Custom }, OrganizationUserType.Owner],
        [new CurrentContextOrganization { Type = OrganizationUserType.Custom }, OrganizationUserType.Admin],
        [new CurrentContextOrganization { Type = OrganizationUserType.Custom }, OrganizationUserType.Custom],
        [new CurrentContextOrganization { Type = OrganizationUserType.Custom }, OrganizationUserType.User],
    };

    [Theory, BitMemberAutoData(nameof(UnauthorizedRoles))]
    public async Task AuthorizeMemberAsync_NonAdmin_NotAuthorized(
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
        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeMemberAsync_TargetUserIsProviderAccount_NotAuthorized(
        SutProvider<RecoverAccountAuthorizationHandler> sutProvider,
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser targetOrganizationUser,
        [CurrentContextOrganization(Type = OrganizationUserType.Owner)] CurrentContextOrganization currentContextOrganization,
        ClaimsPrincipal claimsPrincipal)
    {
        // Arrange
        var context = new AuthorizationHandlerContext(
            [new RecoverAccountAuthorizationRequirement()],
            claimsPrincipal,
            targetOrganizationUser);

        MockOrganizationClaims(sutProvider, claimsPrincipal, targetOrganizationUser, currentContextOrganization);
        MockTargetUserIsProvider(sutProvider, targetOrganizationUser);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
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

    private static void MockTargetUserIsProvider(SutProvider<RecoverAccountAuthorizationHandler> sutProvider,
        OrganizationUser targetOrganizationUser)
    {
        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByOrganizationAsync(targetOrganizationUser.OrganizationId)
            .Returns([new ProviderUser
            {
                Id = Guid.NewGuid(),
                ProviderId = Guid.NewGuid(),
                UserId = targetOrganizationUser.UserId!.Value,
                Status = ProviderUserStatusType.Confirmed,
                Type = ProviderUserType.ProviderAdmin
            }]);
    }
}
