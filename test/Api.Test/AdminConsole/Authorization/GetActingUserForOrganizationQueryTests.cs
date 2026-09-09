using Bit.Api.AdminConsole.Authorization;
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
    public async Task GetActingUserAsync_ManagingProvider_ReturnsStandardUserFlaggedAsProvider(
        Guid userId, Guid organizationId, Guid providerId)
    {
        var currentContext = Substitute.For<ICurrentContext>();
        var sut = new GetActingUserForOrganizationQuery(currentContext);
        currentContext.GetOrganization(organizationId).Returns((CurrentContextOrganization?)null);
        currentContext.ProviderIdForOrg(organizationId).Returns(providerId);

        var result = await sut.GetActingUserAsync(userId, organizationId);

        var standardUser = Assert.IsType<StandardUser>(result);
        Assert.Equal(userId, standardUser.UserId);
        Assert.True(standardUser.IsProvider);
        Assert.Null(standardUser.OrganizationUserType);
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
