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
        Guid organizationId,
        Guid code,
        string email,
        SutProvider<ValidateOrganizationInviteLinkEmailDomainQuery> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(organizationId)
            .Returns((OrganizationInviteLink?)null);

        var result = await sutProvider.Sut.ValidateAsync(organizationId, code, email);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenCodeMismatch_ReturnsNotFoundError(
        OrganizationInviteLink link,
        SutProvider<ValidateOrganizationInviteLinkEmailDomainQuery> sutProvider)
    {
        link.SetAllowedDomains(["acme.com"]);

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(link.OrganizationId)
            .Returns(link);

        var result = await sutProvider.Sut.ValidateAsync(link.OrganizationId, Guid.NewGuid(), "user@acme.com");

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenEmailDomainMatches_ReturnsTrue(
        OrganizationInviteLink link,
        SutProvider<ValidateOrganizationInviteLinkEmailDomainQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        link.Code = code.ToString();
        link.SetAllowedDomains(["acme.com"]);

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(link.OrganizationId)
            .Returns(link);

        var result = await sutProvider.Sut.ValidateAsync(link.OrganizationId, code, "user@acme.com");

        Assert.True(result.IsSuccess);
        Assert.True(result.AsSuccess);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenEmailDomainDoesNotMatch_ReturnsFalse(
        OrganizationInviteLink link,
        SutProvider<ValidateOrganizationInviteLinkEmailDomainQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        link.Code = code.ToString();
        link.SetAllowedDomains(["acme.com"]);

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(link.OrganizationId)
            .Returns(link);

        var result = await sutProvider.Sut.ValidateAsync(link.OrganizationId, code, "user@other.com");

        Assert.True(result.IsSuccess);
        Assert.False(result.AsSuccess);
    }
}
