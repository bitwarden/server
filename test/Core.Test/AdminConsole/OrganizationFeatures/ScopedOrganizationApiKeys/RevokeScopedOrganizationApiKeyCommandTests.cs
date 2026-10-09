using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.ScopedOrganizationApiKeys;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.ScopedOrganizationApiKeys;

[SutProviderCustomize]
public class RevokeScopedOrganizationApiKeyCommandTests
{
    [Theory, BitAutoData]
    public async Task RevokeAsync_KeyInOrganization_DeletesKey(Organization organization,
        SutProvider<RevokeScopedOrganizationApiKeyCommand> sutProvider)
    {
        var apiKey = SetupKey(sutProvider, organization, organization.Id);

        var result = await sutProvider.Sut.RevokeAsync(organization.Id, apiKey.Id);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IApiKeyRepository>().Received(1).DeleteAsync(apiKey);
    }

    [Theory, BitAutoData]
    public async Task RevokeAsync_KeyInOrganization_LogsRevokedEvent(Organization organization,
        SutProvider<RevokeScopedOrganizationApiKeyCommand> sutProvider)
    {
        var apiKey = SetupKey(sutProvider, organization, organization.Id);

        await sutProvider.Sut.RevokeAsync(organization.Id, apiKey.Id);

        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationEventAsync(organization, EventType.Organization_ScopedApiKeyRevoked);
    }

    [Theory, BitAutoData]
    public async Task RevokeAsync_KeyInAnotherOrganization_ReturnsNotFoundWithoutDeleting(Organization organization,
        Guid otherOrganizationId, SutProvider<RevokeScopedOrganizationApiKeyCommand> sutProvider)
    {
        var apiKey = SetupKey(sutProvider, organization, otherOrganizationId);

        var result = await sutProvider.Sut.RevokeAsync(organization.Id, apiKey.Id);

        Assert.IsType<ScopedApiKeyNotFound>(result.AsError);
        await AssertNotRevokedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task RevokeAsync_UnknownKey_ReturnsNotFound(Guid organizationId, Guid id,
        SutProvider<RevokeScopedOrganizationApiKeyCommand> sutProvider)
    {
        var result = await sutProvider.Sut.RevokeAsync(organizationId, id);

        Assert.IsType<ScopedApiKeyNotFound>(result.AsError);
        await AssertNotRevokedAsync(sutProvider);
    }

    private static ApiKey SetupKey(SutProvider<RevokeScopedOrganizationApiKeyCommand> sutProvider,
        Organization organization, Guid keyOrganizationId)
    {
        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            OrganizationId = keyOrganizationId,
            Name = "SIEM export",
            ClientSecretHash = "hash",
            Scope = "[\"api.organization.events.read\"]",
        };
        sutProvider.GetDependency<IApiKeyRepository>().GetByIdAsync(apiKey.Id).Returns(apiKey);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        return apiKey;
    }

    private static async Task AssertNotRevokedAsync(SutProvider<RevokeScopedOrganizationApiKeyCommand> sutProvider)
    {
        await sutProvider.GetDependency<IApiKeyRepository>().DidNotReceiveWithAnyArgs()
            .DeleteAsync(default!);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
            .LogOrganizationEventAsync(default(Organization)!, default);
    }
}
