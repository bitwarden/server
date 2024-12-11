using System.Security.Claims;
using Bit.Core.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using NSubstitute;
using Xunit;
using AuthorizationServiceExtensions = Bit.Core.Utilities.AuthorizationServiceExtensions;

namespace Bit.Core.Test.Utilities;

public class AuthorizationServiceExtensionTests
{
    [Fact]
    async Task AuthorizeOrThrowAsync_ThrowsNotFoundException_IfResourceIsNull()
    {
        var authorizationService = Substitute.For<IAuthorizationService>();
        await Assert.ThrowsAsync<NotFoundException>(
            () =>
                AuthorizationServiceExtensions.AuthorizeOrThrowAsync(
                    authorizationService,
                    new ClaimsPrincipal(),
                    null,
                    new OperationAuthorizationRequirement()
                )
        );
    }

    [Fact]
    async Task AuthorizeOrThrowAsync_ThrowsNotFoundException_IfAuthorizationFails()
    {
        var authorizationService = Substitute.For<IAuthorizationService>();
        var claimsPrincipal = new ClaimsPrincipal();
        var requirement = new OperationAuthorizationRequirement();
        var resource = new object();

        authorizationService
            .AuthorizeAsync(
                claimsPrincipal,
                resource,
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(r => r.First() == requirement)
            )
            .Returns(AuthorizationResult.Failed());

        await Assert.ThrowsAsync<NotFoundException>(
            () =>
                AuthorizationServiceExtensions.AuthorizeOrThrowAsync(
                    authorizationService,
                    claimsPrincipal,
                    resource,
                    requirement
                )
        );
    }
}
