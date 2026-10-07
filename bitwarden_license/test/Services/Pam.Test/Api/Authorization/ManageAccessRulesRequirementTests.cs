using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Services.Pam.Api.Authorization;
using Xunit;

namespace Bit.Services.Pam.Test.Api.Authorization;

public class ManageAccessRulesRequirementTests
{
    private readonly ManageAccessRulesRequirement _sut = new();

    /// <summary>The provider callback costs a database query and cannot change the outcome.</summary>
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
        // A provider user arrives with no organization claims.
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
