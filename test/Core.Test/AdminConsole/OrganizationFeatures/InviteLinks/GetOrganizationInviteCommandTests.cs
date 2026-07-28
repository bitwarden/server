using System.Text.Json;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class GetOrganizationInviteCommandTests
{
    [Theory, BitAutoData]
    public async Task GetInviteAsync_WithLinkNotFound_ReturnsInviteLinkNotFound(
        GetOrganizationInviteRequest request,
        SutProvider<GetOrganizationInviteCommand> sutProvider)
    {
        // Act
        var result = await sutProvider.Sut.GetInviteAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetInviteAsync_WithCodeMismatch_ReturnsInviteLinkNotFound(
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<GetOrganizationInviteCommand> sutProvider)
    {
        // Arrange
        inviteLink.Code = Guid.NewGuid().ToString();

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId)
            .Returns(inviteLink);

        // Act — pass a different code than the one stored on the link
        var request = new GetOrganizationInviteRequest
        {
            OrganizationId = inviteLink.OrganizationId,
            Code = Guid.NewGuid(),
            User = user,
        };
        var result = await sutProvider.Sut.GetInviteAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetInviteAsync_WithOrganizationAbilityNotFound_ReturnsInviteLinkNotFound(
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<GetOrganizationInviteCommand> sutProvider)
    {
        // Arrange
        inviteLink.Code = Guid.NewGuid().ToString();

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId)
            .Returns(inviteLink);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(inviteLink.OrganizationId)
            .Returns((OrganizationAbility?)null);

        // Act
        var request = new GetOrganizationInviteRequest
        {
            OrganizationId = inviteLink.OrganizationId,
            Code = Guid.Parse(inviteLink.Code),
            User = user,
        };
        var result = await sutProvider.Sut.GetInviteAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetInviteAsync_WithOrganizationDisabled_ReturnsInviteLinkNotFound(
        OrganizationInviteLink inviteLink,
        OrganizationAbility organizationAbility,
        User user,
        SutProvider<GetOrganizationInviteCommand> sutProvider)
    {
        // Arrange
        inviteLink.Code = Guid.NewGuid().ToString();
        organizationAbility.Enabled = false;

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId)
            .Returns(inviteLink);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(inviteLink.OrganizationId)
            .Returns(organizationAbility);

        // Act
        var request = new GetOrganizationInviteRequest
        {
            OrganizationId = inviteLink.OrganizationId,
            Code = Guid.Parse(inviteLink.Code),
            User = user,
        };
        var result = await sutProvider.Sut.GetInviteAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetInviteAsync_WhenOrganizationDoesNotUseInviteLinks_ReturnsInviteLinkNotAvailable(
        OrganizationInviteLink inviteLink,
        OrganizationAbility organizationAbility,
        User user,
        SutProvider<GetOrganizationInviteCommand> sutProvider)
    {
        // Arrange
        inviteLink.Code = Guid.NewGuid().ToString();
        organizationAbility.Enabled = true;
        organizationAbility.UseInviteLinks = false;

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId)
            .Returns(inviteLink);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(inviteLink.OrganizationId)
            .Returns(organizationAbility);

        // Act
        var request = new GetOrganizationInviteRequest
        {
            OrganizationId = inviteLink.OrganizationId,
            Code = Guid.Parse(inviteLink.Code),
            User = user,
        };
        var result = await sutProvider.Sut.GetInviteAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotAvailable>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetInviteAsync_WhenEmailDomainNotAllowed_ReturnsEmailDomainNotAllowed(
        OrganizationInviteLink inviteLink,
        OrganizationAbility organizationAbility,
        User user,
        SutProvider<GetOrganizationInviteCommand> sutProvider)
    {
        // Arrange
        inviteLink.Code = Guid.NewGuid().ToString();
        inviteLink.AllowedDomains = JsonSerializer.Serialize(new[] { "allowed.example" });
        organizationAbility.Enabled = true;
        organizationAbility.UseInviteLinks = true;
        user.Email = "user@notallowed.example";

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId)
            .Returns(inviteLink);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(inviteLink.OrganizationId)
            .Returns(organizationAbility);

        // Act
        var request = new GetOrganizationInviteRequest
        {
            OrganizationId = inviteLink.OrganizationId,
            Code = Guid.Parse(inviteLink.Code),
            User = user,
        };
        var result = await sutProvider.Sut.GetInviteAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<EmailDomainNotAllowed>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetInviteAsync_WithValidRequest_ReturnsInvite(
        OrganizationInviteLink inviteLink,
        OrganizationAbility organizationAbility,
        User user,
        SutProvider<GetOrganizationInviteCommand> sutProvider)
    {
        // Arrange
        inviteLink.Code = Guid.NewGuid().ToString();
        inviteLink.AllowedDomains = JsonSerializer.Serialize(new[] { "allowed.example" });
        organizationAbility.Enabled = true;
        organizationAbility.UseInviteLinks = true;
        user.Email = "user@allowed.example";

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId)
            .Returns(inviteLink);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(inviteLink.OrganizationId)
            .Returns(organizationAbility);

        // Act
        var request = new GetOrganizationInviteRequest
        {
            OrganizationId = inviteLink.OrganizationId,
            Code = Guid.Parse(inviteLink.Code),
            User = user,
        };
        var result = await sutProvider.Sut.GetInviteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(inviteLink.Invite, result.AsSuccess);
    }
}
