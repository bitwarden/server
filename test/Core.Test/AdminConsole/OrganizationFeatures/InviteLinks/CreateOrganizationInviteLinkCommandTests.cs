using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class CreateOrganizationInviteLinkCommandTests
{
    [Theory, BitAutoData]
    public async Task CreateAsync_WithValidInput_Success(
        Organization organization,
        SutProvider<CreateOrganizationInviteLinkCommand> sutProvider)
    {
        organization.Enabled = true;
        organization.UseEvents = true;

        SetupAbility(sutProvider, organization.Id);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var request = new CreateOrganizationInviteLinkRequest
        {
            OrganizationId = organization.Id,
            AllowedDomains = ["acme.com", "example.com"],
            EncryptedInviteKey = "encrypted-key-value",
        };

        var result = await sutProvider.Sut.CreateAsync(request);

        Assert.True(result.IsSuccess);
        var link = result.AsSuccess;
        Assert.Equal(organization.Id, link.OrganizationId);
        Assert.NotEqual(Guid.Empty, link.Id);
        Assert.NotEqual(Guid.Empty, link.Code);
        Assert.Equal(request.EncryptedInviteKey, link.EncryptedInviteKey);

        var deserializedDomains = JsonSerializer.Deserialize<List<string>>(link.AllowedDomains);
        Assert.NotNull(deserializedDomains);
        Assert.Equal(2, deserializedDomains.Count);
        Assert.Contains("acme.com", deserializedDomains);
        Assert.Contains("example.com", deserializedDomains);

        await sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .Received(1)
            .CreateAsync(link);

        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationEventAsync(organization, EventType.Organization_InviteLinkCreated);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_WithExistingLinkForOrg_ReturnsConflictError(
        Guid organizationId,
        OrganizationInviteLink existingLink,
        SutProvider<CreateOrganizationInviteLinkCommand> sutProvider)
    {
        SetupAbility(sutProvider, organizationId);

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(organizationId)
            .Returns(existingLink);

        var request = new CreateOrganizationInviteLinkRequest
        {
            OrganizationId = organizationId,
            AllowedDomains = ["acme.com"],
            EncryptedInviteKey = "encrypted-key",
        };

        var result = await sutProvider.Sut.CreateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkAlreadyExists>(result.AsError);

        await sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default!);

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationEventAsync(Arg.Any<Organization>(), Arg.Any<EventType>());
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_WithEmptyDomainsList_ReturnsBadRequestError(
        Guid organizationId,
        SutProvider<CreateOrganizationInviteLinkCommand> sutProvider)
    {
        SetupAbility(sutProvider, organizationId);

        var request = new CreateOrganizationInviteLinkRequest
        {
            OrganizationId = organizationId,
            AllowedDomains = [],
            EncryptedInviteKey = "encrypted-key",
        };

        var result = await sutProvider.Sut.CreateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkDomainsRequired>(result.AsError);

        await sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_WithWhitespaceOnlyDomains_ReturnsBadRequestError(
        Guid organizationId,
        SutProvider<CreateOrganizationInviteLinkCommand> sutProvider)
    {
        SetupAbility(sutProvider, organizationId);

        var request = new CreateOrganizationInviteLinkRequest
        {
            OrganizationId = organizationId,
            AllowedDomains = [" ", ""],
            EncryptedInviteKey = "encrypted-key",
        };

        var result = await sutProvider.Sut.CreateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkDomainsRequired>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_WithNullDomains_ReturnsBadRequestError(
        Guid organizationId,
        SutProvider<CreateOrganizationInviteLinkCommand> sutProvider)
    {
        SetupAbility(sutProvider, organizationId);

        var request = new CreateOrganizationInviteLinkRequest
        {
            OrganizationId = organizationId,
            AllowedDomains = null!,
            EncryptedInviteKey = "encrypted-key",
        };

        var result = await sutProvider.Sut.CreateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkDomainsRequired>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_WithoutUseInviteLinksAbility_ReturnsBadRequestError(
        Guid organizationId,
        SutProvider<CreateOrganizationInviteLinkCommand> sutProvider)
    {
        SetupAbility(sutProvider, organizationId, useInviteLinks: false);

        var request = new CreateOrganizationInviteLinkRequest
        {
            OrganizationId = organizationId,
            AllowedDomains = ["acme.com"],
            EncryptedInviteKey = "encrypted-key",
        };

        var result = await sutProvider.Sut.CreateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotAvailable>(result.AsError);

        await sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_WithNullAbility_ReturnsBadRequestError(
        Guid organizationId,
        SutProvider<CreateOrganizationInviteLinkCommand> sutProvider)
    {
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns((OrganizationAbility?)null);

        var request = new CreateOrganizationInviteLinkRequest
        {
            OrganizationId = organizationId,
            AllowedDomains = ["acme.com"],
            EncryptedInviteKey = "encrypted-key",
        };

        var result = await sutProvider.Sut.CreateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotAvailable>(result.AsError);

        await sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default!);
    }

    private static void SetupAbility(
        SutProvider<CreateOrganizationInviteLinkCommand> sutProvider,
        Guid organizationId,
        bool useInviteLinks = true)
    {
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { UseInviteLinks = useInviteLinks });
    }
}
