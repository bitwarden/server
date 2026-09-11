using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Subscriptions.Organization.Requirements;
using Xunit;

namespace Bit.Subscriptions.Organization.Test.Requirements;

public class StandaloneOrganizationOwnerRequirementTests
{
    private readonly StandaloneOrganizationOwnerRequirement _sut = new();

    [Fact]
    public async Task AuthorizeAsync_OwnerOfStandaloneOrg_Authorizes()
        => Assert.True(await _sut.AuthorizeAsync(
            new CurrentContextOrganization { Type = OrganizationUserType.Owner },
            ProviderUserForOrg(false), ManagedByProvider(false)));

    [Fact]
    public async Task AuthorizeAsync_OwnerOfManagedOrg_Denies()
        => Assert.False(await _sut.AuthorizeAsync(
            new CurrentContextOrganization { Type = OrganizationUserType.Owner },
            ProviderUserForOrg(false), ManagedByProvider(true)));

    [Theory]
    [InlineData(OrganizationUserType.Admin)]
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public async Task AuthorizeAsync_NonOwnerMembership_Denies(OrganizationUserType type)
        => Assert.False(await _sut.AuthorizeAsync(
            new CurrentContextOrganization { Type = type },
            ProviderUserForOrg(true), ManagedByProvider(false)));

    [Fact]
    public async Task AuthorizeAsync_NoMembership_Denies()
        => Assert.False(await _sut.AuthorizeAsync(null, ProviderUserForOrg(true), ManagedByProvider(false)));

    [Fact]
    public async Task AuthorizeAsync_ProviderUserForManagedOrg_Denies()
        => Assert.False(await _sut.AuthorizeAsync(null, ProviderUserForOrg(true), ManagedByProvider(true)));

    private static Func<Task<bool>> ProviderUserForOrg(bool result) => () => Task.FromResult(result);
    private static Func<Task<bool>> ManagedByProvider(bool result) => () => Task.FromResult(result);
}