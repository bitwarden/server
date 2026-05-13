using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class GetOrganizationInviteLinkQueryTests
{
    [Theory, BitAutoData]
    public async Task GetAsync_WithValidInput_Success(
        Guid organizationId,
        OrganizationInviteLink link,
        SutProvider<GetOrganizationInviteLinkQuery> sutProvider)
    {
        SetupAbility(sutProvider, organizationId);
        link.OrganizationId = organizationId;

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(organizationId)
            .Returns(link);

        var result = await sutProvider.Sut.GetAsync(organizationId);

        Assert.True(result.IsSuccess);
        Assert.Same(link, result.AsSuccess);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_WhenNoLinkExists_ReturnsNotFoundError(
        Guid organizationId,
        SutProvider<GetOrganizationInviteLinkQuery> sutProvider)
    {
        SetupAbility(sutProvider, organizationId);

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(organizationId)
            .Returns((OrganizationInviteLink?)null);

        var result = await sutProvider.Sut.GetAsync(organizationId);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_WithoutUseInviteLinksAbility_ReturnsBadRequestError(
        Guid organizationId,
        SutProvider<GetOrganizationInviteLinkQuery> sutProvider)
    {
        SetupAbility(sutProvider, organizationId, useInviteLinks: false);

        var result = await sutProvider.Sut.GetAsync(organizationId);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotAvailable>(result.AsError);

        await sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByOrganizationIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_WithNullAbility_ReturnsBadRequestError(
        Guid organizationId,
        SutProvider<GetOrganizationInviteLinkQuery> sutProvider)
    {
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns((OrganizationAbility?)null);

        var result = await sutProvider.Sut.GetAsync(organizationId);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotAvailable>(result.AsError);

        await sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByOrganizationIdAsync(default);
    }

    private static void SetupAbility(
        SutProvider<GetOrganizationInviteLinkQuery> sutProvider,
        Guid organizationId,
        bool useInviteLinks = true)
    {
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { UseInviteLinks = useInviteLinks });
    }
}
