using Bit.Api.AdminConsole.Authorization.Requirements;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization.Requirements;

[SutProviderCustomize]
public class OwnerOrProviderRequirementTests
{
    [Theory]
    [CurrentContextOrganizationCustomize]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task AuthorizeAsync_WhenUserIsOwner_ThenRequestShouldBeAuthorized(
        OrganizationUserType type,
        CurrentContextOrganization organization,
        SutProvider<OwnerOrProviderRequirement> sutProvider)
    {
        organization.Type = type;

        var actual = await sutProvider.Sut.AuthorizeAsync(organization, () => Task.FromResult(false));

        Assert.True(actual);
    }

    [Theory]
    [CurrentContextOrganizationCustomize]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task AuthorizeAsync_WhenUserIsNotOwnerAndNotProviderUser_ThenRequestShouldBeDenied(
        OrganizationUserType type,
        CurrentContextOrganization organization,
        SutProvider<OwnerOrProviderRequirement> sutProvider)
    {
        organization.Type = type;

        var actual = await sutProvider.Sut.AuthorizeAsync(organization, IsNotProviderUserForOrg);

        Assert.False(actual);
        return;

        Task<bool> IsNotProviderUserForOrg() => Task.FromResult(false);
    }

    [Theory]
    [CurrentContextOrganizationCustomize]
    [BitAutoData]
    public async Task AuthorizeAsync_WhenProviderUserForAnOrganization_ThenRequestShouldBeAuthorized(
        CurrentContextOrganization organization,
        SutProvider<OwnerOrProviderRequirement> sutProvider)
    {
        var actual = await sutProvider.Sut.AuthorizeAsync(organization, IsProviderUserForOrg);

        Assert.True(actual);
        return;

        Task<bool> IsProviderUserForOrg() => Task.FromResult(true);
    }

    [Theory]
    [BitAutoData]
    public async Task AuthorizeAsync_WhenNoOrganizationClaimsAndNotProviderUser_ThenRequestShouldBeDenied(
        SutProvider<OwnerOrProviderRequirement> sutProvider)
    {
        var actual = await sutProvider.Sut.AuthorizeAsync(null, IsNotProviderUserForOrg);

        Assert.False(actual);
        return;

        Task<bool> IsNotProviderUserForOrg() => Task.FromResult(false);
    }

    [Theory]
    [BitAutoData]
    public async Task AuthorizeAsync_WhenNoOrganizationClaimsAndIsProviderUser_ThenRequestShouldBeAuthorized(
        SutProvider<OwnerOrProviderRequirement> sutProvider)
    {
        var actual = await sutProvider.Sut.AuthorizeAsync(null, IsProviderUserForOrg);

        Assert.True(actual);
        return;

        Task<bool> IsProviderUserForOrg() => Task.FromResult(true);
    }
}
