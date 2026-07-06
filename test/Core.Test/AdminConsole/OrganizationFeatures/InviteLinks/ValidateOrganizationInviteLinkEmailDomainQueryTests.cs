using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class ValidateOrganizationInviteLinkEmailDomainQueryTests
{
    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenLinkNotFound_ReturnsNotFoundError(
        Guid code,
        string email,
        SutProvider<ValidateOrganizationInviteLinkEmailDomainQuery> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByCodeAsync(code)
            .Returns((OrganizationInviteLink?)null);

        var result = await sutProvider.Sut.ValidateAsync(code, email);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenEmailDomainMatches_ReturnsAllowed(
        Guid code,
        Guid organizationId,
        SutProvider<ValidateOrganizationInviteLinkEmailDomainQuery> sutProvider)
    {
        var link = new OrganizationInviteLink { OrganizationId = organizationId };
        link.SetAllowedDomains(["acme.com"]);

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByCodeAsync(code)
            .Returns(link);

        var result = await sutProvider.Sut.ValidateAsync(code, "user@acme.com");

        Assert.True(result.IsSuccess);
        Assert.Equal(organizationId, result.AsSuccess.OrganizationId);
        Assert.True(result.AsSuccess.IsAllowed);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenEmailDomainDoesNotMatch_ReturnsNotAllowed(
        Guid code,
        Guid organizationId,
        SutProvider<ValidateOrganizationInviteLinkEmailDomainQuery> sutProvider)
    {
        var link = new OrganizationInviteLink { OrganizationId = organizationId };
        link.SetAllowedDomains(["acme.com"]);

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByCodeAsync(code)
            .Returns(link);

        var result = await sutProvider.Sut.ValidateAsync(code, "user@other.com");

        Assert.True(result.IsSuccess);
        Assert.Equal(organizationId, result.AsSuccess.OrganizationId);
        Assert.False(result.AsSuccess.IsAllowed);
    }
}
