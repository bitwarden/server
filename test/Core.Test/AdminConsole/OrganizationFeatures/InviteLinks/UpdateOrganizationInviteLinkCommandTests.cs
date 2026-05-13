using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class UpdateOrganizationInviteLinkCommandTests
{
    [Theory, BitAutoData]
    public async Task UpdateAsync_WithValidInput_Success(
        Guid organizationId,
        SutProvider<UpdateOrganizationInviteLinkCommand> sutProvider)
    {
        SetupAbility(sutProvider, organizationId);

        var originalCreationDate = DateTime.UtcNow.AddDays(-5);
        var existingLink = new OrganizationInviteLink
        {
            Id = Guid.NewGuid(),
            Code = Guid.NewGuid(),
            OrganizationId = organizationId,
            EncryptedInviteKey = "encrypted-key",
            EncryptedOrgKey = "encrypted-org-key",
            CreationDate = originalCreationDate,
            RevisionDate = originalCreationDate,
        };
        existingLink.SetAllowedDomains(["old.com"]);

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(organizationId)
            .Returns(existingLink);

        var request = CreateRequest(organizationId, [" New.com ", "Example.COM", " "]);

        var result = await sutProvider.Sut.UpdateAsync(request);

        Assert.True(result.IsSuccess);
        var link = result.AsSuccess;
        Assert.Same(existingLink, link);
        Assert.Equal(existingLink.Id, link.Id);
        Assert.Equal(existingLink.Code, link.Code);
        Assert.Equal("encrypted-key", link.EncryptedInviteKey);
        Assert.Equal("encrypted-org-key", link.EncryptedOrgKey);
        Assert.Equal(originalCreationDate, link.CreationDate);
        Assert.True(link.RevisionDate > originalCreationDate);

        var deserializedDomains = JsonSerializer.Deserialize<List<string>>(link.AllowedDomains);
        Assert.NotNull(deserializedDomains);
        Assert.Equal(["new.com", "example.com"], deserializedDomains);

        await sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .Received(1)
            .ReplaceAsync(existingLink);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WithNoExistingLink_ReturnsNotFoundError(
        Guid organizationId,
        SutProvider<UpdateOrganizationInviteLinkCommand> sutProvider)
    {
        SetupAbility(sutProvider, organizationId);
        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(organizationId)
            .Returns((OrganizationInviteLink?)null);

        var request = CreateRequest(organizationId, ["acme.com"]);

        var result = await sutProvider.Sut.UpdateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);

        await sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WithEmptyDomainsList_ReturnsBadRequestError(
        Guid organizationId,
        SutProvider<UpdateOrganizationInviteLinkCommand> sutProvider)
    {
        SetupAbility(sutProvider, organizationId);

        var request = CreateRequest(organizationId, []);

        var result = await sutProvider.Sut.UpdateAsync(request);

        AssertDomainsRequired(result);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WithWhitespaceOnlyDomains_ReturnsBadRequestError(
        Guid organizationId,
        SutProvider<UpdateOrganizationInviteLinkCommand> sutProvider)
    {
        SetupAbility(sutProvider, organizationId);

        var request = CreateRequest(organizationId, [" ", ""]);

        var result = await sutProvider.Sut.UpdateAsync(request);

        AssertDomainsRequired(result);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WithNullDomains_ReturnsBadRequestError(
        Guid organizationId,
        SutProvider<UpdateOrganizationInviteLinkCommand> sutProvider)
    {
        SetupAbility(sutProvider, organizationId);

        var request = CreateRequest(organizationId, null);

        var result = await sutProvider.Sut.UpdateAsync(request);

        AssertDomainsRequired(result);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WithoutUseInviteLinksAbility_ReturnsBadRequestError(
        Guid organizationId,
        SutProvider<UpdateOrganizationInviteLinkCommand> sutProvider)
    {
        SetupAbility(sutProvider, organizationId, useInviteLinks: false);

        var request = CreateRequest(organizationId, ["acme.com"]);

        var result = await sutProvider.Sut.UpdateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotAvailable>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WithNullAbility_ReturnsBadRequestError(
        Guid organizationId,
        SutProvider<UpdateOrganizationInviteLinkCommand> sutProvider)
    {
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns((OrganizationAbility?)null);

        var request = CreateRequest(organizationId, ["acme.com"]);

        var result = await sutProvider.Sut.UpdateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotAvailable>(result.AsError);
    }

    private static void SetupAbility(
        SutProvider<UpdateOrganizationInviteLinkCommand> sutProvider,
        Guid organizationId,
        bool useInviteLinks = true)
    {
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { UseInviteLinks = useInviteLinks });
    }

    private static UpdateOrganizationInviteLinkRequest CreateRequest(
        Guid organizationId,
        IEnumerable<string>? allowedDomains) => new()
        {
            OrganizationId = organizationId,
            AllowedDomains = allowedDomains!,
        };

    private static void AssertDomainsRequired(CommandResult<OrganizationInviteLink> result)
    {
        Assert.True(result.IsError);
        Assert.IsType<InviteLinkDomainsRequired>(result.AsError);
    }
}
