using System.Security.Claims;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Authorization;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.KeyManagement.Authorization;

[SutProviderCustomize]
public class KeyConnectorAuthorizationHandlerTests
{
    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_UserCanUseKeyConnector_Success(
        User user,
        ClaimsPrincipal claimsPrincipal,
        SutProvider<KeyConnectorAuthorizationHandler> sutProvider)
    {
        // Arrange
        user.UsesKeyConnector = false;
        sutProvider.GetDependency<ICurrentContext>().Organizations
            .Returns(new List<CurrentContextOrganization>());

        var requirement = KeyConnectorOperations.Use;
        var context = new AuthorizationHandlerContext([requirement], claimsPrincipal, user);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_UserAlreadyUsesKeyConnector_Fails(
        User user,
        ClaimsPrincipal claimsPrincipal,
        SutProvider<KeyConnectorAuthorizationHandler> sutProvider)
    {
        // Arrange
        user.UsesKeyConnector = true;
        sutProvider.GetDependency<ICurrentContext>().Organizations
            .Returns(new List<CurrentContextOrganization>());

        var requirement = KeyConnectorOperations.Use;
        var context = new AuthorizationHandlerContext([requirement], claimsPrincipal, user);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_UserIsOwner_Fails(
        User user,
        Guid organizationId,
        ClaimsPrincipal claimsPrincipal,
        SutProvider<KeyConnectorAuthorizationHandler> sutProvider)
    {
        // Arrange
        user.UsesKeyConnector = false;
        var organizations = new List<CurrentContextOrganization>
        {
            new() { Id = organizationId, Type = OrganizationUserType.Owner }
        };
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns(organizations);

        var requirement = KeyConnectorOperations.Use;
        var context = new AuthorizationHandlerContext([requirement], claimsPrincipal, user);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_UserIsAdmin_Fails(
        User user,
        Guid organizationId,
        ClaimsPrincipal claimsPrincipal,
        SutProvider<KeyConnectorAuthorizationHandler> sutProvider)
    {
        // Arrange
        user.UsesKeyConnector = false;
        var organizations = new List<CurrentContextOrganization>
        {
            new() { Id = organizationId, Type = OrganizationUserType.Admin }
        };
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns(organizations);

        var requirement = KeyConnectorOperations.Use;
        var context = new AuthorizationHandlerContext([requirement], claimsPrincipal, user);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_UserIsRegularMember_Success(
        User user,
        Guid organizationId,
        ClaimsPrincipal claimsPrincipal,
        SutProvider<KeyConnectorAuthorizationHandler> sutProvider)
    {
        // Arrange
        user.UsesKeyConnector = false;
        var organizations = new List<CurrentContextOrganization>
        {
            new() { Id = organizationId, Type = OrganizationUserType.User }
        };
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns(organizations);

        var requirement = KeyConnectorOperations.Use;
        var context = new AuthorizationHandlerContext([requirement], claimsPrincipal, user);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementAsync_UnsupportedRequirement_ThrowsArgumentException(
        User user,
        ClaimsPrincipal claimsPrincipal,
        SutProvider<KeyConnectorAuthorizationHandler> sutProvider)
    {
        // Arrange
        user.UsesKeyConnector = false;
        sutProvider.GetDependency<ICurrentContext>().Organizations
            .Returns(new List<CurrentContextOrganization>());

        var unsupportedRequirement = new KeyConnectorOperationsRequirement("UnsupportedOperation");
        var context = new AuthorizationHandlerContext([unsupportedRequirement], claimsPrincipal, user);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => sutProvider.Sut.HandleAsync(context));
    }
}
