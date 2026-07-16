using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Models.Data.Organizations;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class UpdateInviteSupportConfirmCommandTests
{
    private static SutProvider<UpdateInviteSupportConfirmCommand> GetSutProvider() =>
        new SutProvider<UpdateInviteSupportConfirmCommand>()
            .WithFakeTimeProvider()
            .Create();

    [Theory, BitAutoData]
    public async Task UpdateAsync_WithValidInput_UpdatesOnlyInviteAndSupportsConfirmation(Guid organizationId)
    {
        // Arrange
        var sutProvider = GetSutProvider();
        var now = new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(now);

        SetupAbility(sutProvider, organizationId);

        var originalCreationDate = now.AddDays(-5);
        var existingLink = new OrganizationInviteLink
        {
            Id = Guid.NewGuid(),
            Code = Guid.NewGuid(),
            OrganizationId = organizationId,
            Invite = "old-invite-blob",
            SupportsConfirmation = false,
            CreationDate = originalCreationDate,
            RevisionDate = originalCreationDate,
        };
        existingLink.SetAllowedDomains(["acme.com"]);
        var originalAllowedDomains = existingLink.AllowedDomains;

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(organizationId)
            .Returns(existingLink);

        var request = CreateRequest(organizationId, "new-invite-blob", supportsConfirmation: true);

        // Act
        var result = await sutProvider.Sut.UpdateAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        var link = result.AsSuccess;
        Assert.Same(existingLink, link);
        Assert.Equal("new-invite-blob", link.Invite);
        Assert.True(link.SupportsConfirmation);
        Assert.Equal(now, link.RevisionDate);
        Assert.Equal(originalCreationDate, link.CreationDate);
        Assert.Equal(originalAllowedDomains, link.AllowedDomains);

        await sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .Received(1)
            .ReplaceAsync(existingLink);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WithNoExistingLink_ReturnsNotFoundError(Guid organizationId)
    {
        // Arrange
        var sutProvider = GetSutProvider();
        SetupAbility(sutProvider, organizationId);
        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(organizationId)
            .Returns((OrganizationInviteLink?)null);

        var request = CreateRequest(organizationId, "new-invite-blob", supportsConfirmation: true);

        // Act
        var result = await sutProvider.Sut.UpdateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);

        await sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WithoutUseInviteLinksAbility_ReturnsNotAvailableError(Guid organizationId)
    {
        // Arrange
        var sutProvider = GetSutProvider();
        SetupAbility(sutProvider, organizationId, useInviteLinks: false);

        var request = CreateRequest(organizationId, "new-invite-blob", supportsConfirmation: true);

        // Act
        var result = await sutProvider.Sut.UpdateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotAvailable>(result.AsError);

        await sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByOrganizationIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WithNullAbility_ReturnsNotAvailableError(Guid organizationId)
    {
        // Arrange
        var sutProvider = GetSutProvider();
        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns((OrganizationAbility?)null);

        var request = CreateRequest(organizationId, "new-invite-blob", supportsConfirmation: true);

        // Act
        var result = await sutProvider.Sut.UpdateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotAvailable>(result.AsError);
    }

    private static void SetupAbility(
        SutProvider<UpdateInviteSupportConfirmCommand> sutProvider,
        Guid organizationId,
        bool useInviteLinks = true)
    {
        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { UseInviteLinks = useInviteLinks });
    }

    private static UpdateInviteSupportConfirmRequest CreateRequest(
        Guid organizationId,
        string invite,
        bool supportsConfirmation) => new()
        {
            OrganizationId = organizationId,
            Invite = invite,
            SupportsConfirmation = supportsConfirmation,
        };
}
