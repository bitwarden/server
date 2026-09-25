using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Services;
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

        var ability = SetupAbility(sutProvider, organizationId);

        var originalCreationDate = now.AddDays(-5);
        var existingLink = new OrganizationInviteLink
        {
            Id = Guid.NewGuid(),
            Code = Guid.NewGuid().ToString(),
            OrganizationId = organizationId,
            Invite = "old-invite",
            SupportsConfirmation = false,
            CreationDate = originalCreationDate,
            RevisionDate = originalCreationDate,
        };
        existingLink.SetAllowedDomains(["acme.com"]);
        var originalAllowedDomains = existingLink.AllowedDomains;

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(organizationId)
            .Returns(existingLink);

        var request = CreateRequest(organizationId, "new-invite", supportsConfirmation: true);

        // Act
        var result = await sutProvider.Sut.UpdateAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        var link = result.AsSuccess;
        Assert.Same(existingLink, link);
        Assert.Equal("new-invite", link.Invite);
        Assert.True(link.SupportsConfirmation);
        Assert.Equal(now, link.RevisionDate);
        Assert.Equal(originalCreationDate, link.CreationDate);
        Assert.Equal(originalAllowedDomains, link.AllowedDomains);

        await sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .Received(1)
            .ReplaceAsync(existingLink);

        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationEventAsync(ability, EventType.Organization_InviteLinkConfirmEnabled);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenConfirmationSupportTurnedOff_LogsConfirmDisabledEvent(Guid organizationId)
    {
        // Arrange
        var sutProvider = GetSutProvider();
        var ability = SetupAbility(sutProvider, organizationId);
        SetupExistingLink(sutProvider, organizationId, supportsConfirmation: true);

        // Act
        var result = await sutProvider.Sut.UpdateAsync(
            CreateRequest(organizationId, "new-invite", supportsConfirmation: false));

        // Assert
        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationEventAsync(ability, EventType.Organization_InviteLinkConfirmDisabled);
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

        var request = CreateRequest(organizationId, "new-invite", supportsConfirmation: true);

        // Act
        var result = await sutProvider.Sut.UpdateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);

        await sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default!);

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationEventAsync(Arg.Any<OrganizationAbility>(), Arg.Any<EventType>());
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WithoutUseInviteLinksAbility_ReturnsNotAvailableError(Guid organizationId)
    {
        // Arrange
        var sutProvider = GetSutProvider();
        SetupAbility(sutProvider, organizationId, useInviteLinks: false);

        var request = CreateRequest(organizationId, "new-invite", supportsConfirmation: true);

        // Act
        var result = await sutProvider.Sut.UpdateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotAvailable>(result.AsError);

        await sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByOrganizationIdAsync(default);

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationEventAsync(Arg.Any<OrganizationAbility>(), Arg.Any<EventType>());
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WithNullAbility_ReturnsNotAvailableError(Guid organizationId)
    {
        // Arrange
        var sutProvider = GetSutProvider();
        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns((OrganizationAbility?)null);

        var request = CreateRequest(organizationId, "new-invite", supportsConfirmation: true);

        // Act
        var result = await sutProvider.Sut.UpdateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotAvailable>(result.AsError);
    }

    private static OrganizationAbility SetupAbility(
        SutProvider<UpdateInviteSupportConfirmCommand> sutProvider,
        Guid organizationId,
        bool useInviteLinks = true)
    {
        var ability = new OrganizationAbility
        {
            Id = organizationId,
            Enabled = true,
            UseEvents = true,
            UseInviteLinks = useInviteLinks,
        };

        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns(ability);

        return ability;
    }

    private static void SetupExistingLink(
        SutProvider<UpdateInviteSupportConfirmCommand> sutProvider,
        Guid organizationId,
        bool supportsConfirmation)
    {
        var existingLink = new OrganizationInviteLink
        {
            Id = Guid.NewGuid(),
            Code = Guid.NewGuid().ToString(),
            OrganizationId = organizationId,
            Invite = "old-invite",
            SupportsConfirmation = supportsConfirmation,
        };
        existingLink.SetAllowedDomains(["acme.com"]);

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(organizationId)
            .Returns(existingLink);
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
