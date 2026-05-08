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
public class RefreshOrganizationInviteLinkCommandTests
{
    [Theory, BitAutoData]
    public async Task RefreshAsync_WithValidInput_Success(
        Guid organizationId,
        OrganizationInviteLink existingLink,
        SutProvider<RefreshOrganizationInviteLinkCommand> sutProvider)
    {
        existingLink.OrganizationId = organizationId;
        existingLink.SetAllowedDomains(["acme.com", "example.com"]);

        SetupAbility(sutProvider, organizationId);
        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(organizationId)
            .Returns(existingLink);

        var request = new RefreshOrganizationInviteLinkRequest
        {
            OrganizationId = organizationId,
            EncryptedInviteKey = "new-encrypted-key-value",
        };

        var result = await sutProvider.Sut.RefreshAsync(request);

        Assert.True(result.IsSuccess);
        var link = result.AsSuccess;
        Assert.Equal(organizationId, link.OrganizationId);
        Assert.NotEqual(Guid.Empty, link.Id);
        Assert.NotEqual(existingLink.Id, link.Id);
        Assert.NotEqual(existingLink.Code, link.Code);
        Assert.Equal(request.EncryptedInviteKey, link.EncryptedInviteKey);
        Assert.Equal(existingLink.AllowedDomains, link.AllowedDomains);
        Assert.Contains("acme.com", link.GetAllowedDomains());
        Assert.Contains("example.com", link.GetAllowedDomains());

        await sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .Received(1)
            .RefreshAsync(existingLink, link);
    }

    [Theory, BitAutoData]
    public async Task RefreshAsync_WithNoExistingLink_ReturnsNotFoundError(
        Guid organizationId,
        SutProvider<RefreshOrganizationInviteLinkCommand> sutProvider)
    {
        SetupAbility(sutProvider, organizationId);
        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(organizationId)
            .Returns((OrganizationInviteLink?)null);

        var request = new RefreshOrganizationInviteLinkRequest
        {
            OrganizationId = organizationId,
            EncryptedInviteKey = "some-encrypted-key",
        };

        var result = await sutProvider.Sut.RefreshAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);

        await sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .DidNotReceiveWithAnyArgs()
            .RefreshAsync(default!, default!);
    }

    [Theory, BitAutoData]
    public async Task RefreshAsync_WithoutUseInviteLinksAbility_ReturnsBadRequestError(
        Guid organizationId,
        SutProvider<RefreshOrganizationInviteLinkCommand> sutProvider)
    {
        SetupAbility(sutProvider, organizationId, useInviteLinks: false);

        var request = new RefreshOrganizationInviteLinkRequest
        {
            OrganizationId = organizationId,
            EncryptedInviteKey = "some-encrypted-key",
        };

        var result = await sutProvider.Sut.RefreshAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotAvailable>(result.AsError);

        await sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .DidNotReceiveWithAnyArgs()
            .RefreshAsync(default!, default!);
    }

    private static void SetupAbility(
        SutProvider<RefreshOrganizationInviteLinkCommand> sutProvider,
        Guid organizationId,
        bool useInviteLinks = true)
    {
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { UseInviteLinks = useInviteLinks });
    }
}
