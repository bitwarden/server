using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Services.Pam.AccessConnector.Api.Authorization;
using Xunit;

namespace Bit.Services.Pam.Test.AccessConnector.Api.Authorization;

/// <summary>
/// An access connector holds the organization key and rewrites credentials at the target system, so authority over the
/// fleet is narrower than the usual custom-permission requirement: Owners and Admins, and nobody else. In particular
/// neither a Custom user holding ManageAccessRules nor a provider managing the organization is authorized, which is
/// why this requirement implements <c>IOrganizationRequirement</c> directly instead of deriving from
/// <c>BasePermissionRequirement</c>.
/// </summary>
public class ManageAccessConnectorRequirementTests
{
    private readonly ManageAccessConnectorRequirement _sut = new();

    /// <summary>
    /// Records whether the requirement consulted provider status. It should never need to: the callback costs a
    /// database query, and a provider has no authority over the rotation fleet either way.
    /// </summary>
    private bool _providerConsulted;

    private Task<bool> IsProviderUserForOrg(bool result = true)
    {
        _providerConsulted = true;
        return Task.FromResult(result);
    }

    private static CurrentContextOrganization Member(OrganizationUserType type, Permissions? permissions = null) =>
        new() { Id = Guid.NewGuid(), Type = type, Permissions = permissions ?? new Permissions() };

    [Theory]
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    public async Task AuthorizeAsync_AuthorizesOwnersAndAdmins(OrganizationUserType type)
    {
        Assert.True(await _sut.AuthorizeAsync(Member(type), () => IsProviderUserForOrg()));
    }

    [Fact]
    public async Task AuthorizeAsync_DoesNotAuthorizeCustomUserWithManageAccessRules()
    {
        // ManageAccessRules is authority over who may lease a credential, not over the access
        // connectors that rotate it.
        var claims = Member(OrganizationUserType.Custom, new Permissions { ManageAccessRules = true });

        Assert.False(await _sut.AuthorizeAsync(claims, () => IsProviderUserForOrg()));
    }

    [Fact]
    public async Task AuthorizeAsync_DoesNotAuthorizeCustomUserWithEveryOtherPermission()
    {
        var claims = Member(OrganizationUserType.Custom, new Permissions
        {
            AccessEventLogs = true,
            AccessImportExport = true,
            AccessReports = true,
            CreateNewCollections = true,
            EditAnyCollection = true,
            DeleteAnyCollection = true,
            ManageGroups = true,
            ManagePolicies = true,
            ManageResetPassword = true,
            ManageScim = true,
            ManageSso = true,
            ManageUsers = true,
            ManageAccessRules = true
        });

        Assert.False(await _sut.AuthorizeAsync(claims, () => IsProviderUserForOrg()));
    }

    [Fact]
    public async Task AuthorizeAsync_DoesNotAuthorizePlainUser()
    {
        Assert.False(await _sut.AuthorizeAsync(Member(OrganizationUserType.User), () => IsProviderUserForOrg()));
    }

    [Fact]
    public async Task AuthorizeAsync_DoesNotAuthorizeProviderForTheOrganization()
    {
        // A provider user is not a member, so they arrive with no organization claims. BasePermissionRequirement's
        // final arm would authorize them here; this requirement must not.
        Assert.False(await _sut.AuthorizeAsync(null, () => IsProviderUserForOrg()));
    }

    [Fact]
    public async Task AuthorizeAsync_NeverConsultsProviderStatus()
    {
        foreach (var claims in new CurrentContextOrganization?[]
                 {
                     null,
                     Member(OrganizationUserType.Owner),
                     Member(OrganizationUserType.Admin),
                     Member(OrganizationUserType.User),
                     Member(OrganizationUserType.Custom),
                     Member(OrganizationUserType.Custom, new Permissions { ManageAccessRules = true })
                 })
        {
            await _sut.AuthorizeAsync(claims, () => IsProviderUserForOrg());
        }

        Assert.False(_providerConsulted);
    }
}
