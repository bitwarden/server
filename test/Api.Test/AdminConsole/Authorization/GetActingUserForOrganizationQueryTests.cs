using Bit.Api.AdminConsole.Authorization;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization;

public class GetActingUserForOrganizationQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task GetActingUserAsync_OrganizationOwner_ReturnsStandardUserAsOwner(Guid userId, Guid organizationId)
    {
        var currentContext = Substitute.For<ICurrentContext>();
        var sut = new GetActingUserForOrganizationQuery(currentContext);
        currentContext.GetOrganization(organizationId).Returns(new CurrentContextOrganization
        {
            Id = organizationId,
            Type = OrganizationUserType.Owner,
        });

        var result = await sut.GetActingUserAsync(userId, organizationId);

        var standardUser = Assert.IsType<StandardUser>(result);
        Assert.Equal(userId, standardUser.UserId);
        Assert.Equal(OrganizationUserType.Owner, standardUser.OrganizationUserType);
        Assert.True(standardUser.IsOrganizationOwnerOrProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task GetActingUserAsync_OrganizationAdmin_ReturnsStandardUserNotOwner(Guid userId, Guid organizationId)
    {
        var currentContext = Substitute.For<ICurrentContext>();
        var sut = new GetActingUserForOrganizationQuery(currentContext);
        currentContext.GetOrganization(organizationId).Returns(new CurrentContextOrganization
        {
            Id = organizationId,
            Type = OrganizationUserType.Admin,
        });

        var result = await sut.GetActingUserAsync(userId, organizationId);

        var standardUser = Assert.IsType<StandardUser>(result);
        Assert.Equal(OrganizationUserType.Admin, standardUser.OrganizationUserType);
        Assert.False(standardUser.IsOrganizationOwnerOrProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task GetActingUserAsync_OrganizationCustom_ReturnsStandardUserWithPermissions(
        Guid userId, Guid organizationId, Permissions permissions)
    {
        var currentContext = Substitute.For<ICurrentContext>();
        var sut = new GetActingUserForOrganizationQuery(currentContext);
        currentContext.GetOrganization(organizationId).Returns(new CurrentContextOrganization
        {
            Id = organizationId,
            Type = OrganizationUserType.Custom,
            Permissions = permissions,
        });

        var result = await sut.GetActingUserAsync(userId, organizationId);

        var standardUser = Assert.IsType<StandardUser>(result);
        Assert.Equal(OrganizationUserType.Custom, standardUser.OrganizationUserType);
        Assert.Same(permissions, standardUser.Permissions);
    }

    [Theory]
    [BitAutoData]
    public async Task GetActingUserAsync_ProviderAdmin_ReturnsProviderUser(
        Guid userId, Guid organizationId, Guid providerId)
    {
        var currentContext = Substitute.For<ICurrentContext>();
        var sut = new GetActingUserForOrganizationQuery(currentContext);
        currentContext.GetOrganization(organizationId).Returns((CurrentContextOrganization?)null);
        currentContext.ProviderIdForOrg(organizationId).Returns(providerId);
        currentContext.ProviderProviderAdmin(providerId).Returns(true);

        var result = await sut.GetActingUserAsync(userId, organizationId);

        var providerUser = Assert.IsType<ProviderUser>(result);
        Assert.Equal(userId, providerUser.UserId);
        Assert.Equal(providerId, providerUser.ProviderId);
        Assert.Equal(ProviderUserType.ProviderAdmin, providerUser.ProviderUserType);
        Assert.True(providerUser.IsOrganizationOwnerOrProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task GetActingUserAsync_ProviderServiceUser_ReturnsProviderUser(
        Guid userId, Guid organizationId, Guid providerId)
    {
        var currentContext = Substitute.For<ICurrentContext>();
        var sut = new GetActingUserForOrganizationQuery(currentContext);
        currentContext.GetOrganization(organizationId).Returns((CurrentContextOrganization?)null);
        currentContext.ProviderIdForOrg(organizationId).Returns(providerId);
        currentContext.ProviderProviderAdmin(providerId).Returns(false);

        var result = await sut.GetActingUserAsync(userId, organizationId);

        var providerUser = Assert.IsType<ProviderUser>(result);
        Assert.Equal(ProviderUserType.ServiceUser, providerUser.ProviderUserType);
    }

    [Theory]
    [BitAutoData]
    public async Task GetActingUserAsync_NeitherMemberNorProvider_Throws(Guid userId, Guid organizationId)
    {
        var currentContext = Substitute.For<ICurrentContext>();
        var sut = new GetActingUserForOrganizationQuery(currentContext);
        currentContext.GetOrganization(organizationId).Returns((CurrentContextOrganization?)null);
        currentContext.ProviderIdForOrg(organizationId).Returns((Guid?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetActingUserAsync(userId, organizationId));
    }
}
