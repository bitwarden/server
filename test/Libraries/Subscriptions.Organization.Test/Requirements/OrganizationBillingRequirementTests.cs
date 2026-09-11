using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Subscriptions.Organization.Requirements;
using Xunit;

namespace Bit.Subscriptions.Organization.Test.Requirements;

public class OrganizationBillingRequirementTests
{
    private readonly OrganizationBillingRequirement _sut = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuthorizeAsync_OwnerMembership_Authorizes(bool managedByProvider)
        => Assert.True(await _sut.AuthorizeAsync(
            new CurrentContextOrganization { Type = OrganizationUserType.Owner },
            ProviderUserForOrg(false), ManagedByProvider(managedByProvider)));

    [Theory]
    [InlineData(OrganizationUserType.Admin)]
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public async Task AuthorizeAsync_NonOwnerMembership_ConfirmedProvider_Authorizes(OrganizationUserType type)
        => Assert.True(await _sut.AuthorizeAsync(
            new CurrentContextOrganization { Type = type },
            ProviderUserForOrg(true), ManagedByProvider(false)));

    [Theory]
    [InlineData(OrganizationUserType.Admin)]
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public async Task AuthorizeAsync_NonOwnerMembership_NotProvider_Denies(OrganizationUserType type)
        => Assert.False(await _sut.AuthorizeAsync(
            new CurrentContextOrganization { Type = type },
            ProviderUserForOrg(false), ManagedByProvider(false)));

    [Fact]
    public async Task AuthorizeAsync_NoMembership_ConfirmedProvider_Authorizes()
        => Assert.True(await _sut.AuthorizeAsync(null, ProviderUserForOrg(true), ManagedByProvider(false)));

    [Fact]
    public async Task AuthorizeAsync_NoMembership_NotProvider_Denies()
        => Assert.False(await _sut.AuthorizeAsync(null, ProviderUserForOrg(false), ManagedByProvider(false)));

    private static Func<Task<bool>> ProviderUserForOrg(bool result) => () => Task.FromResult(result);
    private static Func<Task<bool>> ManagedByProvider(bool result) => () => Task.FromResult(result);
}
