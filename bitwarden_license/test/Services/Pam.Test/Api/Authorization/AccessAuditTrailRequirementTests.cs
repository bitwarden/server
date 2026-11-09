using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Services.Pam.Api.Authorization;
using Xunit;

namespace Bit.Services.Pam.Test.Api.Authorization;

public class AccessAuditTrailRequirementTests
{
    private readonly AccessAuditTrailRequirement _sut = new();

    private bool _providerConsulted;

    private Task<bool> IsProviderUserForOrg()
    {
        _providerConsulted = true;
        return Task.FromResult(true);
    }

    private static CurrentContextOrganization Member(OrganizationUserType type, Permissions? permissions = null) =>
        new() { Id = Guid.NewGuid(), Type = type, Permissions = permissions ?? new Permissions() };

    [Theory]
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    public async Task AuthorizeAsync_AuthorizesOwnersAndAdmins(OrganizationUserType type)
    {
        Assert.True(await _sut.AuthorizeAsync(Member(type), IsProviderUserForOrg));
    }

    [Fact]
    public async Task AuthorizeAsync_AuthorizesCustomUserWithAccessEventLogs()
    {
        var claims = Member(OrganizationUserType.Custom, new Permissions { AccessEventLogs = true });

        Assert.True(await _sut.AuthorizeAsync(claims, IsProviderUserForOrg));
    }

    [Fact]
    public async Task AuthorizeAsync_DoesNotAuthorizeCustomUserWithoutAccessEventLogs()
    {
        var claims = Member(OrganizationUserType.Custom, new Permissions { ManageAccessRules = true });

        Assert.False(await _sut.AuthorizeAsync(claims, IsProviderUserForOrg));
    }

    [Fact]
    public async Task AuthorizeAsync_DoesNotAuthorizePlainUser()
    {
        Assert.False(await _sut.AuthorizeAsync(Member(OrganizationUserType.User), IsProviderUserForOrg));
    }

    // A provider user arrives with no organization claims.
    [Fact]
    public async Task AuthorizeAsync_DoesNotAuthorizeProviderForTheOrganization()
    {
        Assert.False(await _sut.AuthorizeAsync(null, IsProviderUserForOrg));
        Assert.False(_providerConsulted);
    }
}
