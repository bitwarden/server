using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Services.Pam.Api.Authorization;
using Xunit;

namespace Bit.Services.Pam.Test.Api.Authorization;

/// <summary>
/// Access rules gate who can lease credentials out of an organization, so authority over them is narrower than the
/// usual custom-permission requirement: Owners, Admins, and Custom users holding ManageAccessRules, and nobody else.
/// In particular a provider managing the organization is not authorized, which is why this requirement implements
/// <c>IOrganizationRequirement</c> directly instead of deriving from <c>BasePermissionRequirement</c>.
/// </summary>
public class ManageAccessRulesRequirementTests
{
    private readonly ManageAccessRulesRequirement _sut = new();

    /// <summary>
    /// Records whether the requirement consulted provider status. It should never need to: the callback costs a
    /// database query, and a provider has no authority over rule authorship either way.
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
    public async Task AuthorizeAsync_AuthorizesCustomUserWithManageAccessRules()
    {
        var claims = Member(OrganizationUserType.Custom, new Permissions { ManageAccessRules = true });

        Assert.True(await _sut.AuthorizeAsync(claims, () => IsProviderUserForOrg()));
    }

    [Fact]
    public async Task AuthorizeAsync_DoesNotAuthorizeCustomUserWithoutManageAccessRules()
    {
        // Holding every other permission is not authority over rule authorship.
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
            ManageAccessRules = false
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
