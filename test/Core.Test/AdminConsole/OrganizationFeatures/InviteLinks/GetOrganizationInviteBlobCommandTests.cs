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
public class GetOrganizationInviteBlobCommandTests
{
    [Theory, BitAutoData]
    public async Task GetInviteBlobAsync_WithLinkNotFound_ReturnsInviteLinkNotFound(
        GetOrganizationInviteBlobRequest request,
        SutProvider<GetOrganizationInviteBlobCommand> sutProvider)
    {
        // Act
        var result = await sutProvider.Sut.GetInviteBlobAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetInviteBlobAsync_WithCodeMismatch_ReturnsInviteLinkNotFound(
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<GetOrganizationInviteBlobCommand> sutProvider)
    {
        // Arrange
        inviteLink.Code = Guid.NewGuid().ToString();

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId)
            .Returns(inviteLink);

        // Act — pass a different code than the one stored on the link
        var request = new GetOrganizationInviteBlobRequest
        {
            OrganizationId = inviteLink.OrganizationId,
            Code = Guid.NewGuid(),
            User = user,
        };
        var result = await sutProvider.Sut.GetInviteBlobAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetInviteBlobAsync_WithOrganizationAbilityNotFound_ReturnsInviteLinkNotFound(
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<GetOrganizationInviteBlobCommand> sutProvider)
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
        var request = new GetOrganizationInviteBlobRequest
        {
            OrganizationId = inviteLink.OrganizationId,
            Code = Guid.Parse(inviteLink.Code),
            User = user,
        };
        var result = await sutProvider.Sut.GetInviteBlobAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetInviteBlobAsync_WithOrganizationDisabled_ReturnsInviteLinkNotFound(
        OrganizationInviteLink inviteLink,
        OrganizationAbility organizationAbility,
        User user,
        SutProvider<GetOrganizationInviteBlobCommand> sutProvider)
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
        var request = new GetOrganizationInviteBlobRequest
        {
            OrganizationId = inviteLink.OrganizationId,
            Code = Guid.Parse(inviteLink.Code),
            User = user,
        };
        var result = await sutProvider.Sut.GetInviteBlobAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetInviteBlobAsync_WhenOrganizationDoesNotUseInviteLinks_ReturnsInviteLinkNotAvailable(
        OrganizationInviteLink inviteLink,
        OrganizationAbility organizationAbility,
        User user,
        SutProvider<GetOrganizationInviteBlobCommand> sutProvider)
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
        var request = new GetOrganizationInviteBlobRequest
        {
            OrganizationId = inviteLink.OrganizationId,
            Code = Guid.Parse(inviteLink.Code),
            User = user,
        };
        var result = await sutProvider.Sut.GetInviteBlobAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotAvailable>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetInviteBlobAsync_WhenEmailDomainNotAllowed_ReturnsEmailDomainNotAllowed(
        OrganizationInviteLink inviteLink,
        OrganizationAbility organizationAbility,
        User user,
        SutProvider<GetOrganizationInviteBlobCommand> sutProvider)
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
        var request = new GetOrganizationInviteBlobRequest
        {
            OrganizationId = inviteLink.OrganizationId,
            Code = Guid.Parse(inviteLink.Code),
            User = user,
        };
        var result = await sutProvider.Sut.GetInviteBlobAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<EmailDomainNotAllowed>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetInviteBlobAsync_WithValidRequest_ReturnsInviteBlob(
        OrganizationInviteLink inviteLink,
        OrganizationAbility organizationAbility,
        User user,
        SutProvider<GetOrganizationInviteBlobCommand> sutProvider)
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
        var request = new GetOrganizationInviteBlobRequest
        {
            OrganizationId = inviteLink.OrganizationId,
            Code = Guid.Parse(inviteLink.Code),
            User = user,
        };
        var result = await sutProvider.Sut.GetInviteBlobAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(inviteLink.Invite, result.AsSuccess);
    }
}
